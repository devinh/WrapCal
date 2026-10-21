using System.Globalization;
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
    private const string DrawingScale = "drawingScale";
    private const string CutUnderPointer = "cutUnderPointer";

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

        double inputCenter = input.Y + (input.Height / 2);
        double removeCenter = remove.Y + (remove.Height / 2);

        // The inputs sit at the top of their cell with the reserved slot beneath, so the button
        // has to line up with the input rather than with the whole cell.
        await Assert.That(Math.Abs(inputCenter - removeCenter)).IsLessThan(2.0);
    }

    [Then("the box name input should line up with the other inputs")]
    public async Task ThenTheBoxNameShouldLineUpWithTheOtherInputsAsync()
    {
        ILocator row = Ui.Page.Locator(BoxRowSelector).First;

        var name = (await row.Locator("[data-testid='box-name']").BoundingBoxAsync())!;
        var length = (await row.Locator("[data-testid='box-length']").BoundingBoxAsync())!;

        double nameCenter = name.Y + (name.Height / 2);
        double lengthCenter = length.Y + (length.Height / 2);

        // The name field and the three dimensions are the same control with the same label and
        // the same reserved message line, so they must sit on one line. They stopped doing so
        // once the name field alone was left without a message slot: a shorter control centers
        // itself in the row and drops below its neighbours.
        await Assert.That(Math.Abs(nameCenter - lengthCenter)).IsLessThan(2.0);
    }

    [Given("a phone's width")]
    public async Task GivenAPhonesWidthAsync() =>
        // Narrow enough to drop below every breakpoint — where the row and table were both broken.
        // The width survives the later navigations on this page.
        await Ui.Page.SetViewportSizeAsync(390, 844);

    [Then("the Remove button should share a line with the box's dimensions")]
    public async Task ThenTheRemoveButtonShouldShareALineWithTheDimensionsAsync()
    {
        ILocator row = Ui.Page.Locator(BoxRowSelector).First;

        var height = (await row.Locator("[data-testid='box-height']").BoundingBoxAsync())!;
        var remove = (await row.Locator("[data-testid='remove-box']").BoundingBoxAsync())!;

        // Given a line to itself at a phone's width, the button read as a stray control in the
        // whitespace under the row rather than part of it.
        double heightCenter = height.Y + (height.Height / 2);
        double removeCenter = remove.Y + (remove.Height / 2);

        await Assert.That(Math.Abs(heightCenter - removeCenter)).IsLessThan(2.0);
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

        // Opening at the origin is a scroll call made after the render rather than a layout trick,
        // so the position has to arrive before it can be read.
        await Ui.Page.WaitForFunctionAsync(
            """
            () => {
                const scroller = document.querySelector(".diagram-scroller");
                const dot = document.querySelector(".origin-dot");
                if (!scroller || !dot) return false;

                const s = scroller.getBoundingClientRect();
                const d = dot.getBoundingClientRect();
                const x = d.left + (d.width / 2);
                const y = d.top + (d.height / 2);

                return x >= s.left && x <= s.right && y >= s.top && y <= s.bottom;
            }
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 10_000 });

        // Bounding boxes are in viewport coordinates, so this accounts for the scroll position
        // however it was arrived at.
        var origin = (await Ui.Page.Locator(".origin-dot").BoundingBoxAsync())!;
        var visible = (await Ui.Page.Locator(".diagram-scroller").BoundingBoxAsync())!;

        double originX = origin.X + (origin.Width / 2);
        double originY = origin.Y + (origin.Height / 2);

        await Assert.That(originX).IsBetween(visible.X, visible.X + visible.Width);
        await Assert.That(originY).IsBetween(visible.Y, visible.Y + visible.Height);
    }

    [When("a Ctrl wheel of one notch arrives over the diagram")]
    public async Task WhenACtrlWheelOfOneNotchArrivesAsync() =>
        await WheelOverDiagramAsync(turns: 1, deltaPerTurn: -120);

    [When("a Ctrl wheel of four small turns arrives over the diagram")]
    public async Task WhenACtrlWheelOfFourSmallTurnsArrivesAsync() =>
        await WheelOverDiagramAsync(turns: 4, deltaPerTurn: -30);

    [When("a trackpad pinch of {int} percent arrives")]
    public async Task WhenATrackpadPinchArrivesAsync(int percent)
    {
        // Safari's gesture events, dispatched rather than performed: no automation can produce a
        // real trackpad pinch. The handler is the same one a device would drive.
        await Ui.Page.EvaluateAsync(
            """
            (scale) => {
                // The listener goes on the document and scopes itself by where the pointer is, so
                // a dispatched gesture has to carry coordinates inside the drawing like a real one.
                const box = document.querySelector(".wrap-diagram").getBoundingClientRect();
                const x = box.left + (box.width / 2);
                const y = box.top + (box.height / 2);

                const fire = (name) => {
                    const event = new Event(name, { bubbles: true, cancelable: true });
                    event.scale = scale;
                    Object.defineProperty(event, "clientX", { value: x });
                    Object.defineProperty(event, "clientY", { value: y });
                    document.dispatchEvent(event);
                };

                fire("gesturestart");
                fire("gesturechange");
                fire("gestureend");
            }
            """,
            percent / 100.0);
    }

    [When("I note the cut under the pointer")]
    public async Task WhenINoteTheCutUnderThePointerAsync()
    {
        // A point on a cut near the middle of the frame. Picked by proximity rather than by
        // document order: a mirrored layout puts the first cut at the far end, so "the first big
        // one" can sit outside the frame entirely and the pointer would never be over the drawing.
        double[] noted = await Ui.Page.EvaluateAsync<double[]>(
            """
            () => {
                // Start from the middle of the scroll range: Up and Left open at the far end, and
                // nothing can scroll past an end.
                const scroller = document.querySelector(".diagram-scroller");
                scroller.scrollLeft = (scroller.scrollWidth - scroller.clientWidth) / 2;
                scroller.scrollTop = (scroller.scrollHeight - scroller.clientHeight) / 2;

                const frame = scroller.getBoundingClientRect();
                const middleX = frame.left + (frame.width / 2);
                const middleY = frame.top + (frame.height / 2);

                let best = -1;
                let bestDistance = Number.MAX_VALUE;
                let point = [0, 0];

                [...document.querySelectorAll(".wrap-diagram g.cut rect")].forEach((cut, i) => {
                    const r = cut.getBoundingClientRect();

                    if (r.width <= 12 || r.height <= 12) {
                        return;
                    }

                    const x = r.left + (r.width / 2);
                    const y = r.top + (r.height / 2);
                    const distance = Math.hypot(x - middleX, y - middleY);

                    if (distance < bestDistance) {
                        bestDistance = distance;
                        best = i;
                        point = [x, y];
                    }
                });

                return [best, point[0], point[1]];
            }
            """);

        Context.Set(noted, CutUnderPointer);
    }

    [When("I scroll the drawing to the middle")]
    public async Task WhenIScrollTheDrawingToTheMiddleAsync() =>
        await Ui.Page.EvaluateAsync(
            """
            () => {
                const scroller = document.querySelector(".diagram-scroller");

                scroller.scrollLeft = (scroller.scrollWidth - scroller.clientWidth) / 2;
                scroller.scrollTop = (scroller.scrollHeight - scroller.clientHeight) / 2;
            }
            """);

    [Then("the drawing should sit at the left of its frame")]
    public async Task ThenTheDrawingShouldSitAtTheLeftAsync()
    {
        // A drawing narrower than its frame used to land on the right for Left, because a reversed
        // row packs its content against the right edge.
        double edge = await Ui.Page.EvaluateAsync<double>(
            """
            () => {
                const scroller = document.querySelector(".diagram-scroller");

                // The svg, not the paper: the drawing is inset from its box by the gutter that
                // frames the roll, so the paper is legitimately a little in from the edge.
                return scroller.querySelector("svg").getBoundingClientRect().left
                    - scroller.getBoundingClientRect().left;
            }
            """);

        await Assert.That(edge).IsGreaterThanOrEqualTo(-1.0);
        await Assert.That(edge).IsLessThan(2.0);
    }

    [Then("the drawing should be scrollable across its whole width")]
    public async Task ThenTheDrawingShouldBeScrollableAsync()
    {
        // Scrolling from one end to the other must move the drawing by exactly the scrollable
        // distance. It does not when the drawing is letterboxed inside a frame-wide box: then the
        // box scrolls and the drawing inside it does not, and its far end cannot be reached at all.
        double[] result = await Ui.Page.EvaluateAsync<double[]>(
            """
            () => {
                const scroller = document.querySelector(".diagram-scroller");
                const paper = scroller.querySelector(".paper");
                const max = scroller.scrollWidth - scroller.clientWidth;

                // Driven to absurd values so the browser clamps to whichever end it counts from.
                scroller.scrollLeft = -1e7;
                const start = paper.getBoundingClientRect().left;
                scroller.scrollLeft = 1e7;
                const end = paper.getBoundingClientRect().left;

                return [max, Math.abs(start - end)];
            }
            """);

        await Assert.That(result[0]).IsGreaterThan(1.0);
        await Assert.That(Math.Abs(result[0] - result[1])).IsLessThan(2.0);
    }

    [Then("the cut under the pointer should not have moved")]
    public async Task ThenTheCutUnderThePointerShouldNotHaveMovedAsync()
    {
        double[] noted = Context.Get<double[]>(CutUnderPointer);
        int index = (int)noted[0];

        await Assert.That(index).IsGreaterThanOrEqualTo(0);

        double[] now = await Ui.Page.EvaluateAsync<double[]>(
            """
            (args) => {
                const [index, x, y] = args;
                const r = document.querySelectorAll(".wrap-diagram g.cut rect")[index].getBoundingClientRect();
                const inside = x >= r.left && x <= r.right && y >= r.top && y <= r.bottom;

                return [inside ? 1 : 0, (x - r.left) / r.width, (y - r.top) / r.height];
            }
            """,
            new[] { (double)index, noted[1], noted[2] });

        // The same cut, still under the same point, at the same place within it. Without the
        // correction the drawing slides out from under the pointer instead.
        await Assert.That(now[0]).IsEqualTo(1.0);
        await Assert.That(Math.Abs(now[1] - 0.5)).IsLessThan(0.05);
        await Assert.That(Math.Abs(now[2] - 0.5)).IsLessThan(0.05);
    }

    [Then("the zoom should read {word}")]
    public async Task ThenTheZoomShouldReadAsync(string expected)
    {
        string? readout = await Ui.Page.Locator("[data-testid='zoom-level']").TextContentAsync();

        await Assert.That(readout?.Trim()).IsEqualTo(expected);
    }

    [Then("the drawing's scale should match the zoom")]
    public async Task ThenTheDrawingScaleShouldMatchTheZoomAsync()
    {
        // Compared against the page's own readout rather than an expected number, so this holds
        // whatever value a gesture happens to land on — and it ties the figure to the picture.
        string? readout = await Ui.Page.Locator("[data-testid='zoom-level']").TextContentAsync();
        double percent = double.Parse(
            (readout ?? "100%").Trim().TrimEnd('%'), CultureInfo.InvariantCulture);

        double before = Context.Get<double>(DrawingScale);
        double after = await CrossRollExtentAsync();

        await Assert.That(Math.Abs(after - (before * percent / 100.0))).IsLessThan(2.0);
    }

    private async Task WheelOverDiagramAsync(int turns, int deltaPerTurn)
    {
        // Dispatched, not driven through Playwright's mouse: on Firefox its synthetic wheel arrives
        // without the pointer coordinates the handler scopes by, so the zoom never moves. A real
        // wheel carries them. This covers everything downstream of the event arriving, but no
        // automated test now proves a physical wheel reaches the handler on Firefox. Aimed at the
        // noted cut when a scenario has one, otherwise the middle of the drawing.
        double[]? noted = Context.ContainsKey(CutUnderPointer)
            ? Context.Get<double[]>(CutUnderPointer)
            : null;

        double[] point = noted is { Length: 3 } ? [noted[1], noted[2]] : [];

        for (int i = 0; i < turns; i++)
        {
            await Ui.Page.EvaluateAsync(
                """
                (args) => {
                    const [delta, point] = args;
                    const figure = document.querySelector(".wrap-diagram");
                    const box = figure.getBoundingClientRect();
                    const x = point.length ? point[0] : box.left + (box.width / 2);
                    const y = point.length ? point[1] : box.top + (box.height / 2);

                    figure.dispatchEvent(new WheelEvent("wheel", {
                        deltaY: delta,
                        ctrlKey: true,
                        bubbles: true,
                        cancelable: true,
                        clientX: x,
                        clientY: y
                    }));
                }
                """,
                new object[] { deltaPerTurn, point });
        }
    }

    [When("I note the drawing's scale")]
    public async Task WhenINoteTheDrawingsScaleAsync() =>
        Context.Set(await CrossRollExtentAsync(), DrawingScale);

    [Then("the drawing's scale should be unchanged")]
    public async Task ThenTheDrawingsScaleShouldBeUnchangedAsync()
    {
        // Nobody reading the page can tell a scale from a plan, but the browser can: a drawing
        // shrunk to fit its frame gets smaller as boxes arrive, and this does not care how long
        // the plan is. That is the point — the scroll scenario depends on the plan outgrowing
        // its frame, so better packing can quietly make it prove nothing. This cannot.
        double before = Context.Get<double>(DrawingScale);
        double after = await CrossRollExtentAsync();

        await Assert.That(Math.Abs(after - before)).IsLessThanOrEqualTo(1.0);
    }

    /// <summary>
    /// Pixels the paper spans across the roll: constant however many boxes there are, unless the
    /// drawing is scaled to fit, when it shrinks with every box added. Read off the drawn paper
    /// rather than the &lt;svg&gt;, which is told to fill its frame either way.
    /// </summary>
    private async Task<double> CrossRollExtentAsync()
    {
        double extent = await Ui.Page.EvaluateAsync<double>(
            """
            () => {
                const scroller = document.querySelector('.diagram-scroller');
                const paper = scroller.querySelector('.paper').getBoundingClientRect();

                // The roll runs across the page when the drawing is horizontal, so across the
                // roll is the paper's height; otherwise it is its width.
                return scroller.classList.contains('is-horizontal') ? paper.height : paper.width;
            }
            """);

        return extent;
    }

    /// <summary>
    /// Whether the drawing spills out of its scroller, as (along the roll, across it). Which axis
    /// is the roll's depends on which way the drawing faces, so it is read from the scroller's
    /// direction class rather than assumed.
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
