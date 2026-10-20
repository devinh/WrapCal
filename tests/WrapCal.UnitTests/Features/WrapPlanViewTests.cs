using Bunit;
using WrapCal.Features.PlanWrap;

namespace WrapCal.UnitTests;

public class WrapPlanViewTests : WrapCalBunitContext
{
    private static WrapPlan Plan(LengthUnit unit = LengthUnit.Centimeters) =>
        PlanWrapLogic.Plan(
            [new GiftBox("Book", 22, 15, 4)],
            new WrapOptions(30, 0.5, InputUnit: unit, OutputUnit: unit, SearchIterations: 0)).Value;

    [Test]
    public async Task Renders_a_table_row_per_cut_section()
    {
        WrapPlan plan = Plan();
        var cut = Render<WrapPlanView>(parameters => parameters.Add(p => p.Plan, plan));

        await Assert.That(cut.FindAll("[data-testid='sections'] tbody tr").Count)
            .IsEqualTo(plan.Sections.Count);
    }

    [Test]
    public async Task Names_the_box_in_its_row()
    {
        var cut = Render<WrapPlanView>(parameters => parameters.Add(p => p.Plan, Plan()));

        await Assert.That(cut.Markup).Contains("Book");
    }

    [Test]
    public async Task Reports_all_four_summary_numbers()
    {
        var cut = Render<WrapPlanView>(parameters => parameters.Add(p => p.Plan, Plan()));

        string rollLength = cut.Find("[data-testid='roll-length']").TextContent ?? string.Empty;
        string paperArea = cut.Find("[data-testid='paper-area']").TextContent ?? string.Empty;
        string wasteArea = cut.Find("[data-testid='waste-area']").TextContent ?? string.Empty;
        string efficiency = cut.Find("[data-testid='efficiency']").TextContent ?? string.Empty;

        await Assert.That(rollLength.Length).IsGreaterThan(0);
        await Assert.That(paperArea.Length).IsGreaterThan(0);
        await Assert.That(wasteArea.Length).IsGreaterThan(0);

        // Efficiency is formatted as a percentage.
        await Assert.That(efficiency).Contains("%");
    }

    [Test]
    public async Task Uses_squared_centimeters_for_areas_on_a_metric_plan()
    {
        var cut = Render<WrapPlanView>(parameters =>
            parameters.Add(p => p.Plan, Plan(LengthUnit.Centimeters)));

        await Assert.That(cut.Markup).Contains("cm²");
    }

    [Test]
    public async Task Uses_squared_inches_for_areas_on_an_imperial_plan()
    {
        var cut = Render<WrapPlanView>(parameters =>
            parameters.Add(p => p.Plan, Plan(LengthUnit.Inches)));

        await Assert.That(cut.Markup).Contains("in²");
        await Assert.That(cut.Markup.Contains("cm²")).IsFalse();
    }

    [Test]
    public async Task Shows_the_orientation_note_beside_each_cut()
    {
        var cut = Render<WrapPlanView>(parameters => parameters.Add(p => p.Plan, Plan()));

        await Assert.That(cut.Markup).Contains("Wrapped around the box's");
    }

    [Test]
    [Arguments(RollDirection.Up, "Top", "top", "Bottom-Left")]
    [Arguments(RollDirection.Down, "Bottom", "bottom", "Top-Left")]
    [Arguments(RollDirection.Right, "Right", "right", "Bottom-Left")]
    [Arguments(RollDirection.Left, "Left", "left", "Bottom-Right")]
    public async Task States_the_roll_orientation_and_where_the_origin_is(
        RollDirection direction,
        string side,
        string edge,
        string corner)
    {
        // All four, because the corner is not simply opposite the side the roll runs past: Up and
        // Right share Bottom-Left, and only Left puts the origin at a right-hand corner.
        var cut = Render<WrapPlanView>(parameters => parameters
            .Add(p => p.Plan, Plan())
            .Add(p => p.Direction, direction));

        string line = cut.Find("[data-testid='plan-orientation']").TextContent;

        await Assert.That(line).IsEqualTo(
            $"Roll orientation: {side} — the roll continues past the {edge} edge. "
            + $"Origin (0,0): {corner} corner.");
    }

    [Test]
    public async Task Capitalises_both_words_of_the_origin_corner()
    {
        var cut = Render<WrapPlanView>(parameters => parameters
            .Add(p => p.Plan, Plan())
            .Add(p => p.Direction, RollDirection.Up));

        string line = cut.Find("[data-testid='plan-orientation']").TextContent ?? string.Empty;

        await Assert.That(line).Contains("Origin (0,0): Bottom-Left corner.");
        await Assert.That(line.Contains("Bottom-left", StringComparison.Ordinal)).IsFalse();
    }
}
