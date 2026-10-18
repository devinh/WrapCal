using Microsoft.Playwright;
using Reqnroll;

namespace WrapCal.FunctionalTests.StepDefinitions;

/// <summary>Setting the form up: the roll, the boxes, and the controls that change them.</summary>
[Binding]
public sealed class FormSteps : StepsBase
{
    private int _rowsFilled;

    public FormSteps(ScenarioContext scenarioContext)
        : base(scenarioContext)
    {
    }

    [Given("the plan page")]
    public async Task GivenThePlanPageAsync() => await OpenPlanPageAsync();

    [Given("a roll {double} {word} wide with an overlap of {double}")]
    public async Task GivenARollAsync(double rollWidth, string unit, double overlap)
    {
        await OpenPlanPageAsync();
        await SetUnitAsync(unit);

        await FillAndLeaveAsync(Ui.Page.Locator("#rollWidth"), Inv(rollWidth));
        await FillAndLeaveAsync(Ui.Page.Locator("#overlapMargin"), Inv(overlap));
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

    [Given("{int} identical boxes measuring {double} by {double} by {double}")]
    public async Task GivenIdenticalBoxesAsync(
        int count, double length, double width, double height)
    {
        // Long plans are what the diagram has to cope with; spelling out a dozen near-identical
        // rows would bury the point of the scenario.
        for (int i = 1; i <= count; i++)
        {
            await GivenABoxAsync($"Box {i}", length, width, height);
        }
    }

    [When("I remove the last box")]
    public async Task WhenIRemoveTheLastBoxAsync()
    {
        ILocator rows = Ui.Page.Locator(BoxRowSelector);
        int before = await rows.CountAsync();

        await Ui.Page.Locator("[data-testid='remove-box']").Nth(before - 1).ClickAsync();

        await Ui.Page.WaitForFunctionAsync(
            $"() => document.querySelectorAll(\"{BoxRowSelector}\").length < {before}",
            null,
            new PageWaitForFunctionOptions { Timeout = 10_000 });

        _rowsFilled--;
    }

    [When("I set the roll to continue {word}")]
    public async Task WhenISetTheRollToContinueAsync(string direction)
    {
        bool horizontal = direction is "Leftwards" or "Rightwards";
        bool reversed = direction is "Upwards" or "Leftwards";

        // Selected by label: the option values are the enum names (Down, Up, ...), which read
        // badly in a feature file.
        await Ui.Page.Locator("[data-testid='roll-direction']")
            .SelectOptionAsync(new SelectOptionValue { Label = direction });

        // Changing direction redraws the diagram, and the scroller is re-keyed, so the previous
        // drawing's elements are detached for a moment. Waiting for the new scroller to carry this
        // direction's classes means the next step measures the drawing it means to — reading a
        // bounding box in between comes back null.
        await Ui.Page.WaitForFunctionAsync(
            """
            ([horizontal, reversed]) => {
                const scroller = document.querySelector('.diagram-scroller');
                return scroller !== null
                    && scroller.classList.contains('is-horizontal') === horizontal
                    && scroller.classList.contains('is-reversed') === reversed;
            }
            """,
            new object[] { horizontal, reversed },
            new PageWaitForFunctionOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// Returns the row for the next box, clicking Add box when one is needed. The click only adds
    /// a row after a circuit round trip, so this waits for the row to appear — re-checking the
    /// count immediately used to add a row per retry and leave phantom boxes behind.
    /// </summary>
    private async Task<ILocator> NextBoxRowAsync()
    {
        ILocator rows = Ui.Page.Locator(BoxRowSelector);
        int before = await rows.CountAsync();

        if (before <= _rowsFilled)
        {
            await Ui.Page.Locator("[data-testid='add-box']").ClickAsync();

            await Ui.Page.WaitForFunctionAsync(
                $"() => document.querySelectorAll(\"{BoxRowSelector}\").length > {before}",
                null,
                new PageWaitForFunctionOptions { Timeout = 10_000 });
        }

        return rows.Nth(_rowsFilled++);
    }
}
