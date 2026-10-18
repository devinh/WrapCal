using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;
using MudBlazor;

namespace WrapCal.Components.Layout;

public partial class MainLayout : IDisposable
{
    private const string CookieName = "wrapcal-theme";
    private const string PersistKey = "wrapcal-theme-dark";

    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    [Inject]
    private IHttpContextAccessor Http { get; set; } = default!;

    private readonly MudTheme _theme = new();

    private bool _isDark;

    private PersistingComponentStateSubscription? _persisting;

    protected override void OnInitialized()
    {
        // The cookie is only readable while the request is in flight, and that request's
        // HttpContext does not reach the circuit. So the prerender reads it and hands the answer
        // to the interactive instance through persistent component state; without that handoff
        // the theme would snap back to light the moment the circuit attached.
        //
        // PersistentComponentState has no public constructor and is only registered by a real
        // Razor host, so it is resolved rather than injected — under bUnit there is no prerender
        // and nothing to carry, which the component has to tolerate anyway.
        PersistentComponentState? state =
            Services.GetService(typeof(PersistentComponentState)) as PersistentComponentState;

        _isDark = state is not null && state.TryTakeFromJson(PersistKey, out bool persisted)
            ? persisted
            : string.Equals(Http.HttpContext?.Request.Cookies[CookieName], "dark", StringComparison.Ordinal);

        _persisting = state?.RegisterOnPersisting(() =>
        {
            state.PersistAsJson(PersistKey, _isDark);
            return Task.CompletedTask;
        });
    }

    private string ThemeIcon => _isDark ? Icons.Material.Filled.LightMode : Icons.Material.Filled.DarkMode;

    private async Task ToggleThemeAsync()
    {
        _isDark = !_isDark;

        // Written from the browser because an interactive component cannot set a response
        // cookie. The theme itself already changed in _isDark, so the plan on screen survives.
        await Js.InvokeVoidAsync("wrapcalTheme.set", _isDark);
    }

    public void Dispose() => _persisting?.Dispose();
}
