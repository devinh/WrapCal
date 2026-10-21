using WrapCal.Features.PlanWrap;

namespace WrapCal.UnitTests;

public class ShelfOptimumTests
{
    [Test]
    public async Task Leaves_a_pair_alone_when_it_is_too_large_to_solve_exactly()
    {
        // MaxBoxes is where the exhaustive solve stops being affordable, so a pair above it is
        // skipped rather than solved. This is the guard that keeps a long list from hanging in
        // here — and the plan it declines to improve has to come back untouched.
        List<PlanWrapLogic.SheetOption> noOptions = [];
        List<List<PlanWrapLogic.SheetOption>> optionsPerBox =
            [.. Enumerable.Range(0, ShelfOptimum.MaxBoxes + 1).Select(_ => noOptions)];

        List<ShelfPacker.Shelf> shelves =
        [
            ShelfHolding(0, 7),
            ShelfHolding(7, 6),
        ];

        List<ShelfPacker.Shelf> polished = ShelfOptimum.Polish(shelves, optionsPerBox, rollWidth: 30);

        await Assert.That(polished.Count).IsEqualTo(2);
    }

    private static ShelfPacker.Shelf ShelfHolding(int firstIndex, int count) => new()
    {
        Placed =
        [
            .. Enumerable.Range(firstIndex, count)
                .Select(index => (index, default(PlanWrapLogic.SheetOption), 0.0))
        ],
    };
}
