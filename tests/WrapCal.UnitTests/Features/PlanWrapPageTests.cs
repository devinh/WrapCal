using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using WrapCal.Features.PlanWrap;

namespace WrapCal.UnitTests;

/// <summary>
/// The page is built from MudBlazor components now, so the selectors follow what those render:
/// <c>data-testid</c> lands on the input element itself, numeric fields are text inputs, and the
/// unit picker is a MudSelect rather than a native &lt;select&gt;. Validation follows Mud's
/// timing too — a field reports when it is left, not as each character is typed.
/// </summary>
public class PlanWrapPageTests : WrapCalBunitContext
{
    private const string RollWidth = "[data-testid='roll-width']";
    private const string OverlapMargin = "[data-testid='overlap-margin']";
    private const string UnitSelect = "[data-testid='unit-select']";
    private const string PlanButton = "[data-testid='plan-button']";
    private const string BoxLength = "[data-testid='box-length']";
    private const string BoxWidth = "[data-testid='box-width']";
    private const string BoxHeight = "[data-testid='box-height']";
    private const string BoxName = "[data-testid='box-name']";

    [Test]
    public void Plan_page_renders_the_roll_inputs()
    {
        var cut = Render<PlanWrap>();

        // Find throws when the element is absent, so reaching the end is the assertion.
        cut.Find(RollWidth);
        cut.Find(OverlapMargin);
        cut.Find(UnitSelect);
        cut.Find(PlanButton);
    }

    [Test]
    public async Task Plan_page_has_one_level_one_heading()
    {
        var cut = Render<PlanWrap>();

        // Router's FocusOnNavigate targets h1, and a page with no heading element is one a
        // screen reader cannot skim. MudText renders a <p> unless HtmlTag says otherwise, which
        // is exactly how this page ended up with no heading at all.
        var headings = cut.FindAll("h1");

        await Assert.That(headings.Count).IsEqualTo(1);
        await Assert.That(headings[0].TextContent).IsEqualTo("Wrapping Paper Planner");
    }

    [Test]
    public async Task Plan_page_starts_with_a_single_box_row()
    {
        var cut = Render<PlanWrap>();

        await Assert.That(cut.FindAll("[data-testid='box-name']").Count).IsEqualTo(1);
    }

    [Test]
    public async Task Submitting_valid_defaults_shows_a_plan_and_a_diagram()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(PlanButton).ClickAsync();

        await Assert.That(cut.FindAll("[data-testid='sections']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-testid='diagram']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-testid='plan-error']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Submitting_a_box_too_large_for_the_roll_shows_an_error_instead_of_a_plan()
    {
        var cut = Render<PlanWrap>();

        foreach (string dimension in new[] { "box-length", "box-width", "box-height" })
        {
            await cut.Find($"[data-testid='{dimension}']").ChangeAsync("500");
        }

        await cut.Find(PlanButton).ClickAsync();

        await Assert.That(cut.FindAll("[data-testid='plan-error']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-testid='diagram']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-testid='sections']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Changing_units_converts_every_entered_length()
    {
        var cut = Render<PlanWrap>();

        await SelectUnitAsync(cut, LengthUnit.Centimeters);

        // 30in, 0.5in, and a 9 x 6 x 1.5in box, all restated in centimeters.
        await Assert.That(cut.Find(RollWidth).GetAttribute("value")).IsEqualTo("76.2");
        await Assert.That(cut.Find(OverlapMargin).GetAttribute("value")).IsEqualTo("1.27");

        await Assert.That(cut.Find("[data-testid='box-length']").GetAttribute("value")).IsEqualTo("22.86");
        await Assert.That(cut.Find("[data-testid='box-width']").GetAttribute("value")).IsEqualTo("15.24");
        await Assert.That(cut.Find("[data-testid='box-height']").GetAttribute("value")).IsEqualTo("3.81");
    }

    [Test]
    public async Task Rounds_converted_lengths_to_two_decimals()
    {
        var cut = Render<PlanWrap>();

        await SelectUnitAsync(cut, LengthUnit.Centimeters);
        await cut.Find(RollWidth).ChangeAsync("10");
        await SelectUnitAsync(cut, LengthUnit.Inches);

        // 10 cm is 3.9370078... inches.
        await Assert.That(cut.Find(RollWidth).GetAttribute("value")).IsEqualTo("3.94");
    }

    [Test]
    public async Task Changing_units_discards_a_plan_stated_in_the_old_units()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(PlanButton).ClickAsync();
        await Assert.That(cut.FindAll("[data-testid='diagram']").Count).IsEqualTo(1);

        await SelectUnitAsync(cut, LengthUnit.Centimeters);

        await Assert.That(cut.FindAll("[data-testid='diagram']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Reports_a_bad_box_dimension_as_soon_as_the_field_is_left()
    {
        var cut = Render<PlanWrap>();

        // No submit: leaving a box field is enough, which is MudBlazor's own timing.
        await cut.Find(BoxLength).ChangeAsync("0");
        await cut.Find(BoxLength).BlurAsync();

        await Assert.That(cut.Markup).Contains("Length must be between 0.1 and 1000.");
    }

    [Test]
    public async Task Clears_the_box_message_once_the_dimension_is_fixed()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(BoxLength).ChangeAsync("0");
        await cut.Find(BoxLength).BlurAsync();
        await Assert.That(cut.Markup).Contains("Length must be between 0.1 and 1000.");

        await cut.Find(BoxLength).ChangeAsync("9");
        await cut.Find(BoxLength).BlurAsync();
        await Assert.That(cut.Markup.Contains("Length must be between 0.1 and 1000.")).IsFalse();
    }

    [Test]
    public async Task Reports_roll_option_problems_on_change_too()
    {
        // The behavior being matched: a roll option reports without submitting.
        var cut = Render<PlanWrap>();

        await cut.Find(RollWidth).ChangeAsync("0");
        await cut.Find(RollWidth).BlurAsync();

        await Assert.That(cut.Markup).Contains("Roll Width must be between 1 and 1000.");
    }

    [Test]
    public async Task Reports_an_overlap_margin_outside_its_range()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(OverlapMargin).ChangeAsync("200");
        await cut.Find(OverlapMargin).BlurAsync();

        await Assert.That(cut.Markup).Contains("Overlap Margin must be between 0 and 100.");

        // A range has two ends, and the message alone cannot tell which one was tripped, so the
        // valid value in between is what proves both sides are checked.
        await cut.Find(OverlapMargin).ChangeAsync("10");
        await cut.Find(OverlapMargin).BlurAsync();
        await Assert.That(cut.Markup.Contains("Overlap Margin must be between")).IsFalse();

        await cut.Find(OverlapMargin).ChangeAsync("-1");
        await cut.Find(OverlapMargin).BlurAsync();

        await Assert.That(cut.Markup).Contains("Overlap Margin must be between 0 and 100.");
    }

    [Test]
    public async Task Reports_a_bad_box_width()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(BoxWidth).ChangeAsync("0");
        await cut.Find(BoxWidth).BlurAsync();

        // The whole message, upper bound included: "Width must be between" is a substring of the
        // roll width's message, so a shorter assertion here would pass on the wrong field's text.
        await Assert.That(cut.Markup).Contains("Width must be between 0.1 and 1000.");
    }

    [Test]
    public async Task Reports_a_bad_box_height()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(BoxHeight).ChangeAsync("0");
        await cut.Find(BoxHeight).BlurAsync();

        await Assert.That(cut.Markup).Contains("Height must be between 0.1 and 1000.");
    }

    [Test]
    public async Task Reports_a_roll_wider_than_the_limit()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(RollWidth).ChangeAsync("1000.1");
        await cut.Find(RollWidth).BlurAsync();

        await Assert.That(cut.Markup).Contains("Roll Width must be between 1 and 1000.");
    }

    [Test]
    public async Task Reports_a_name_longer_than_the_limit()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(BoxName).ChangeAsync(new string('x', PlanWrapInput.MaxNameLength + 1));
        await cut.Find(BoxName).BlurAsync();

        await Assert.That(cut.Markup).Contains("Name must be 50 characters or fewer.");

        // The limit is inclusive, so a name exactly at it is allowed.
        await cut.Find(BoxName).ChangeAsync(new string('x', PlanWrapInput.MaxNameLength));
        await cut.Find(BoxName).BlurAsync();

        await Assert.That(cut.Markup.Contains("Name must be 50 characters or fewer.")).IsFalse();
    }

    [Test]
    public async Task Measures_the_name_after_trimming_it()
    {
        // The rule trims before measuring, so padding a name to the limit is fine and padding it
        // past the limit is not — the two differ only in whitespace, which is exactly what a
        // length check on the raw text would get wrong.
        string atLimit = new('x', PlanWrapInput.MaxNameLength);
        var cut = Render<PlanWrap>();

        await cut.Find(BoxName).ChangeAsync($"   {atLimit}   ");
        await cut.Find(BoxName).BlurAsync();

        await Assert.That(cut.Markup.Contains("Name must be 50 characters or fewer.")).IsFalse();

        await cut.Find(BoxName).ChangeAsync($"   {atLimit}x   ");
        await cut.Find(BoxName).BlurAsync();

        await Assert.That(cut.Markup).Contains("Name must be 50 characters or fewer.");
    }

    [Test]
    public async Task Reports_every_bad_field_at_once()
    {
        // One message per field, not one per submit: a person fixing a row usually has more than
        // one thing wrong with it, and being told one at a time is a loop of failed submits.
        var cut = Render<PlanWrap>();

        await cut.Find(BoxLength).ChangeAsync("0");
        await cut.Find(BoxLength).BlurAsync();
        await cut.Find(BoxWidth).ChangeAsync("0");
        await cut.Find(BoxWidth).BlurAsync();

        await Assert.That(cut.Markup).Contains("Length must be between 0.1 and 1000.");
        await Assert.That(cut.Markup).Contains("Width must be between 0.1 and 1000.");
    }

    [Test]
    public async Task Accepts_values_exactly_on_the_limits()
    {
        // Every rule is written `value is < Min or > Max`, so a value sitting exactly on a limit
        // is valid. That is the off-by-one this shape of guard invites, so each end of each rule
        // is pinned — including the lower ends, which are one character away from the bounds
        // above them and would otherwise go untested.
        var cut = Render<PlanWrap>();

        foreach (string atLimit in (string[])["1", "1000"])
        {
            await cut.Find(RollWidth).ChangeAsync(atLimit);
            await cut.Find(RollWidth).BlurAsync();
            await Assert.That(cut.Markup.Contains("Roll Width must be between")).IsFalse();
        }

        foreach (string atLimit in (string[])["0", "100"])
        {
            await cut.Find(OverlapMargin).ChangeAsync(atLimit);
            await cut.Find(OverlapMargin).BlurAsync();
            await Assert.That(cut.Markup.Contains("Overlap Margin must be between")).IsFalse();
        }

        foreach (string atLimit in (string[])["0.1", "1000"])
        {
            await cut.Find(BoxLength).ChangeAsync(atLimit);
            await cut.Find(BoxLength).BlurAsync();
            await cut.Find(BoxWidth).ChangeAsync(atLimit);
            await cut.Find(BoxWidth).BlurAsync();
            await cut.Find(BoxHeight).ChangeAsync(atLimit);
            await cut.Find(BoxHeight).BlurAsync();

            await Assert.That(cut.Markup.Contains("must be between")).IsFalse();
        }
    }

    [Test]
    public async Task Rejects_a_dimension_just_past_the_limit()
    {
        // The companion to the boundary test: a guard that accepted everything would pass that
        // one, so the far side of the same limit has to be pinned too.
        var cut = Render<PlanWrap>();

        await cut.Find(BoxLength).ChangeAsync("1000.1");
        await cut.Find(BoxLength).BlurAsync();

        await Assert.That(cut.Markup).Contains("Length must be between 0.1 and 1000.");

        await cut.Find(BoxWidth).ChangeAsync("0.05");
        await cut.Find(BoxWidth).BlurAsync();

        await Assert.That(cut.Markup).Contains("Width must be between 0.1 and 1000.");
    }

    [Test]
    public async Task Rejects_a_box_with_a_non_positive_dimension_before_planning()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(BoxLength).ChangeAsync("0");
        await cut.Find(PlanButton).ClickAsync();

        // Field-level validation blocks the submit, so no plan is produced and the message is
        // attached to the offending field rather than surfacing as a logic error.
        await Assert.That(cut.FindAll("[data-testid='diagram']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-testid='plan-error']").Count).IsEqualTo(0);
        await Assert.That(cut.Markup).Contains("Length must be between 0.1 and 1000.");
    }

    [Test]
    public async Task Signs_off_at_the_bottom_of_the_page()
    {
        // Outside the plan block, so it is there before a plan exists as well as after.
        var cut = Render<PlanWrap>();

        await Assert.That(cut.Markup).Contains("Vibed With 🫶");

        await cut.Find(PlanButton).ClickAsync();

        await Assert.That(cut.Markup).Contains("Vibed With 🫶");
    }

    [Test]
    public async Task Defaults_to_the_roll_continuing_upwards()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(PlanButton).ClickAsync();

        string? diagram = cut.Find("[data-testid='diagram']").TextContent;

        await Assert.That(diagram).Contains("↑ Roll continues");
    }

    [Test]
    public async Task Discards_a_plan_the_form_no_longer_describes()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(PlanButton).ClickAsync();
        await Assert.That(cut.FindAll("[data-testid='diagram']").Count).IsEqualTo(1);

        // Editing a field leaves the plan describing inputs that are gone; showing it anyway
        // would state numbers the form in front of the user does not produce.
        await cut.Find(BoxLength).ChangeAsync("18");

        await Assert.That(cut.FindAll("[data-testid='diagram']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Discards_the_plan_when_a_box_is_added()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(PlanButton).ClickAsync();
        await Assert.That(cut.FindAll("[data-testid='diagram']").Count).IsEqualTo(1);

        await cut.Find("[data-testid='add-box']").ClickAsync();

        await Assert.That(cut.FindAll("[data-testid='diagram']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Stops_offering_new_rows_at_the_box_limit()
    {
        // The planner is quadratic in the box count, so an uncapped list is a way to make the
        // page that is showing it unresponsive. The cap is the form's half of that guard.
        var cut = Render<PlanWrap>();

        for (int i = 1; i < PlanWrapInput.MaxBoxCount; i++)
        {
            await cut.Find("[data-testid='add-box']").ClickAsync();
        }

        await Assert.That(cut.FindAll("[data-testid='box-name']").Count)
            .IsEqualTo(PlanWrapInput.MaxBoxCount);
        await Assert.That(cut.Find("[data-testid='add-box']").HasAttribute("disabled")).IsTrue();
    }

    [Test]
    public async Task Refuses_a_box_past_the_limit_when_the_page_is_driven_directly()
    {
        // Past the limit the button is disabled, so a click cannot reach the page at all — this is
        // the path in, and the guard is what keeps the limit a rule rather than a rendering detail.
        var cut = Render<PlanWrap>();

        for (int i = 1; i < PlanWrapInput.MaxBoxCount; i++)
        {
            await cut.Find("[data-testid='add-box']").ClickAsync();
        }

        cut.Instance.AddBox();
        cut.Render(_ => { });

        await Assert.That(cut.FindAll("[data-testid='box-name']").Count)
            .IsEqualTo(PlanWrapInput.MaxBoxCount);
    }

    [Test]
    public async Task Reapplying_the_unit_already_in_force_leaves_the_plan_alone()
    {
        // The select only raises a change when the value actually moves, so a duplicate or
        // replayed event lands on the no-op path instead — and must not throw away a plan that
        // still describes the form in front of the reader.
        var cut = Render<PlanWrap>();

        await cut.Find(PlanButton).ClickAsync();
        await Assert.That(cut.FindAll("[data-testid='diagram']").Count).IsEqualTo(1);

        await cut.Instance.OnUnitChangedAsync();

        await Assert.That(cut.FindAll("[data-testid='diagram']").Count).IsEqualTo(1);
    }

    [Test]
    public async Task Adding_and_removing_boxes_changes_the_row_count()
    {
        var cut = Render<PlanWrap>();

        await Assert.That(cut.FindAll("[data-testid='box-name']").Count).IsEqualTo(1);

        await cut.Find("[data-testid='add-box']").ClickAsync();
        await Assert.That(cut.FindAll("[data-testid='box-name']").Count).IsEqualTo(2);

        await cut.FindAll("[data-testid='remove-box']")[1].ClickAsync();
        await Assert.That(cut.FindAll("[data-testid='box-name']").Count).IsEqualTo(1);
    }

    /// <summary>
    /// MudSelect renders an overlay, not a native &lt;select&gt;, so there is nothing for bUnit's
    /// ChangeAsync to drive. Asking the component to select the option is the same call its own
    /// menu item makes, and it flows through the page's two-way binding.
    /// </summary>
    private static Task SelectUnitAsync(IRenderedComponent<PlanWrap> cut, LengthUnit unit) =>
        cut.InvokeAsync(() => cut.FindComponent<MudSelect<LengthUnit>>().Instance.SelectOption(unit));
}
