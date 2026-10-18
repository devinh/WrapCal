using Bunit;
using MudBlazor;
using MudBlazor.Extensions;
using WrapCal.Components.Layout;

namespace WrapCal.UnitTests;

public class MainLayoutTests : WrapCalBunitContext
{
    [Test]
    public async Task Renders_the_app_name_and_the_page_content()
    {
        var cut = Render<MainLayout>(parameters =>
            parameters.Add(p => p.Body, "page content"));

        await Assert.That(cut.Markup).Contains("WrapCal");
        await Assert.That(cut.Markup).Contains("page content");
    }

    [Test]
    public async Task Has_no_navigation()
    {
        // The whole site is the calculator, so a nav creeping back in is a regression.
        var cut = Render<MainLayout>(parameters =>
            parameters.Add(p => p.Body, "page content"));

        await Assert.That(cut.FindAll("nav").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll(".nav-link").Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(null, false, false)]
    [Arguments(null, true, true)]
    [Arguments("system", false, false)]
    [Arguments("system", true, true)]
    [Arguments("light", false, false)]
    [Arguments("light", true, false)]
    [Arguments("dark", false, true)]
    [Arguments("dark", true, true)]
    public async Task Draws_the_theme_the_choice_and_the_system_together_describe(
        string? choice, bool systemIsDark, bool expectedDark)
    {
        // The choice is a cookie the browser hands back; the system's preference is a browser
        // fact, and the only thing that can answer it is the same call MudBlazor's provider
        // makes. An explicit choice has to win over it either way — that is the whole point of
        // choosing.
        SetSystemPreference(systemIsDark);
        SetThemePreference(choice);

        var cut = Render<MainLayout>(parameters => parameters.Add(p => p.Body, "page content"));

        await Assert.That(IsDark(cut)).IsEqualTo(expectedDark);
    }

    [Test]
    [Arguments("system")]
    [Arguments("light")]
    [Arguments("dark")]
    public async Task Choosing_a_theme_applies_it_and_hands_it_to_the_browser(string choice)
    {
        var cut = Render<MainLayout>(parameters => parameters.Add(p => p.Body, "page content"));

        // The activator is the button inside the wrapper — the wrapper carries only the testid.
        await cut.Find("[data-testid='theme-menu'] button").ClickAsync();
        await cut.Find($"[data-testid='theme-{choice}']").ClickAsync();

        await Assert.That(IsDark(cut)).IsEqualTo(choice == "dark");

        // The component cannot write a response cookie, so the choice is only durable if it
        // reaches the browser — a menu that changes the page and forgets is a bug on reload.
        await Assert.That(JSInterop.Invocations.Any(
            i => i.Identifier == "wrapcalTheme.set"
                && i.Arguments.Count == 1
                && Equals(i.Arguments[0], choice))).IsTrue();
    }

    [Test]
    public async Task Defaults_to_the_system_setting()
    {
        // Nobody who has expressed no preference asked to be pinned to one.
        var cut = Render<MainLayout>(parameters => parameters.Add(p => p.Body, "page content"));

        string? label = cut.Find("[data-testid='theme-menu'] .mud-menu").GetAttribute("aria-label");

        await Assert.That(label).IsEqualTo("Theme: System");
    }

    /// <summary>
    /// What the browser tells MudBlazor when it asks whether the system is dark. bUnit has no
    /// media query and no operating system, so the answer is arranged rather than observed —
    /// `Theme.feature` is where the real one is read.
    /// </summary>
    private void SetSystemPreference(bool isDark) =>
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode").SetResult(isDark);

    private static bool IsDark(IRenderedComponent<MainLayout> cut) =>
        cut.FindComponent<MudThemeProvider>().Instance.GetState(x => x.IsDarkMode);
}
