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
- **A paper layout** — an SVG diagram of the sheet with each cut drawn to scale and labeled
  with the box name and its size. The origin is marked, and a hatched band shows which side
  the unused roll is on — top, bottom, left or right, your choice. A long plan scrolls
  along the roll rather than shrinking to fit: the more boxes you add, the longer the
  drawing gets, and it opens at the origin.
- **A wrapping note per box** — which pair of the box's own sides the paper goes around.

Units switch between inches and centimeters, converting what you have already typed.

## How the calculation works

Each box is wrapped in one rectangular sheet, wrapped around four of its sides and folded over the
two ends. Everything is worked out in centimeters and converted back to your units at the end.

### What sheet does a box need?

Three orientations are possible, one per axis of the box. For a chosen axis, `p` and `q` are the two
sides across it:

```
wrap-around = 2 × (p + q) + margin       the perimeter the paper goes around, plus a lap at the seam
along-box   = axis + min(p, q) + margin  the box's length, plus enough to fold over both ends
```

The fold-over is `min(p, q)` because folding closes the end face — a `p × q` rectangle — and half its
**shorter** side per end is enough. The flaps that meet in the middle already span the long
direction, so charging for that would cut more paper than wrapping needs.

Each orientation gives up to two candidate sheets (the sheet and its transpose), kept only if the
sheet's width fits across the roll.

### Which candidates are thrown away

- **Duplicates**, by size.
- **Dominated sheets**: if another sheet for the same box is no wider *and* no longer, it fits
  wherever the bigger one does without making the strip taller — so cutting the bigger one can only
  spend paper. A 9 × 6 × 1.5 box is never offered an 8 × 30.5 sheet when 8 × 21.5 exists.
- If nothing survives, the box is reported as too wide to wrap in one sheet.

**Boxes of the same size are cut from the same paper.** Three identical presents should come off
three identical sheets rather than three different ones, so each size of box gets one piece — usable
either way round, which is what lets two of them share a strip. This is a deliberate trade: it costs
a couple of percent of roll on repeated sizes.

### How the sheets are arranged on the roll

Sheets are packed into full-width **strips** (shelves), so every cut is a straight line: across the
roll between strips, then along each strip between pieces. A strip is as tall as its tallest sheet,
and the roll length used is the sum of the strip heights.

Which boxes share a strip is the whole difficulty — it is a two-dimensional packing problem — so the
planner tries two things depending on how many boxes there are:

- **Twelve boxes or fewer: solved exactly.** Every way of splitting the boxes into strips is
  enumerated, and the split whose heights add up to the least wins. This is optimal, and still over
  in a few tens of milliseconds at twelve boxes.
- **More than that: searched, then repaired.** A greedy arrangement is improved by moving boxes
  between strips, re-cutting a box to a different sheet, and occasionally starting over. Between
  rounds, every pair of strips is re-solved exactly and kept if it comes out shorter. A seeded
  random number generator makes this reproducible, and the work is capped at 50,000 ÷ the number of
  boxes so a long list stays responsive.

Plans are compared by **roll length first**, with paper cut as the tie-break: the roll is what you
buy and cut, but a tie should not be broken by cutting more paper.

### What the figures mean

- **Roll Length Used** — the sum of the strip heights: how much of the roll the plan consumes.
- **Paper Cut** — the total area of the sheets, which is less than the roll consumed because strips
  are only as tall as their tallest sheet.
- **Waste** — the roll consumed but not covered by a sheet.
- **Efficiency** — paper cut as a fraction of the roll consumed.

So a long box and a short one in the same strip waste the difference in height, and the efficiency
figure is what that costs you.

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

### Build automation

`cake/` holds a [Cake](https://cakebuild.net/) build script using the Cake SDK. It is in the
solution but marked `<Build Project="false" />`, so a plain `dotnet build` at the root builds
the app and the tests but never the build script — which could not be rebuilt underneath itself
while it is the thing running the build.

```bash
./cake.sh                          # Clean → Build → Tests (the default)
./cake.sh --target=UnitTests       # just the unit + component suite
./cake.sh --target=FunctionalTests # just the browser suite
./cake.sh --target=Release         # every suite, then the site to ./output (what CI runs)
./cake.sh --target=Publish         # just publish the site to ./output
./cake.sh --target=Build --configuration=Debug

# Add --coverage to any target that runs the unit suite to measure it
./cake.sh --target=UnitTests --coverage
```

`Tests` is both suites — it runs each as a target of its own, so running them together and
running one alone share a single definition. On Windows use `.\cake.ps1` with the same
arguments. `Publish` then checks the published `index.html` for unresolved `#[.{fingerprint}]`
asset placeholders — the one failure that still deploys and still serves, with a blank page as its
only symptom — and fails the run rather than letting it reach Pages.

### Publishing and hosting

`./cake.sh --target=Publish` leaves a folder of static files in `./output/wwwroot` — no
server, no runtime to install. (The publish also writes a few host files beside it that a
static host ignores; `wwwroot` is the site.)

Pushing to `main` deploys that folder to GitHub Pages through
`.github/workflows/deploy-pages.yml`, at **https://devinh.github.io/WrapCal/**. Pages has to
be turned on for the repository once, with *GitHub Actions* as the source, under
Settings → Pages.

A project page is served from a subpath, so `index.html` raises its own `<base href>` to the
directory it was actually loaded from — `/WrapCal/` on Pages, `/` from the dev server. Nothing
else in the app mentions the path, and no `.nojekyll` is needed: deploying through Actions
skips Jekyll, which only processes branch-based deployments.

Because everything runs in the browser, the page is blank for a moment while the runtime
downloads. That is the one thing server rendering did better.

## Project layout

```
WrapCal.slnx                 # solution
global.json                  # opts dotnet test into Microsoft.Testing.Platform mode
Directory.Build.props        # shared compiler + analyzer settings
.editorconfig                # code style, enforced at build time
CLAUDE.md                    # conventions and architecture notes
.claude/skills/              # project skills: functional-tests, unit-tests, theme, build-and-deploy

cake/                        # Cake build (Cake SDK, net10.0) — in the solution, marked not to build
cake.sh / cake.ps1           # run it: ./cake.sh --target=Tests

src/
  WrapCal/                   # the app (Blazor WebAssembly, styled with MudBlazor)
    Components/
      Layout/                # MainLayout — app bar and content, no navigation
      Pages/                 # NotFound
      Routes.razor, _Imports.razor
    Common/
      Result.cs              # Result<T>
    Features/
      PlanWrap/              # the calculator: page, logic, models, summary view, SVG diagram
    wwwroot/                 # index.html, Roboto, app.css, theme.js; MudBlazor ships its own
    Program.cs
    WrapCal.csproj

tests/
  WrapCal.UnitTests/         # TUnit + bUnit — logic, components, form model
    Common/ Components/ Features/

  WrapCal.FunctionalTests/   # TUnit + Reqnroll + Playwright — BDD against a real browser
    Features/                #   PlanWrap (behavior), CutPlans (spec tables), Layout
    StepDefinitions/         #   StepsBase + Form / Planning / Layout steps
    Hooks/                   #   app + browser lifecycle
    Support/                 #   AppHost, UiContext
```

## Architecture

WrapCal uses **vertical slice architecture** — code is organized by feature, not by
technical layer. The calculator lives in `src/WrapCal/Features/PlanWrap/`, holding its page,
logic, models, and display component together.

See [CLAUDE.md](CLAUDE.md) for conventions, coding standards, and anti-patterns, and
[`src/WrapCal/Features/PlanWrap/README.md`](src/WrapCal/Features/PlanWrap/README.md) for how the
planner and the diagram work — why the maths is what it is, and what each of the search's knobs
buys.

## Testing

Two test projects, deliberately separate — run either on its own.

| Project | Stack | Covers | Takes |
|---|---|---|---|
| `tests/WrapCal.UnitTests` | TUnit + bUnit | Planner maths, form model, components | under a second |
| `tests/WrapCal.FunctionalTests` | TUnit + Reqnroll + Playwright | The real page in a real browser | a few seconds |

`CutPlans.feature` is the readable specification: concrete roll and box inputs, with the
sheet each box should get back. `PlanWrap.feature` covers behavior and errors.
`Layout.feature` measures where things sit on the page, and that the paper layout overflows
its frame rather than shrinking into it. `Assets.feature` checks what a browser cannot
show but a diff can hide: that Bootstrap is neither linked nor served, and that MudBlazor
is doing the styling.

The functional suite runs two scenarios at a time. Every scenario boots the WebAssembly runtime
in its own browser context and that is a whole-machine burst, so TUnit's default of running
everything at once left the machine unusable for the length of a run. Set
`WRAPCAL_SCENARIO_PARALLELISM=1` to serialize them when you need the machine back — it takes
about as long as the unbounded run did, at a fraction of the load.

Both run on **Microsoft.Testing.Platform**, opted into via `global.json`. Consequences:

- Test projects are **executables** (`<OutputType>Exe</OutputType>`) — expected, not a mistake.
- `Microsoft.NET.Test.Sdk` is **not** used. Don't add it back.
- **`dotnet test --filter` does not work.** TUnit runs on MTP, not VSTest, so the flag is
  silently rejected and the run reports "Zero tests ran". Use `--treenode-filter`:
  `--treenode-filter "/*/*/*/*[Category=Smoke]"`.

### Coverage and reports

Every test run writes a report of itself to `./output/reports`, through Cake:

| Path | What it is |
|---|---|
| `output/reports/unit` | TUnit's HTML report + TRX for the unit and component suite |
| `output/reports/functional` | The same for the browser suite |
| `output/reports/coverage` | ReportGenerator's HTML coverage report, and `SummaryGithub.md` |

Coverage is **off by default** — instrumenting the suite costs a couple of seconds against a run
that otherwise takes under one, and that speed is why the unit suite is worth running constantly.
Add `--coverage` to switch it on:

```bash
./cake.sh --target=UnitTests --coverage
```

That collects Cobertura coverage of the `WrapCal` assembly as the unit suite runs, then runs
[ReportGenerator](https://github.com/danielpalme/ReportGenerator) over it. The tool installs
itself from `cake/cake.cs` — no separate restore step, and a fresh checkout and a runner get the
same version.

Coverage is only collected from the **unit** suite. The functional suite drives the app in a
browser, where the code under test is WebAssembly in a separate process — the test process would
only instrument its own steps.

On a pull request, CI uploads `output/reports` as a run artifact in both workflows, and `ci.yml`
also posts the coverage summary as a comment that each push replaces in place rather than
appending to. The reports stay out of `output/wwwroot`, so they are never deployed to Pages.

### Functional tests

The functional suite publishes the app, serves that output from an in-process Kestrel under
`/WrapCal`, and drives it with a real browser. It is deliberately the artifact that ships rather
than a development host — a `dotnet build` output is not servable at all, since the app's own
assets only land in the folder on publish. Startup costs a few seconds, hence the separate
project.

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
