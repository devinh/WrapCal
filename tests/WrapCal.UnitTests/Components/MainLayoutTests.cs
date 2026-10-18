using Bunit;
using WrapCal.Components.Layout;

namespace WrapCal.UnitTests;

public class MainLayoutTests : BunitContext
{
    [Test]
    public async Task Renders_the_app_name_and_the_page_content()
    {
        var cut = Render<MainLayout>(parameters =>
            parameters.Add(p => p.Body, "page content"));

        await Assert.That(cut.Markup).Contains("WrapCal");
        await Assert.That(cut.Markup).Contains("page content");
    }

    [Test]
    public async Task Has_no_navigation()
    {
        // The whole site is the calculator, so a nav creeping back in is a regression.
        var cut = Render<MainLayout>(parameters =>
            parameters.Add(p => p.Body, "page content"));

        await Assert.That(cut.FindAll("nav").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll(".nav-link").Count).IsEqualTo(0);
    }
}
