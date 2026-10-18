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
    public async Task Reports_a_bad_box_dimension_as_soon_as_the_field_changes()
    {
        var cut = Render<PlanWrap>();

        // No submit: box rows should report on blur, the same as the roll options.
        await cut.FindAll("tbody tr input[type='number']")[0].ChangeAsync("0");

        await Assert.That(cut.Markup).Contains("Length must be between 0.1 and 1000.");
    }

    [Test]
    public async Task Clears_the_box_message_once_the_dimension_is_fixed()
    {
        var cut = Render<PlanWrap>();

        await cut.FindAll("tbody tr input[type='number']")[0].ChangeAsync("0");
        await Assert.That(cut.Markup).Contains("Length must be between 0.1 and 1000.");

        await cut.FindAll("tbody tr input[type='number']")[0].ChangeAsync("9");
        await Assert.That(cut.Markup.Contains("Length must be between 0.1 and 1000.")).IsFalse();
    }

    [Test]
    public async Task Reports_roll_option_problems_on_change_too()
    {
        // The behaviour being matched: a roll option reports without submitting.
        var cut = Render<PlanWrap>();

        await cut.Find("#rollWidth").ChangeAsync("0");

        await Assert.That(cut.Markup).Contains("Roll Width must be between 1 and 1000.");
    }

    [Test]
    public async Task Rejects_a_box_with_a_non_positive_dimension_before_planning()
    {
        var cut = Render<PlanWrap>();

        await cut.FindAll("tbody tr input[type='number']")[0].ChangeAsync("0");
        await cut.Find("form").SubmitAsync();

        // Field-level validation blocks the submit, so no plan is produced and the message is
        // attached to the offending field rather than surfacing as a logic error.
        await Assert.That(cut.FindAll("svg[role='img']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-testid='plan-error']").Count).IsEqualTo(0);
        await Assert.That(cut.Markup).Contains("Length must be between 0.1 and 1000.");
    }

    [Test]
    public async Task Defaults_to_the_roll_continuing_upwards()
    {
        var cut = Render<PlanWrap>();

        await cut.Find("form").SubmitAsync();

        string? diagram = cut.Find("svg[role='img']").TextContent;

        await Assert.That(diagram).Contains("↑ Roll continues");
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
