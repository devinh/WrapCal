using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace WrapCal.FunctionalTests.Support;

/// <summary>
/// Serves a published copy of WrapCal as static files.
/// <para>
/// This is what the app now ships as — a folder of files with no server behind it — so the suite
/// drives the artifact rather than a development host that production never runs. Publishing
/// once per run and serving the result also means there is no child process to supervise, no
/// port to race for and no readiness poll.
/// </para>
/// </summary>
public sealed class AppHost : IAsyncDisposable
{
    private const int PublishTimeoutSeconds = 300;

    /// <summary>
    /// Where GitHub serves the site from, given the repository is not a user page and has no
    /// custom domain. Kept in step with the deployment workflow.
    /// </summary>
    public const string PublishSubpath = "/WrapCal";

    private readonly WebApplication _server;

    private AppHost(WebApplication server, string baseUrl)
    {
        _server = server;
        BaseUrl = baseUrl;
    }

    /// <summary>
    /// Where the site is served, e.g. <c>http://127.0.0.1:53124/WrapCal</c> — the subpath
    /// included, because that is where every page actually lives.
    /// </summary>
    public string BaseUrl { get; }

    public static async Task<AppHost> StartAsync()
    {
        string published = await PublishAsync();
        string siteRoot = ResolveSiteRoot(published);
        string origin = $"http://127.0.0.1:{GetFreePort()}";
        string baseUrl = $"{origin}{PublishSubpath}";

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(origin);

        WebApplication server = builder.Build();

        var contentTypes = new FileExtensionContentTypeProvider();
        // Not in the middleware's default map, and browsers refuse to stream-compile a
        // WebAssembly module that is not served as application/wasm.
        contentTypes.Mappings[".wasm"] = "application/wasm";
        contentTypes.Mappings[".webcil"] = "application/octet-stream";
        contentTypes.Mappings[".dat"] = "application/octet-stream";
        contentTypes.Mappings[".blat"] = "application/octet-stream";

        var files = new PhysicalFileProvider(siteRoot);

        // Served only at the subpath a GitHub project page uses, because that is the only shape
        // the site is ever deployed in — and because serving it at the root as well would make
        // the suite pass even when index.html had failed to raise its base href to the subpath,
        // which is the one thing about the deployment that can silently break.
        server.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = files,
            RequestPath = PublishSubpath,
        });

        // Deliberately uncompressed. Each scenario gets a fresh browser context, so the runtime
        // is fetched again for every one of them, but the transfer is over loopback where it is
        // nearly free — compressing 19 MB per scenario costs more in CPU than it saves, measured
        // at 65s against 49s for the whole suite.
        server.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ContentTypeProvider = contentTypes,
            RequestPath = PublishSubpath,
        });

        await server.StartAsync();

        return new AppHost(server, baseUrl);
    }

    private const string StampFileName = ".published";

    /// <summary>
    /// Builds and publishes the app. Publishing rather than building is deliberate: a build
    /// leaves the app's own static files out of the output folder — only <c>_framework/</c>
    /// lands there, with the rest coming from the project's <c>wwwroot</c> and the MudBlazor
    /// package — so a built app is not servable on its own. Publish flattens all of it into one
    /// tree.
    /// <para>
    /// The result is cached beside the test binaries and reused while the app's sources are
    /// older than the last publish. Publishing runs the IL linker, which uses every core, and
    /// paying that on every test run is a burst of exactly the kind of load this suite is
    /// supposed to stop producing.
    /// </para>
    /// </summary>
    private static async Task<string> PublishAsync()
    {
        string projectPath = ResolveAppProjectPath();
        string outputDirectory = Path.Combine(AppContext.BaseDirectory, "app-publish");

        if (IsPublishFresh(outputDirectory, projectPath))
        {
            return outputDirectory;
        }

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("publish");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(Configuration);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputDirectory);
        // Compression happens in the test host instead, so the brotli and gzip copies of every
        // asset would be dead weight — and producing ~400 of them is most of a publish.
        startInfo.ArgumentList.Add("-p:BlazorEnableCompression=false");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start 'dotnet publish {projectPath}'.");

        // Read to the end rather than letting the pipes fill: a blocked child looks like a hang.
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(PublishTimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"Publishing {projectPath} did not finish within {PublishTimeoutSeconds}s.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Publishing {projectPath} failed with exit code {process.ExitCode}.{Environment.NewLine}"
                + $"{await output}{await error}");
        }

        // Written last, so a crash mid-publish never leaves a stamp claiming the tree is good.
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, StampFileName), DateTime.UtcNow.ToString("O"));

        return outputDirectory;
    }

    /// <summary>
    /// Whether the cached publish is still current: it exists, it is complete, and nothing the
    /// app is built from has been written since.
    /// </summary>
    private static bool IsPublishFresh(string outputDirectory, string projectPath)
    {
        string stamp = Path.Combine(outputDirectory, StampFileName);

        if (!File.Exists(stamp) || !File.Exists(Path.Combine(outputDirectory, "wwwroot", "index.html")))
        {
            return false;
        }

        return NewestSourceWriteTime(projectPath) <= File.GetLastWriteTimeUtc(stamp);
    }

    /// <summary>
    /// The latest write time of anything the published app is built from: the app's own files,
    /// and the build settings at the repository root, which apply to every project.
    /// </summary>
    private static DateTime NewestSourceWriteTime(string projectPath)
    {
        string appDirectory = Path.GetDirectoryName(projectPath)!;

        DateTime newest = Directory
            .EnumerateFiles(appDirectory, "*", SearchOption.AllDirectories)
            .Where(file => !file.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.GetLastWriteTimeUtc)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        // src/WrapCal -> src -> the repository root.
        string? repositoryRoot = Directory.GetParent(appDirectory)?.Parent?.FullName;

        if (repositoryRoot is not null)
        {
            foreach (string name in (string[])["Directory.Build.props", "global.json"])
            {
                string path = Path.Combine(repositoryRoot, name);

                if (File.Exists(path) && File.GetLastWriteTimeUtc(path) > newest)
                {
                    newest = File.GetLastWriteTimeUtc(path);
                }
            }
        }

        return newest;
    }

    /// <summary>
    /// Where the servable files ended up. A publish of a Blazor WebAssembly project nests them
    /// one level down, under <c>wwwroot/</c>; the check keeps that from being a silent guess.
    /// </summary>
    private static string ResolveSiteRoot(string published)
    {
        string nested = Path.Combine(published, "wwwroot");

        if (File.Exists(Path.Combine(nested, "index.html")))
        {
            return nested;
        }

        return File.Exists(Path.Combine(published, "index.html"))
            ? published
            : throw new InvalidOperationException(
                $"No index.html in the publish output at {published}. The app did not produce a site.");
    }

    private static string ResolveAppProjectPath()
    {
        string? overridden = Environment.GetEnvironmentVariable("WRAPCAL_APP_PROJECT");
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return overridden;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "WrapCal.slnx")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException(
                "Could not find WrapCal.slnx above the test output directory. " +
                "Set WRAPCAL_APP_PROJECT to the app's .csproj path.")
            : Path.Combine(directory.FullName, "src", "WrapCal", "WrapCal.csproj");
    }

    /// <summary>
    /// The configuration the test binary was built in, read back out of its own output path
    /// (<c>…/bin/&lt;Configuration&gt;/&lt;tfm&gt;/</c>), so the app is published the same way.
    /// </summary>
    private static string Configuration
    {
        get
        {
            string[] parts = AppContext.BaseDirectory.Split(
                Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

            int bin = Array.LastIndexOf(parts, "bin");

            return bin >= 0 && bin + 1 < parts.Length ? parts[bin + 1] : "Debug";
        }
    }

    /// <summary>Binds port 0 to let the OS hand back a free port, then releases it.</summary>
    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        await _server.StopAsync();
        await _server.DisposeAsync();

        // The published tree is deliberately left in place: it is the cache the next run reuses.
    }
}
