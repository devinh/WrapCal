using Bunit;
using Microsoft.AspNetCore.Components;
using WrapCal.Features.PlanWrap;

namespace WrapCal.UnitTests;

public class PlanWrapPageTests : BunitContext
{
    public PlanWrapPageTests()
    {
        // PlanWrap reads RendererInfo.IsInteractive to publish its data-interactive marker for
        // the Playwright suite. bUnit does not populate it by default and throws on access.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    [Test]
    public void Plan_page_renders_the_roll_inputs()
    {
        var cut = Render<PlanWrap>();

        // Find throws when the element is absent, so reaching the end is the assertion.
        cut.Find("#rollWidth");
        cut.Find("#overlapMargin");
        cut.Find("[data-testid='unit-select']");
        cut.Find("[data-testid='plan-button']");
    }

    [Test]
    public async Task Plan_page_starts_with_a_single_box_row()
    {
        var cut = Render<PlanWrap>();

        await Assert.That(cut.FindAll("tbody tr").Count).IsEqualTo(1);
    }

    [Test]
    public async Task Submitting_valid_defaults_shows_a_plan_and_a_diagram()
    {
        var cut = Render<PlanWrap>();

        await cut.Find("form").SubmitAsync();

        await Assert.That(cut.FindAll("[data-testid='sections']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("svg[role='img']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-testid='plan-error']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Submitting_a_box_too_large_for_the_roll_shows_an_error_instead_of_a_plan()
    {
        var cut = Render<PlanWrap>();

        for (int i = 0; i < 3; i++)
        {
            await cut.FindAll("tbody tr input[type='number']")[i].ChangeAsync("500");
        }

        await cut.Find("form").SubmitAsync();

        await Assert.That(cut.FindAll("[data-testid='plan-error']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("svg[role='img']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-testid='sections']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Adding_and_removing_boxes_changes_the_row_count()
    {
        var cut = Render<PlanWrap>();

        await Assert.That(cut.FindAll("tbody tr").Count).IsEqualTo(1);

        await cut.Find("[data-testid='add-box']").ClickAsync();
        await Assert.That(cut.FindAll("tbody tr").Count).IsEqualTo(2);

        await cut.FindAll("[data-testid='remove-box']")[1].ClickAsync();
        await Assert.That(cut.FindAll("tbody tr").Count).IsEqualTo(1);
    }
}
