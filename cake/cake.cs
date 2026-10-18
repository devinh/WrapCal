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

// Publishing is deliberately a placeholder: it drops the site into ./output and nothing
// more. Whatever the real release needs — versioning, packaging, a host — belongs here.
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

Task("Default")
    .IsDependentOn("Tests");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
