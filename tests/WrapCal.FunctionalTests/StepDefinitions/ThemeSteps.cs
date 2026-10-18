using Microsoft.Playwright;
using Reqnroll;

namespace WrapCal.FunctionalTests.StepDefinitions;

/// <summary>
/// What only a real browser can settle about the theme: the cookie round trip through an actual
/// document, and System deferring to a media query — bUnit has no media query and no operating
/// system to ask.
/// </summary>
[Binding]
public sealed class ThemeSteps : StepsBase
{
    private const string ChoiceCookieName = "wrapcal-theme";

    /// <summary>
    /// The palette variable the theme writes onto the document root, which is where a theme
    /// becomes visible at all — MudBlazor's layout carries its colours as custom properties. Read
    /// from the theme's own contract rather than a test hook added to the page, so a repaint in
    /// either direction is caught by the same assertion.
    /// </summary>
    private const string BackgroundVariable = "--mud-palette-background";

    /// <summary>
    /// Generous, because on a first visit in System mode the answer is not knowable until the app
    /// has booted and can ask the browser.
    /// </summary>
    private const int ThemeSettleTimeout = 5_000;

    public ThemeSteps(ScenarioContext scenarioContext)
        : base(scenarioContext)
    {
    }

    [Given("the browser prefers a {word} colour scheme")]
    public async Task GivenTheBrowserPrefersAColourSchemeAsync(string scheme) =>
        await Ui.Page.EmulateMediaAsync(new PageEmulateMediaOptions
        {
            ColorScheme = scheme == "dark" ? ColorScheme.Dark : ColorScheme.Light,
        });

    /// <summary>
    /// Changes the media query without navigating, which is the only way to see whether an open
    /// page is following the system or merely read it once when the app booted.
    /// </summary>
    [When("the browser switches to a {word} colour scheme")]
    public async Task WhenTheBrowserSwitchesToAColourSchemeAsync(string scheme) =>
        await Ui.Page.EmulateMediaAsync(new PageEmulateMediaOptions
        {
            ColorScheme = scheme == "dark" ? ColorScheme.Dark : ColorScheme.Light,
        });

    [Given("the visitor last chose the dark theme")]
    public async Task GivenTheVisitorLastChoseTheDarkThemeAsync() =>
        await Ui.Context.AddCookiesAsync(
        [
            new Cookie { Name = ChoiceCookieName, Value = "dark", Url = Ui.Url("/") },
        ]);

    [When("I choose the {word} theme")]
    public async Task WhenIChooseTheThemeAsync(string choice)
    {
        // The activator is the button inside the wrapper — the wrapper carries only the testid,
        // because MudMenu puts unmatched attributes on markup of its own choosing.
        await Ui.Page.Locator("[data-testid='theme-menu'] button").ClickAsync();
        await Ui.Page.Locator($"[data-testid='theme-{choice}']").ClickAsync();

        // Waited for by name rather than by colour: System resolves to whichever way the browser
        // is set, so the colour says nothing about whether the click arrived.
        await Ui.Page.WaitForFunctionAsync(
            """
            (label) => document.querySelector("[data-testid='theme-menu'] .mud-menu")
                ?.getAttribute('aria-label') === label
            """,
            $"Theme: {char.ToUpperInvariant(choice[0])}{choice[1..]}",
            new PageWaitForFunctionOptions { Timeout = 10_000 });
    }

    [When("I reload the page")]
    public async Task WhenIReloadThePageAsync()
    {
        await Ui.Page.ReloadAsync();
        await WaitForAppAsync();
    }

    [Then("the page should be in the {word} theme")]
    public async Task ThenThePageShouldBeInTheThemeAsync(string theme)
    {
        bool expectedDark = theme == "dark";

        try
        {
            await WaitForThemeAsync(expectedDark);
        }
        catch (TimeoutException)
        {
            // Stated this way round so a failure says what the page is, not just what it isn't.
            bool isDark = await BackgroundIsDarkAsync();

            Assert.Fail(
                $"The page is in the {(isDark ? "dark" : "light")} theme "
                + $"but the {theme} theme was expected.");
        }
    }

    /// <summary>
    /// Waits for the palette to reach the expected side of mid-grey. The comparison is by
    /// luminance rather than against the palette's exact values: which colours a theme uses is
    /// the theme's business, and pinning them would make a restyle look like a broken preference.
    /// </summary>
    private async Task WaitForThemeAsync(bool expectedDark) =>
        await Ui.Page.WaitForFunctionAsync(
            $$"""
            (dark) => {
                const value = getComputedStyle(document.documentElement)
                    .getPropertyValue('{{BackgroundVariable}}');
                const [red, green, blue] = value.match(/[\d.]+/g).map(Number);
                return ((((red + green + blue) / 3) / 255) < 0.5) === dark;
            }
            """,
            expectedDark,
            new PageWaitForFunctionOptions { Timeout = ThemeSettleTimeout });

    private async Task<bool> BackgroundIsDarkAsync()
    {
        double luminance = await Ui.Page.EvaluateAsync<double>(
            $$"""
            () => {
                const value = getComputedStyle(document.documentElement)
                    .getPropertyValue('{{BackgroundVariable}}');
                const [red, green, blue] = value.match(/[\d.]+/g).map(Number);
                return ((red + green + blue) / 3) / 255;
            }
            """);

        return luminance < 0.5;
    }
}
