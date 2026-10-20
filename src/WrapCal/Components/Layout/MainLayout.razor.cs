using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace WrapCal.Components.Layout;

public partial class MainLayout
{
    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    private readonly MudTheme _theme = new();

    private MudThemeProvider _provider = default!;

    private ThemeChoice _choice = ThemeChoice.System;

    private bool _systemIsDark;

    /// <summary>
    /// What the page is actually drawn in. <see cref="ThemeChoice.System"/> defers to the operating
    /// system, which only the browser can report — so this is the last answer it gave.
    /// </summary>
    private bool IsDarkMode => _choice switch
    {
        ThemeChoice.Dark => true,
        ThemeChoice.Light => false,
        _ => _systemIsDark,
    };

    private string ThemeIcon => _choice switch
    {
        ThemeChoice.Dark => Icons.Material.Filled.DarkMode,
        ThemeChoice.Light => Icons.Material.Filled.LightMode,
        _ => Icons.Material.Filled.BrightnessAuto,
    };

    private string ThemeLabel => _choice switch
    {
        ThemeChoice.Dark => "Dark",
        ThemeChoice.Light => "Light",
        _ => "System",
    };

    protected override async Task OnInitializedAsync()
    {
        // The app is static files and a browser, so there is no request to read the theme from:
        // the same script that writes the cookies reads them back. Done while initializing rather
        // than after the first render, because the first frame should already be the right way
        // round — a cookie read cannot fail or take time, so there is no reason to draw light
        // first and correct it afterwards.
        //
        // Whatever the browser cannot answer yet — no cookie, no preference recorded — leaves the
        // two defaults in place: System, drawn light. `OnAfterRenderAsync` is the first moment the
        // system's real preference is knowable at all.
        ThemePreferences? preferences = await Js.InvokeAsync<ThemePreferences?>("wrapcalTheme.load");

        _choice = ParseChoice(preferences?.Choice);
        _systemIsDark = IsDark(preferences?.System);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        // Asked once here, then watched, because in System mode a page left open has to follow
        // the operating system as it changes.
        //
        // Leave MudThemeProvider's ObserveSystemDarkModeChange at its default: it is the provider
        // that holds the browser listener, and turning it off silences this watcher too. Tried,
        // and "A page left open follows the system as it changes" is the scenario that caught it.
        await _provider.WatchSystemDarkModeAsync(ApplySystemPreferenceAsync);
        await ApplySystemPreferenceAsync(await _provider.GetSystemDarkModeAsync());
    }

    private async Task ApplySystemPreferenceAsync(bool systemIsDark)
    {
        // Recorded whichever mode is chosen: the next page load may well be in System mode, and
        // this is the only thing that lets it be drawn the right way round from the first frame.
        await Js.InvokeVoidAsync("wrapcalTheme.rememberSystem", systemIsDark);

        if (_systemIsDark == systemIsDark)
        {
            return;
        }

        _systemIsDark = systemIsDark;

        if (_choice == ThemeChoice.System)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ChooseThemeAsync(ThemeChoice choice)
    {
        _choice = choice;

        // Written from the browser so the preference outlives this page. The theme itself already
        // changed in _choice, so the plan on screen survives.
        await Js.InvokeVoidAsync("wrapcalTheme.set", choice.ToString().ToLowerInvariant());
    }

    private static ThemeChoice ParseChoice(string? value) => value switch
    {
        "light" => ThemeChoice.Light,
        "dark" => ThemeChoice.Dark,
        _ => ThemeChoice.System,
    };

    private static bool IsDark(string? value) => string.Equals(value, "dark", StringComparison.Ordinal);
}
