using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace WrapCal.FunctionalTests.Support;

/// <summary>
/// Hosts the real WrapCal app in a child process on a free port.
/// <para>
/// A browser talks TCP, so an in-memory <c>TestServer</c> is not an option — Playwright
/// cannot reach it. Blazor Server also needs a real SignalR circuit, which only exists
/// on a real server. Hence: real process, real Kestrel.
/// </para>
/// </summary>
public sealed class AppHost : IAsyncDisposable
{
    private const int StartupTimeoutSeconds = 90;

    private readonly Process _process;

    private AppHost(Process process, string baseUrl)
    {
        _process = process;
        BaseUrl = baseUrl;
    }

    public string BaseUrl { get; }

    public static async Task<AppHost> StartAsync()
    {
        string projectPath = ResolveAppProjectPath();
        string baseUrl = $"http://127.0.0.1:{GetFreePort()}";

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // --no-launch-profile keeps launchSettings.json out of the picture. Without it,
        // ASPNETCORE_HTTPS_PORT could be set, and UseHttpsRedirection would try to bounce
        // every request to an https port this host isn't listening on.
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(baseUrl);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start 'dotnet run --project {projectPath}'.");

        // Drain both pipes. The child writes to them, and a full pipe buffer would block it.
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var host = new AppHost(process, baseUrl);
        await host.WaitUntilReadyAsync();
        return host;
    }

    /// <summary>
    /// Polls until the app answers. Because <see cref="HttpClient"/> follows redirects,
    /// this also fails loudly if HTTPS redirection is active and pointing at a dead port.
    /// </summary>
    private async Task WaitUntilReadyAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(StartupTimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The app exited with code {_process.ExitCode} before becoming ready. " +
                    $"Run 'dotnet run --project src/WrapCal --no-launch-profile --urls {BaseUrl}' to see why.");
            }

            try
            {
                using var response = await client.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Nothing listening yet — keep waiting.
            }
            catch (TaskCanceledException)
            {
                // Still starting — keep waiting.
            }

            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"The app did not become ready at {BaseUrl} within {StartupTimeoutSeconds}s.");
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
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
    }
}
