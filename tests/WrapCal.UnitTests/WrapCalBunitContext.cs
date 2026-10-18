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
    private readonly HttpContextAccessor _http = new();

    protected WrapCalBunitContext()
    {
        Services.AddMudServices();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<IHttpContextAccessor>(_http);

        // MudBlazor calls into JS for popovers and focus. Loose mode keeps a unit test about
        // markup from needing a browser.
        JSInterop.Mode = JSRuntimeMode.Loose;

        // PlanWrap reads RendererInfo.IsInteractive to decide whether its controls are live.
        // bUnit does not populate it by default and throws on access.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    /// <summary>
    /// Gives the components a request to read, standing in for the prerender they would get from
    /// a real host.
    /// <para>
    /// Set here, at call time, rather than in the constructor: <c>HttpContextAccessor</c> holds
    /// its context in an <see cref="AsyncLocal{T}"/>, so a value assigned while the test class is
    /// being constructed does not flow into the test method that follows — it reads back null.
    /// </para>
    /// </summary>
    protected void SetRequestCookie(string name, string value)
    {
        // Successive calls accumulate, so a test can present the request a returning visitor
        // would send — the theme, for one, arrives as two cookies plus the plan's own state.
        string existing = _http.HttpContext is null
            ? string.Empty
            : _http.HttpContext.Request.Headers.Cookie.ToString();

        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = existing.Length == 0
            ? $"{name}={value}"
            : $"{existing}; {name}={value}";

        _http.HttpContext = context;
    }
}
