using Microsoft.Playwright;
using Reqnroll;
using WrapCal.FunctionalTests.Support;

namespace WrapCal.FunctionalTests.Hooks;

/// <summary>
/// Run-wide setup. The app and the browser are both expensive, so each is started once
/// for the whole run rather than per scenario.
/// </summary>
[Binding]
public static class TestRunHooks
{
    public static AppHost App { get; private set; } = null!;

    public static IPlaywright Playwright { get; private set; } = null!;

    public static IBrowser Browser { get; private set; } = null!;

    public static string ArtifactsDirectory { get; } =
        Path.Combine(AppContext.BaseDirectory, "playwright-artifacts");

    [BeforeTestRun]
    public static async Task StartAppAndBrowserAsync()
    {
        Directory.CreateDirectory(ArtifactsDirectory);

        App = await AppHost.StartAsync();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await LaunchBrowserAsync(new BrowserTypeLaunchOptions
        {
            // HEADED=1 makes the run visible — by far the fastest way to debug a failure.
            Headless = Environment.GetEnvironmentVariable("HEADED") != "1",
        });
    }

    [AfterTestRun]
    public static async Task StopAppAndBrowserAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        Playwright?.Dispose();

        if (App is not null)
        {
            await App.DisposeAsync();
        }
    }

    /// <summary>
    /// macOS runs against WebKit (Safari's engine) because Firefox cannot launch on
    /// macOS 27 at all — see README.md for the upstream bug. Everywhere else the default
    /// is Firefox. Override either way with the <c>BROWSER</c> environment variable.
    /// </summary>
    private static string DefaultBrowserName => OperatingSystem.IsMacOS() ? "webkit" : "firefox";

    private static string BrowserName =>
        (Environment.GetEnvironmentVariable("BROWSER") ?? DefaultBrowserName).ToLowerInvariant();

    private static Task<IBrowser> LaunchBrowserAsync(BrowserTypeLaunchOptions options) =>
        BrowserName switch
        {
            "webkit" => Playwright.Webkit.LaunchAsync(options),
            "firefox" => Playwright.Firefox.LaunchAsync(options),
            "chromium" => Playwright.Chromium.LaunchAsync(options),
            _ => throw new InvalidOperationException(
                $"BROWSER='{BrowserName}' is not supported. Use webkit, firefox, or chromium."),
        };
}
