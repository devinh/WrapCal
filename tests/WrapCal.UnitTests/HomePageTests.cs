using Bunit;
using WrapCal.Components.Pages;

namespace WrapCal.UnitTests;

public class HomePageTests : BunitContext
{
    [Test]
    public async Task Home_renders_the_app_heading()
    {
        var cut = Render<Home>();

        await Assert.That(cut.Markup).Contains("WrapCal");
    }
}
