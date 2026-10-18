using Microsoft.Playwright;
using Reqnroll;

namespace WrapCal.FunctionalTests.StepDefinitions;

/// <summary>
/// What the page loads to style itself. These read the live document and the server rather than
/// the source, so they fail if a build step quietly puts Bootstrap back.
/// </summary>
[Binding]
public sealed class AssetSteps : StepsBase
{
    public AssetSteps(ScenarioContext scenarioContext)
        : base(scenarioContext)
    {
    }

    [Then("no Bootstrap stylesheet should be linked")]
    public async Task ThenNoBootstrapStylesheetShouldBeLinkedAsync()
    {
        string[] hrefs = await StylesheetHrefsAsync();

        await Assert.That(hrefs.Any(href => href.Contains("bootstrap", StringComparison.OrdinalIgnoreCase)))
            .IsFalse();
    }

    [Then("the old Bootstrap file should no longer be served")]
    public async Task ThenTheOldBootstrapFileShouldNoLongerBeServedAsync()
    {
        IAPIResponse response = await Ui.Page.APIRequest.GetAsync(
            Ui.Url("/lib/bootstrap/dist/css/bootstrap.min.css"));

        // The site is static files, so a missing one is simply missing: the status is the whole
        // answer, and it is the file not being there any more.
        await Assert.That(response.Status).IsEqualTo(404);
    }

    [Then("the MudBlazor stylesheet should be linked")]
    public async Task ThenTheMudBlazorStylesheetShouldBeLinkedAsync()
    {
        string[] hrefs = await StylesheetHrefsAsync();

        await Assert.That(hrefs.Any(href => href.Contains("MudBlazor", StringComparison.OrdinalIgnoreCase)))
            .IsTrue();
    }

    [Then("the MudBlazor theme should be applied")]
    public async Task ThenTheMudBlazorThemeShouldBeAppliedAsync()
    {
        // The provider writes the palette as custom properties, so one of them resolving to a
        // colour is proof the theme is live rather than the stylesheet being linked and ignored.
        string? primary = await Ui.Page.EvaluateAsync<string?>(
            "() => getComputedStyle(document.documentElement)"
            + ".getPropertyValue('--mud-palette-primary').trim()");

        await Assert.That(string.IsNullOrWhiteSpace(primary)).IsFalse();
    }

    private async Task<string[]> StylesheetHrefsAsync() =>
        await Ui.Page.EvaluateAsync<string[]>(
            "() => Array.from(document.querySelectorAll('link[rel=\"stylesheet\"]'))"
            + ".map(link => link.getAttribute('href') ?? '')");
}
