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

    [Then("the layout should be cut off by its box rather than shrunk to fit it")]
    public async Task ThenTheLayoutShouldBeCutOffByItsBoxAsync()
    {
        // The drawing used to be scaled down until the whole plan fitted its box, so more boxes
        // meant smaller paper. It now keeps the scale the roll width gives it, and overflows along
        // the roll — which is the axis the whole diagram is here to be scrolled down.
        (bool alongRoll, _) = await OverflowAsync();

        await Assert.That(alongRoll).IsTrue();
    }

    [Then("the origin should be in view")]
    public async Task ThenTheOriginShouldBeInViewAsync()
    {
        // Only meaningful if the drawing really does run off the edge; otherwise the origin being
        // visible says nothing about where the scroller opens.
        (bool alongRoll, bool acrossRoll) = await OverflowAsync();

        await Assert.That(alongRoll || acrossRoll).IsTrue();

        // Bounding boxes are in viewport coordinates, so this accounts for the scroll position
        // however it was arrived at.
        var origin = (await Ui.Page.Locator(".origin-dot").BoundingBoxAsync())!;
        var visible = (await Ui.Page.Locator(".diagram-scroller").BoundingBoxAsync())!;

        double originX = origin.X + (origin.Width / 2);
        double originY = origin.Y + (origin.Height / 2);

        await Assert.That(originX).IsBetween(visible.X, visible.X + visible.Width);
        await Assert.That(originY).IsBetween(visible.Y, visible.Y + visible.Height);
    }

    /// <summary>
    /// Whether the drawing spills out of its scroller, reported as (along the roll, across it).
    /// The roll's own axis depends on which way the drawing faces, so it is read from the
    /// scroller's direction class rather than assumed.
    /// </summary>
    private async Task<(bool AlongRoll, bool AcrossRoll)> OverflowAsync()
    {
        string js =
            """
            () => {
                const scroller = document.querySelector('.diagram-scroller');
                const horizontal = scroller.classList.contains('is-horizontal');
                return [
                    scroller.scrollHeight > scroller.clientHeight + 1,
                    scroller.scrollWidth > scroller.clientWidth + 1,
                    horizontal,
                ];
            }
            """;

        bool[] result = await Ui.Page.EvaluateAsync<bool[]>(js);
        bool overflowingLength = result[0];
        bool overflowingWidth = result[1];
        bool horizontal = result[2];

        return horizontal
            ? (overflowingWidth, overflowingLength)
            : (overflowingLength, overflowingWidth);
    }
}
