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
            PaperMargin = 1.5,
        };

        WrapOptions options = input.ToOptions();

        await Assert.That(options.RollWidth).IsEqualTo(24.0);
        await Assert.That(options.PaperMargin).IsEqualTo(1.5);
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
}
