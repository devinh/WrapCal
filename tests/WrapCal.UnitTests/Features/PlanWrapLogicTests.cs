using System.Globalization;
using WrapCal.Features.PlanWrap;

namespace WrapCal.UnitTests;

public class PlanWrapLogicTests
{
    private static WrapOptions Options(
        double rollWidth = 30,
        LengthUnit inputUnit = LengthUnit.Centimeters,
        LengthUnit outputUnit = LengthUnit.Centimeters) =>
        new(rollWidth, OverlapMargin: 0.5, InputUnit: inputUnit, OutputUnit: outputUnit,
            RandomRestarts: 25, Seed: 1);

    private static GiftBox Box(string name = "Book", double length = 22, double width = 15, double height = 4) =>
        new(name, length, width, height);

    [Test]
    public async Task Rejects_non_positive_roll_width()
    {
        var result = PlanWrapLogic.Plan([Box()], Options(rollWidth: 0));

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Code).IsEqualTo("roll.width");
    }

    [Test]
    public async Task Rejects_non_positive_box_dimension()
    {
        var result = PlanWrapLogic.Plan([Box(height: 0)], Options());

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Code).IsEqualTo("box.dimensions");
    }

    [Test]
    public async Task Rejects_a_negative_overlap_margin()
    {
        var result = PlanWrapLogic.Plan(
            [Box()],
            new WrapOptions(30, OverlapMargin: -0.5, RandomRestarts: 0));

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Code).IsEqualTo("overlap.margin");
    }

    [Test]
    public async Task Rejects_a_negative_restart_count()
    {
        var result = PlanWrapLogic.Plan([Box()], new WrapOptions(30, 0.5, RandomRestarts: -1));

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Code).IsEqualTo("options.restarts");
    }

    [Test]
    public async Task Rejects_more_boxes_than_the_form_allows()
    {
        // The planner keeps its own guard, so a caller that never saw the form cannot hand it a
        // list long enough to block the thread it runs on.
        GiftBox[] boxes = [.. Enumerable.Range(1, PlanWrapInput.MaxBoxCount + 1)
            .Select(i => new GiftBox($"Box {i}", 10, 10, 10))];

        var result = PlanWrapLogic.Plan(boxes, Options());

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Code).IsEqualTo("boxes.count");
    }

    [Test]
    public async Task Accepts_exactly_as_many_boxes_as_the_form_allows()
    {
        GiftBox[] boxes = [.. Enumerable.Range(1, PlanWrapInput.MaxBoxCount)
            .Select(i => new GiftBox($"Box {i}", 10, 10, 10))];

        var result = PlanWrapLogic.Plan(boxes, Options());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Sections.Count).IsEqualTo(PlanWrapInput.MaxBoxCount);
    }

    [Test]
    public async Task Rejects_a_box_too_wide_for_the_roll()
    {
        var result = PlanWrapLogic.Plan([Box(length: 500, width: 500, height: 500)], Options(rollWidth: 10));

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Code).IsEqualTo("box.too-wide");
    }

    [Test]
    public async Task Returns_an_empty_plan_for_no_boxes()
    {
        var result = PlanWrapLogic.Plan([], Options());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Sections.Count).IsEqualTo(0);
        await Assert.That(result.Value.RollLengthUsed).IsEqualTo(0.0);

        // Nothing was cut, so the plan is not 100% efficient — the empty ratio is not a ratio.
        await Assert.That(result.Value.Efficiency).IsEqualTo(0.0);
    }

    [Test]
    [Arguments(LengthUnit.Centimeters)]
    [Arguments(LengthUnit.Inches)]
    public async Task Areas_account_for_exactly_the_roll_it_used(LengthUnit unit)
    {
        // The one relation that catches an area-conversion mistake: everything cut plus
        // everything wasted is the roll the summary claims to have used.
        var result = PlanWrapLogic.Plan(
            [Box(), Box("Mug", 12, 12, 12)],
            new WrapOptions(30, 0.5, InputUnit: unit, OutputUnit: unit, RandomRestarts: 25, Seed: 1));

        await Assert.That(result.IsSuccess).IsTrue();

        WrapPlan plan = result.Value;
        double rollArea = plan.RollWidth * plan.RollLengthUsed;

        await Assert.That(Math.Abs(plan.PaperAreaCut + plan.WasteArea - rollArea)).IsLessThan(0.01);
        await Assert.That(plan.WasteArea).IsGreaterThanOrEqualTo(0.0);
    }

    [Test]
    public async Task Every_section_fits_across_the_roll()
    {
        const double rollWidth = 30;
        var result = PlanWrapLogic.Plan([Box(), Box("Mug", 12, 12, 12)], Options(rollWidth));

        await Assert.That(result.IsSuccess).IsTrue();

        foreach (CutSection section in result.Value.Sections)
        {
            await Assert.That(section.X + section.Width).IsLessThanOrEqualTo(rollWidth + 1e-6);
        }
    }

    [Test]
    public async Task Sections_never_overlap()
    {
        // All three must be wrappable within a 30 cm roll, or the plan fails before packing.
        var result = PlanWrapLogic.Plan(
            [Box(), Box("Mug", 10, 10, 10), Box("Shirt", 15, 10, 2)], Options());

        await Assert.That(result.IsSuccess).IsTrue();

        IReadOnlyList<CutSection> sections = result.Value.Sections;
        await Assert.That(sections.Count).IsGreaterThanOrEqualTo(3);

        for (int i = 0; i < sections.Count; i++)
        {
            for (int j = i + 1; j < sections.Count; j++)
            {
                await Assert.That(Overlaps(sections[i], sections[j])).IsFalse();
            }
        }
    }

    [Test]
    public async Task Treats_the_same_physical_box_identically_in_both_units()
    {
        // Same physical setup expressed twice: 8in x 4in x 2in is 20.32cm x 10.16cm x 5.08cm,
        // and a 30cm roll is 30/2.54 inches. The roll has to be converted too — comparing a
        // 30cm roll against a 30in roll would be comparing different physical rolls.
        var metric = PlanWrapLogic.Plan(
            [new GiftBox("B", 20.32, 10.16, 5.08)],
            new WrapOptions(30, 0.5, InputUnit: LengthUnit.Centimeters, OutputUnit: LengthUnit.Centimeters,
                RandomRestarts: 0));

        var imperial = PlanWrapLogic.Plan(
            [new GiftBox("B", 8, 4, 2)],
            new WrapOptions(
                RollWidth: 30 / UnitConverter.CmPerInch,
                OverlapMargin: 0.5 / UnitConverter.CmPerInch,
                InputUnit: LengthUnit.Inches,
                OutputUnit: LengthUnit.Centimeters,
                RandomRestarts: 0));

        await Assert.That(metric.IsSuccess).IsTrue();
        await Assert.That(imperial.IsSuccess).IsTrue();

        double difference = Math.Abs(metric.Value.RollLengthUsed - imperial.Value.RollLengthUsed);
        await Assert.That(difference).IsLessThanOrEqualTo(0.01);
    }

    [Test]
    public async Task Is_repeatable_for_a_fixed_seed()
    {
        var options = new WrapOptions(30, 0.5, RandomRestarts: 50, Seed: 12345);
        GiftBox[] boxes = [Box(), Box("Mug", 12, 12, 12)];

        var first = PlanWrapLogic.Plan(boxes, options);
        var second = PlanWrapLogic.Plan(boxes, options);

        await Assert.That(second.Value.RollLengthUsed).IsEqualTo(first.Value.RollLengthUsed);
    }

    [Test]
    public async Task Overlap_margin_is_exactly_the_seam_overlap()
    {
        // A 20cm cube goes around in 2 * (20 + 20) = 80cm, and needs 20 + max(20, 20) = 40cm
        // along the box for the fold-over (10cm per end, meeting in the middle of the end
        // face). Each of those dimensions has exactly one seam to close, so the margin is
        // added once to each: with 0 the paper meets without overlapping anywhere.
        WrapPlan without = OneBox(new GiftBox("Cube", 20, 20, 20), overlapMargin: 0);
        WrapPlan with = OneBox(new GiftBox("Cube", 20, 20, 20), overlapMargin: 2);

        await Assert.That(without.Sections[0].Width).IsEqualTo(80.0);
        await Assert.That(without.Sections[0].Length).IsEqualTo(40.0);

        // Both dimensions grow by exactly the margin — not twice it.
        await Assert.That(with.Sections[0].Width).IsEqualTo(82.0);
        await Assert.That(with.Sections[0].Length).IsEqualTo(42.0);
    }

    private static WrapPlan OneBox(GiftBox box, double overlapMargin) =>
        PlanWrapLogic.Plan(
            [box],
            new WrapOptions(200, overlapMargin, InputUnit: LengthUnit.Centimeters,
                OutputUnit: LengthUnit.Centimeters, RandomRestarts: 0)).Value;

    [Test]
    public async Task Describes_the_box_sides_the_paper_goes_around()
    {
        using var culture = InvariantCulture();

        // A flat book wraps about its length, so the paper encircles the 15 and 4 sides. It used
        // to be the 4 and 22 sides: that sheet is 52.5 wide where this one is 38.5, and no
        // shorter, so another sheet for the same box dominated it and the roll gained nothing by
        // carrying it. See Drops_a_dominated_sheet below.
        WrapPlan book = PlanWrapLogic.Plan(
            [new GiftBox("Book", 22, 15, 4)],
            new WrapOptions(60, 0.5, RandomRestarts: 0)).Value;

        await Assert.That(book.Sections[0].Note)
            .IsEqualTo("Wrapped around the box's 15.0 × 4.0 cm sides.");

        // An upright crate wraps about its height, so the paper goes around the 30 and 20 sides.
        WrapPlan crate = PlanWrapLogic.Plan(
            [new GiftBox("Crate", 30, 20, 10)],
            new WrapOptions(50, 1, RandomRestarts: 0)).Value;

        await Assert.That(crate.Sections[0].Note)
            .IsEqualTo("Wrapped around the box's 30.0 × 20.0 cm sides.");
    }

    [Test]
    public async Task States_the_note_in_the_requested_units()
    {
        using var culture = InvariantCulture();

        WrapPlan plan = PlanWrapLogic.Plan(
            [new GiftBox("Gadget", 10, 5, 2)],
            new WrapOptions(
                30, 0.5, InputUnit: LengthUnit.Inches, OutputUnit: LengthUnit.Inches,
                RandomRestarts: 0)).Value;

        await Assert.That(plan.Sections[0].Note)
            .IsEqualTo("Wrapped around the box's 5.0 × 2.0 in sides.");
    }

    [Test]
    public async Task Never_refers_to_the_roll()
    {
        // The sheet is cut off the roll before anyone wraps, so the roll is no reference to
        // orient against — every note has to stand on the box's own dimensions.
        WrapPlan plan = PlanWrapLogic.Plan(
            [new GiftBox("Book", 22, 15, 4), new GiftBox("Crate", 30, 20, 10)],
            new WrapOptions(60, 0.5)).Value;

        await Assert.That(plan.Sections.Count).IsEqualTo(2);

        foreach (CutSection section in plan.Sections)
        {
            await Assert.That(section.Note.Contains("roll", StringComparison.OrdinalIgnoreCase)).IsFalse();
        }
    }

    [Test]
    public async Task Reports_the_roll_width_in_the_plan()
    {
        var result = PlanWrapLogic.Plan([Box()], Options(rollWidth: 30));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.RollWidth).IsEqualTo(30.0);
    }

    [Test]
    public async Task Converts_the_reported_roll_width_to_the_output_unit()
    {
        // A 30 cm roll, reported in inches: 30 / 2.54 = 11.811...
        var result = PlanWrapLogic.Plan(
            [new GiftBox("B", 8, 4, 2)],
            new WrapOptions(30, 0.5, InputUnit: LengthUnit.Centimeters, OutputUnit: LengthUnit.Inches,
                RandomRestarts: 0));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(Math.Round(result.Value.RollWidth, 3)).IsEqualTo(11.811);
    }

    [Test]
    public async Task Efficiency_stays_within_zero_and_one()
    {
        var result = PlanWrapLogic.Plan([Box()], Options());

        await Assert.That(result.Value.Efficiency).IsGreaterThanOrEqualTo(0.0);
        await Assert.That(result.Value.Efficiency).IsLessThanOrEqualTo(1.0);
    }

    [Test]
    public async Task Drops_a_dominated_sheet()
    {
        // A 22 x 15 x 4 book can be cut 52.5 x 37.5, wrapping around the 15 x 4 sides, or
        // 38.5 x 37.5 around the 22 x 15 ones. The wider sheet is no shorter, so it fits
        // wherever the narrower one does and can never make the roll shorter — it only spends
        // paper. It used to be chosen, for exactly that width.
        WrapPlan plan = PlanWrapLogic.Plan(
            [new GiftBox("Book", 22, 15, 4)],
            new WrapOptions(60, 0.5, RandomRestarts: 0)).Value;

        await Assert.That(plan.Sections.Count).IsEqualTo(1);
        await Assert.That(plan.Sections[0].Width).IsEqualTo(38.5);
        await Assert.That(plan.Sections[0].Length).IsEqualTo(37.5);

        // The roll is the same either way; what changes is how much of it gets cut up.
        await Assert.That(plan.RollLengthUsed).IsEqualTo(37.5);
        await Assert.That(Math.Round(plan.PaperAreaCut, 6)).IsEqualTo(38.5 * 37.5);
    }

    [Test]
    public async Task Changes_a_boxes_orientation_when_that_shortens_the_roll()
    {
        // Twelve identical boxes are the case the first guess gets most wrong. It opens every
        // shelf with that box's shortest sheet, 28.5 x 12.5 — minimal in height, but too wide
        // for a second box to share the shelf, so twelve boxes draw twelve shelves. Rotating to
        // the 14.5 x 20.5 sheet costs height per shelf and halves the number of shelves.
        GiftBox[] boxes = [.. Enumerable.Range(0, 12).Select(_ => new GiftBox("Tile", 8, 6, 4))];

        WrapPlan firstGuess = PlanWrapLogic
            .Plan(boxes, new WrapOptions(30, 0.5, RandomRestarts: 0)).Value;

        WrapPlan searched = PlanWrapLogic
            .Plan(boxes, new WrapOptions(30, 0.5, RandomRestarts: 500, Seed: 42)).Value;

        await Assert.That(firstGuess.RollLengthUsed).IsEqualTo(150.0);
        await Assert.That(firstGuess.Sections.All(section => section.Width == 28.5)).IsTrue();

        // 5 shelves holding two each, plus two shelves holding one.
        await Assert.That(searched.RollLengthUsed).IsEqualTo(127.5);
        await Assert.That(searched.Sections.Count(section => section.Width == 14.5)).IsEqualTo(10);
        await Assert.That(searched.PaperAreaCut).IsLessThan(firstGuess.PaperAreaCut);
    }

    [Test]
    public async Task Never_uses_a_roll_the_paper_cannot_fill()
    {
        // Every sheet covers part of the roll, so no plan can use less roll area than the paper
        // it cuts. The bound ignores every gap, which is what makes it a bound rather than a
        // target — but it holds whatever the packer does, so a search that quietly stopped
        // earning its keep shows up here even though no exact plan is pinned. Measured at 1.17.
        GiftBox[] boxes =
        [
            new("A", 20, 14, 6), new("B", 15, 15, 15), new("C", 8, 8, 2), new("D", 25, 18, 5),
            new("E", 12, 9, 3), new("F", 18, 12, 7), new("G", 10, 10, 10), new("H", 30, 20, 8),
        ];

        WrapPlan plan = PlanWrapLogic.Plan(
            boxes,
            new WrapOptions(
                40, 0.5, InputUnit: LengthUnit.Inches, OutputUnit: LengthUnit.Inches,
                RandomRestarts: 200, Seed: 42)).Value;

        await Assert.That(plan.RollWidth * plan.RollLengthUsed)
            .IsLessThanOrEqualTo(plan.PaperAreaCut * 1.3);
    }

    [Test]
    public async Task Produces_a_known_plan_for_a_known_input()
    {
        // Golden test. The invariant tests above check that a plan is *valid*; none of them
        // check that it is *good*. A refactor could quietly double the roll length used and
        // every other test here would still pass. This pins the actual output.
        //
        // Seeded, so it is reproducible. If these numbers move, the optimiser's behaviour
        // changed — treat that as a deliberate decision to re-baseline, not an accident.
        //
        // Eight varied boxes on a 40in roll, deliberately: the search only earns its keep on an
        // input where the greedy order is contested. Measured on this exact set, roll length is
        // 380.5 with the search disabled and 371.5 with it on, so this test fails if the search
        // stops working.
        //
        // Re-check that sensitivity whenever the sheet maths changes. An earlier revision used
        // six boxes on a 60in roll; a margin change made it restart-insensitive (identical
        // output at every restart count) without touching a single line of this test, so it
        // would have passed with the optimiser crippled.
        GiftBox[] boxes =
        [
            new("A", 20, 14, 6), new("B", 15, 15, 15), new("C", 8, 8, 2),
            new("D", 25, 18, 5), new("E", 12, 9, 3), new("F", 18, 12, 7),
            new("G", 10, 10, 10), new("H", 30, 20, 8),
        ];

        var options = new WrapOptions(
            RollWidth: 40,
            OverlapMargin: 0.5,
            InputUnit: LengthUnit.Inches,
            OutputUnit: LengthUnit.Inches,
            RandomRestarts: 200,
            Seed: 42);

        var result = PlanWrapLogic.Plan(boxes, options);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Sections.Count).IsEqualTo(8);

        // Exact values are 371.5 and 0.8583109017496634, identical in Debug and Release.
        // Rounded to survive harmless float noise, but finely enough that any real change in
        // packing quality fails the test rather than slipping through.
        await Assert.That(Math.Round(result.Value.RollLengthUsed, 3)).IsEqualTo(371.5);
        await Assert.That(Math.Round(result.Value.Efficiency, 6)).IsEqualTo(0.858311);
    }

    /// <summary>
    /// The note is spelled in the reader's culture, so asserting its wording needs a culture of
    /// its own — otherwise these pass on a machine that writes 4.0 and fail on one that writes
    /// 4,0, for a reason that has nothing to do with the note.
    /// </summary>
    private static IDisposable InvariantCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        return new RestoreCulture(previous);
    }

    private sealed class RestoreCulture(CultureInfo previous) : IDisposable
    {
        public void Dispose() => CultureInfo.CurrentCulture = previous;
    }

    private static bool Overlaps(CutSection a, CutSection b) =>
        a.X < b.X + b.Width - 1e-6 &&
        b.X < a.X + a.Width - 1e-6 &&
        a.Y < b.Y + b.Length - 1e-6 &&
        b.Y < a.Y + a.Length - 1e-6;
}
