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
        DotNetPublish(appProject, new DotNetPublishSettings
        {
            Configuration = configuration,
            OutputDirectory = output,
            NoBuild = true
        });
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
