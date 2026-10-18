using WrapCal.Features.PlanWrap;

namespace WrapCal.UnitTests;

public class PlanWrapInputTests
{
    [Test]
    public async Task Defaults_to_inches()
    {
        await Assert.That(new PlanWrapInput().InputUnit).IsEqualTo(LengthUnit.Inches);
    }

    [Test]
    public async Task Reports_results_in_the_same_unit_that_was_entered()
    {
        // The form used to carry a separate output unit that nothing could set, so choosing
        // inches still produced centimetres. Output now follows input.
        foreach (LengthUnit unit in new[] { LengthUnit.Inches, LengthUnit.Centimeters })
        {
            WrapOptions options = new PlanWrapInput { InputUnit = unit }.ToOptions();

            await Assert.That(options.InputUnit).IsEqualTo(unit);
            await Assert.That(options.OutputUnit).IsEqualTo(unit);
        }
    }

    [Test]
    public async Task Carries_the_roll_options_through_to_the_domain()
    {
        var input = new PlanWrapInput
        {
            RollWidth = 24,
            OverlapMargin = 1.5,
        };

        WrapOptions options = input.ToOptions();

        await Assert.That(options.RollWidth).IsEqualTo(24.0);
        await Assert.That(options.OverlapMargin).IsEqualTo(1.5);
    }

    [Test]
    public async Task Names_blank_boxes_by_their_position()
    {
        var input = new PlanWrapInput
        {
            Boxes = [new BoxInput { Name = "   " }, new BoxInput { Name = "Mug" }],
        };

        IReadOnlyList<GiftBox> boxes = input.ToBoxes();

        await Assert.That(boxes[0].Name).IsEqualTo("Box 1");
        await Assert.That(boxes[1].Name).IsEqualTo("Mug");
    }

    [Test]
    public async Task Maps_box_dimensions_through_untouched()
    {
        var input = new PlanWrapInput
        {
            Boxes = [new BoxInput { Name = "Mug", Length = 12, Width = 9, Height = 7 }],
        };

        GiftBox box = input.ToBoxes()[0];

        await Assert.That(box.Length).IsEqualTo(12.0);
        await Assert.That(box.Width).IsEqualTo(9.0);
        await Assert.That(box.Height).IsEqualTo(7.0);
    }

    [Test]
    public async Task Trims_whitespace_around_box_names()
    {
        var input = new PlanWrapInput { Boxes = [new BoxInput { Name = "  Mug  " }] };

        await Assert.That(input.ToBoxes()[0].Name).IsEqualTo("Mug");
    }

    [Test]
    public async Task Accepts_the_default_box()
    {
        await Assert.That(new PlanWrapInput().ValidateBoxes().Count).IsEqualTo(0);
    }

    [Test]
    public async Task Flags_a_bad_dimension_against_its_own_field_and_row()
    {
        var input = new PlanWrapInput { Boxes = [new BoxInput { Length = 0 }] };

        IReadOnlyList<BoxProblem> problems = input.ValidateBoxes();

        await Assert.That(problems.Count).IsEqualTo(1);
        await Assert.That(problems[0].Field).IsEqualTo(nameof(BoxInput.Length));
        await Assert.That(problems[0].Message).Contains("Length must be between");

        // Keyed to the row's own instance, so the message lands under that input and not elsewhere.
        await Assert.That(problems[0].Box).IsSameReferenceAs(input.Boxes[0]);
    }

    [Test]
    public async Task Flags_every_bad_side_of_a_box()
    {
        var input = new PlanWrapInput { Boxes = [new BoxInput { Length = 0, Width = -1, Height = 2000 }] };

        await Assert.That(input.ValidateBoxes().Count).IsEqualTo(3);
    }

    [Test]
    public async Task Flags_an_over_long_name()
    {
        var input = new PlanWrapInput { Boxes = [new BoxInput { Name = new string('x', 51) }] };

        IReadOnlyList<BoxProblem> problems = input.ValidateBoxes();

        await Assert.That(problems.Count).IsEqualTo(1);
        await Assert.That(problems[0].Field).IsEqualTo(nameof(BoxInput.Name));
    }

    [Test]
    public async Task Checks_every_box_row()
    {
        var input = new PlanWrapInput
        {
            Boxes = [new BoxInput(), new BoxInput { Width = 0 }],
        };

        await Assert.That(input.ValidateBoxes().Count).IsEqualTo(1);
    }
}
