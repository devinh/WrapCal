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
    private static readonly string[] AxisNames = ["length", "width", "height"];

    /// <summary>One legal way to cut a sheet for a box: its size and what it wraps.</summary>
    private readonly record struct SheetOption(double Width, double Length, string Note);

    private sealed class Shelf
    {
        public double Y;
        public double Height;
        public double UsedX;
        public List<(int ItemIndex, SheetOption Option, double X)> Placed = [];
    }

    public static Result<WrapPlan> Plan(IReadOnlyList<GiftBox> inputBoxes, WrapOptions inputOptions)
    {
        if (inputOptions.RollWidth <= 0)
        {
            return Result<WrapPlan>.Failure("roll.width", "Roll width must be positive.");
        }

        if (inputOptions.OverlapMargin < 0 || inputOptions.EndAllowance < 0)
        {
            return Result<WrapPlan>.Failure(
                "roll.allowance", "Overlap margin and end allowance cannot be negative.");
        }

        if (inputOptions.RandomRestarts < 0)
        {
            return Result<WrapPlan>.Failure("options.restarts", "Random restarts cannot be negative.");
        }

        if (inputBoxes.Any(box => box.Length <= 0 || box.Width <= 0 || box.Height <= 0))
        {
            return Result<WrapPlan>.Failure("box.dimensions", "All box dimensions must be positive.");
        }

        LengthUnit unit = inputOptions.InputUnit;
        WrapOptions options = inputOptions with
        {
            RollWidth = UnitConverter.ToCm(inputOptions.RollWidth, unit),
            OverlapMargin = UnitConverter.ToCm(inputOptions.OverlapMargin, unit),
            EndAllowance = UnitConverter.ToCm(inputOptions.EndAllowance, unit),
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
            return Result<WrapPlan>.Success(new WrapPlan([], 0, 0, 0, 1, inputOptions.OutputUnit));
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

        // Deterministic first guess (tallest first), then randomised restarts, keeping the
        // shortest roll. Seeded, so a given plan is reproducible.
        var random = new Random(options.Seed);
        List<int> order =
        [
            .. Enumerable.Range(0, boxes.Count)
                .OrderByDescending(i => optionsPerBox[i].Min(option => option.Length)),
        ];

        List<Shelf> best = Pack(order, optionsPerBox, options.RollWidth);
        double bestLength = TotalLength(best);

        for (int restart = 0; restart < options.RandomRestarts; restart++)
        {
            List<int> trial = [.. order.OrderBy(_ => random.Next())];
            List<Shelf> packed = Pack(trial, optionsPerBox, options.RollWidth);
            double length = TotalLength(packed);

            if (length < bestLength - 1e-9)
            {
                best = packed;
                bestLength = length;
            }
        }

        List<CutSection> sections = [];
        foreach (Shelf shelf in best)
        {
            foreach ((int itemIndex, SheetOption option, double x) in shelf.Placed)
            {
                sections.Add(new CutSection(
                    boxes[itemIndex].Name, x, shelf.Y, option.Width, option.Length, option.Note));
            }
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
            ToOutput(bestLength),
            AreaToOutput(paperCut),
            AreaToOutput(rollArea - paperCut),
            efficiency,
            outputUnit));
    }

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

            // Paper must go around the cross-section (p x q) plus a seam overlap.
            double circumference = (2 * (p + q)) + options.OverlapMargin;

            // Along the axis the paper covers the box, folds over each end face (half of the
            // larger cross-section side per end), plus any tuck allowance.
            double axisLength = axis + Math.Max(p, q) + (2 * options.EndAllowance);

            string label = $"wrapped around the {AxisNames[axisIndex]}-axis";

            if (circumference <= options.RollWidth + 1e-9)
            {
                result.Add(new SheetOption(circumference, axisLength, label + ", seam runs along roll length"));
            }

            if (axisLength <= options.RollWidth + 1e-9)
            {
                result.Add(new SheetOption(axisLength, circumference, label + ", seam runs across roll width"));
            }
        }

        return
        [
            .. result
                .GroupBy(option => (Math.Round(option.Width, 6), Math.Round(option.Length, 6)))
                .Select(group => group.First()),
        ];
    }

    private static double TotalLength(List<Shelf> shelves) =>
        shelves.Count == 0 ? 0 : shelves[^1].Y + shelves[^1].Height;

    /// <summary>
    /// Shelf packing. Each shelf is a full-width strip, so every cut is a straight line: across
    /// the roll between shelves, then along each shelf between pieces.
    /// </summary>
    private static List<Shelf> Pack(List<int> order, List<List<SheetOption>> optionsPerBox, double rollWidth)
    {
        List<Shelf> shelves = [];

        foreach (int index in order)
        {
            List<SheetOption> options = optionsPerBox[index];

            // a) Best fit in an existing shelf without making it taller.
            Shelf? bestShelf = null;
            SheetOption bestOption = default;
            double bestScore = double.MaxValue;

            foreach (Shelf shelf in shelves)
            {
                foreach (SheetOption option in options)
                {
                    if (option.Width <= rollWidth - shelf.UsedX + 1e-9 &&
                        option.Length <= shelf.Height + 1e-9)
                    {
                        double score = ((shelf.Height - option.Length) * option.Width)
                            + ((rollWidth - shelf.UsedX - option.Width) * 1e-3);

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestShelf = shelf;
                            bestOption = option;
                        }
                    }
                }
            }

            if (bestShelf is not null)
            {
                Place(bestShelf, index, bestOption);
                continue;
            }

            // b) Grow the last shelf if the piece fits beside what is already there — but only
            // when that costs less than starting a fresh shelf.
            if (shelves.Count > 0)
            {
                Shelf last = shelves[^1];
                double bestGrowth = double.MaxValue;
                bool found = false;
                SheetOption grownOption = default;

                foreach (SheetOption option in options)
                {
                    if (option.Width <= rollWidth - last.UsedX + 1e-9)
                    {
                        double growth = option.Length - last.Height;

                        if (growth < bestGrowth)
                        {
                            bestGrowth = growth;
                            grownOption = option;
                            found = true;
                        }
                    }
                }

                if (found && bestGrowth < options.Min(option => option.Length))
                {
                    last.Height = Math.Max(last.Height, grownOption.Length);
                    Place(last, index, grownOption);
                    continue;
                }
            }

            // c) Start a new shelf with the shortest option.
            SheetOption pick = options.OrderBy(option => option.Length)
                .ThenByDescending(option => option.Width)
                .First();

            var newShelf = new Shelf { Y = TotalLength(shelves), Height = pick.Length };
            shelves.Add(newShelf);
            Place(newShelf, index, pick);
        }

        return shelves;
    }

    private static void Place(Shelf shelf, int itemIndex, SheetOption option)
    {
        shelf.Placed.Add((itemIndex, option, shelf.UsedX));
        shelf.UsedX += option.Width;
    }
}
