using Reqnroll;
using WrapCal.FunctionalTests.Support;

namespace WrapCal.FunctionalTests.StepDefinitions;

[Binding]
public sealed class HomeSteps
{
    private readonly UiContext _ui;

    public HomeSteps(ScenarioContext scenarioContext) => _ui = scenarioContext.Get<UiContext>();

    [When("I open the home page")]
    public async Task WhenIOpenTheHomePageAsync() => await _ui.Page.GotoAsync(_ui.Url());

    [Then("the page heading should read {string}")]
    public async Task ThenThePageHeadingShouldReadAsync(string expected)
    {
        await _ui.Page.WaitForSelectorAsync("h1");

        string? actual = await _ui.Page.Locator("h1").TextContentAsync();

        await Assert.That(actual?.Trim()).IsEqualTo(expected);
    }
}
