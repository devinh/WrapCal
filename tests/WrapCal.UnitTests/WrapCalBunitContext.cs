using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace WrapCal.UnitTests;

/// <summary>
/// The bUnit context every WrapCal component test starts from.
/// <para>
/// MudBlazor's components resolve their services and a <see cref="TimeProvider"/> from DI — a
/// form that cannot find one throws rather than degrading, which turns every test that renders a
/// page into a wall of the same error. <c>AddRazorComponents</c> supplies the
/// <c>PersistentComponentState</c> the layout uses to carry its prerendered values into the
/// circuit; bUnit does not prerender, so it simply has nothing to carry.
/// </para>
/// </summary>
public abstract class WrapCalBunitContext : BunitContext
{
    protected WrapCalBunitContext()
    {
        Services.AddMudServices();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        });

        // MudBlazor calls into JS for popovers and focus. Loose mode keeps a unit test about
        // markup from needing a browser.
        JSInterop.Mode = JSRuntimeMode.Loose;

        // PlanWrap reads RendererInfo.IsInteractive to decide whether its controls are live.
        // bUnit does not populate it by default and throws on access.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }
}
