using System.Globalization;
using Microsoft.Playwright;
using Reqnroll;
using WrapCal.FunctionalTests.Support;

namespace WrapCal.FunctionalTests.StepDefinitions;

[Binding]
public sealed class PlanWrapSteps
{
    private const int PageTimeout = 20_000;

    private const string BoxRowSelector = "[data-testid='boxes'] tbody tr";

    private readonly UiContext _ui;
    private readonly ScenarioContext _context;
    private int _rowsFilled;

    public PlanWrapSteps(ScenarioContext scenarioContext)
    {
        _ui = scenarioContext.Get<UiContext>();
        _context = scenarioContext;
    }

    [Given("the plan page")]
    public async Task GivenThePlanPageAsync() => await OpenPlanPageAsync();

    [Then("the Remove button should line up with the row's inputs")]
    public async Task ThenTheRemoveButtonShouldLineUpWithTheRowsInputsAsync()
    {
        var input = (await _ui.Page.Locator(BoxRowSelector).First
            .Locator("input[type='number']").First.BoundingBoxAsync())!;

        var remove = (await _ui.Page.Locator("[data-testid='remove-box']").First.BoundingBoxAsync())!;

        double inputCentre = input.Y + (input.Height / 2);
        double removeCentre = remove.Y + (remove.Height / 2);

        // The inputs sit at the top of their cell with the reserved slot beneath, so the button
        // has to line up with the input rather than with the whole cell.
        await Assert.That(Math.Abs(inputCentre - removeCentre)).IsLessThan(2.0);
    }

    [Then("the option descriptions should all line up")]
    public async Task ThenTheOptionDescriptionsShouldAllLineUpAsync()
    {
        // Validation messages sit below the descriptions, so a column showing one must not push
        // its description out of line with the others.
        ILocator descriptions = _ui.Page.Locator(".col-md-3 .form-text");
        int count = await descriptions.CountAsync();

        var tops = new List<double>();
        for (int i = 0; i < count; i++)
        {
            tops.Add((await descriptions.Nth(i).BoundingBoxAsync())!.Y);
        }

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(tops.Max() - tops.Min()).IsLessThan(2.0);
    }

    [When("a box dimension becomes invalid")]
    public async Task WhenABoxDimensionBecomesInvalidAsync()
    {
        double before = (await _ui.Page.Locator("[data-testid='plan-button']").BoundingBoxAsync())!.Y;
        _context.Set(before, "planButtonTop");

        ILocator length = _ui.Page.Locator(BoxRowSelector).First.Locator("input[type='number']").First;
        await FillAndLeaveAsync(length, "0");

        await _ui.Page.WaitForSelectorAsync(
            "text=Length must be between",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    [Then("the Plan button should not have moved")]
    public async Task ThenThePlanButtonShouldNotHaveMovedAsync()
    {
        double before = _context.Get<double>("planButtonTop");
        double after = (await _ui.Page.Locator("[data-testid='plan-button']").BoundingBoxAsync())!.Y;

        // A validation message must not reflow the page: the reserved slot keeps the row height
        // constant whether or not a message is showing.
        await Assert.That(Math.Abs(after - before)).IsLessThan(1.0);
    }

    [Then("I should see a validation message about the length")]
    public async Task ThenIShouldSeeAValidationMessageAboutTheLengthAsync()
    {
        await _ui.Page.WaitForSelectorAsync(
            "text=Length must be between",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    [When("I try to plan the wrap")]
    public async Task WhenITryToPlanTheWrapAsync()
    {
        // No wait: a field-level problem blocks the submit, so neither a plan nor a logic error
        // appears. The Then step waits for the message.
        await _ui.Page.Locator("[data-testid='plan-button']").ClickAsync();
    }

    [When("I set the roll to continue {word}")]
    public async Task WhenISetTheRollToContinueAsync(string direction)
    {
        // Selected by label: the option values are the enum names (Down, Up, ...), which read
        // badly in a feature file.
        await _ui.Page.Locator("[data-testid='roll-direction']")
            .SelectOptionAsync(new SelectOptionValue { Label = direction });
    }

    [When("I remove the last box")]
    public async Task WhenIRemoveTheLastBoxAsync()
    {
        ILocator rows = _ui.Page.Locator(BoxRowSelector);
        int before = await rows.CountAsync();

        await _ui.Page.Locator("[data-testid='remove-box']").Nth(before - 1).ClickAsync();

        await _ui.Page.WaitForFunctionAsync(
            $"() => document.querySelectorAll(\"{BoxRowSelector}\").length < {before}",
            null,
            new PageWaitForFunctionOptions { Timeout = 10_000 });

        _rowsFilled--;
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

    [Then("the paper layout should say the roll continues past the {word} edge")]
    public async Task ThenThePaperLayoutShouldSayTheRollContinuesPastTheEdgeAsync(string edge)
    {
        // Asserted through the caption rather than the on-diagram arrow: the side labels are
        // rotated, so the glyph in the markup is not the one that ends up on screen.
        string expected = $"past the {edge} edge";

        await _ui.Page.WaitForFunctionAsync(
            "expected => (document.querySelector('figcaption')?.textContent || '').includes(expected)",
            expected,
            new PageWaitForFunctionOptions { Timeout = 10_000 });
    }

    [Given("a roll {double} {word} wide with an overlap of {double}")]
    public async Task GivenARollAsync(double rollWidth, string unit, double overlap)
    {
        await OpenPlanPageAsync();
        await SetUnitAsync(unit);

        await FillAndLeaveAsync(_ui.Page.Locator("#rollWidth"), Inv(rollWidth));
        await FillAndLeaveAsync(_ui.Page.Locator("#overlapMargin"), Inv(overlap));
    }

    [Given("the units are set to {word}")]
    public async Task GivenTheUnitsAreSetToAsync(string unit) => await SetUnitAsync(unit);

    /// <summary>
    /// Selects a unit and waits for the converted value to arrive. Changing the unit re-expresses
    /// everything already in the form over the circuit; typing before that lands gets overwritten,
    /// and waiting for the value to change is the same signal on a fast machine or a slow one.
    /// </summary>
    private async Task SetUnitAsync(string unit)
    {
        string target = unit.StartsWith("in", StringComparison.OrdinalIgnoreCase)
            ? "Inches"
            : "Centimeters";

        ILocator select = _ui.Page.Locator("[data-testid='unit-select']");

        if (await select.InputValueAsync() == target)
        {
            return;
        }

        string before = await _ui.Page.Locator("#rollWidth").InputValueAsync();

        await select.SelectOptionAsync(target);

        await _ui.Page.WaitForFunctionAsync(
            "([selector, previous]) => document.querySelector(selector)?.value !== previous",
            new[] { "#rollWidth", before },
            new PageWaitForFunctionOptions { Timeout = 10_000 });
    }

    [Given("a box {string} measuring {double} by {double} by {double}")]
    public async Task GivenABoxAsync(string name, double length, double width, double height)
    {
        ILocator row = await NextBoxRowAsync();

        // InputText renders no type attribute, so it cannot be found by input[type='text'].
        await FillAndLeaveAsync(row.Locator("[data-testid='box-name']"), name);

        ILocator sides = row.Locator("input[type='number']");
        await FillAndLeaveAsync(sides.Nth(0), Inv(length));
        await FillAndLeaveAsync(sides.Nth(1), Inv(width));
        await FillAndLeaveAsync(sides.Nth(2), Inv(height));
    }

    /// <summary>
    /// Returns the row for the next box, clicking Add box when one is needed. The click only adds
    /// a row after a circuit round trip, so this waits for the row to appear — re-checking the
    /// count immediately used to add a row per retry and leave phantom boxes behind.
    /// </summary>
    private async Task<ILocator> NextBoxRowAsync()
    {
        ILocator rows = _ui.Page.Locator(BoxRowSelector);
        int before = await rows.CountAsync();

        if (before <= _rowsFilled)
        {
            await _ui.Page.Locator("[data-testid='add-box']").ClickAsync();

            await _ui.Page.WaitForFunctionAsync(
                $"() => document.querySelectorAll(\"{BoxRowSelector}\").length > {before}",
                null,
                new PageWaitForFunctionOptions { Timeout = 10_000 });
        }

        return rows.Nth(_rowsFilled++);
    }

    [When("I plan the wrap")]
    public async Task WhenIPlanTheWrapAsync() => await ClickPlanAsync();

    [Then("the cuts should be:")]
    public async Task ThenTheCutsShouldBeAsync(DataTable expected)
    {
        ILocator rows = _ui.Page.Locator("[data-testid='sections'] tbody tr");
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
        string? text = await _ui.Page.Locator("[data-testid='roll-length']").TextContentAsync();
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
        int rows = await _ui.Page.Locator("[data-testid='sections'] tbody tr").CountAsync();

        await Assert.That(rows).IsEqualTo(expected);
    }

    /// <summary>
    /// Fills a field and then leaves it. Blazor's inputs bind on <c>change</c>, which the browser
    /// only fires on blur, while Playwright's fill dispatches <c>input</c> alone.
    /// </summary>
    private static async Task FillAndLeaveAsync(ILocator input, string value)
    {
        await input.FillAsync(value);
        await input.PressAsync("Tab");
    }

    private static string Inv(double value) => value.ToString(CultureInfo.InvariantCulture);

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
