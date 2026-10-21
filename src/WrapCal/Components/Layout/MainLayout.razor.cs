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
        // the same script reads the cookies the boot script wrote. Read while initializing, not
        // after the first render, so the first frame is already right. Anything the browser cannot
        // answer yet — no cookie — leaves the defaults: System, drawn light.
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

        // Asked once, then watched, so a page left open follows the system as it changes. Leave
        // MudThemeProvider's ObserveSystemDarkModeChange at its default: it holds the browser
        // listener, and turning it off silences this watcher too.
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
