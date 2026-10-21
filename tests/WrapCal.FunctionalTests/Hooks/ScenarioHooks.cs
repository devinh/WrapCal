using Microsoft.Playwright;
using Reqnroll;
using WrapCal.FunctionalTests.Support;

namespace WrapCal.FunctionalTests.Hooks;

/// <summary>
/// A fresh context per scenario keeps cookies, storage, and cache isolated. Sharing one across
/// the run was tried and bought nothing (34.3s against 34.2s): booting WebAssembly costs
/// compilation, not fetching, and compilation is not cached across pages.
/// </summary>
[Binding]
public sealed class ScenarioHooks
{
    [BeforeScenario]
    public async Task OpenPageAsync(ScenarioContext scenarioContext)
    {
        IBrowserContext context = await TestRunHooks.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = TestRunHooks.App.BaseUrl,
        });

        scenarioContext.Set(new UiContext(await context.NewPageAsync(), context, TestRunHooks.App.BaseUrl));
    }

    [AfterScenario]
    public async Task ClosePageAsync(ScenarioContext scenarioContext)
    {
        if (!scenarioContext.TryGetValue<UiContext>(out UiContext? ui) || ui is null)
        {
            return;
        }

        // Screenshot before teardown — after the context closes there is nothing left to capture.
        if (scenarioContext.TestError is not null)
        {
            await CaptureFailureScreenshotAsync(ui, scenarioContext);
        }

        await ui.Context.CloseAsync();
    }

    private static async Task CaptureFailureScreenshotAsync(UiContext ui, ScenarioContext scenarioContext)
    {
        string safeTitle = string.Join('_', scenarioContext.ScenarioInfo.Title
            .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        await ui.Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(TestRunHooks.ArtifactsDirectory, $"{safeTitle}.png"),
            FullPage = true,
        });
    }
}
