using Microsoft.Playwright;

namespace WrapCal.FunctionalTests.Support;

/// <summary>
/// The per-scenario browser state, handed to step definitions through Reqnroll's
/// <c>ScenarioContext</c>.
/// </summary>
public sealed class UiContext(IPage page, IBrowserContext context, string baseUrl)
{
    public IPage Page { get; } = page;

    public IBrowserContext Context { get; } = context;

    public string BaseUrl { get; } = baseUrl;

    public string Url(string path = "/") => $"{BaseUrl}{(path.StartsWith('/') ? path : "/" + path)}";
}
