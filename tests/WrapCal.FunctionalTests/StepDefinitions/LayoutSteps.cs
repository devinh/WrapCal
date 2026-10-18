using Microsoft.Playwright;
using Reqnroll;

namespace WrapCal.FunctionalTests.StepDefinitions;

/// <summary>
/// Where things sit on the page. These are measured in the browser rather than reasoned about in
/// CSS, since they are claims about geometry.
/// </summary>
[Binding]
public sealed class LayoutSteps : StepsBase
{
    private const string PlanButtonTop = "planButtonTop";

    public LayoutSteps(ScenarioContext scenarioContext)
        : base(scenarioContext)
    {
    }

    [When("a box dimension becomes invalid")]
    public async Task WhenABoxDimensionBecomesInvalidAsync()
    {
        double before = (await Ui.Page.Locator("[data-testid='plan-button']").BoundingBoxAsync())!.Y;
        Context.Set(before, PlanButtonTop);

        ILocator length = Ui.Page.Locator(BoxRowSelector).First.Locator("input[type='number']").First;
        await FillAndLeaveAsync(length, "0");

        await Ui.Page.WaitForSelectorAsync(
            "text=Length must be between",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    [Then("the Plan button should not have moved")]
    public async Task ThenThePlanButtonShouldNotHaveMovedAsync()
    {
        double before = Context.Get<double>(PlanButtonTop);
        double after = (await Ui.Page.Locator("[data-testid='plan-button']").BoundingBoxAsync())!.Y;

        // A validation message must not reflow the page: the reserved slot keeps the row height
        // constant whether or not a message is showing.
        await Assert.That(Math.Abs(after - before)).IsLessThan(1.0);
    }

    [Then("the Remove button should line up with the row's inputs")]
    public async Task ThenTheRemoveButtonShouldLineUpWithTheRowsInputsAsync()
    {
        var input = (await Ui.Page.Locator(BoxRowSelector).First
            .Locator("input[type='number']").First.BoundingBoxAsync())!;

        var remove = (await Ui.Page.Locator("[data-testid='remove-box']").First.BoundingBoxAsync())!;

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
        ILocator descriptions = Ui.Page.Locator(".col-md-3 .form-text");
        int count = await descriptions.CountAsync();

        var tops = new List<double>();
        for (int i = 0; i < count; i++)
        {
            tops.Add((await descriptions.Nth(i).BoundingBoxAsync())!.Y);
        }

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(tops.Max() - tops.Min()).IsLessThan(2.0);
    }
}
