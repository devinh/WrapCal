using Cake.Common.Tools.ReportGenerator;

// Installed by the script rather than pinned in a tool manifest, so running the build is the
// only thing a fresh checkout or a runner has to do — no separate `dotnet tool restore` step
// and no second file to keep in step with the version below. Cake skips the download once the
// version is in its tool cache. Bump the version here; nothing else tracks it.
InstallTool("dotnet:?package=dotnet-reportgenerator-globaltool&version=5.5.11");

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

var reports = $"{output}/reports";
var unitTestResults = $"{reports}/unit";
var functionalTestResults = $"{reports}/functional";
var coverageReport = $"{reports}/coverage";

// Which browser the suite drives, following the suite's own rule: WebKit on macOS, because
// Firefox cannot launch there at all, and Firefox everywhere else. BROWSER overrides it.
string browser = EnvironmentVariable("BROWSER")
    ?? (OperatingSystem.IsMacOS() ? "webkit" : "firefox");

// Coverage is opt-in: instrumenting the suite costs a couple of seconds against a run that
// otherwise takes under one, and that fast loop is the reason the suite is separate.
//
// Both spellings of the flag count. `Argument<string?>` alone is not enough, because Cake
// parses a valueless `--coverage` to an empty value that converts to the default — the flag is
// only visible through HasArgument. `--coverage=false` is the opt-out, for a script that sets
// it unconditionally.
string? coverageArgument = Argument<string?>("coverage", null);
var collectCoverage = Context.Arguments.HasArgument("coverage")
    && !string.Equals(coverageArgument, "false", StringComparison.OrdinalIgnoreCase);

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
        DotNetTest(unitTestsProject, TestSettings(unitTestResults, collectCoverage));

        // Here rather than in a target of its own: the report is a step of collecting coverage,
        // and a separate task would be skipped by `--target=UnitTests --coverage`, which is
        // exactly the run that asks for it.
        if (collectCoverage)
        {
            GenerateCoverageReport();
        }
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
        DotNetTest(functionalTestsProject, TestSettings(functionalTestResults));
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

// Every suite writes its own HTML report and TRX into its own folder under ./output/reports,
// which is what the workflows upload. Everything the test app understands goes after `--`: in
// MTP mode `dotnet test` hands those to the app instead of parsing them itself.
DotNetTestSettings TestSettings(string resultsDirectory, bool withCoverage = false)
{
    return new DotNetTestSettings
    {
        Configuration = configuration,
        NoBuild = true,
        ArgumentCustomization = args =>
        {
            args.Append("--")
                .Append("--results-directory").AppendQuoted(resultsDirectory)
                .Append("--report-trx");

            if (withCoverage)
            {
                args.Append("--coverage")
                    .Append("--coverage-output-format").Append("cobertura")
                    .Append("--coverage-output").Append("coverage.cobertura.xml");
            }

            return args;
        }
    };
}

// Turns the cobertura XML into something a person can read — the HTML report in the artifacts —
// and into the markdown that ci.yml posts as the pull request comment.
void GenerateCoverageReport()
{
    ReportGenerator(
        new FilePath($"{unitTestResults}/coverage.cobertura.xml"),
        new DirectoryPath(coverageReport),
        new ReportGeneratorSettings
        {
            ReportTypes =
            [
                ReportGeneratorReportType.Html,
                ReportGeneratorReportType.MarkdownSummaryGithub
            ]
        });

    AssertCoverageReportExists();
}

// ReportGenerator failing quietly would leave a workflow that uploads nothing and comments
// nothing, with every exit code zero up to that point.
void AssertCoverageReportExists()
{
    string summary = $"{coverageReport}/SummaryGithub.md";

    if (!FileExists(summary))
    {
        throw new CakeException(
            "Coverage was collected but ReportGenerator wrote no summary: " + summary
            + " does not exist.");
    }
}

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
