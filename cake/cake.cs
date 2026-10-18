var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var solution = "./WrapCal.slnx";
var appProject = "./src/WrapCal/WrapCal.csproj";
var unitTestsProject = "./tests/WrapCal.UnitTests/WrapCal.UnitTests.csproj";
var functionalTestsProject = "./tests/WrapCal.FunctionalTests/WrapCal.FunctionalTests.csproj";
var output = "./output";

var functionalTestsBin = $"./tests/WrapCal.FunctionalTests/bin/{configuration}/net10.0";
var playwrightDll = $"{functionalTestsBin}/Microsoft.Playwright.dll";
var playwrightRuntimeConfig = $"{functionalTestsBin}/WrapCal.FunctionalTests.runtimeconfig.json";

// Which browser the suite drives, following the suite's own rule: WebKit on macOS, because
// Firefox cannot launch there at all, and Firefox everywhere else. BROWSER overrides it.
string browser = EnvironmentVariable("BROWSER")
    ?? (OperatingSystem.IsMacOS() ? "webkit" : "firefox");

var testSettings = new DotNetTestSettings
{
    Configuration = configuration,
    NoBuild = true
};

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
    {
        DotNetClean(solution, new DotNetCleanSettings
        {
            Configuration = configuration
        });

        if (DirectoryExists(output))
        {
            CleanDirectory(output);
        }
    });

Task("Build")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        DotNetBuild(solution, new DotNetBuildSettings
        {
            Configuration = configuration
        });
    });

Task("UnitTests")
    .IsDependentOn("Build")
    .Does(() =>
    {
        DotNetTest(unitTestsProject, testSettings);
    });

// The functional suite drives a real browser, so a fresh machine — a CI runner especially —
// has to have one. Idempotent: Playwright skips the download when the browser is already there.
Task("InstallBrowsers")
    .IsDependentOn("Build")
    .Does(() =>
    {
        // Playwright's own installer is a PowerShell script, which is available on neither this
        // machine nor a Linux runner. The script only loads Microsoft.Playwright.dll and calls
        // its Main, so the DLL is invoked through `dotnet exec` instead.
        //
        // --runtimeconfig has to sit between `exec` and the assembly — passed after it, it
        // reaches the app rather than the host. DotNetExecute puts the assembly in place first,
        // so the whole line is rebuilt in ArgumentCustomization, and the arguments parameter
        // here is deliberately empty: anything given to it is discarded, and repeating the app
        // arguments in both places only makes it look as though both are used. The flag is not
        // optional — Microsoft.Playwright.dll ships no .runtimeconfig.json of its own, so
        // without it the host reads the DLL as a self-contained app and fails on a missing
        // libhostpolicy.
        DotNetExecute(
            playwrightDll,
            new ProcessArgumentBuilder(),
            new DotNetExecuteSettings
            {
                ArgumentCustomization = _ => new ProcessArgumentBuilder()
                    .Append("exec")
                    .Append("--runtimeconfig")
                    .AppendQuoted(playwrightRuntimeConfig)
                    .AppendQuoted(playwrightDll)
                    .Append("install")
                    .Append(browser)
            });
    });

Task("FunctionalTests")
    .IsDependentOn("InstallBrowsers")
    .Does(() =>
    {
        DotNetTest(functionalTestsProject, testSettings);
    });

Task("Tests")
    .IsDependentOn("UnitTests")
    .IsDependentOn("FunctionalTests");

// Publishes the site into ./output, where the deploy workflow uploads output/wwwroot from.
Task("Publish")
    .IsDependentOn("Build")
    .Does(() =>
    {
        // Deliberately no --no-build, even though Build has just run. The #[.{fingerprint}]
        // placeholders in index.html are rewritten during publish and not during build, so
        // publishing a build that already happened leaves them literal — and a literal one is a
        // 404 for the boot script, which means a site that never starts.
        DotNetPublish(appProject, new DotNetPublishSettings
        {
            Configuration = configuration,
            OutputDirectory = output
        });

        AssertSiteIsBootable($"{output}/wwwroot/index.html");
    });

// What the deploy workflow asks for: every suite has to pass, then the site is published.
// The functional suite is the one that catches a UI regression — a MudBlazor bump that changes
// a layout, say — which is exactly the kind of change that would otherwise deploy unnoticed.
Task("Release")
    .IsDependentOn("Tests")
    .IsDependentOn("Publish");

Task("Default")
    .IsDependentOn("Tests");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

//////////////////////////////////////////////////////////////////////
// HELPERS
//////////////////////////////////////////////////////////////////////

// The published index.html is the one file that can be wrong while every exit code is zero:
// an unresolved #[.{fingerprint}] placeholder still deploys and still serves, and the only
// symptom is a page that never boots. Checked here rather than discovered in a browser.
void AssertSiteIsBootable(string indexPath)
{
    if (!FileExists(indexPath))
    {
        throw new CakeException("The publish produced no site: " + indexPath + " does not exist.");
    }

    if (System.IO.File.ReadAllText(indexPath).Contains("#[.{fingerprint}]", StringComparison.Ordinal))
    {
        throw new CakeException(
            indexPath + " still contains an unresolved #[.{fingerprint}] placeholder, so the boot "
            + "script would 404 and the site would never start. Publishing with --no-build skips "
            + "the rewrite that resolves it.");
    }
}
