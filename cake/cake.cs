var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var solution = "./WrapCal.slnx";
var appProject = "./src/WrapCal/WrapCal.csproj";
var unitTestsProject = "./tests/WrapCal.UnitTests/WrapCal.UnitTests.csproj";
var functionalTestsProject = "./tests/WrapCal.FunctionalTests/WrapCal.FunctionalTests.csproj";
var output = "./output";

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

Task("FunctionalTests")
    .IsDependentOn("Build")
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

// What the deploy workflow asks for: the fast suite has to pass, then the site is published.
// The functional suite is not in this path — it needs a browser and a WebAssembly boot per
// scenario, and a deployment is not the place to discover it is broken.
Task("Release")
    .IsDependentOn("UnitTests")
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
