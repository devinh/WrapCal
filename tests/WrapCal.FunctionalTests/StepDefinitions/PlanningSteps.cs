using System.Globalization;
using Microsoft.Playwright;
using Reqnroll;

namespace WrapCal.FunctionalTests.StepDefinitions;

/// <summary>Planning, and what the resulting plan says.</summary>
[Binding]
public sealed class PlanningSteps : StepsBase
{
    public PlanningSteps(ScenarioContext scenarioContext)
        : base(scenarioContext)
    {
    }

    [When("I plan the wrap")]
    public async Task WhenIPlanTheWrapAsync() => await ClickPlanAsync();

    [When("I try to plan the wrap")]
    public async Task WhenITryToPlanTheWrapAsync()
    {
        // No wait: a field-level problem blocks the submit, so neither a plan nor a logic error
        // appears. The Then step waits for the message instead.
        await Ui.Page.Locator("[data-testid='plan-button']").ClickAsync();
    }

    [Then("I should see a cut plan")]
    public async Task ThenIShouldSeeACutPlanAsync()
    {
        string? rollLength = await Ui.Page.Locator("[data-testid='roll-length']").TextContentAsync();

        await Assert.That(string.IsNullOrWhiteSpace(rollLength)).IsFalse();
    }

    [Then("I should see a cut plan with {int} section")]
    [Then("I should see a cut plan with {int} sections")]
    public async Task ThenIShouldSeeACutPlanWithSectionsAsync(int expected)
    {
        ILocator sections = Ui.Page.Locator("[data-testid='sections']");
        await sections.WaitForAsync(new LocatorWaitForOptions { Timeout = PageTimeout });

        int rows = await sections.Locator("tbody tr").CountAsync();

        await Assert.That(rows).IsEqualTo(expected);
    }

    [Then("I should see the paper layout")]
    public async Task ThenIShouldSeeThePaperLayoutAsync()
    {
        ILocator diagram = Ui.Page.Locator("[data-testid='diagram']");
        await diagram.WaitForAsync(new LocatorWaitForOptions { Timeout = PageTimeout });

        await Assert.That(await diagram.CountAsync()).IsEqualTo(1);

        // The labels live inside the SVG, so reading one back proves the diagram was drawn and
        // not merely an empty <svg> element.
        string? label = await diagram.TextContentAsync();

        await Assert.That(label).Contains("×");
    }

    [Then("I should not see the paper layout")]
    public async Task ThenIShouldNotSeeThePaperLayoutAsync()
    {
        await Assert.That(await Ui.Page.Locator("[data-testid='diagram']").CountAsync()).IsEqualTo(0);
    }

    [Then("I should see an error instead of a plan")]
    public async Task ThenIShouldSeeAnErrorInsteadOfAPlanAsync()
    {
        ILocator error = Ui.Page.Locator("[data-testid='plan-error']");
        await error.WaitForAsync(new LocatorWaitForOptions { Timeout = PageTimeout });

        string? message = await error.TextContentAsync();

        await Assert.That(message).Contains("cannot be wrapped");
        await Assert.That(await Ui.Page.Locator("[data-testid='sections']").CountAsync()).IsEqualTo(0);
    }

    [Then("I should see a validation message about the {word}")]
    public async Task ThenIShouldSeeAValidationMessageAboutAsync(string field)
    {
        // Each message is matched past its own number, because "Width must be between" is a
        // substring of "Roll Width must be between" — a shorter prefix would let the roll's
        // message stand in for the box width's and the test would pass without one existing.
        string message = field switch
        {
            "roll" => "Roll Width must be between 1 and",
            "overlap" => "Overlap Margin must be between 0 and",
            "length" => "Length must be between 0.1 and",
            "width" => "Width must be between 0.1 and",
            "height" => "Height must be between 0.1 and",
            "name" => "Name must be 50 characters or fewer",
            _ => throw new ArgumentOutOfRangeException(
                nameof(field), field, "No such field on the form."),
        };

        await Ui.Page.WaitForSelectorAsync(
            $"text={message}", new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    [When("I set the length to {double}")]
    public async Task WhenISetTheLengthToAsync(double length)
    {
        ILocator row = Ui.Page.Locator(BoxRowSelector).First;

        await FillAndLeaveAsync(row.Locator("[data-testid='box-length']"), Inv(length));
    }

    [Then("the results should be reported in {string}")]
    public async Task ThenTheResultsShouldBeReportedInAsync(string unit)
    {
        string? caption = await Ui.Page.Locator("figcaption").TextContentAsync();

        await Assert.That(caption).Contains(unit);
    }

    [Then("the paper layout should say the roll continues past the {word} edge")]
    public async Task ThenThePaperLayoutShouldSayTheRollContinuesPastTheEdgeAsync(string edge)
    {
        // Asserted through the caption rather than the on-diagram arrow: the side labels are
        // rotated, so the glyph in the markup is not the one that ends up on screen.
        await Ui.Page.WaitForFunctionAsync(
            "expected => (document.querySelector('figcaption')?.textContent || '').includes(expected)",
            $"past the {edge} edge",
            new PageWaitForFunctionOptions { Timeout = 10_000 });
    }

    [Then("the cuts should be:")]
    public async Task ThenTheCutsShouldBeAsync(DataTable expected)
    {
        ILocator rows = Ui.Page.Locator("[data-testid='sections'] tbody tr");
        int count = await rows.CountAsync();

        var actual = new Dictionary<string, (string Width, string Length)>(StringComparer.Ordinal);

        for (int i = 0; i < count; i++)
        {
            ILocator cells = rows.Nth(i).Locator("td");
            string name = (await cells.Nth(0).TextContentAsync())?.Trim() ?? string.Empty;

            actual[name] = (
                (await cells.Nth(3).TextContentAsync())?.Trim() ?? string.Empty,
                (await cells.Nth(4).TextContentAsync())?.Trim() ?? string.Empty);
        }

        await Assert.That(count).IsEqualTo(expected.RowCount);

        foreach (DataTableRow row in expected.Rows)
        {
            string name = row["name"];

            if (!actual.TryGetValue(name, out (string Width, string Length) got))
            {
                Assert.Fail($"The plan has no cut for '{name}'.");
                return;
            }

            await Assert.That(got.Width).IsEqualTo(row["width"]);
            await Assert.That(got.Length).IsEqualTo(row["length"]);
        }
    }

    [Then("the cut plan should use {double} of roll")]
    public async Task ThenTheCutPlanShouldUseOfRollAsync(double expected)
    {
        string? text = await Ui.Page.Locator("[data-testid='roll-length']").TextContentAsync();
        double actual = double.Parse(text?.Trim() ?? "NaN", CultureInfo.InvariantCulture);

        // The page reports one decimal place; stated this way round so a failure says what it got.
        if (Math.Abs(actual - expected) >= 0.05)
        {
            Assert.Fail($"The plan used {actual} of roll but {expected} was expected.");
        }
    }

    [Then("the cut plan should report {int} cut")]
    [Then("the cut plan should report {int} cuts")]
    public async Task ThenTheCutPlanShouldReportCutsAsync(int expected)
    {
        int rows = await Ui.Page.Locator("[data-testid='sections'] tbody tr").CountAsync();

        await Assert.That(rows).IsEqualTo(expected);
    }
}
