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
            SearchIterations: 25, Seed: 1);

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
            new WrapOptions(30, OverlapMargin: -0.5, SearchIterations: 0));

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Code).IsEqualTo("overlap.margin");
    }

    [Test]
    public async Task Rejects_a_negative_search_iteration_count()
    {
        var result = PlanWrapLogic.Plan([Box()], new WrapOptions(30, 0.5, SearchIterations: -1));

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Code).IsEqualTo("options.iterations");
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
            new WrapOptions(30, 0.5, InputUnit: unit, OutputUnit: unit, SearchIterations: 25, Seed: 1));

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
                SearchIterations: 0));

        var imperial = PlanWrapLogic.Plan(
            [new GiftBox("B", 8, 4, 2)],
            new WrapOptions(
                RollWidth: 30 / UnitConverter.CmPerInch,
                OverlapMargin: 0.5 / UnitConverter.CmPerInch,
                InputUnit: LengthUnit.Inches,
                OutputUnit: LengthUnit.Centimeters,
                SearchIterations: 0));

        await Assert.That(metric.IsSuccess).IsTrue();
        await Assert.That(imperial.IsSuccess).IsTrue();

        double difference = Math.Abs(metric.Value.RollLengthUsed - imperial.Value.RollLengthUsed);
        await Assert.That(difference).IsLessThanOrEqualTo(0.01);
    }

    [Test]
    public async Task Is_repeatable_for_a_fixed_seed()
    {
        // Sixteen boxes of different sizes, so this exercises the seeded path: the search, the
        // re-partitioning and the rounds between them. Two boxes would land on the exhaustive
        // solver, which takes no random number at all and would be repeatable without a seed.
        var options = new WrapOptions(60, 0.5, Seed: 12345);
        GiftBox[] boxes = DifferentSizes(16);

        WrapPlan first = PlanWrapLogic.Plan(boxes, options).Value;
        WrapPlan second = PlanWrapLogic.Plan(boxes, options).Value;

        await Assert.That(second.RollLengthUsed).IsEqualTo(first.RollLengthUsed);

        // The same plan, not merely the same length — the layout has to be identical too.
        await Assert.That(second.Sections.Count).IsEqualTo(first.Sections.Count);

        for (int i = 0; i < first.Sections.Count; i++)
        {
            await Assert.That(second.Sections[i]).IsEqualTo(first.Sections[i]);
        }
    }

    [Test]
    public async Task Overlap_margin_is_exactly_the_seam_overlap()
    {
        // A 22 x 15 x 4 book wraps around its width, so the paper goes round the 22 x 4 end in
        // 2 * (22 + 4) = 52cm, and needs 15 + min(22, 4) = 19cm along the box: the fold-over
        // closes the 22 x 4 end face, and half its SHORTER side per end is enough, because the
        // flaps that meet in the middle already span the longer direction. Each dimension has
        // exactly one seam to close, so the margin is added once to each: with 0 the paper meets
        // without overlapping anywhere.
        //
        // All three sides differ on purpose. A cube cannot tell min from max — the two sides are
        // equal — which is how the fold-over came to be using the larger one unnoticed; and a box
        // with two equal sides is only half better, because its axes produce duplicate sheets
        // that the dedupe merges, so only two distinct orientations get exercised. Here all three
        // axes compute differently, and the same one is chosen at both margins, so the "grows by
        // exactly the margin" claim is about one sheet rather than two.
        WrapPlan without = OneBox(new GiftBox("Book", 22, 15, 4), overlapMargin: 0);
        WrapPlan with = OneBox(new GiftBox("Book", 22, 15, 4), overlapMargin: 2);

        await Assert.That(without.Sections[0].Width).IsEqualTo(52.0);
        await Assert.That(without.Sections[0].Length).IsEqualTo(19.0);

        // Both dimensions grow by exactly the margin — not twice it.
        await Assert.That(with.Sections[0].Width).IsEqualTo(54.0);
        await Assert.That(with.Sections[0].Length).IsEqualTo(21.0);
    }

    private static WrapPlan OneBox(GiftBox box, double overlapMargin) =>
        PlanWrapLogic.Plan(
            [box],
            new WrapOptions(200, overlapMargin, InputUnit: LengthUnit.Centimeters,
                OutputUnit: LengthUnit.Centimeters, SearchIterations: 0)).Value;

    [Test]
    public async Task Describes_the_box_sides_the_paper_goes_around()
    {
        using var culture = InvariantCulture();

        // A flat book wraps about its width, so the paper encircles the 4 and 22 sides. Which
        // sides get encircled follows the shortest sheet, and that moved when the fold-over
        // stopped charging for the longer cross-section side: 52.5 x 19.5 now beats 38.5 x 26.5.
        WrapPlan book = PlanWrapLogic.Plan(
            [new GiftBox("Book", 22, 15, 4)],
            new WrapOptions(60, 0.5, SearchIterations: 0)).Value;

        await Assert.That(book.Sections[0].Note)
            .IsEqualTo("Wrapped around the box's 4.0 × 22.0 cm sides.");

        // An upright crate wraps about its height, so the paper goes around the 30 and 20 sides.
        WrapPlan crate = PlanWrapLogic.Plan(
            [new GiftBox("Crate", 30, 20, 10)],
            new WrapOptions(50, 1, SearchIterations: 0)).Value;

        await Assert.That(crate.Sections[0].Note)
            .IsEqualTo("Wrapped around the box's 20.0 × 10.0 cm sides.");
    }

    [Test]
    public async Task States_the_note_in_the_requested_units()
    {
        using var culture = InvariantCulture();

        WrapPlan plan = PlanWrapLogic.Plan(
            [new GiftBox("Gadget", 10, 5, 2)],
            new WrapOptions(
                30, 0.5, InputUnit: LengthUnit.Inches, OutputUnit: LengthUnit.Inches,
                SearchIterations: 0)).Value;

        await Assert.That(plan.Sections[0].Note)
            .IsEqualTo("Wrapped around the box's 2.0 × 10.0 in sides.");
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
                SearchIterations: 0));

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
    public async Task Never_offers_a_dominated_sheet()
    {
        // A 9 x 6 x 1.5 box wrapped around its 1.5 height is 30.5 around and 8 along, and the
        // same box wrapped around its 6 width is 21.5 around and 8 along. Both are the same
        // width on the roll, so the 30.5 one fits wherever the 8 x 21.5 one does and can never
        // shorten the plan — it only spends paper. It must not be offered at all.
        List<PlanWrapLogic.SheetOption> options =
            PlanWrapLogic.SheetOptions(new GiftBox("Box", 9, 6, 1.5), new WrapOptions(30, 0.5));

        await Assert.That(options.Any(option => Is(option.Width, 8) && Is(option.Length, 30.5)))
            .IsFalse();

        // The sheet that dominates it survives, along with every sheet it did not dominate.
        await Assert.That(options.Any(option => Is(option.Width, 8) && Is(option.Length, 21.5)))
            .IsTrue();
        await Assert.That(options.Count).IsEqualTo(4);

        static bool Is(double actual, double expected) => Math.Abs(actual - expected) <= 1e-9;
    }

    [Test]
    public async Task Cuts_identical_boxes_from_identical_paper()
    {
        // The reported case, as reported: three 9 x 6 x 1.5 boxes came back as 15.5 x 15.5,
        // 11 x 30.5 and 15.5 x 15.5, using 46 of roll. Both halves of that were wrong — the
        // fold-over was cutting far more paper than wrapping needs, and nothing stopped two
        // identically sized boxes being cut differently.
        GiftBox[] boxes = [.. Enumerable.Range(1, 3).Select(n => new GiftBox($"Box {n}", 9, 6, 1.5))];

        WrapPlan plan = PlanWrapLogic.Plan(boxes, new WrapOptions(30, 0.5)).Value;

        await Assert.That(plan.Sections.Count).IsEqualTo(3);
        await Assert.That(Math.Round(plan.RollLengthUsed, 3)).IsEqualTo(21.5);

        // One piece of paper for all three. Any of them may be laid out either way round — that
        // is the same rectangle of paper, so the pair is compared unordered.
        var pieces = plan.Sections
            .Select(section => (Small: Math.Min(section.Width, section.Length),
                Large: Math.Max(section.Width, section.Length)))
            .Distinct();

        await Assert.That(pieces.Count()).IsEqualTo(1);
        await Assert.That(Math.Round(pieces.Single().Small, 3)).IsEqualTo(8.0);
        await Assert.That(Math.Round(pieces.Single().Large, 3)).IsEqualTo(21.5);
    }

    [Test]
    public async Task Still_cuts_differently_sized_boxes_differently()
    {
        // The grouping is on the dimensions, not on the plan: boxes that are not the same size
        // must each get the sheet that suits them.
        GiftBox[] boxes =
        [
            new("Book", 22, 15, 4), new("Mug", 10, 10, 10), new("Book", 22, 15, 4),
        ];

        WrapPlan plan = PlanWrapLogic.Plan(boxes, new WrapOptions(60, 0.5)).Value;

        var books = plan.Sections.Where(section => section.ItemName == "Book").ToList();

        await Assert.That(books.Count).IsEqualTo(2);
        await Assert.That(Math.Abs(books[0].Width - books[1].Width)).IsLessThan(1e-9);
        await Assert.That(Math.Abs(books[0].Length - books[1].Length)).IsLessThan(1e-9);
        await Assert.That(Math.Abs(books[0].Width - plan.Sections.Single(s => s.ItemName == "Mug").Width))
            .IsGreaterThan(1e-9);
    }

    [Test]
    public async Task Picks_the_rotation_that_shortens_the_roll()
    {
        // An 8 x 6 x 4 box is cut from a 24.5 x 10.5 piece once the fold-over is right. Laid
        // down 24.5 wide it needs a shelf each — twelve shelves of 10.5 — while laid down 10.5
        // wide two fit across a 30cm roll but every strip would be 24.5 deep. One per shelf wins
        // by 21cm of roll, so the rotation is not a detail.
        GiftBox[] boxes = [.. Enumerable.Range(0, 12).Select(_ => new GiftBox("Tile", 8, 6, 4))];

        WrapPlan plan = PlanWrapLogic.Plan(boxes, new WrapOptions(30, 0.5)).Value;

        await Assert.That(Math.Round(plan.RollLengthUsed, 3)).IsEqualTo(126.0);
        await Assert.That(plan.Sections.Count).IsEqualTo(12);
        await Assert.That(plan.Sections.All(section => section.Width == 24.5)).IsTrue();
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
                SearchIterations: 200, Seed: 42)).Value;

        await Assert.That(plan.RollWidth * plan.RollLengthUsed)
            .IsLessThanOrEqualTo(plan.PaperAreaCut * 1.3);
    }

    [Test]
    public async Task Produces_a_known_plan_above_the_exact_threshold()
    {
        // The same guard as the golden test, for the half of the planner it cannot reach: sixteen
        // boxes is over the exhaustive solver's limit, so this input exercises the search and the
        // exact re-partitioning that follows it. Every box is a different size on purpose — when
        // sizes repeat, the uniform-piece rule makes them interchangeable and the first guess
        // usually lands on the answer by itself, which would prove nothing.
        GiftBox[] boxes = DifferentSizes(16);

        WrapPlan searched = PlanWrapLogic.Plan(boxes, new WrapOptions(60, 0.5, Seed: 42)).Value;

        // Nothing but the first guess, and no re-partitioning either — which is what makes this
        // test fail if the optimizer stops working, in either half.
        WrapPlan firstGuess = PlanWrapLogic
            .Plan(boxes, new WrapOptions(60, 0.5, SearchIterations: 0)).Value;

        await Assert.That(firstGuess.RollLengthUsed).IsGreaterThan(searched.RollLengthUsed);
        await Assert.That(Math.Round(searched.RollLengthUsed, 1)).IsEqualTo(67.0);
    }

    /// <summary>Boxes that differ from one another, so nothing can be shared between them.</summary>
    private static GiftBox[] DifferentSizes(int count) =>
    [
        .. Enumerable.Range(0, count).Select(i => new GiftBox(
            $"Box {i + 1}", 4 + (i % 9), 3 + (i % 7), 2 + (i % 5))),
    ];

    [Test]
    public async Task A_missing_iteration_cap_means_the_work_budget()
    {
        // Two ways of clamping to the same thing have to produce the same plan: dropping the cap
        // and asking for far more than the budget allows both mean "as much as the budget will
        // pay for". What they must not mean is "no search" or "no limit".
        GiftBox[] boxes = ManyBoxes(7);

        WrapPlan byDefault = PlanWrapLogic.Plan(boxes, new WrapOptions(60, 0.5, Seed: 42)).Value;

        WrapPlan byHugeCap = PlanWrapLogic
            .Plan(boxes, new WrapOptions(60, 0.5, SearchIterations: 100_000, Seed: 42)).Value;

        await Assert.That(byHugeCap.RollLengthUsed).IsEqualTo(byDefault.RollLengthUsed);
    }

    /// <summary>Four sizes repeated, so the input is stated here rather than generated.</summary>
    private static GiftBox[] ManyBoxes(int copies = 4)
    {
        GiftBox[] sizes =
        [
            new("A", 20, 14, 6), new("B", 15, 15, 15), new("C", 8, 8, 2), new("D", 25, 18, 5),
        ];

        return
        [
            .. sizes.SelectMany(size =>
                Enumerable.Range(1, copies).Select(n => size with { Name = $"{size.Name}{n}" })),
        ];
    }

    [Test]
    public async Task Produces_a_known_plan_for_a_known_input()
    {
        // Golden test. The invariant tests above check that a plan is *valid*; none of them
        // check that it is *good*. A refactor could quietly double the roll length used and
        // every other test here would still pass. This pins the actual output.
        //
        // Seeded, so it is reproducible. If these numbers move, the optimizer's behavior
        // changed — treat that as a deliberate decision to re-baseline, not an accident.
        //
        // Eight varied boxes on a 40in roll. Eight is under the exhaustive solver's limit, so
        // this pins an *optimal* plan rather than a searched one — 265, which the solver
        // confirms is the best layout there is for these boxes. The guard on the search itself
        // lives in Produces_a_known_plan_above_the_exact_threshold, because nothing here
        // exercises the search any more.
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
            Seed: 42);

        var result = PlanWrapLogic.Plan(boxes, options);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Sections.Count).IsEqualTo(8);

        // No iteration count: eight boxes never reach the search, so passing one would suggest
        // it does something here. Exact values are 265 and 0.84184, identical in Debug and
        // Release.
        // Rounded to survive harmless float noise, but finely enough that any real change in
        // packing quality fails the test rather than slipping through.
        await Assert.That(Math.Round(result.Value.RollLengthUsed, 3)).IsEqualTo(265.0);
        await Assert.That(Math.Round(result.Value.Efficiency, 6)).IsEqualTo(0.84184);
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
