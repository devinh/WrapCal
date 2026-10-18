using Bunit;
using WrapCal.Features.PlanWrap;

namespace WrapCal.UnitTests;

public class WrapPlanDiagramTests : BunitContext
{
    private static WrapPlan OneBoxPlan() =>
        PlanWrapLogic.Plan(
            [new GiftBox("Book", 22, 15, 4)],
            new WrapOptions(30, 0.5, RandomRestarts: 0)).Value;

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

        // viewBox is "0 -head rollWidth height" — the width still comes straight from the plan.
        string viewBox = cut.Find("svg").GetAttribute("viewBox") ?? string.Empty;
        string[] parts = viewBox.Split(' ');

        await Assert.That(parts[2]).IsEqualTo("30");
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

    [Test]
    public async Task Draws_the_surplus_above_when_the_roll_continues_upwards()
    {
        var cut = Render<WrapPlanDiagram>(parameters => parameters
            .Add(p => p.Plan, OneBoxPlan())
            .Add(p => p.ContinuesDown, false));

        string markup = cut.Markup;

        await Assert.That(markup).Contains("↑ Roll continues");
        await Assert.That(markup.Contains("↓ Roll continues")).IsFalse();
    }

    [Test]
    public async Task Mirrors_the_cuts_when_the_roll_continues_upwards()
    {
        // One box would not distinguish the two: a single section starts at Y=0 either way.
        WrapPlan plan = PlanWrapLogic.Plan(
            [new GiftBox("Book", 22, 15, 4), new GiftBox("Mug", 12, 12, 12)],
            new WrapOptions(60, 0.5, RandomRestarts: 0)).Value;

        var down = Render<WrapPlanDiagram>(parameters => parameters
            .Add(p => p.Plan, plan).Add(p => p.ContinuesDown, true));
        var up = Render<WrapPlanDiagram>(parameters => parameters
            .Add(p => p.Plan, plan).Add(p => p.ContinuesDown, false));

        string firstYDown = down.FindAll("g.cut rect")[0].GetAttribute("y") ?? string.Empty;
        string firstYUp = up.FindAll("g.cut rect")[0].GetAttribute("y") ?? string.Empty;

        await Assert.That(firstYUp).IsNotEqualTo(firstYDown);
    }
}
