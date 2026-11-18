namespace WrapCal.Common;

/// <summary>
/// Where a swallowed JS interop failure goes. The browser console is the only place a line can go
/// in a browser tab — the runtime binds <see cref="Console.Out"/> to <c>console.log</c> and
/// <see cref="Console.Error"/> to <c>console.error</c> — so this is the whole of the app's logging,
/// and it exists only because a failure the app deliberately ignores would otherwise be invisible.
/// </summary>
internal static class JsInteropLog
{
    /// <summary>
    /// Reports a failure the caller has chosen to swallow. A cancellation is the routine teardown
    /// of a navigation, so it goes in as information; anything else is a genuine failure and
    /// belongs in the console's error stream, which is what keeps that stream worth reading.
    /// </summary>
    public static void Failure(string identifier, Exception exception)
    {
        TextWriter console = exception is OperationCanceledException ? Console.Out : Console.Error;

        console.WriteLine($"{identifier} failed: {exception}");
    }
}
