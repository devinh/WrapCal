namespace WrapCal.Features.PlanWrap;

/// <summary>
/// Shelf packing, and the local search over placements.
/// <para>
/// Every shelf is a full-width strip, so every cut is a straight line: across the roll between
/// shelves, then along each shelf between pieces. The search only ever changes which sheet a
/// box was cut to and where it sits — the geometry of a shelf is fixed by this rule.
/// </para>
/// </summary>
internal static class ShelfPacker
{
    /// <summary>How often the search keeps a plan that is worse than the one it started from.</summary>
    private const double UphillChance = 0.1;

    internal sealed class Shelf
    {
        public double Height;
        public double UsedX;
        public List<(int ItemIndex, PlanWrapLogic.SheetOption Option, double X)> Placed = [];

        public Shelf Clone() => new()
        {
            Height = Height,
            UsedX = UsedX,
            Placed = [.. Placed],
        };
    }

    /// <summary>What a candidate plan costs: the roll it consumes, then the paper it cuts.</summary>
    private readonly record struct Cost(double Length, double Area);

    private static List<Shelf> Pack(
        IReadOnlyList<int> order,
        IReadOnlyList<List<PlanWrapLogic.SheetOption>> optionsPerBox,
        double rollWidth)
    {
        List<Shelf> shelves = [];

        foreach (int index in order)
        {
            TryPlace(shelves, index, optionsPerBox[index], rollWidth, forced: null, random: null);
        }

        return shelves;
    }

    /// <summary>
    /// Iterated local search: keep the best plan so far and keep nudging *it*, rather than
    /// repacking from scratch each time. A restart that begins again from the original order
    /// throws away everything the previous attempt learned, so for the same amount of work this
    /// reaches shorter rolls.
    /// </summary>
    /// <param name="iterations">
    /// How many nudges to attempt. Zero returns the first guess untouched, which is what tests
    /// that pin a specific packing rely on.
    /// </param>
    public static List<Shelf> Search(
        IReadOnlyList<int> order,
        IReadOnlyList<List<PlanWrapLogic.SheetOption>> optionsPerBox,
        double rollWidth,
        int iterations,
        Random random)
    {
        List<Shelf> best = Pack(order, optionsPerBox, rollWidth);
        Cost bestCost = Measure(best);

        if (iterations == 0)
        {
            return best;
        }

        List<Shelf> current = best;
        Cost currentCost = bestCost;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            List<Shelf> trial = Perturb(current, optionsPerBox, rollWidth, random);
            Cost trialCost = Measure(trial);

            // Step uphill now and then. Refusing every worse plan makes this a strict hill climb,
            // which cannot walk down into the next valley to reach a better one — measured on the
            // golden input, ruin-and-recreate found no improvement at all without this.
            if (IsBetter(currentCost, trialCost) && random.NextDouble() >= UphillChance)
            {
                continue;
            }

            current = trial;
            currentCost = trialCost;

            if (IsBetter(trialCost, bestCost))
            {
                best = trial;
                bestCost = trialCost;
            }
        }

        return best;
    }

    /// <summary>
    /// One nudge of an existing plan. Three of the four moves take boxes back out and put them
    /// down again, which is why they cost a fraction of a full repack; the fourth packs the lot
    /// afresh in a new order, which is diversification rather than refinement.
    /// </summary>
    private static List<Shelf> Perturb(
        List<Shelf> source,
        IReadOnlyList<List<PlanWrapLogic.SheetOption>> optionsPerBox,
        double rollWidth,
        Random random)
    {
        List<Shelf> shelves = Clone(source);

        if (shelves.Count == 0)
        {
            return shelves;
        }

        List<int> Some(int count) => SomeBoxes(shelves, count, random);

        switch (random.Next(4))
        {
            case 0:
                // Repack everything in a new order. Kept in the move set deliberately: it is the
                // perturbation the old search used on every iteration, so whatever that found is
                // still reachable from here.
                return Pack(Some(optionsPerBox.Count), optionsPerBox, rollWidth);

            case 1:
                Replace(shelves, Some(RuinSize(optionsPerBox.Count, random)),
                    optionsPerBox, rollWidth, random);
                break;

            case 2:
                // A whole shelf at once is the bigger kick: it can undo a strip opened at a bad
                // height, which moving single boxes tends not to reach.
                Replace(
                    shelves,
                    [.. shelves[random.Next(shelves.Count)].Placed.Select(placed => placed.ItemIndex)],
                    optionsPerBox, rollWidth, random);
                break;

            default:
                Reorient(shelves, optionsPerBox, rollWidth, random);
                break;
        }

        return shelves;
    }

    private static void Replace(
        List<Shelf> shelves,
        List<int> items,
        IReadOnlyList<List<PlanWrapLogic.SheetOption>> optionsPerBox,
        double rollWidth,
        Random random)
    {
        foreach (int item in items)
        {
            Remove(shelves, item);
        }

        // Random insertion order, so the same boxes can land in a different arrangement.
        foreach (int item in items.OrderBy(_ => random.Next()))
        {
            TryPlace(shelves, item, optionsPerBox[item], rollWidth, forced: null, random);
        }
    }

    /// <summary>
    /// Cut one box to a sheet it is not currently using. Orientation is otherwise decided afresh
    /// by every placement, greedily — this is the only move that can prefer a sheet the greedy
    /// fit would never have picked, which is what lets the search spend paper only where it buys
    /// roll back.
    /// </summary>
    private static void Reorient(
        List<Shelf> shelves,
        IReadOnlyList<List<PlanWrapLogic.SheetOption>> optionsPerBox,
        double rollWidth,
        Random random)
    {
        Shelf shelf = shelves[random.Next(shelves.Count)];
        (int ItemIndex, PlanWrapLogic.SheetOption Option, double X) placed =
            shelf.Placed[random.Next(shelf.Placed.Count)];

        List<PlanWrapLogic.SheetOption> options = optionsPerBox[placed.ItemIndex];

        List<PlanWrapLogic.SheetOption> alternatives =
            [.. options.Where(option => option != placed.Option)];

        if (alternatives.Count == 0)
        {
            return;
        }

        Remove(shelves, placed.ItemIndex);
        TryPlace(
            shelves,
            placed.ItemIndex,
            options,
            rollWidth,
            forced: alternatives[random.Next(alternatives.Count)],
            random);
    }

    /// <summary>
    /// How many boxes a ruin takes out. It has to be enough to actually disturb the plan: with
    /// best-fit reinsertion, pulling one box out and putting it straight back tends to land it
    /// exactly where it was, which makes the move a no-op and the iteration wasted.
    /// </summary>
    private static int RuinSize(int boxCount, Random random) =>
        1 + random.Next(Math.Max(2, boxCount / 3));

    private static List<int> SomeBoxes(List<Shelf> shelves, int count, Random random) =>
        [.. shelves.SelectMany(shelf => shelf.Placed)
            .Select(placed => placed.ItemIndex)
            .OrderBy(_ => random.Next())
            .Take(count)];

    /// <summary>
    /// Puts one box down: beside what is already on a shelf, by growing the last shelf, or on a
    /// shelf of its own. <paramref name="forced"/> restricts every option to one sheet, which is
    /// how the search tries an orientation the greedy fit would not have chosen.
    /// <para>
    /// <paramref name="random"/> is what makes a ruin-and-recreate move able to return something
    /// other than what it started from. Reinserting by best fit alone is close to a fixed point:
    /// take a box out of a plan and the same plan is what puts it back, which is why the search
    /// did nothing at all until this was randomised.
    /// </para>
    /// </summary>
    private static void TryPlace(
        List<Shelf> shelves,
        int itemIndex,
        IReadOnlyList<PlanWrapLogic.SheetOption> options,
        double rollWidth,
        PlanWrapLogic.SheetOption? forced,
        Random? random)
    {
        // a) Fit in an existing shelf without making it taller, and remember every shelf that
        // would take it — the search picks between them at random, the first guess takes the best.
        Shelf? bestShelf = null;
        PlanWrapLogic.SheetOption bestOption = default;
        double bestScore = double.MaxValue;
        List<(Shelf Shelf, PlanWrapLogic.SheetOption Option)>? feasible = random is null ? null : [];

        foreach (Shelf shelf in shelves)
        {
            foreach (PlanWrapLogic.SheetOption option in options)
            {
                if (forced is { } only && option != only)
                {
                    continue;
                }

                if (option.Width <= rollWidth - shelf.UsedX + 1e-9 &&
                    option.Length <= shelf.Height + 1e-9)
                {
                    feasible?.Add((shelf, option));

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

        if (feasible is { Count: > 0 } && random is not null && random.Next(2) == 0)
        {
            (Shelf shelf, PlanWrapLogic.SheetOption option) = feasible[random.Next(feasible.Count)];
            Place(shelf, itemIndex, option);
            return;
        }

        if (bestShelf is not null)
        {
            Place(bestShelf, itemIndex, bestOption);
            return;
        }

        PlanWrapLogic.SheetOption shortest = Shortest(options, forced);

        // b) Grow the last shelf if the piece fits beside what is already there — but only
        // when that costs less than starting a fresh shelf.
        if (shelves.Count > 0)
        {
            Shelf last = shelves[^1];
            double bestGrowth = double.MaxValue;
            bool found = false;
            PlanWrapLogic.SheetOption grownOption = default;

            foreach (PlanWrapLogic.SheetOption option in options)
            {
                if (forced is { } only && option != only)
                {
                    continue;
                }

                if (option.Width > rollWidth - last.UsedX + 1e-9)
                {
                    continue;
                }

                double growth = option.Length - last.Height;

                if (growth < bestGrowth)
                {
                    bestGrowth = growth;
                    grownOption = option;
                    found = true;
                }
            }

            if (found && bestGrowth < shortest.Length)
            {
                last.Height = Math.Max(last.Height, grownOption.Length);
                Place(last, itemIndex, grownOption);
                return;
            }
        }

        // c) Start a new shelf. Always taking the shortest sheet here is what made a run of
        // identical boxes draw one shelf each — every shelf minimal in height, none of them able
        // to share its width with the next box. The search sometimes opens with a sheet narrow
        // enough that another can join it, which is the only way two of them end up side by side.
        PlanWrapLogic.SheetOption pick = random is not null && random.Next(2) == 0
            ? ShortestThatShares(options, rollWidth, shortest)
            : shortest;

        var newShelf = new Shelf { Height = pick.Length };
        shelves.Add(newShelf);
        Place(newShelf, itemIndex, pick);
    }

    /// <summary>The sheet a fresh shelf would be built for: the shortest, then the widest.</summary>
    private static PlanWrapLogic.SheetOption Shortest(
        IReadOnlyList<PlanWrapLogic.SheetOption> options,
        PlanWrapLogic.SheetOption? forced) =>
        forced ?? options.OrderBy(option => option.Length)
            .ThenByDescending(option => option.Width)
            .First();

    /// <summary>
    /// The shortest sheet that still leaves half the roll for whatever comes next. A shelf is
    /// only ever as long as its tallest piece, so opening one with the shortest sheet — the
    /// greedy choice, and the right one when the shelf ends up alone — can leave a sliver too
    /// narrow for a second box that would otherwise have cost nothing at all.
    /// </summary>
    private static PlanWrapLogic.SheetOption ShortestThatShares(
        IReadOnlyList<PlanWrapLogic.SheetOption> options,
        double rollWidth,
        PlanWrapLogic.SheetOption fallback)
    {
        foreach (PlanWrapLogic.SheetOption option in options.OrderBy(o => o.Length)
                     .ThenByDescending(o => o.Width))
        {
            if (option.Width <= (rollWidth / 2) + 1e-9)
            {
                return option;
            }
        }

        return fallback;
    }

    private static void Place(Shelf shelf, int itemIndex, PlanWrapLogic.SheetOption option)
    {
        shelf.Placed.Add((itemIndex, option, shelf.UsedX));
        shelf.UsedX += option.Width;
    }

    /// <summary>
    /// Takes a box back out, leaving every other cut where it was: the shelf closes the gap,
    /// drops in height if that box was its tallest piece, and disappears once empty. Height and
    /// X are derived from the pieces left, so nothing can go stale behind a removal.
    /// </summary>
    private static void Remove(List<Shelf> shelves, int itemIndex)
    {
        for (int i = 0; i < shelves.Count; i++)
        {
            Shelf shelf = shelves[i];
            int at = shelf.Placed.FindIndex(placed => placed.ItemIndex == itemIndex);

            if (at < 0)
            {
                continue;
            }

            shelf.Placed.RemoveAt(at);

            if (shelf.Placed.Count == 0)
            {
                shelves.RemoveAt(i);
                return;
            }

            double x = 0;

            for (int j = 0; j < shelf.Placed.Count; j++)
            {
                shelf.Placed[j] = shelf.Placed[j] with { X = x };
                x += shelf.Placed[j].Option.Width;
            }

            shelf.UsedX = x;
            shelf.Height = shelf.Placed.Max(placed => placed.Option.Length);
            return;
        }
    }

    private static List<Shelf> Clone(List<Shelf> shelves) =>
        [.. shelves.Select(shelf => shelf.Clone())];

    private static Cost Measure(List<Shelf> shelves)
    {
        double length = 0;
        double area = 0;

        foreach (Shelf shelf in shelves)
        {
            length += shelf.Height;

            foreach ((_, PlanWrapLogic.SheetOption option, _) in shelf.Placed)
            {
                area += option.Width * option.Length;
            }
        }

        return new Cost(length, area);
    }

    /// <summary>
    /// Shorter always wins. Lengths within a hair of each other are a tie, and a tie goes to the
    /// plan that cuts less paper — otherwise an orientation change could cost paper and buy
    /// nothing, which is the waste this search exists to remove.
    /// </summary>
    private static bool IsBetter(Cost candidate, Cost incumbent) =>
        candidate.Length < incumbent.Length - 1e-9
        || (candidate.Length <= incumbent.Length + 1e-9 && candidate.Area < incumbent.Area - 1e-9);
}
