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

        // 30in, 0.5in, and a 9 x 6 x 1.5in box, all restated in centimetres.
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
        // The behaviour being matched: a roll option reports without submitting.
        var cut = Render<PlanWrap>();

        await cut.Find(RollWidth).ChangeAsync("0");
        await cut.Find(RollWidth).BlurAsync();

        await Assert.That(cut.Markup).Contains("Roll Width must be between 1 and 1000.");
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
    public async Task Defaults_to_the_roll_continuing_upwards()
    {
        var cut = Render<PlanWrap>();

        await cut.Find(PlanButton).ClickAsync();

        string? diagram = cut.Find("[data-testid='diagram']").TextContent;

        await Assert.That(diagram).Contains("↑ Roll continues");
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
