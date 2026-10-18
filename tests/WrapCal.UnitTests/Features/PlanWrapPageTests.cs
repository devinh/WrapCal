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
}
