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

        ILocator length = Ui.Page.Locator(BoxRowSelector).First.Locator("[data-testid='box-length']");
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
        // Measured against the input the user sees. Measuring the whole control looked correct
        // while the button sat 16px low, because the control is taller than its input at both
        // ends: the floating label above and the reserved message line below.
        var input = (await Ui.Page.Locator(BoxRowSelector).First
            .Locator("[data-testid='box-length']").BoundingBoxAsync())!;

        var remove = (await Ui.Page.Locator("[data-testid='remove-box']").First.BoundingBoxAsync())!;

        double inputCentre = input.Y + (input.Height / 2);
        double removeCentre = remove.Y + (remove.Height / 2);

        // The inputs sit at the top of their cell with the reserved slot beneath, so the button
        // has to line up with the input rather than with the whole cell.
        await Assert.That(Math.Abs(inputCentre - removeCentre)).IsLessThan(2.0);
    }

    [Then("the box name input should line up with the other inputs")]
    public async Task ThenTheBoxNameShouldLineUpWithTheOtherInputsAsync()
    {
        ILocator row = Ui.Page.Locator(BoxRowSelector).First;

        var name = (await row.Locator("[data-testid='box-name']").BoundingBoxAsync())!;
        var length = (await row.Locator("[data-testid='box-length']").BoundingBoxAsync())!;

        double nameCentre = name.Y + (name.Height / 2);
        double lengthCentre = length.Y + (length.Height / 2);

        // The name field and the three dimensions are the same control with the same label and
        // the same reserved message line, so they must sit on one line. They stopped doing so
        // once the name field alone was left without a message slot: a shorter control centres
        // itself in the row and drops below its neighbours.
        await Assert.That(Math.Abs(nameCentre - lengthCentre)).IsLessThan(2.0);
    }

    [Given("a phone's width")]
    public async Task GivenAPhonesWidthAsync() =>
        // Narrow enough to drop below every breakpoint the page uses, and the case the row and the
        // table were both broken at. Everything after this runs on the same page, so the width
        // survives the navigations the later steps do.
        await Ui.Page.SetViewportSizeAsync(390, 844);

    [Then("the Remove button should share a line with the box's dimensions")]
    public async Task ThenTheRemoveButtonShouldShareALineWithTheDimensionsAsync()
    {
        ILocator row = Ui.Page.Locator(BoxRowSelector).First;

        var height = (await row.Locator("[data-testid='box-height']").BoundingBoxAsync())!;
        var remove = (await row.Locator("[data-testid='remove-box']").BoundingBoxAsync())!;

        // Given a line to itself at a phone's width, the button read as a stray control in the
        // whitespace under the row rather than part of it.
        double heightCentre = height.Y + (height.Height / 2);
        double removeCentre = remove.Y + (remove.Height / 2);

        await Assert.That(Math.Abs(heightCentre - removeCentre)).IsLessThan(2.0);
    }

    [Then("the cut plan table should fit inside its card")]
    public async Task ThenTheCutPlanTableShouldFitInsideItsCardAsync()
    {
        // A table cannot shrink below its content, and six numeric columns plus a prose Note come
        // to more than a phone's width — left to itself it grew past the card's right edge.
        double overflow = await Ui.Page.EvaluateAsync<double>(
            """
            () => {
                const table = document.querySelector(".wrapcal-sections");
                const card = table.closest(".mud-paper");
                return table.getBoundingClientRect().right - card.getBoundingClientRect().right;
            }
            """);

        await Assert.That(overflow).IsLessThanOrEqualTo(0.5);
    }

    [Then("the page should not scroll sideways")]
    public async Task ThenThePageShouldNotScrollSidewaysAsync()
    {
        // The table spilling out of its card widened the document itself, so the whole page slid
        // sideways under the reader.
        bool overflows = await Ui.Page.EvaluateAsync<bool>(
            """
            () => {
                const root = document.documentElement;
                return root.scrollWidth > root.clientWidth + 1;
            }
            """);

        await Assert.That(overflows).IsFalse();
    }

    [Then("every roll direction option should fit in its menu")]
    public async Task ThenEveryRollDirectionOptionShouldFitInItsMenuAsync()
    {
        ILocator host = Ui.Page.Locator("[data-testid='roll-direction']");

        // The shortest label on show first: left to size itself, the select takes the width of
        // whichever option is displayed, and the menu is sized from the anchor — so "Top"
        // used to leave the longer options clipped.
        await PickAsync(host, "Top");
        await host.ClickAsync();

        await Ui.Page.WaitForSelectorAsync(
            "[role='option']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });

        // The option boxes are all as wide as the menu, so comparing boxes can never fail. What
        // gets clipped is the label inside each option: the menu takes the anchor's width, and the
        // anchor is only as wide as whichever option is showing.
        bool allFit = await Ui.Page.EvaluateAsync<bool>(
            """
            () => {
                const options = Array.from(document.querySelectorAll("[role='option']"));
                if (options.length === 0) return false;

                return options.every(option => {
                    const text = option.querySelector('.mud-list-item-text') ?? option;
                    return text.scrollWidth <= text.clientWidth + 1;
                });
            }
            """);

        await Assert.That(allFit).IsTrue();
    }

    [Then("the option descriptions should all line up")]
    public async Task ThenTheOptionDescriptionsShouldAllLineUpAsync()
    {
        // Validation messages sit below the descriptions, so a column showing one must not push
        // its description out of line with the others.
        ILocator descriptions = Ui.Page.Locator("[data-testid='roll-options'] .mud-input-helper-text");
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
