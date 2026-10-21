using System.Globalization;
using Bunit;
using WrapCal.Features.PlanWrap;

namespace WrapCal.UnitTests;

public class WrapPlanDiagramTests : WrapCalBunitContext
{
    private static WrapPlan OneBoxPlan() =>
        PlanWrapLogic.Plan(
            [new GiftBox("Book", 22, 15, 4)],
            new WrapOptions(30, 0.5, SearchIterations: 0)).Value;

    [Test]
    public async Task Starts_the_zoom_at_one_hundred_percent()
    {
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, OneBoxPlan()));

        await Assert.That(cut.Find("[data-testid='zoom-level']").TextContent).IsEqualTo("100%");
    }

    [Test]
    public async Task Moves_the_zoom_with_the_wheel()
    {
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, OneBoxPlan()));

        // One notch of a mouse wheel, upwards: a ratio of 1.5 rather than a step of 25.
        await cut.Instance.ZoomByWheelAsync(-120);

        await Assert.That(cut.Find("[data-testid='zoom-level']").TextContent).IsEqualTo("150%");
    }

    [Test]
    public async Task Zooms_as_much_in_small_turns_as_in_one()
    {
        // A trackpad sends a stream of small deltas where a mouse sends one of 120. Because the
        // wheel scales by a ratio, four quarter-turns compose to exactly one notch — which is what
        // makes a pinch feel smooth instead of stepping.
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, OneBoxPlan()));

        for (int i = 0; i < 4; i++)
        {
            await cut.Instance.ZoomByWheelAsync(-30);
        }

        await Assert.That(cut.Find("[data-testid='zoom-level']").TextContent).IsEqualTo("150%");
    }

    [Test]
    public async Task Refuses_to_zoom_past_its_limits()
    {
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, OneBoxPlan()));

        for (int i = 0; i < 40; i++)
        {
            await cut.Instance.ZoomByWheelAsync(-120);
        }

        await Assert.That(cut.Find("[data-testid='zoom-level']").TextContent).IsEqualTo("400%");

        for (int i = 0; i < 40; i++)
        {
            await cut.Instance.ZoomByWheelAsync(120);
        }

        await Assert.That(cut.Find("[data-testid='zoom-level']").TextContent).IsEqualTo("25%");
    }

    [Test]
    public async Task Ignores_the_wheel_while_a_pinch_is_in_progress()
    {
        // A browser that reports a pinch as both gesture events and ctrl+wheel would otherwise
        // zoom twice there and once everywhere else — which is what "faster on Safari" looks like.
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, OneBoxPlan()));

        await cut.Instance.BeginZoomGestureAsync();
        await cut.Instance.ZoomByRatioAsync(2.25);
        await cut.Instance.ZoomByWheelAsync(-120);

        await Assert.That(cut.Find("[data-testid='zoom-level']").TextContent).IsEqualTo("150%");

        // Once the gesture ends the wheel is heard again.
        await cut.Instance.EndZoomGestureAsync();
        await cut.Instance.ZoomByWheelAsync(-120);

        await Assert.That(cut.Find("[data-testid='zoom-level']").TextContent).IsEqualTo("225%");
    }

    [Test]
    public async Task Applies_a_pinch_ratio_to_the_zoom_it_started_from()
    {
        // Safari reports a pinch as a ratio since the gesture began, so the zoom it started from
        // is what the ratio multiplies — not the zoom at each event, which would compound.
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, OneBoxPlan()));

        await cut.Instance.BeginZoomGestureAsync();
        await cut.Instance.ZoomByRatioAsync(1.5);

        // Damped: a spread of 1.5 zooms 1.5^0.5, not 1.5, so Safari answers about as gently as a
        // wheel does in the browsers that report pixel deltas.
        await Assert.That(cut.Find("[data-testid='zoom-level']").TextContent).IsEqualTo("122%");

        await cut.Instance.ZoomByRatioAsync(1.5);

        await Assert.That(cut.Find("[data-testid='zoom-level']").TextContent).IsEqualTo("122%");
    }

    [Test]
    public async Task Draws_one_rectangle_per_cut_section()
    {
        WrapPlan plan = OneBoxPlan();
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, plan));

        await Assert.That(cut.FindAll("g.cut").Count).IsEqualTo(plan.Sections.Count);
    }

    [Test]
    public async Task Labels_each_rectangle_with_the_box_name_and_its_cut_size()
    {
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, OneBoxPlan()));

        string markup = cut.Markup;

        await Assert.That(markup).Contains("Book");

        // The cut dimension line, e.g. "26.0 × 74.5 cm".
        await Assert.That(markup).Contains("×");
        await Assert.That(markup).Contains("cm");
    }

    [Test]
    public async Task Draws_the_paper_at_the_plan_s_roll_width()
    {
        WrapPlan plan = OneBoxPlan();
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, plan));

        // Read off the paper rect rather than the viewBox: the viewBox also carries the margins
        // that annotate the ends and frame the drawing.
        string width = cut.Find("rect.paper").GetAttribute("width") ?? string.Empty;

        await Assert.That(width).IsEqualTo("30");
    }

    [Test]
    public async Task Leaves_a_margin_either_side_of_the_roll()
    {
        WrapPlan plan = OneBoxPlan();
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, plan));

        string[] viewBox = (cut.Find("svg").GetAttribute("viewBox") ?? string.Empty).Split(' ');
        double paper = double.Parse(cut.Find("rect.paper").GetAttribute("width") ?? "0",
            CultureInfo.InvariantCulture);
        double framed = double.Parse(viewBox[2], CultureInfo.InvariantCulture);

        // The roll spans the width here, so the extra is the gutter, split evenly between the
        // two sides. Without it the paper sits flush against the diagram's border.
        double gutter = (framed - paper) / 2;

        await Assert.That(gutter).IsGreaterThan(paper * 0.02);

        // The viewBox is written to three decimals, so compare the left edge to the computed
        // gutter with a tolerance rather than exactly.
        double left = double.Parse(viewBox[0], CultureInfo.InvariantCulture);
        await Assert.That(Math.Abs(left + gutter)).IsLessThan(0.01);
    }

    [Test]
    public async Task Marks_both_ends_so_it_is_clear_which_way_the_roll_runs()
    {
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, OneBoxPlan()));

        string markup = cut.Markup;

        await Assert.That(markup).Contains("Start of roll");
        await Assert.That(markup).Contains("Roll continues");
    }

    [Test]
    public async Task Shows_a_message_instead_of_a_diagram_when_there_is_nothing_to_cut()
    {
        WrapPlan empty = PlanWrapLogic.Plan([], new WrapOptions(30, 0.5)).Value;
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, empty));

        await Assert.That(cut.FindAll("svg").Count).IsEqualTo(0);
        await Assert.That(cut.Markup).Contains("Nothing to draw");
    }

    // The expected glyph is the one that ends up in the markup, which is NOT the one that ends
    // up on screen for Left/Right: those labels are rotated, and the arrow turns with them. A
    // "down" glyph renders pointing right; an "up" glyph renders pointing left.
    [Test]
    [Arguments(RollDirection.Down, "↓ Roll continues")]
    [Arguments(RollDirection.Up, "↑ Roll continues")]
    [Arguments(RollDirection.Right, "↓ Roll continues")]
    [Arguments(RollDirection.Left, "↑ Roll continues")]
    public async Task Points_the_continues_label_the_right_way(RollDirection direction, string expected)
    {
        var cut = Render<WrapPlanDiagram>(parameters => parameters
            .Add(p => p.Plan, OneBoxPlan())
            .Add(p => p.Direction, direction));

        await Assert.That(cut.Markup).Contains(expected);
    }

    [Test]
    public async Task Mirrors_the_cuts_when_the_roll_continues_upwards()
    {
        // One box would not distinguish the two: a single section starts at Y=0 either way.
        WrapPlan plan = TwoBoxPlan();

        var down = Render<WrapPlanDiagram>(parameters => parameters
            .Add(p => p.Plan, plan).Add(p => p.Direction, RollDirection.Down));
        var up = Render<WrapPlanDiagram>(parameters => parameters
            .Add(p => p.Plan, plan).Add(p => p.Direction, RollDirection.Up));

        string firstYDown = down.FindAll("g.cut rect")[0].GetAttribute("y") ?? string.Empty;
        string firstYUp = up.FindAll("g.cut rect")[0].GetAttribute("y") ?? string.Empty;

        await Assert.That(firstYUp).IsNotEqualTo(firstYDown);
    }

    [Test]
    public async Task Swaps_the_axes_when_the_roll_runs_horizontally()
    {
        WrapPlan plan = TwoBoxPlan();

        var down = Render<WrapPlanDiagram>(parameters => parameters
            .Add(p => p.Plan, plan).Add(p => p.Direction, RollDirection.Down));
        var right = Render<WrapPlanDiagram>(parameters => parameters
            .Add(p => p.Plan, plan).Add(p => p.Direction, RollDirection.Right));

        // Down: the paper is as wide as the roll. Right: as tall as the roll.
        await Assert.That(down.Find("rect.paper").GetAttribute("width")).IsEqualTo("60");
        await Assert.That(right.Find("rect.paper").GetAttribute("height")).IsEqualTo("60");
    }

    [Test]
    public async Task Marks_and_labels_the_origin()
    {
        var cut = Render<WrapPlanDiagram>(parameters => parameters.Add(p => p.Plan, OneBoxPlan()));

        await Assert.That(cut.FindAll("circle.origin-dot").Count).IsEqualTo(1);
        await Assert.That(cut.Markup).Contains("0,0");
    }

    [Test]
    [Arguments(RollDirection.Down)]
    [Arguments(RollDirection.Up)]
    [Arguments(RollDirection.Right)]
    [Arguments(RollDirection.Left)]
    public async Task Puts_the_origin_on_the_corner_where_both_axes_are_zero(RollDirection direction)
    {
        WrapPlan plan = OneBoxPlan();
        var cut = Render<WrapPlanDiagram>(parameters => parameters
            .Add(p => p.Plan, plan)
            .Add(p => p.Direction, direction));

        var dot = cut.Find("circle.origin-dot");
        double cx = double.Parse(dot.GetAttribute("cx") ?? "0", CultureInfo.InvariantCulture);
        double cy = double.Parse(dot.GetAttribute("cy") ?? "0", CultureInfo.InvariantCulture);

        // The origin moves with the drawing: left for Up, bottom for the horizontal layouts.
        double expectedX = direction == RollDirection.Left ? plan.RollLengthUsed : 0;

        double expectedY = direction switch
        {
            RollDirection.Up => plan.RollLengthUsed,
            RollDirection.Left or RollDirection.Right => plan.RollWidth,
            _ => 0,
        };

        await Assert.That(cx).IsEqualTo(expectedX);
        await Assert.That(cy).IsEqualTo(expectedY);
    }

    // The scroller is the element that keeps the drawing at a fixed scale and moves it under the
    // reader's eye. Its axis follows the roll, and it is laid out from the far end exactly when the
    // origin is at that end — which is what makes it open on the origin.
    [Test]
    [Arguments(RollDirection.Down, "is-vertical")]
    [Arguments(RollDirection.Up, "is-vertical")]
    [Arguments(RollDirection.Left, "is-horizontal")]
    [Arguments(RollDirection.Right, "is-horizontal")]
    public async Task Lays_the_scroller_out_along_the_roll(
        RollDirection direction, string axis)
    {
        // Which end the scroller opens at is no longer a layout trick: a reversed row packed its
        // content against the right edge, which put a left-running drawing on the wrong side of
        // its frame and made its scroll offsets run backwards. Opening at the origin is a scroll
        // call now (wrapcalDiagram.openAtOrigin), exercised by the browser suite's
        // "the origin should be in view" outline for all four directions.
        var cut = Render<WrapPlanDiagram>(parameters => parameters
            .Add(p => p.Plan, OneBoxPlan())
            .Add(p => p.Direction, direction));

        string classes = cut.Find(".diagram-scroller").GetAttribute("class") ?? string.Empty;

        await Assert.That(classes).Contains(axis);
        await Assert.That(classes.Contains("is-reversed")).IsFalse();
    }

    private static WrapPlan TwoBoxPlan() =>
        PlanWrapLogic.Plan(
            [new GiftBox("Book", 22, 15, 4), new GiftBox("Mug", 12, 12, 12)],
            new WrapOptions(60, 0.5, SearchIterations: 0)).Value;
}
