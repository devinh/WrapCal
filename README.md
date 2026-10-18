# WrapCal

Plan how much wrapping paper a set of gift boxes needs.

Given box dimensions and the usable width of a paper roll, WrapCal aims to produce a
cut plan: where to cut the roll, how much roll length the boxes consume, and how much
paper is left as waste.

> **Status: early scaffold.** The Blazor shell, build config, and test setup exist.
> No wrap-planning logic is implemented and the only page is a placeholder.

## Status

| | |
|---|---|
| Blazor shell (.NET 10, static SSR) | ✅ Done |
| Build, test, and lint config | ✅ Done |
| Wrap-planning domain logic | ❌ Not started |
| Planning UI | ❌ Not started |
| Persistence | None by design — the app is stateless |

Nothing in the app currently reads or produces wrap plans.

## Requirements

- **.NET SDK 10.0.x** — developed against `10.0.402`
- No database, no external services, no secrets
- Playwright browsers for the functional suite — see [Testing](#testing)

## Getting started

```bash
# Build
dotnet build

# Run — serves http://localhost:5127 and https://localhost:7002
dotnet run --project src/WrapCal

# Run with hot reload
dotnet watch --project src/WrapCal

# Test everything
dotnet test

# Test one project at a time (the usual way to work — the functional suite
# starts a real server and browser, so it takes seconds not milliseconds)
dotnet test --project tests/WrapCal.UnitTests
dotnet test --project tests/WrapCal.FunctionalTests
```

## Project layout

```
WrapCal.slnx                 # solution
global.json                  # opts dotnet test into Microsoft.Testing.Platform mode
Directory.Build.props        # shared compiler + analyzer settings
.editorconfig                # code style, enforced at build time
CLAUDE.md                    # conventions and architecture notes

WrappingPaperOptimizer.cs    # domain reference — NOT compiled (see below)

src/
  WrapCal/                   # the app (Blazor Web App)
    Components/
      Layout/                # MainLayout, NavMenu, ReconnectModal
      Pages/                 # Home, Error, NotFound
      App.razor, Routes.razor, _Imports.razor
    wwwroot/                 # static assets (Bootstrap, app.css)
    Program.cs
    WrapCal.csproj

tests/
  WrapCal.UnitTests/         # TUnit + bUnit — unit and component tests
    HomePageTests.cs

  WrapCal.FunctionalTests/   # TUnit + Reqnroll + Playwright — BDD against a real browser
    Features/                #   Gherkin .feature files
    StepDefinitions/         #   bindings
    Hooks/                   #   app + browser lifecycle
    Support/                 #   AppHost, UiContext
```

## Testing

Two test projects, deliberately separate — run either on its own.

| Project | Stack | Covers |
|---|---|---|
| `tests/WrapCal.UnitTests` | TUnit + bUnit | Unit tests and Blazor component rendering |
| `tests/WrapCal.FunctionalTests` | TUnit + Reqnroll + Playwright | Gherkin scenarios against a real browser |

Both run on **Microsoft.Testing.Platform**, opted into via `global.json`. Consequences:

- Test projects are **executables** (`<OutputType>Exe</OutputType>`) — expected, not a mistake.
- `Microsoft.NET.Test.Sdk` is **not** used. Don't add it back.
- **`dotnet test --filter` does not work.** TUnit runs on MTP, not VSTest, so the flag is
  silently rejected and the run reports "Zero tests ran". Use `--treenode-filter`:
  `--treenode-filter "/*/*/*/*[Category=Smoke]"`.

### Functional tests

The functional suite starts the app as a real process on a free port and drives it with a
real browser. An in-memory test server can't work here: Playwright talks TCP, and Blazor
Server needs a real SignalR circuit. Startup costs a few seconds, hence the separate project.

**Install browsers once.** Playwright .NET ships its installer as `playwright.ps1`, which
needs PowerShell — not available by default here. The script just calls
`Microsoft.Playwright.Program.Main`, so invoke the DLL directly:

```bash
# Run `dotnet build` first — the DLL lives in the build output.
BIN=tests/WrapCal.FunctionalTests/bin/Debug/net10.0
dotnet exec --runtimeconfig "$BIN/WrapCal.FunctionalTests.runtimeconfig.json" \
  "$BIN/Microsoft.Playwright.dll" install webkit firefox
```

**Browser selection** follows the OS, overridable with `BROWSER=webkit|firefox|chromium`:

| OS | Default | Why |
|---|---|---|
| macOS | `webkit` (Safari's engine) | Firefox cannot launch on macOS 27 — see below |
| Linux / Windows | `firefox` | |

Set `HEADED=1` to run with a visible browser — the fastest way to debug a failure.

> **Firefox is broken on macOS 27.** It cannot start at all, sandboxed or not, with or
> without Playwright. The macOS sandbox denies Firefox's `plugin-container` a sandbox
> extension and startup never completes, surfacing misleadingly as *"Could not find profile
> folder"* — the profile directory is created fine. This is upstream in Firefox;
> microsoft/playwright#42082 was closed as *not planned*. Chromium and WebKit are unaffected,
> which is why macOS defaults to WebKit.

## Domain reference: `WrappingPaperOptimizer.cs`

The repository root holds a standalone, working implementation of the wrapping domain
(namespace `GiftWrapOptimizer`) covering box/roll models, unit conversion, and a
shelf-based packing optimizer with randomised restarts.

**It is not part of the build and is never compiled** — the web project only globs
`src/WrapCal/**/*.cs`. The build will not fail if you break it.

Treat it as a **specification**: port what you need into a feature slice and adapt it,
rather than assuming its types are available.

## Architecture

WrapCal uses **vertical slice architecture** — code is organized by feature, not by
technical layer, and there is no shared `Services/` or `Models/` folder. New work goes
in `src/WrapCal/Features/<FeatureName>/`, containing that feature's page, logic, and
models together.

See [CLAUDE.md](CLAUDE.md) for the full conventions, coding standards, and anti-patterns.
