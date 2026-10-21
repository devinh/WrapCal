# WrapCal

Conventions and hard-won gotchas for working on this repo. `README.md` says what the app is and
how to run it; this file says how to change it without breaking it.

The calculator's own knowledge — the maths, the packing search, the diagram and zoom — lives
with the code it describes, in
[`src/WrapCal/Features/PlanWrap/README.md`](src/WrapCal/Features/PlanWrap/README.md). **Read that
before touching the planner or the drawing.** The area-specific rules for the test suites, the
theme and the build live in the project skills under `.claude/skills/`; this file keeps what has
to be true before you know which of them you are in.

## Project Context

WrapCal is a .NET 10 Blazor WebAssembly app that plans how much wrapping paper a set of gift
boxes needs — given box dimensions and a paper roll's width, it produces a cut plan:
where to cut the roll, how much length is consumed, and how much paper is wasted.

**The whole site is that calculator.** There is no navigation, no second feature, and none
planned — this is a single-purpose tool, not a platform. Keep it that way: resist adding
orthogonal features, accounts, saved plans, or a database.

## Tech Stack

- **.NET 10** / C# 14 — SDK `10.0.402`
- **Standalone Blazor WebAssembly** — `Microsoft.NET.Sdk.BlazorWebAssembly`, with
  `WebAssemblyHostBuilder` in `Program.cs` mounting `Routes` on `#app` and a `HeadOutlet` on
  `head::after`. There is **no server and no prerender**: the app is a folder of static files
  that a browser downloads and runs. `dotnet publish` is the deliverable; `dotnet run` exists
  only because `WebAssembly.DevServer` serves those files locally.
- **Everything is interactive, always.** There is no render-mode directive anywhere, and
  `@rendermode` should never appear — a second render mode has nothing to mean here.
- **Hosted as a GitHub project page**, `https://devinh.github.io/WrapCal/`, deployed by
  `.github/workflows/deploy-pages.yml` on every push to `main`. The site therefore lives under
  a subpath rather than at an origin root, so `wwwroot/index.html` raises its own `<base href>`
  to the directory it was loaded from. That script is the one piece of the deployment that is
  app code, and it has to run **before** the links and scripts below it — a relative URL is
  resolved against the base in force when its element is parsed, not when it is fetched.
- **MudBlazor 9.11** — the entire UI, and the only styling system. Its providers
  (`MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`) live in
  `MainLayout`, which wraps the whole page — a `MudSelect` whose popover provider is not an
  ancestor reports a missing `MudPopoverProvider` and cannot open.
- **The theme has three choices, System is the default, and `theme.js` is the only owner of the
  cookies** — Light, Dark or System, picked from the app bar, with the shell painted from the
  same cookies before Blazor boots so a returning visit never flashes. Nothing in C# touches
  `document.cookie`. The boot script deliberately ignores `prefers-color-scheme`; the `theme`
  skill says why, and what it costs.
- **No persistence** — the app is stateless. No database, no EF Core, no Identity.
  Wrap planning is a pure function of user input. The only exception is those two theme
  cookies, which are display preferences rather than application state.
- **No logging library and no configuration** — there is nowhere for a log to go in a
  browser tab, and the app reads no settings.
- **TUnit** + **bUnit** for unit and component tests; **Reqnroll** (Gherkin BDD) + **Playwright**
  for functional tests against a real browser. The runner is Microsoft.Testing.Platform — see
  the test runner note below.

## Architecture

**Vertical Slice Architecture.** Organize by feature, not by technical layer. A feature
owns its page, its logic, and its models in one folder. There is no shared
`Services/` or `Models/` layer — that split is deliberately avoided.

```
src/
  WrapCal/                      # the only production project
    Components/
      Layout/                   # MainLayout — app bar + content, deliberately no nav
      Pages/
        NotFound.razor
      Routes.razor
      _Imports.razor
    Common/
      Result.cs                 # Result<T> — cross-cutting, outside any slice
    Features/                   # vertical slices
      _Imports.razor            # REQUIRED — Components/_Imports.razor does NOT reach here
      PlanWrap/                 # the calculator — see its README.md for how it decides things
        PlanWrap.razor          # the routable page — "/"
        PlanWrap.razor.cs       # form state, field rules, unit conversion
        PlanWrapLogic.cs        # the planner → Result<WrapPlan>
        ShelfPacker.cs          # shelf packing + the search over placements
        ShelfOptimum.cs         # the exact layout for small lists, and re-partitioning for long ones
        PlanWrapModels.cs       # domain records
        PlanWrapInput.cs        # form model + the field limits
        RollDirectionText.cs    # which corner the origin is, in words
        WrapPlanView.razor      # summary figures + cut sections table
        WrapPlanView.razor.css
        WrapPlanDiagram.razor   # SVG cut layout — needs WrapPlan.RollWidth
        WrapPlanDiagram.razor.css
    Program.cs
    wwwroot/
      index.html              # the document — base href, asset links, the boot scripts
      app.css                 # Roboto @font-face, framework furniture, message slot
      theme.js                # the theme cookies
      diagram.js              # wheel and touch, which Blazor cannot preventDefault on
tests/
  WrapCal.UnitTests/
    Common/ResultTests.cs
    Components/MainLayoutTests.cs
    Features/                   # one file per production concern
  WrapCal.FunctionalTests/      # Reqnroll + Playwright — separate project, own runner
    Features/                   # PlanWrap (behavior), CutPlans (spec tables), Layout, Assets, Theme
    StepDefinitions/            # StepsBase + Form / Planning / Layout / Asset / Theme
    Hooks/
    Support/
```

`PlanWrapLogic` takes a `PlanWrapInput` and returns a `Result<WrapPlan>`. Nothing outside the
slice knows how the packing works, and nothing in it knows how the page looks — the page, the
logic and the models are one unit, and they change together.

### Slice conventions

- **A slice is self-contained.** If two slices ever need the same thing, promote it to a
  small shared type — or a `Components/Shared/` folder for UI, which does not exist yet
  because there is only one slice. Don't reach across slices.
- **Keep `@code` blocks small.** Past ~30 lines, extract to a code-behind
  (`.razor.cs`) or a slice logic class.
- **Shared state goes in a scoped DI service**, not cascading parameters.
- **`Features/_Imports.razor` is load-bearing.** `_Imports.razor` only applies to its own
  folder and below, and the one in `Components/` is a sibling — without the one in
  `Features/`, `EditForm`, `InputText` and `PageTitle` are unresolved in slices.
- **Never name a component after a domain record.** Component names derive from the file,
  so `WrapPlan.razor` beside the `WrapPlan` record collides. Hence `WrapPlanView.razor`.
- **There is no "until the circuit is live" gate, and adding one would be dead code.** The
  page does not exist until the WebAssembly runtime has booted, so a click cannot arrive
  early — there is nothing to click. `RendererInfo.IsInteractive` is always true here and
  reading it says nothing. The functional suite still waits for
  `[data-testid='plan-button']:not([disabled])`, which means "the app has rendered" and is
  the same signal after a reload as after a first navigation.

## Coding Standards

- **C# 14** — primary constructors, collection expressions, records, pattern matching
- **File-scoped namespaces** — always
- **`var`** — for obvious types only; explicit types when the type isn't apparent
- **Naming** — PascalCase public members, `_camelCase` private fields, `Async` suffix
  on async methods
- **No regions** — ever
- **Comments explain "why", never "what"** — prefer no comment over an obvious one
- **One component per file**, except tiny render fragments
- **`@inject` at the top of `.razor` files**, one per line; `@code` at the bottom
- **`EventCallback<T>` over `Action<T>`** for component events

These are backed by `.editorconfig` and enforced during build via
`EnforceCodeStyleInBuild`.

## Commands

```bash
dotnet build                         # build the app and the tests
dotnet run --project src/WrapCal     # run it
dotnet watch --project src/WrapCal   # run with hot reload
dotnet format                        # format
dotnet format --verify-no-changes    # what CI checks

dotnet test                                        # everything
dotnet test --project tests/WrapCal.UnitTests      # the suite to work against
dotnet test --project tests/WrapCal.FunctionalTests

./cake.sh                            # Clean → Build → Tests (the default target)
./cake.sh --target=UnitTests
./cake.sh --target=FunctionalTests
./cake.sh --target=Publish           # the site, into ./output
./cake.sh --target=Release           # every suite, then publish — what CI runs
./cake.sh --target=InstallBrowsers
./cake.sh --target=UnitTests --coverage=true  # + Cobertura and the HTML coverage report
```

The functional suite starts a browser and boots WebAssembly per scenario, so it takes seconds
rather than milliseconds — run the unit suite while working, both before calling it done.

**Reports live in `./output/reports`**, written by Cake on every test run: TUnit's HTML report and
TRX per suite, plus `coverage/` when `--coverage=true` is passed. Coverage is opt-in because
instrumenting costs a couple of seconds against a suite that runs in under one, and only the unit
suite is instrumented — the functional suite's code under test is WebAssembly in a browser
process. ReportGenerator is installed by `InstallTool` in `cake/cake.cs`, so running the build is
the only step a fresh checkout needs and no tool manifest is kept in step. Both workflows upload
`output/reports`; `ci.yml` also posts the summary as a PR comment that each push edits in place.
Nothing here is published to Pages — `deploy-pages.yml` uploads `output/wwwroot` only.

Cake is the definition of what a build is: the workflows call it and nothing else, `Release`
gates the deploy on every suite, and the `cake/` project is in the solution but marked
`<Build Project="false" />` so a running build script is never rebuilt underneath itself. All
of that, and the hosting setup it publishes to, is in the `build-and-deploy` skill.

### Test runner note

`global.json` opts `dotnet test` into .NET 10's **MTP mode**
(`"test": { "runner": "Microsoft.Testing.Platform" }`). Consequences:

- TUnit test projects are **executables** (`<OutputType>Exe</OutputType>`).
- `dotnet test --filter` does **not** work — TUnit runs on MTP, not VSTest, and the flag
  is silently rejected (the run exits with "Zero tests ran"). Use `--treenode-filter`
  instead, e.g. `--treenode-filter "/*/*/*/*[Category=Smoke]"`.
- `Microsoft.NET.Test.Sdk` and `TestingPlatformDotnetTestSupport` are **not** used —
  don't add them back.
- Arguments for the test app go after `--`, e.g. `dotnet test -- --report-trx`.
- VSTest-style `--logger` options are ignored.

## Testing

Two test projects, deliberately separate. Run either on its own, or `dotnet test` for both.

### Unit + component — `tests/WrapCal.UnitTests`

TUnit with bUnit for rendering. The three things that bite: the context class is `BunitContext`
(not bUnit's `TestContext`, which clashes with TUnit's), assertions are awaited, and `.razor`
test files are invisible to discovery unless TUnit runs in reflection mode — which this project
deliberately does not. The `unit-tests` skill has the details.

### Functional — `tests/WrapCal.FunctionalTests`

Reqnroll driving Playwright against the published site served from `/WrapCal`. The `functional-tests`
skill covers the step-definition layout, the hosting, the parallelism limiter, and the assertions
that would otherwise pass without testing anything — read it before touching a `.feature` file.
Browser selection and the Firefox-on-macOS-27 blocker are in the root `README.md`.

## MCP Tools

`cwm-roslyn-navigator` is installed globally and registered at **user scope by the
dotnet-claude-kit plugin**. There is deliberately **no project-level `.mcp.json`** — adding one
registers a second, identical server that needs its own approval and then shows up as a
duplicate. Solution discovery is automatic (the server scans the working directory, skipping
`bin`, `obj`, `.git`), so it needs no `--solution` argument.

Use it to save tokens: `find_symbol` to locate a type, `get_public_api` for its surface,
`find_references` before adding one, `find_implementations` instead of grep, `get_diagnostics`
after a change.

**It cannot see Razor-generated types.** The workspace loads through `MSBuildWorkspace`, which
does not execute source generators, and Razor compiles components with one — so `App`, `Routes`,
`PlanWrap`, `WrapPlanView` and their namespaces do not exist in the tool's view.

- `get_diagnostics` reports phantom `CS0246`/`CS0234` errors for every reference to a
  Razor-generated type, in `Program.cs` and in any test that renders a component. **`dotnet
  build` is the source of truth** — it reports 0 errors. Ignore these and never "fix" them.
- `find_symbol`, `find_references` and `get_public_api` return nothing for Razor components. Use
  grep or read the `.razor` file.
- `find_dead_code` reports members called only from markup — `AddBox`, `RemoveBox`,
  `ToggleThemeAsync`, the `*Validation` properties — as unreferenced with `Confidence: high`, for
  the same reason. Every one of them is called from a `.razor` file. Grep before deleting.
- `get_test_coverage_map` counts only non-Razor types, so its percentage is meaningless here. It
  returns `applicable: false` with a reason, which is what makes that safely ignorable.

The workspace loads **once at server start**. After adding or removing a *project*, or making a
large structural change, restart the session — otherwise tools silently report a stale project
list, which is worse than reporting nothing: it comes back with files and projects that no longer
exist, and silently drops response fields.

## Workflow

- **Plan first** — use plan mode for anything non-trivial (3+ steps or an architecture
  decision).
- **Verify before done** — run `dotnet build` and `dotnet test` after changes. The
  build is currently clean at 0 warnings; keep it that way.
- **Fix bugs autonomously** — investigate, reproduce, fix, verify.
- **Stop and re-plan** if the approach goes sideways rather than pushing through.
- **Use subagents** for research and parallel exploration — one focused task each.

## Anti-patterns

Do NOT generate code that:

- **Adds `@rendermode`** — there is one render mode and it is the whole app; the attribute
  has nothing left to select
- **Puts heavy work in `OnInitializedAsync`** — blocks the first paint, and here that is the
  only paint there is
- **Calls `StateHasChanged()` liberally** — the framework already calls it after event
  handlers and lifecycle methods
- **Uses `[CascadingParameter]` as general state management** — cascading values
  re-render the whole subtree; use a scoped DI service
- **Uses `JSRuntime` where CSS or Blazor suffices** — prefer `@bind`,
  `NavigationManager`, `FocusAsync()`
- **Uses `async void`** — return `Task`; the only exception is component lifecycle event
  handlers where the framework requires it
- **Blocks on `.Result` or `.Wait()`** — await instead
- **Catches bare `Exception`** — catch specific types; use `ErrorBoundary` for UI-level
  error handling
- **Reintroduces `Microsoft.NET.Test.Sdk`** — this repo runs MTP mode (see above)

Two general rules that have no code to point at yet, because this app has neither: inject
`TimeProvider` rather than reading `DateTime.Now` / `DateTime.UtcNow`, and never construct an
`HttpClient` by hand — the app makes no outbound calls at all, so a new one means something has
gone wrong.

## Skills

Four project skills in `.claude/skills/` carry the detail this file used to repeat. Each is
loaded when its description matches the task, so the one-line summary above each pointer is what
has to be true for it to be found.

| Skill | Read it when |
|---|---|
| `functional-tests` | touching `tests/WrapCal.FunctionalTests` — Reqnroll, Playwright, AppHost, assertions |
| `unit-tests` | touching `tests/WrapCal.UnitTests` — TUnit, bUnit, the `.razor` reflection trap |
| `theme` | touching the theme — cookies, boot script, `theme.js`, what only a browser can test |
| `build-and-deploy` | touching Cake, the workflows, the solution, or hosting — including what ships |

Load these dotnet-claude-kit skills for general .NET context:

- `modern-csharp` — C# 14 features and idioms
- `architecture-advisor` — structuring the app
- `testing` — TUnit, bUnit component testing (the kit's own copy says xUnit; this repo uses TUnit)
- `dependency-injection` — lifetimes and registration
- `error-handling` — Result pattern, error boundaries
- `workflow-mastery` — parallel worktrees, verification loops, subagents
- `wrap-up` — session handoff to `.claude/handoff.md`
