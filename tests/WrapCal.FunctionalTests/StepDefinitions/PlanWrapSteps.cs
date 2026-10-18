using Microsoft.Playwright;
using Reqnroll;
using WrapCal.FunctionalTests.Support;

namespace WrapCal.FunctionalTests.StepDefinitions;

[Binding]
public sealed class PlanWrapSteps
{
    private readonly UiContext _ui;

    public PlanWrapSteps(ScenarioContext scenarioContext) => _ui = scenarioContext.Get<UiContext>();

    [When("I plan a single box")]
    public async Task WhenIPlanASingleBoxAsync()
    {
        await _ui.Page.GotoAsync(_ui.Url("/"));

        // The page is InteractiveServer. Until the SignalR circuit attaches, the submit button
        // is a plain HTML submit and clicking it POSTs and navigates away — so wait for the
        // component's own "I am interactive" marker rather than clicking and retrying.
        await _ui.Page.WaitForSelectorAsync(
            "form[data-interactive='true']",
            new PageWaitForSelectorOptions { Timeout = 20_000 });

        await _ui.Page.Locator("[data-testid='plan-button']").ClickAsync();

        await _ui.Page.WaitForSelectorAsync(
            "[data-testid='sections']",
            new PageWaitForSelectorOptions { Timeout = 20_000 });
    }

    [Then("I should see a cut plan")]
    public async Task ThenIShouldSeeACutPlanAsync()
    {
        ILocator rollLength = _ui.Page.Locator("[data-testid='roll-length']");
        string? value = await rollLength.TextContentAsync();

        await Assert.That(string.IsNullOrWhiteSpace(value)).IsFalse();

        // One box in, at least one cut section out.
        int rows = await _ui.Page.Locator("[data-testid='sections'] tbody tr").CountAsync();
        await Assert.That(rows).IsGreaterThanOrEqualTo(1);
    }
}
