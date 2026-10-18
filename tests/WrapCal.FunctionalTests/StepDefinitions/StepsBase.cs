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

    protected const string BoxRowSelector = "[data-testid='box-row']";

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
        await WaitForCircuitAsync();
    }

    /// <summary>
    /// Waits for the SignalR circuit to attach. Until then the page is the prerendered copy and
    /// every control is disabled — a click would either do nothing or POST the form and navigate
    /// away. Waiting for the button to become enabled is waiting for the page to be genuinely
    /// live, and it is the same wait after a reload as after a first navigation.
    /// </summary>
    protected async Task WaitForCircuitAsync() =>
        await Ui.Page.WaitForSelectorAsync(
            "[data-testid='plan-button']:not([disabled])",
            new PageWaitForSelectorOptions { Timeout = PageTimeout });

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
            : "Centimetres";

        ILocator host = Ui.Page.Locator("[data-testid='unit-select']");

        if (await SelectedTextAsync(host) == target)
        {
            return;
        }

        string before = await Ui.Page.Locator("[data-testid='roll-width']").InputValueAsync();

        await PickAsync(host, target);

        await Ui.Page.WaitForFunctionAsync(
            "([selector, previous]) => document.querySelector(selector)?.value !== previous",
            new[] { "[data-testid='roll-width']", before },
            new PageWaitForFunctionOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// A MudSelect is an overlay, not a native &lt;select&gt;: there is nothing for
    /// selectOption to drive, so the control is opened and the option clicked by its label.
    /// </summary>
    protected async Task PickAsync(ILocator host, string label)
    {
        await host.ClickAsync();

        await Ui.Page.GetByRole(AriaRole.Option, new() { Name = label, Exact = true }).ClickAsync();
    }

    /// <summary>
    /// What a MudSelect currently shows. The visible text is rendered in the component's own
    /// markup; the hidden input it keeps carries the same value and is simpler to read.
    /// </summary>
    protected static Task<string> SelectedTextAsync(ILocator host) =>
        host.Locator("input").First.InputValueAsync();

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
