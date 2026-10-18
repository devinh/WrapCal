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
  with the box name and its size. The origin is marked, and a hatched band shows which side
  the unused roll is on — top, bottom, left or right, your choice. A long plan scrolls
  along the roll rather than shrinking to fit: the more boxes you add, the longer the
  drawing gets, and it opens at the origin.
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

### Build automation

`cake/` holds a [Cake](https://cakebuild.net/) build script using the Cake SDK, kept beside
the solution rather than inside it — a plain `dotnet build` at the root builds the app and
the tests, never the build script.

```bash
./cake.sh                          # Clean → Build → Tests (the default)
./cake.sh --target=UnitTests       # just the unit + component suite
./cake.sh --target=FunctionalTests # just the browser suite
./cake.sh --target=Publish         # also publish the site to ./output
./cake.sh --target=Build --configuration=Debug
```

`Tests` is both suites — it runs each as a target of its own, so running them together and
running one alone share a single definition. On Windows use `.\cake.ps1` with the same
arguments. Publishing is a placeholder for now: it drops the site in `./output` and nothing
more.

### Publishing and hosting

`./cake.sh --target=Publish` leaves a folder of static files in `./output/wwwroot` — no
server, no runtime to install. (The publish also writes a few host files beside it that a
static host ignores; `wwwroot` is the site.) Any static host will do, GitHub Pages included.

`index.html` declares `<base href="/" />`, so the site must be served from the root of an
origin: a user page (`devinh.github.io`) or a custom domain. A GitHub *project* page lives
under `/WrapCal/` and would need the base href changed to match — and the dev server, which
serves at the root, would then need adjusting too.

Because everything runs in the browser, the page is blank for a moment while the runtime
downloads. That is the one thing server rendering did better.

## Project layout

```
WrapCal.slnx                 # solution
global.json                  # opts dotnet test into Microsoft.Testing.Platform mode
Directory.Build.props        # shared compiler + analyzer settings
.editorconfig                # code style, enforced at build time
CLAUDE.md                    # conventions and architecture notes

cake/                        # Cake build (Cake SDK, net10.0) — not in the solution
cake.sh / cake.ps1           # run it: ./cake.sh --target=Tests

src/
  WrapCal/                   # the app (Blazor WebAssembly, styled with MudBlazor)
    Components/
      Layout/                # MainLayout — app bar and content, no navigation
      Pages/                 # Error, NotFound
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
sheet each box should get back. `PlanWrap.feature` covers behaviour and errors.
`Layout.feature` measures where things sit on the page, and that the paper layout overflows
its frame rather than shrinking into it. `Assets.feature` checks what a browser cannot
show but a diff can hide: that Bootstrap is neither linked nor served, and that MudBlazor
is doing the styling.

The functional suite runs two scenarios at a time. Every scenario boots the WebAssembly runtime
in its own browser context and that is a whole-machine burst, so TUnit's default of running
everything at once left the machine unusable for the length of a run. Set
`WRAPCAL_SCENARIO_PARALLELISM=1` to serialise them when you need the machine back — it takes
about as long as the unbounded run did, at a fraction of the load.

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
