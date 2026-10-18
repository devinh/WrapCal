using TUnit.Core;
using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<WrapCal.FunctionalTests.Support.BrowserScenarios>]

namespace WrapCal.FunctionalTests.Support;

/// <summary>
/// How many scenarios may run at once.
/// <para>
/// TUnit runs its tests in parallel by default, which is the wrong default for this suite: every
/// scenario is a browser context booting a WebAssembly runtime, and compiling that runtime is a
/// whole-machine burst. Unbounded, it holds all six cores of the development machine at once for
/// the length of a run.
/// </para>
/// <para>
/// Measured on that six-core machine, 35 scenarios:
/// </para>
/// <list type="bullet">
/// <item>unbounded — 44s, and every core saturated throughout;</item>
/// <item>two (the default) — 31s, roughly three cores on average;</item>
/// <item>one — 44s, roughly two and a half cores, the gentlest.</item>
/// </list>
/// <para>
/// Two is the default because it is the fastest and still leaves half the machine. Drop to one
/// when something else needs to run alongside the tests — it costs the same wall clock the
/// previous unbounded run did, for far less load.
/// </para>
/// <para>
/// Scenarios are independently parallel-safe — each has its own <c>IBrowserContext</c> — so this
/// is about load, not correctness.
/// </para>
/// </summary>
public sealed class BrowserScenarios : IParallelLimit
{
    /// <summary>
    /// Two by default; <c>WRAPCAL_SCENARIO_PARALLELISM</c> overrides it, so a build machine can
    /// turn it up and a laptop trying to do something else can turn it down.
    /// </summary>
    public int Limit =>
        int.TryParse(Environment.GetEnvironmentVariable("WRAPCAL_SCENARIO_PARALLELISM"), out int limit)
            && limit > 0
                ? limit
                : 2;
}
