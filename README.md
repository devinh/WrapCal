# WrapCal

Plan how much wrapping paper a set of gift boxes needs.

Given box dimensions and the usable width of a paper roll, WrapCal produces a cut plan:
where to cut the roll, how much roll length the boxes consume, and how much paper is left
as waste.

It is a **single-purpose calculator** — that page is the whole site. There is no
navigation, and no plan to add other features.

## What you get

Enter the roll you have and the boxes you want to wrap:

- **A cut plan** — the roll length the boxes consume, the paper cut, the waste, and an
  efficiency figure.
- **A paper layout** — an SVG diagram of the sheet with each cut drawn to scale and labelled
  with the box name and its size. The origin is marked, and a hatched band shows which way
  the unused roll continues — up, down, left or right, your choice.
- **A wrapping note per box** — which pair of the box's own sides the paper goes around.

Units switch between inches and centimetres, converting what you have already typed.

## Requirements

- **.NET SDK 10.0.x** — developed against `10.0.402`
- No database, no external services, no secrets
- Playwright browsers for the functional suite — see [Testing](#testing)

## Getting started

```bash
# Build
dotnet build

# Run — the calculator is at http://localhost:5127
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

src/
  WrapCal/                   # the app (Blazor Web App)
    Components/
      Layout/                # MainLayout — header only, no navigation
      Pages/                 # Error, NotFound
      App.razor, Routes.razor, _Imports.razor
    Common/
      Result.cs              # Result<T>
    Features/
      PlanWrap/              # the calculator: page, logic, models, summary view, SVG diagram
    wwwroot/                 # static assets (Bootstrap, app.css)
    Program.cs
    WrapCal.csproj

tests/
  WrapCal.UnitTests/         # TUnit + bUnit — logic, components, form model
    Common/ Components/ Features/

  WrapCal.FunctionalTests/   # TUnit + Reqnroll + Playwright — BDD against a real browser
    Features/                #   PlanWrap (behaviour), CutPlans (spec tables), Layout
    StepDefinitions/         #   StepsBase + Form / Planning / Layout steps
    Hooks/                   #   app + browser lifecycle
    Support/                 #   AppHost, UiContext
```

## Architecture

WrapCal uses **vertical slice architecture** — code is organized by feature, not by
technical layer. The calculator lives in `src/WrapCal/Features/PlanWrap/`, holding its page,
logic, models, and display component together.

See [CLAUDE.md](CLAUDE.md) for conventions, coding standards, and anti-patterns.

## Testing

Two test projects, deliberately separate — run either on its own.

| Project | Stack | Covers | Takes |
|---|---|---|---|
| `tests/WrapCal.UnitTests` | TUnit + bUnit | Planner maths, form model, components | under a second |
| `tests/WrapCal.FunctionalTests` | TUnit + Reqnroll + Playwright | The real page in a real browser | a few seconds |

`CutPlans.feature` is the readable specification: concrete roll and box inputs, with the
sheet each box should get back. `PlanWrap.feature` covers behaviour and errors, and
`Layout.feature` measures where things sit on the page.

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
