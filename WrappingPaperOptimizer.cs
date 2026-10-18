using System;
using System.Collections.Generic;
using System.Linq;

namespace GiftWrapOptimizer;

/// <summary>A box to wrap. All units must match the roll units (e.g. inches or cm).</summary>
public record GiftBox(string Name, double Length, double Width, double Height);

public enum LengthUnit { Centimeters, Inches }

public static class UnitConverter
{
    public const double CmPerInch = 2.54;

    public static double ToCm(double value, LengthUnit unit) =>
        unit == LengthUnit.Inches ? value * CmPerInch : value;

    public static double FromCm(double cm, LengthUnit unit) =>
        unit == LengthUnit.Inches ? cm / CmPerInch : cm;
}

/// <summary>
/// RollWidth      : usable width of the roll (the X axis), in InputUnit.
/// OverlapMargin  : extra paper added to the wrap circumference so the seam overlaps, in InputUnit.
/// EndAllowance   : optional extra paper added to EACH end of the box for tucking/taping, in InputUnit (default 0).
/// InputUnit      : unit of RollWidth, OverlapMargin, EndAllowance AND the box dimensions.
///                  Everything is converted to centimeters first; all calculations run in centimeters.
/// OutputUnit     : unit used to report the cut sections, roll length and areas.
///                  Converted from centimeters as the very last step.
/// RandomRestarts : number of shuffled attempts used to improve the packing (more = better, slower).
/// Seed           : fixed seed so results are repeatable.
/// </summary>
public record WrapOptions(
    double RollWidth,
    double OverlapMargin,
    double EndAllowance = 0,
    LengthUnit InputUnit = LengthUnit.Centimeters,
    LengthUnit OutputUnit = LengthUnit.Centimeters,
    int RandomRestarts = 500,
    int Seed = 1);

/// <summary>
/// One rectangle to cut from the roll.
/// X/Y is the rectangle's corner closest to the origin.
/// X runs across the roll width, Y runs down the roll length.
/// Origin (0,0) is the corner of the starting end, which is a full uncut straight edge.
/// Width is the extent along X, Length is the extent along Y.
/// </summary>
public record CutSection(
    string ItemName,
    double X,
    double Y,
    double Width,
    double Length,
    string Note);

public record WrapPlan(
    IReadOnlyList<CutSection> Sections,
    double RollLengthUsed,
    double PaperAreaCut,
    double WasteArea,
    double Efficiency,
    LengthUnit Unit);   // unit of all lengths above; areas are Unit squared

public static class WrappingPaperOptimizer
{
    // One way of cutting a sheet for a box: a size and a description.
    private readonly record struct Option(double W, double L, string Note);

    private sealed class Shelf
    {
        public double Y;
        public double Height;
        public double UsedX;
        public List<(int ItemIndex, Option Opt, double X)> Placed = new();
    }

    /// <summary>
    /// Inputs are in InputUnit. Outputs (X, Y, Width, Length, roll length, areas) are in OutputUnit.
    /// All internal math is done in centimeters.
    /// </summary>
    public static WrapPlan Plan(IReadOnlyList<GiftBox> inputBoxes, WrapOptions inputOpt)
    {
        if (inputOpt.RollWidth <= 0) throw new ArgumentException("RollWidth must be positive.");
        if (inputOpt.OverlapMargin < 0 || inputOpt.EndAllowance < 0)
            throw new ArgumentException("OverlapMargin and EndAllowance cannot be negative.");
        if (inputBoxes.Any(b => b.Length <= 0 || b.Width <= 0 || b.Height <= 0))
            throw new ArgumentException("All box dimensions must be positive.");

        // Step 1: convert everything to centimeters. All logic below works in cm only.
        var u = inputOpt.InputUnit;
        var opt = inputOpt with
        {
            RollWidth = UnitConverter.ToCm(inputOpt.RollWidth, u),
            OverlapMargin = UnitConverter.ToCm(inputOpt.OverlapMargin, u),
            EndAllowance = UnitConverter.ToCm(inputOpt.EndAllowance, u),
            InputUnit = LengthUnit.Centimeters
        };
        var boxes = inputBoxes
            .Select(b => new GiftBox(
                b.Name,
                UnitConverter.ToCm(b.Length, u),
                UnitConverter.ToCm(b.Width, u),
                UnitConverter.ToCm(b.Height, u)))
            .ToList();

        if (boxes.Count == 0) return new WrapPlan(Array.Empty<CutSection>(), 0, 0, 0, 1, inputOpt.OutputUnit);

        // 1. Every legal sheet size per box (3 wrap axes x 2 orientations on the roll).
        var options = boxes.Select(b => SheetOptions(b, opt)).ToList();
        for (int i = 0; i < boxes.Count; i++)
            if (options[i].Count == 0)
                throw new InvalidOperationException(
                    $"'{boxes[i].Name}' cannot be wrapped in one sheet: every orientation is wider than the roll ({opt.RollWidth}).");

        // 2. Try a deterministic order first, then random orders; keep the shortest roll length.
        var rng = new Random(opt.Seed);
        var order = Enumerable.Range(0, boxes.Count)
            .OrderByDescending(i => options[i].Min(o => o.L)).ToList();

        List<Shelf> best = Pack(order, options, opt.RollWidth);
        double bestLen = TotalLength(best);

        for (int r = 0; r < opt.RandomRestarts; r++)
        {
            var trial = order.OrderBy(_ => rng.Next()).ToList();
            var packed = Pack(trial, options, opt.RollWidth);
            double len = TotalLength(packed);
            if (len < bestLen - 1e-9) { best = packed; bestLen = len; }
        }

        // 3. Convert shelves to output rectangles.
        var sections = new List<CutSection>();
        foreach (var shelf in best)
            foreach (var (idx, o, x) in shelf.Placed)
                sections.Add(new CutSection(boxes[idx].Name, x, shelf.Y, o.W, o.L, o.Note));

        double cut = sections.Sum(s => s.Width * s.Length);
        double total = bestLen * opt.RollWidth;
        double efficiency = total > 0 ? cut / total : 1;

        // Last step: convert results from centimeters to the requested output unit.
        var outUnit = inputOpt.OutputUnit;
        double L(double cm) => UnitConverter.FromCm(cm, outUnit);
        var converted = sections
            .Select(s => s with { X = L(s.X), Y = L(s.Y), Width = L(s.Width), Length = L(s.Length) })
            .ToList();

        return new WrapPlan(
            converted,
            L(bestLen),
            L(L(cut)),               // area: convert once per dimension
            L(L(total - cut)),
            efficiency,
            outUnit);
    }

    private static readonly string[] AxisNames = { "length", "width", "height" };

    /// <summary>All distinct sheet sizes that can wrap this box and fit across the roll.</summary>
    private static List<Option> SheetOptions(GiftBox b, WrapOptions opt)
    {
        double[] d = { b.Length, b.Width, b.Height };
        var result = new List<Option>();

        for (int axisIdx = 0; axisIdx < 3; axisIdx++)
        {
            double axis = d[axisIdx];
            double p = d[(axisIdx + 1) % 3];
            double q = d[(axisIdx + 2) % 3];

            // Paper must go around the box cross-section (p x q) plus a seam overlap.
            double circumference = 2 * (p + q) + opt.OverlapMargin;
            // Along the axis the paper must cover the box plus enough at each end to fold over
            // the end face (half of the larger cross-section side per end) plus any tuck allowance.
            double axisLen = axis + Math.Max(p, q) + 2 * opt.EndAllowance;

            string label = $"wrapped around the {AxisNames[axisIdx]}-axis";

            // Circumference across the roll width.
            if (circumference <= opt.RollWidth + 1e-9)
                result.Add(new Option(circumference, axisLen, label + ", seam runs along roll length"));

            // Circumference down the roll length (box axis across the width).
            if (axisLen <= opt.RollWidth + 1e-9)
                result.Add(new Option(axisLen, circumference, label + ", seam runs across roll width"));
        }

        return result
            .GroupBy(o => (Math.Round(o.W, 6), Math.Round(o.L, 6)))
            .Select(g => g.First())
            .ToList();
    }

    private static double TotalLength(List<Shelf> shelves) =>
        shelves.Count == 0 ? 0 : shelves[^1].Y + shelves[^1].Height;

    /// <summary>
    /// Shelf packing. Each shelf is a full-width strip, so every cut is a straight line:
    /// first cut across the roll between shelves, then cut along each shelf between pieces.
    /// </summary>
    private static List<Shelf> Pack(List<int> order, List<List<Option>> options, double rollWidth)
    {
        var shelves = new List<Shelf>();

        foreach (int idx in order)
        {
            var opts = options[idx];

            // a) Best fit in an existing shelf without making it taller (least wasted strip area).
            Shelf? bestShelf = null; Option bestOpt = default; double bestScore = double.MaxValue;
            foreach (var s in shelves)
                foreach (var o in opts)
                    if (o.W <= rollWidth - s.UsedX + 1e-9 && o.L <= s.Height + 1e-9)
                    {
                        double score = (s.Height - o.L) * o.W + (rollWidth - s.UsedX - o.W) * 1e-3;
                        if (score < bestScore) { bestScore = score; bestShelf = s; bestOpt = o; }
                    }
            if (bestShelf != null) { Place(bestShelf, idx, bestOpt); continue; }

            // b) Grow the last shelf if the piece fits beside what is already there.
            if (shelves.Count > 0)
            {
                var last = shelves[^1];
                double bestGrow = double.MaxValue; bool found = false; Option growOpt = default;
                foreach (var o in opts)
                    if (o.W <= rollWidth - last.UsedX + 1e-9)
                    {
                        double grow = o.L - last.Height;
                        if (grow < bestGrow) { bestGrow = grow; growOpt = o; found = true; }
                    }
                // Only grow if that is cheaper than starting a new shelf with the smallest piece.
                if (found && bestGrow < opts.Min(o => o.L))
                {
                    last.Height = Math.Max(last.Height, growOpt.L);
                    Place(last, idx, growOpt);
                    continue;
                }
            }

            // c) Start a new shelf with the shortest option.
            var pick = opts.OrderBy(o => o.L).ThenByDescending(o => o.W).First();
            var shelf = new Shelf { Y = TotalLength(shelves), Height = pick.L };
            shelves.Add(shelf);
            Place(shelf, idx, pick);
        }

        return shelves;
    }

    private static void Place(Shelf s, int idx, Option o)
    {
        s.Placed.Add((idx, o, s.UsedX));
        s.UsedX += o.W;
    }
}

// ---------------------------------------------------------------------------
// Example usage
// ---------------------------------------------------------------------------
// var boxes = new List<GiftBox>
// {
//     new("Book",   9, 6, 1.5),
//     new("Shirt",  12, 9, 2),
//     new("Mug",    5, 5, 5),
//     new("Console", 14, 10, 4),
// };
// // Inputs in inches, results reported in centimeters (swap either unit as needed).
// var plan = WrappingPaperOptimizer.Plan(boxes, new WrapOptions(
//     RollWidth: 30, OverlapMargin: 0.5,
//     InputUnit: LengthUnit.Inches, OutputUnit: LengthUnit.Centimeters));
// Console.WriteLine($"Roll length needed: {plan.RollLengthUsed:F1} {plan.Unit}  Efficiency: {plan.Efficiency:P1}");
// foreach (var s in plan.Sections)
//     Console.WriteLine($"{s.ItemName}: x={s.X:F1}, y={s.Y:F1}, width={s.Width:F1}, length={s.Length:F1} {plan.Unit}  ({s.Note})");
