using Microsoft.Playwright;

namespace WrapCal.FunctionalTests.Support;

/// <summary>
/// The per-scenario browser state, handed to step definitions through Reqnroll's
/// <c>ScenarioContext</c>. One instance per scenario, disposed when it ends.
/// </summary>
public sealed class UiContext(IPage page, IBrowserContext context, string baseUrl)
{
    public IPage Page { get; } = page;

    public IBrowserContext Context { get; } = context;

    /// <summary>Origin of the app under test, e.g. <c>http://127.0.0.1:53124</c>.</summary>
    public string BaseUrl { get; } = baseUrl;

    /// <summary>Builds an absolute URL from a path such as <c>/</c> or <c>/wrap</c>.</summary>
    public string Url(string path = "/") => $"{BaseUrl}{(path.StartsWith('/') ? path : "/" + path)}";
}
