using Bunit;
using WrapCal.Components.Pages;

namespace WrapCal.Tests;

public class HomePageTests : BunitContext
{
    [Fact]
    public void Home_renders_the_app_heading()
    {
        var cut = Render<Home>();

        Assert.Contains("WrapCal", cut.Markup);
    }
}
