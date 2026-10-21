using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using WrapCal.Components.Layout;

namespace WrapCal.UnitTests;

/// <summary>
/// The bUnit context every WrapCal component test starts from. MudBlazor's components resolve
/// their services and a <see cref="TimeProvider"/> from DI, and a form that cannot find one
/// throws rather than degrading.
/// </summary>
public abstract class WrapCalBunitContext : BunitContext
{
    protected WrapCalBunitContext()
    {
        Services.AddMudServices();
        Services.AddSingleton(TimeProvider.System);

        // MudBlazor calls into JS for popovers and focus. Loose mode keeps a unit test about
        // markup from needing a browser, and leaves the theme read answering nothing — which is
        // exactly what a first visit looks like.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    /// Stands in for the browser's half of the theme: what <c>theme.js</c> would have found in the
    /// cookies when the layout asked. Passing null for either is a preference never expressed, so
    /// the layout falls back to System and to drawing light.
    /// </summary>
    protected void SetThemePreference(string? choice, string? system = null) =>
        JSInterop.Setup<ThemePreferences>("wrapcalTheme.load")
            .SetResult(new ThemePreferences(choice, system));
}
