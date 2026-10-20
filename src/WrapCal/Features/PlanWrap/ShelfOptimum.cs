namespace WrapCal.Features.PlanWrap;

/// <summary>
/// The best shelf layout that exists for a set of boxes, found exhaustively rather than guessed.
/// <para>
/// A shelf's height is its tallest sheet, so a layout is a partition of the boxes into shelves
/// and the answer is the partition whose heights add up to the least. Partitions are enumerated
/// by dynamic programming over subsets, which is exact and needs no solver — the reason this is
/// affordable at all is that all the maths is local to a shelf.
/// </para>
/// <para>
/// Exhaustive costs O(3ⁿ), so it is offered only up to <see cref="MaxBoxes"/>; measured in a
/// Debug build at 22ms for 12 boxes, 198ms for 14, 890ms for 16. Above that the search does the
/// partitioning, and this class is used only on small groups of shelves.
/// </para>
/// </summary>
internal static class ShelfOptimum
{
    /// <summary>The most boxes this can solve outright without the wait becoming visible.</summary>
    public const int MaxBoxes = 12;

    /// <summary>
    /// How many times the polish may sweep the whole plan. Each improvement restarts a sweep, so
    /// this is a stop rather than a convergence claim.
    /// </summary>
    private const int MaxPolishSweeps = 4;

    /// <summary>
    /// The optimal layout. Item indices refer to positions in <paramref name="optionsPerBox"/>.
    /// </summary>
    public static List<ShelfPacker.Shelf> Solve(
        IReadOnlyList<List<PlanWrapLogic.SheetOption>> optionsPerBox,
        double rollWidth)
    {
        int count = optionsPerBox.Count;
        int subsets = 1 << count;

        // Only a sheet's own length can ever be a shelf height: a shelf is exactly as tall as
        // the tallest sheet standing on it, so the least workable height is always one of these.
        double[] heights =
        [
            .. optionsPerBox.SelectMany(options => options).Select(option => option.Length)
                .Distinct().Order(),
        ];

        double[] shelfHeight = new double[subsets];
        Array.Fill(shelfHeight, double.MaxValue);

        for (int subset = 1; subset < subsets; subset++)
        {
            foreach (double height in heights)
            {
                double totalWidth = 0;
                bool fitting = true;

                for (int i = 0; i < count && fitting; i++)
                {
                    if ((subset & (1 << i)) == 0)
                    {
                        continue;
                    }

                    PlanWrapLogic.SheetOption? narrowest = NarrowestAt(optionsPerBox[i], height);

                    if (narrowest is null)
                    {
                        fitting = false;
                    }
                    else
                    {
                        totalWidth += narrowest.Value.Width;
                    }
                }

                if (fitting && totalWidth <= rollWidth + 1e-9)
                {
                    shelfHeight[subset] = height;
                    break;
                }
            }
        }

        double[] best = new double[subsets];
        Array.Fill(best, double.MaxValue);
        best[0] = 0;

        int[] split = new int[subsets];

        for (int subset = 1; subset < subsets; subset++)
        {
            // Anchor every partition on its lowest box, so a partition is counted once rather
            // than once per shelf order.
            int lowest = subset & -subset;

            for (int shelf = subset; shelf > 0; shelf = (shelf - 1) & subset)
            {
                if ((shelf & lowest) == 0 || shelfHeight[shelf] == double.MaxValue)
                {
                    continue;
                }

                double candidate = shelfHeight[shelf] + best[subset ^ shelf];

                // Strict: the first shelf reaching a height wins, so the output is reproducible.
                if (candidate < best[subset] - 1e-12)
                {
                    best[subset] = candidate;
                    split[subset] = shelf;
                }
            }
        }

        List<ShelfPacker.Shelf> shelves = [];

        for (int remaining = subsets - 1; remaining != 0; remaining ^= split[remaining])
        {
            shelves.Add(Build(
                optionsPerBox, split[remaining], shelfHeight[split[remaining]], rollWidth));
        }

        return shelves;
    }

    /// <summary>
    /// Re-solves small groups of shelves exactly and keeps the result where it is shorter. The
    /// search's weakness on a long list is which boxes share a shelf, and moves that shuffle
    /// boxes between shelves have no way to see the partition that would beat what they hold;
    /// re-solving a pair outright does.
    /// </summary>
    /// <param name="optionsPerBox">Indexed by the item indices already held on the shelves.</param>
    public static List<ShelfPacker.Shelf> Polish(
        List<ShelfPacker.Shelf> shelves,
        IReadOnlyList<List<PlanWrapLogic.SheetOption>> optionsPerBox,
        double rollWidth)
    {
        for (int sweep = 0; sweep < MaxPolishSweeps; sweep++)
        {
            bool improved = false;

            for (int i = 0; i < shelves.Count && !improved; i++)
            {
                for (int j = i + 1; j < shelves.Count && !improved; j++)
                {
                    improved = TryRepartition(shelves, i, j, optionsPerBox, rollWidth);
                }
            }

            if (!improved)
            {
                break;
            }
        }

        return shelves;
    }

    private static bool TryRepartition(
        List<ShelfPacker.Shelf> shelves,
        int first,
        int second,
        IReadOnlyList<List<PlanWrapLogic.SheetOption>> optionsPerBox,
        double rollWidth)
    {
        int[] picked =
        [
            .. shelves[first].Placed.Select(placed => placed.ItemIndex),
            .. shelves[second].Placed.Select(placed => placed.ItemIndex),
        ];

        if (picked.Length > MaxBoxes)
        {
            return false;
        }

        List<List<PlanWrapLogic.SheetOption>> options =
            [.. picked.Select(item => optionsPerBox[item])];

        List<ShelfPacker.Shelf> proposal = Solve(options, rollWidth);

        ShelfPacker.Cost before = Cost([shelves[first], shelves[second]]);
        ShelfPacker.Cost after = Cost(proposal);

        if (!ShelfPacker.IsBetter(after, before))
        {
            return false;
        }

        foreach (ShelfPacker.Shelf shelf in proposal)
        {
            for (int i = 0; i < shelf.Placed.Count; i++)
            {
                shelf.Placed[i] = shelf.Placed[i] with { ItemIndex = picked[shelf.Placed[i].ItemIndex] };
            }
        }

        shelves.RemoveAt(second);
        shelves.RemoveAt(first);
        shelves.InsertRange(first, proposal);
        return true;
    }

    private static ShelfPacker.Cost Cost(IReadOnlyList<ShelfPacker.Shelf> shelves)
    {
        double length = 0;
        double area = 0;

        foreach (ShelfPacker.Shelf shelf in shelves)
        {
            length += shelf.Height;

            foreach ((_, PlanWrapLogic.SheetOption option, _) in shelf.Placed)
            {
                area += option.Width * option.Length;
            }
        }

        return new ShelfPacker.Cost(length, area);
    }

    private static ShelfPacker.Shelf Build(
        IReadOnlyList<List<PlanWrapLogic.SheetOption>> optionsPerBox,
        int subset,
        double height,
        double rollWidth)
    {
        List<PlanWrapLogic.SheetOption> chosen = ChooseSheets(optionsPerBox, subset, height, rollWidth);

        var shelf = new ShelfPacker.Shelf();
        double x = 0;
        double tallest = 0;

        for (int i = 0; i < optionsPerBox.Count; i++)
        {
            if ((subset & (1 << i)) == 0)
            {
                continue;
            }

            PlanWrapLogic.SheetOption option = chosen[i];
            shelf.Placed.Add((i, option, x));
            x += option.Width;
            tallest = Math.Max(tallest, option.Length);
        }

        shelf.UsedX = x;
        shelf.Height = tallest;
        return shelf;
    }

    /// <summary>
    /// Which sheet each box on this shelf is cut to, once the shelf's height is settled.
    /// <para>
    /// Height is fixed by now, so every sheet no longer than the shelf is equally good for the
    /// roll and the only thing left to choose is paper: take the least. That is the same tie-break
    /// the search applies between plans, and taking the narrowest sheet instead would quietly cut
    /// more paper on any shelf with width to spare.
    /// </para>
    /// </summary>
    private static List<PlanWrapLogic.SheetOption> ChooseSheets(
        IReadOnlyList<List<PlanWrapLogic.SheetOption>> optionsPerBox,
        int subset,
        double height,
        double rollWidth)
    {
        int count = optionsPerBox.Count;
        List<PlanWrapLogic.SheetOption> chosen = new(count);
        double used = 0;

        for (int i = 0; i < count; i++)
        {
            PlanWrapLogic.SheetOption pick = (subset & (1 << i)) == 0
                ? default
                : LeastPaperAt(optionsPerBox[i], height)!.Value;

            chosen.Add(pick);

            if ((subset & (1 << i)) != 0)
            {
                used += pick.Width;
            }
        }

        // Least paper can want more width than the roll has; narrow the worst offenders until it
        // fits. The narrowest sheet for every box always fits — that is what put this set on a
        // shelf at this height in the first place.
        while (used > rollWidth + 1e-9)
        {
            int swap = -1;
            double cheapestPenalty = double.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if ((subset & (1 << i)) == 0)
                {
                    continue;
                }

                PlanWrapLogic.SheetOption narrowest = NarrowestAt(optionsPerBox[i], height)!.Value;
                double saved = chosen[i].Width - narrowest.Width;

                if (saved <= 1e-9)
                {
                    continue;
                }

                double penalty = (chosen[i].Width * chosen[i].Length)
                    - (narrowest.Width * narrowest.Length);

                if (penalty / saved < cheapestPenalty)
                {
                    cheapestPenalty = penalty / saved;
                    swap = i;
                }
            }

            if (swap < 0)
            {
                break;
            }

            used -= chosen[swap].Width - NarrowestAt(optionsPerBox[swap], height)!.Value.Width;
            chosen[swap] = NarrowestAt(optionsPerBox[swap], height)!.Value;
        }

        return chosen;
    }

    /// <summary>The narrowest sheet this box has that is no longer than the shelf.</summary>
    private static PlanWrapLogic.SheetOption? NarrowestAt(
        List<PlanWrapLogic.SheetOption> options,
        double height)
    {
        PlanWrapLogic.SheetOption? best = null;

        foreach (PlanWrapLogic.SheetOption option in options)
        {
            if (option.Length > height + 1e-9)
            {
                continue;
            }

            if (best is null
                || option.Width < best.Value.Width - 1e-9
                || (Math.Abs(option.Width - best.Value.Width) <= 1e-9
                    && option.Length < best.Value.Length - 1e-9))
            {
                best = option;
            }
        }

        return best;
    }

    /// <summary>The sheet cutting the least paper that is no longer than the shelf; ties narrower.</summary>
    private static PlanWrapLogic.SheetOption? LeastPaperAt(
        List<PlanWrapLogic.SheetOption> options,
        double height)
    {
        PlanWrapLogic.SheetOption? best = null;
        double bestArea = double.MaxValue;

        foreach (PlanWrapLogic.SheetOption option in options)
        {
            if (option.Length > height + 1e-9)
            {
                continue;
            }

            double area = option.Width * option.Length;

            if (best is null
                || area < bestArea - 1e-9
                || (Math.Abs(area - bestArea) <= 1e-9 && option.Width < best.Value.Width - 1e-9))
            {
                best = option;
                bestArea = area;
            }
        }

        return best;
    }
}
