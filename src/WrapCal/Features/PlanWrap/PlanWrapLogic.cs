using System.Globalization;
using WrapCal.Common;

namespace WrapCal.Features.PlanWrap;

/// <summary>
/// Turns a set of boxes plus roll options into a cut plan.
/// <para>
/// Ported from the reference implementation at the repository root. All maths runs in
/// centimetres; units are converted in once at the start and out once at the end, which keeps
/// the packing code free of unit handling.
/// </para>
/// </summary>
public static class PlanWrapLogic
{
    /// <summary>
    /// How much work the search may cost, in units of "boxes × iterations". Packing is quadratic
    /// in the number of boxes, so a fixed iteration count turns a long list into seconds of
    /// blocked work — 2000 boxes at 500 iterations measured 25s, enough to freeze the page
    /// handling the click. A short list keeps every iteration it asked for; a long one trades
    /// iterations for staying responsive.
    /// </summary>
    private const int SearchWorkBudget = 50_000;

    /// <summary>One legal way to cut a sheet: its size, and the box axis it wraps about.</summary>
    internal readonly record struct SheetOption(double Width, double Length, int Axis);

    public static Result<WrapPlan> Plan(IReadOnlyList<GiftBox> inputBoxes, WrapOptions inputOptions)
    {
        if (inputOptions.RollWidth <= 0)
        {
            return Result<WrapPlan>.Failure("roll.width", "Roll width must be positive.");
        }

        if (inputOptions.OverlapMargin < 0)
        {
            return Result<WrapPlan>.Failure("overlap.margin", "Overlap margin cannot be negative.");
        }

        if (inputOptions.SearchIterations < 0)
        {
            return Result<WrapPlan>.Failure("options.iterations", "Search iterations cannot be negative.");
        }

        if (inputBoxes.Any(box => box.Length <= 0 || box.Width <= 0 || box.Height <= 0))
        {
            return Result<WrapPlan>.Failure("box.dimensions", "All box dimensions must be positive.");
        }

        // Same limit the form enforces through its Add button, restated for callers that bypass
        // the form — one const, so there is nothing to drift.
        if (inputBoxes.Count > PlanWrapInput.MaxBoxCount)
        {
            return Result<WrapPlan>.Failure(
                "boxes.count",
                $"A plan can hold at most {PlanWrapInput.MaxBoxCount} boxes.");
        }

        LengthUnit unit = inputOptions.InputUnit;
        WrapOptions options = inputOptions with
        {
            RollWidth = UnitConverter.ToCm(inputOptions.RollWidth, unit),
            OverlapMargin = UnitConverter.ToCm(inputOptions.OverlapMargin, unit),
            InputUnit = LengthUnit.Centimeters,
        };

        List<GiftBox> boxes =
        [
            .. inputBoxes.Select(box => new GiftBox(
                box.Name,
                UnitConverter.ToCm(box.Length, unit),
                UnitConverter.ToCm(box.Width, unit),
                UnitConverter.ToCm(box.Height, unit))),
        ];

        if (boxes.Count == 0)
        {
            // No paper is used, so nothing can be said to be efficient — reporting the empty
            // ratio as 100% would read as a perfect plan.
            return Result<WrapPlan>.Success(new WrapPlan(
                [],
                UnitConverter.FromCm(options.RollWidth, inputOptions.OutputUnit),
                0,
                0,
                0,
                0,
                inputOptions.OutputUnit));
        }

        List<List<SheetOption>> optionsPerBox = [.. boxes.Select(box => SheetOptions(box, options))];

        for (int i = 0; i < boxes.Count; i++)
        {
            if (optionsPerBox[i].Count == 0)
            {
                return Result<WrapPlan>.Failure(
                    "box.too-wide",
                    $"'{boxes[i].Name}' cannot be wrapped in one sheet: every orientation is wider than the roll.");
            }
        }

        // Deterministic first guess (tallest first), then the search. Seeded, so a given plan is
        // reproducible.
        var random = new Random(options.Seed);
        List<int> order =
        [
            .. Enumerable.Range(0, boxes.Count)
                .OrderByDescending(i => optionsPerBox[i].Min(option => option.Length)),
        ];

        int iterations = Math.Min(
            options.SearchIterations,
            Math.Max(1, SearchWorkBudget / boxes.Count));

        List<ShelfPacker.Shelf> best = ShelfPacker.Search(
            order, optionsPerBox, options.RollWidth, iterations, random);

        // Each shelf starts where the one before it ended, so Y falls out of the heights rather
        // than being stored on the shelf — the search moves pieces between shelves, and a stored
        // Y would have to be kept in step through every move.
        List<CutSection> sections = [];
        double bestLength = 0;

        foreach (ShelfPacker.Shelf shelf in best)
        {
            foreach ((int itemIndex, SheetOption option, double x) in shelf.Placed)
            {
                sections.Add(new CutSection(
                    boxes[itemIndex].Name,
                    x,
                    bestLength,
                    option.Width,
                    option.Length,
                    DescribeWrap(boxes[itemIndex], option.Axis, inputOptions.OutputUnit)));
            }

            bestLength += shelf.Height;
        }

        double paperCut = sections.Sum(section => section.Width * section.Length);
        double rollArea = bestLength * options.RollWidth;
        double efficiency = rollArea > 0 ? paperCut / rollArea : 1;

        LengthUnit outputUnit = inputOptions.OutputUnit;
        double scale = UnitConverter.FromCm(1, outputUnit);
        double ToOutput(double centimetres) => UnitConverter.FromCm(centimetres, outputUnit);
        double AreaToOutput(double squareCentimetres) => squareCentimetres * scale * scale;

        List<CutSection> converted =
        [
            .. sections.Select(section => section with
            {
                X = ToOutput(section.X),
                Y = ToOutput(section.Y),
                Width = ToOutput(section.Width),
                Length = ToOutput(section.Length),
            }),
        ];

        return Result<WrapPlan>.Success(new WrapPlan(
            converted,
            ToOutput(options.RollWidth),
            ToOutput(bestLength),
            AreaToOutput(paperCut),
            // Clamped: the sum of the pieces can exceed the roll's own area by a rounding step,
            // and negative waste reads as a bug in the plan rather than in the floats.
            AreaToOutput(Math.Max(0, rollArea - paperCut)),
            efficiency,
            outputUnit));
    }

    /// <summary>
    /// How to orient the cut sheet against the box, named by the box's own sides. The roll is no
    /// use as a reference: by the time anyone wraps, the sheet is cut off it.
    /// </summary>
    private static string DescribeWrap(GiftBox box, int axisIndex, LengthUnit unit)
    {
        double[] dimensions = [box.Length, box.Width, box.Height];

        // The two sides the paper has to go around: the cross-section at this axis.
        double first = UnitConverter.FromCm(dimensions[(axisIndex + 1) % 3], unit);
        double second = UnitConverter.FromCm(dimensions[(axisIndex + 2) % 3], unit);
        string label = unit == LengthUnit.Centimeters ? "cm" : "in";

        return $"Wrapped around the box's {One(first)} × {One(second)} {label} sides.";
    }

    // The user's culture, not the invariant one: the form's numeric fields parse in the culture
    // the app runs under, so a note written with a decimal point beside fields showing a decimal
    // comma would be the same number spelled two ways on one screen.
    private static string One(double value) => value.ToString("F1", CultureInfo.CurrentCulture);

    /// <summary>Every distinct sheet size that wraps this box and fits across the roll.</summary>
    private static List<SheetOption> SheetOptions(GiftBox box, WrapOptions options)
    {
        double[] dimensions = [box.Length, box.Width, box.Height];
        List<SheetOption> result = [];

        for (int axisIndex = 0; axisIndex < 3; axisIndex++)
        {
            double axis = dimensions[axisIndex];
            double p = dimensions[(axisIndex + 1) % 3];
            double q = dimensions[(axisIndex + 2) % 3];

            // Each sheet dimension has exactly one overlap to close, so the margin is added
            // once to each — not once per edge.
            //
            //  around the cross-section: the two ends of the wrap lap over each other
            //  along the box: the flaps folded onto each end face meet in the middle, and
            //  without the margin they only just meet — there is no lap at all
            double circumference = (2 * (p + q)) + options.OverlapMargin;

            // The fold-over is half of the larger cross-section side per end: enough to reach
            // the middle of the end face from both sides.
            double axisLength = axis + Math.Max(p, q) + options.OverlapMargin;

            if (circumference <= options.RollWidth + 1e-9)
            {
                result.Add(new SheetOption(circumference, axisLength, axisIndex));
            }

            if (axisLength <= options.RollWidth + 1e-9)
            {
                result.Add(new SheetOption(axisLength, circumference, axisIndex));
            }
        }

        List<SheetOption> distinct =
        [
            .. result
                .GroupBy(option => (Math.Round(option.Width, 6), Math.Round(option.Length, 6)))
                .Select(group => group.First()),
        ];

        // Drop any sheet that another sheet for the same box dominates — one no wider and no
        // longer. It fits wherever the larger one fits without making the shelf taller, so
        // cutting it can never shorten the roll; it only spends paper, which is the thing the
        // plan exists to conserve. A box always keeps at least one option: nothing can dominate
        // a sheet that is already the smallest in each direction.
        return [.. distinct.Where(option => !distinct.Any(other => Dominates(other, option)))];
    }

    /// <summary>
    /// Strictly better on both counts, or equal on one and better on the other. Requiring one
    /// strict improvement is what keeps two sheets a hair apart from dominating each other and
    /// leaving the box with no option at all.
    /// </summary>
    private static bool Dominates(SheetOption candidate, SheetOption option) =>
        candidate.Width <= option.Width + 1e-9
        && candidate.Length <= option.Length + 1e-9
        && (candidate.Width < option.Width - 1e-9 || candidate.Length < option.Length - 1e-9);
}
