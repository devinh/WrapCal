using Reqnroll;

namespace WrapCal.FunctionalTests.StepDefinitions;

/// <summary>
/// What the app says when a JS interop call fails. The call itself is swallowed on purpose — the
/// drawing renders and only the zoom is lost — so the console line is the whole of the evidence,
/// and whether it lands there is the runtime's decision rather than the app's.
/// </summary>
[Binding]
public sealed class DiagnosticsSteps : StepsBase
{
    private const string BrokenCall = "wrapcalDiagram.attach";

    /// <summary>
    /// Generous: the drawing renders first, and the interop call that fails happens in the render
    /// that follows it.
    /// </summary>
    private const int ConsoleTimeout = 10_000;

    private readonly TaskCompletionSource<string> _failureLogged =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DiagnosticsSteps(ScenarioContext scenarioContext)
        : base(scenarioContext)
    {
    }

    /// <summary>
    /// Runs before any script on the page, so the listener is attached before the first navigation
    /// and the script under test is already broken by the time it loads. The global is made
    /// read-only rather than deleted, because <c>diagram.js</c> assigns it from a classic,
    /// non-strict script — where an assignment to a read-only property is ignored. What is left is
    /// the app calling a function that is not there, which is what a stale or half-loaded script
    /// looks like from Blazor's side.
    /// </summary>
    [Given("the diagram script is broken before the page loads")]
    public async Task GivenTheDiagramScriptIsBrokenBeforeThePageLoadsAsync()
    {
        await Ui.Page.AddInitScriptAsync(
            "Object.defineProperty(window, 'wrapcalDiagram', "
            + "{ value: {}, writable: false, configurable: false });");

        Ui.Page.Console += (_, message) =>
        {
            if (message.Type == "error" && message.Text.Contains(BrokenCall, StringComparison.Ordinal))
            {
                _failureLogged.TrySetResult(message.Text);
            }
        };
    }

    [Then("the console should report the failed diagram call")]
    public async Task ThenTheConsoleShouldReportTheFailedDiagramCallAsync()
    {
        // Awaited rather than read off a list: the console event arrives on the browser's schedule,
        // which is after the layout step that proves the drawing rendered.
        string reported = await _failureLogged.Task.WaitAsync(TimeSpan.FromSeconds(ConsoleTimeout));

        await Assert.That(reported).Contains($"{BrokenCall} failed");
    }
}
