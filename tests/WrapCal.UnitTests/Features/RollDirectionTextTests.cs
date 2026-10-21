using WrapCal.Features.PlanWrap;

namespace WrapCal.UnitTests;

public class RollDirectionTextTests
{
    [Test]
    public async Task Names_every_direction()
    {
        // The drawing, the Cut Plan and the menu all read their words from here, so a direction
        // that quietly lost its word would show up in three places at once.
        foreach (RollDirection direction in Enum.GetValues<RollDirection>())
        {
            await Assert.That(RollDirectionText.EdgeWord(direction)).IsNotEmpty();
            await Assert.That(RollDirectionText.MenuLabel(direction)).IsNotEmpty();
            await Assert.That(RollDirectionText.OriginCorner(direction)).IsNotEmpty();
        }
    }

    [Test]
    public async Task Rejects_a_value_that_is_not_a_direction()
    {
        // Only reachable through a bad cast, but the throw is what stops one being drawn as though
        // it were a real direction — a silent "Up" is worse than a failure.
        const RollDirection nonsense = (RollDirection)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => RollDirectionText.EdgeWord(nonsense));
        Assert.Throws<ArgumentOutOfRangeException>(() => RollDirectionText.MenuLabel(nonsense));
    }
}
