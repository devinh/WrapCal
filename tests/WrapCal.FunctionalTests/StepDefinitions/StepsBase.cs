using System.Globalization;
using Microsoft.Playwright;
using Reqnroll;
using WrapCal.FunctionalTests.Support;

namespace WrapCal.FunctionalTests.StepDefinitions;

/// <summary>
/// Page helpers shared by the step definitions. Bindings are split by concern — setting the form
/// up, asserting the plan, measuring the layout — and all of them drive the same page.
/// </summary>
public abstract class StepsBase
{
    protected const int PageTimeout = 20_000;

    protected const string BoxRowSelector = "[data-testid='boxes'] tbody tr";

    protected StepsBase(ScenarioContext scenarioContext)
    {
        Ui = scenarioContext.Get<UiContext>();
        Context = scenarioContext;
    }

    protected UiContext Ui { get; }

    protected ScenarioContext Context { get; }

    protected async Task OpenPlanPageAsync()
    {
        await Ui.Page.GotoAsync(Ui.Url("/"));

        // The page is InteractiveServer. Until the SignalR circuit attaches, the submit button
        // is a plain HTML submit and clicking it POSTs and navigates away — so wait for the
        // component's own "I am interactive" marker rather than clicking and retrying.
        await Ui.Page.WaitForSelectorAsync(
            "form[data-interactive='true']",
            new PageWaitForSelectorOptions { Timeout = PageTimeout });
    }

    protected async Task ClickPlanAsync()
    {
        await Ui.Page.Locator("[data-testid='plan-button']").ClickAsync();

        // Either a plan or an error must arrive; both prove the click landed.
        await Ui.Page.WaitForSelectorAsync(
            "[data-testid='sections'], [data-testid='plan-error']",
            new PageWaitForSelectorOptions { Timeout = PageTimeout });
    }

    /// <summary>
    /// Selects a unit and waits for the converted value to arrive. Changing the unit re-expresses
    /// everything already in the form over the circuit; typing before that lands gets overwritten,
    /// and waiting for the value to change is the same signal on a fast machine or a slow one.
    /// </summary>
    protected async Task SetUnitAsync(string unit)
    {
        string target = unit.StartsWith("in", StringComparison.OrdinalIgnoreCase)
            ? "Inches"
            : "Centimeters";

        ILocator select = Ui.Page.Locator("[data-testid='unit-select']");

        if (await select.InputValueAsync() == target)
        {
            return;
        }

        string before = await Ui.Page.Locator("#rollWidth").InputValueAsync();

        await select.SelectOptionAsync(target);

        await Ui.Page.WaitForFunctionAsync(
            "([selector, previous]) => document.querySelector(selector)?.value !== previous",
            new[] { "#rollWidth", before },
            new PageWaitForFunctionOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// Fills a field and then leaves it. Blazor's inputs bind on <c>change</c>, which the browser
    /// only fires on blur, while Playwright's fill dispatches <c>input</c> alone.
    /// </summary>
    protected static async Task FillAndLeaveAsync(ILocator input, string value)
    {
        await input.FillAsync(value);
        await input.PressAsync("Tab");
    }

    protected static string Inv(double value) => value.ToString(CultureInfo.InvariantCulture);
}
