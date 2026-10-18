using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;
using MudBlazor;

namespace WrapCal.Components.Layout;

public partial class MainLayout : IDisposable
{
    private const string ChoiceCookie = "wrapcal-theme";
    private const string SystemPreferenceCookie = "wrapcal-system-theme";

    private const string ChoicePersistKey = "wrapcal-theme-choice";
    private const string SystemPreferencePersistKey = "wrapcal-system-theme";

    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    [Inject]
    private IHttpContextAccessor Http { get; set; } = default!;

    private readonly MudTheme _theme = new();

    private MudThemeProvider _provider = default!;

    private ThemeChoice _choice = ThemeChoice.System;

    private bool _systemIsDark;

    private PersistingComponentStateSubscription? _persisting;

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

    protected override void OnInitialized()
    {
        // The choice and the system's last answer are read from the request during the prerender
        // and handed to the interactive instance through persistent component state, so the theme
        // the page was drawn with is the theme the circuit carries on with.
        //
        // Both also travel as cookies, which is what makes the handoff resilient: a circuit's own
        // connection request carries them too, so the fallback alone keeps the theme right. See
        // `Theme.feature`, which drives the round trip in a browser.
        //
        // PersistentComponentState has no public constructor and is only registered by a real
        // Razor host, so it is resolved rather than injected — under bUnit there is no prerender
        // and nothing to carry, which the component has to tolerate anyway.
        PersistentComponentState? state =
            Services.GetService(typeof(PersistentComponentState)) as PersistentComponentState;

        _choice = state is not null && state.TryTakeFromJson(ChoicePersistKey, out ThemeChoice persisted)
            ? persisted
            : ParseChoice(Http.HttpContext?.Request.Cookies[ChoiceCookie]);

        // No recorded answer means this is the first visit, or the first since the cookie was
        // cleared. Drawing light is the honest guess: the correction below arrives within a frame
        // of the circuit attaching, and from then on the cookie makes the prerender right.
        _systemIsDark = state is not null
            && state.TryTakeFromJson(SystemPreferencePersistKey, out bool persistedSystem)
                ? persistedSystem
                : IsDark(Http.HttpContext?.Request.Cookies[SystemPreferenceCookie]);

        _persisting = state?.RegisterOnPersisting(() =>
        {
            state.PersistAsJson(ChoicePersistKey, _choice);
            state.PersistAsJson(SystemPreferencePersistKey, _systemIsDark);
            return Task.CompletedTask;
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        // The prerender cannot ask the browser anything, so this is the first moment the system
        // preference is knowable at all. Asked once here, then watched, because in System mode a
        // page left open has to follow the operating system as it changes.
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
        // this is the only thing that lets its prerender be drawn the right way round.
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

        // Written from the browser because an interactive component cannot set a response cookie.
        // The theme itself already changed in _choice, so the plan on screen survives.
        await Js.InvokeVoidAsync("wrapcalTheme.set", choice.ToString().ToLowerInvariant());
    }

    private static ThemeChoice ParseChoice(string? value) => value switch
    {
        "light" => ThemeChoice.Light,
        "dark" => ThemeChoice.Dark,
        _ => ThemeChoice.System,
    };

    private static bool IsDark(string? value) => string.Equals(value, "dark", StringComparison.Ordinal);

    public void Dispose() => _persisting?.Dispose();
}
