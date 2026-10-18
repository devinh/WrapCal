using Microsoft.Playwright;
using Reqnroll;
using WrapCal.FunctionalTests.Support;

namespace WrapCal.FunctionalTests.StepDefinitions;

[Binding]
public sealed class PlanWrapSteps
{
    private const int PageTimeout = 20_000;

    private readonly UiContext _ui;

    public PlanWrapSteps(ScenarioContext scenarioContext) => _ui = scenarioContext.Get<UiContext>();

    [When("I plan the default box")]
    public async Task WhenIPlanTheDefaultBoxAsync()
    {
        await OpenPlanPageAsync();
        await ClickPlanAsync();
    }

    [When("I plan {int} boxes")]
    public async Task WhenIPlanBoxesAsync(int count)
    {
        await OpenPlanPageAsync();

        for (int i = 1; i < count; i++)
        {
            await _ui.Page.Locator("[data-testid='add-box']").ClickAsync();
        }

        await ClickPlanAsync();
    }

    [When("I plan a box far larger than the roll")]
    public async Task WhenIPlanABoxFarLargerThanTheRollAsync()
    {
        await OpenPlanPageAsync();

        ILocator sides = _ui.Page.Locator("tbody tr").First.Locator("input[type='number']");

        for (int i = 0; i < 3; i++)
        {
            await sides.Nth(i).FillAsync("500");
        }

        await ClickPlanAsync();
    }

    [When("I plan using centimetres")]
    public async Task WhenIPlanUsingCentimetresAsync()
    {
        await OpenPlanPageAsync();
        await _ui.Page.Locator("[data-testid='unit-select']").SelectOptionAsync("Centimeters");
        await ClickPlanAsync();
    }

    [When("I add a second box, remove it, then plan")]
    public async Task WhenIAddASecondBoxRemoveItThenPlanAsync()
    {
        await OpenPlanPageAsync();

        await _ui.Page.Locator("[data-testid='add-box']").ClickAsync();
        await _ui.Page.Locator("[data-testid='remove-box']").Nth(1).ClickAsync();

        await ClickPlanAsync();
    }

    [When("I plan and set the roll to continue {word}")]
    public async Task WhenIPlanAndSetTheRollToContinueAsync(string direction)
    {
        await OpenPlanPageAsync();
        await ClickPlanAsync();

        // Selected by label: the option values are the enum names (Down, Up, ...), which read
        // badly in a feature file.
        await _ui.Page.Locator("[data-testid='roll-direction']")
            .SelectOptionAsync(new SelectOptionValue { Label = direction });
    }

    [Then("I should see a cut plan")]
    public async Task ThenIShouldSeeACutPlanAsync()
    {
        string? rollLength = await _ui.Page.Locator("[data-testid='roll-length']").TextContentAsync();

        await Assert.That(string.IsNullOrWhiteSpace(rollLength)).IsFalse();
    }

    [Then("I should see a cut plan with {int} section")]
    [Then("I should see a cut plan with {int} sections")]
    public async Task ThenIShouldSeeACutPlanWithSectionsAsync(int expected)
    {
        ILocator sections = _ui.Page.Locator("[data-testid='sections']");
        await sections.WaitForAsync(new LocatorWaitForOptions { Timeout = PageTimeout });

        int rows = await sections.Locator("tbody tr").CountAsync();

        await Assert.That(rows).IsEqualTo(expected);
    }

    [Then("I should see the paper layout")]
    public async Task ThenIShouldSeeThePaperLayoutAsync()
    {
        ILocator diagram = _ui.Page.Locator("svg[role='img']");
        await diagram.WaitForAsync(new LocatorWaitForOptions { Timeout = PageTimeout });

        await Assert.That(await diagram.CountAsync()).IsEqualTo(1);

        // The label lives inside the SVG, so reading it back proves the diagram was drawn and
        // not merely an empty <svg> element.
        string? label = await diagram.TextContentAsync();
        await Assert.That(label).Contains("Box 1");
    }

    [Then("I should not see the paper layout")]
    public async Task ThenIShouldNotSeeThePaperLayoutAsync()
    {
        await Assert.That(await _ui.Page.Locator("svg[role='img']").CountAsync()).IsEqualTo(0);
    }

    [Then("I should see an error instead of a plan")]
    public async Task ThenIShouldSeeAnErrorInsteadOfAPlanAsync()
    {
        ILocator error = _ui.Page.Locator("[data-testid='plan-error']");
        await error.WaitForAsync(new LocatorWaitForOptions { Timeout = PageTimeout });

        string? message = await error.TextContentAsync();

        await Assert.That(message).Contains("cannot be wrapped");
        await Assert.That(await _ui.Page.Locator("[data-testid='sections']").CountAsync()).IsEqualTo(0);
    }

    [Then("the results should be reported in {string}")]
    public async Task ThenTheResultsShouldBeReportedInAsync(string unit)
    {
        string? caption = await _ui.Page.Locator("figcaption").TextContentAsync();

        await Assert.That(caption).Contains(unit);
    }

    [Then("the paper layout should show {string}")]
    public async Task ThenThePaperLayoutShouldShowAsync(string expected)
    {
        // Blazor re-renders when the select changes, so poll rather than reading immediately.
        await _ui.Page.WaitForFunctionAsync(
            "expected => (document.querySelector(\"svg[role='img']\")?.textContent || '').includes(expected)",
            expected,
            new PageWaitForFunctionOptions { Timeout = 10_000 });
    }

    private async Task OpenPlanPageAsync()
    {
        await _ui.Page.GotoAsync(_ui.Url("/"));

        // The page is InteractiveServer. Until the SignalR circuit attaches, the submit button
        // is a plain HTML submit and clicking it POSTs and navigates away — so wait for the
        // component's own "I am interactive" marker rather than clicking and retrying.
        await _ui.Page.WaitForSelectorAsync(
            "form[data-interactive='true']",
            new PageWaitForSelectorOptions { Timeout = PageTimeout });
    }

    private async Task ClickPlanAsync()
    {
        await _ui.Page.Locator("[data-testid='plan-button']").ClickAsync();

        // Either a plan or an error must arrive; both prove the click landed.
        await _ui.Page.WaitForSelectorAsync(
            "[data-testid='sections'], [data-testid='plan-error']",
            new PageWaitForSelectorOptions { Timeout = PageTimeout });
    }
}
