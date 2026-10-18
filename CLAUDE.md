# WrapCal

Conventions and hard-won gotchas for working on this repo. `README.md` says what the app is
and how to run it; this file says how to change it without breaking it.

## Project Context

WrapCal is a .NET 10 Blazor Web App that plans how much wrapping paper a set of gift
boxes needs — given box dimensions and a paper roll's width, it produces a cut plan:
where to cut the roll, how much length is consumed, and how much paper is wasted.

**The whole site is that calculator.** There is no navigation, no second feature, and none
planned — this is a single-purpose tool, not a platform. Keep it that way: resist adding
orthogonal features, accounts, saved plans, or a database.

## Tech Stack

- **.NET 10** / C# 14 — SDK `10.0.402`
- **Blazor Web App** — `InteractiveServer` is registered in `Program.cs`
  (`AddInteractiveServerRenderMode`) and declared **once**, on `<Routes>` in `App.razor`.
  Every route is interactive; a page must not add its own `@rendermode`, which cannot nest
  inside an interactive root.
- **MudBlazor 9.11** — the entire UI. Bootstrap was removed; there is one styling system and
  no second one to fight it. Its providers (`MudThemeProvider`, `MudPopoverProvider`,
  `MudDialogProvider`, `MudSnackbarProvider`) live in `MainLayout`, which is inside the
  interactive tree — a provider rendered statically outside it leaves `MudSelect` unable to
  open its overlay.
- **The theme has three choices and System is the default** — `ThemeChoice` in
  `Components/Layout`, picked from a `MudMenu` in the app bar. Only the browser can answer
  what the system is set to, and the prerender runs before any JavaScript it owns, so the
  prerender draws from `wrapcal-system-theme` (what the browser reported last time) and the
  circuit corrects it a frame after attaching. A first visit on a dark system therefore flashes
  light once; every visit after that is drawn right from the start. `MudThemeProvider` supplies
  the answer — `GetSystemDarkModeAsync` and `WatchSystemDarkModeAsync`, so a page left open
  follows the system as it changes.
- **No persistence** — the app is stateless. No database, no EF Core, no Identity.
  Wrap planning is a pure function of user input. The one exception is the theme, which
  travels in two cookies — `wrapcal-theme` (the choice) and `wrapcal-system-theme` (the last
  system preference the browser reported). Both are display preferences rather than
  application state.
- **No logging library** — the built-in `Microsoft.Extensions.Logging` from the
  template is all that is configured.
- **TUnit** + **bUnit** — unit and component testing
- **Reqnroll** (Gherkin BDD) + **Playwright** — functional tests against a real browser

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
        Error.razor
        NotFound.razor
      App.razor
      Routes.razor
      _Imports.razor
    Common/
      Result.cs                 # Result<T> — cross-cutting, outside any slice
    Features/                   # vertical slices
      _Imports.razor            # REQUIRED — Components/_Imports.razor does NOT reach here
      PlanWrap/
        PlanWrap.razor          # the routable page — "/"
        PlanWrap.razor.cs       # form state, field rules, unit conversion
        PlanWrapLogic.cs        # the planner → Result<WrapPlan>
        PlanWrapModels.cs       # domain records
        PlanWrapInput.cs        # form model + the field limits
        WrapPlanView.razor      # summary figures + cut sections table
        WrapPlanView.razor.css  # that table's styling
        WrapPlanDiagram.razor   # SVG cut layout — needs WrapPlan.RollWidth
        WrapPlanDiagram.razor.css
    Program.cs
    wwwroot/
      app.css                 # Roboto @font-face, framework furniture, message slot
      theme.js                # writes the theme cookie (the only JS we own)
tests/
  WrapCal.UnitTests/
    Common/ResultTests.cs
    Components/MainLayoutTests.cs
    Features/                   # one file per production concern
  WrapCal.FunctionalTests/      # Reqnroll + Playwright — separate project, own runner
    Features/                   # PlanWrap (behaviour), CutPlans (spec tables), Layout, Assets, Theme
    StepDefinitions/            # StepsBase + Form / Planning / Layout / Asset / Theme
    Hooks/
    Support/
```

## How the planner decides things

Read this before changing the maths. Every point here has been got wrong once already.

- **Overlap margin is the seam overlap, and each sheet dimension has exactly one seam.** It
  is added once to the wrap-around length (where the two ends of the wrap lap) and once to
  the along-the-box length (where the two folded end flaps lap). Not once per edge — that
  reads a typed 0.5 as an overlap of 1.0, and has been fixed twice.
- **Along-the-box length is `axis + max(p, q) + margin`.** The `max(p, q)` is the fold-over:
  half the larger cross-section side per end, enough for both flaps to meet in the middle of
  the end face. Without the margin they meet exactly and never lap.
- **The note names the box sides the paper goes around**, e.g. `Wrapped around the box's
  4.0 × 22.0 cm sides.` It deliberately never mentions the roll: by the time anyone wraps,
  the sheet is cut off it and the roll is away.
- **`WrapPlan` carries `RollWidth`, not just the length used** — the diagram has no canvas
  without it.
- **Changing the unit converts what is already typed**, rounded to 2 decimals, and discards
  any displayed plan, which is stated in the unit just left.
- **Any change to the form discards the displayed plan**, not just a unit change: editing a
  field, adding a box, or removing one. A plan is only ever on screen while it still describes
  the inputs in front of the reader. `DiscardPlan` is the single place that clears it, wired to
  every field with `@bind-Value:after`.
- **A plan holds at most 100 boxes**, `PlanWrapInput.MaxBoxCount`, enforced twice: the Add
  button disables at the limit, and `PlanWrapLogic` refuses more for callers that never saw the
  form. Packing is quadratic in the box count and runs on the circuit's thread, so an uncapped
  list is a way to freeze the page that is drawing it.
- **Packing is a greedy shelf fit plus randomised restarts**, seeded, so a given input always
  produces the same plan. `PlanWrapLogicTests` holds a golden test pinning the output — read
  its comment before touching `Pack` or `SheetOptions`.
- **The restart count is capped by a work budget, not just by `RandomRestarts`.**
  `RestartWorkBudget / boxes.Count` bounds the whole run; a short list still gets every restart
  it asked for, a long one trades restarts for staying responsive. Do not "fix" it back to a
  flat count — 2000 boxes at 500 restarts measured 25 seconds of blocked circuit.
- **Numbers the reader sees follow their culture; numbers in SVG attributes do not.**
  `WrapPlanView.Format`, `WrapPlanDiagram.F` and the note use `CurrentCulture`, matching the
  form's numeric fields. `WrapPlanDiagram.N` stays invariant — it writes `viewBox` and
  coordinates, which only parse with a decimal point. Tests that assert the wording of a note
  pin the culture for exactly this reason.

### Slice conventions

- **A slice is self-contained.** If two slices ever need the same thing, promote it to a
  small shared type — or a `Components/Shared/` folder for UI, which does not exist yet
  because there is only one slice. Don't reach across slices.
- **Keep `@code` blocks small.** Past ~30 lines, extract to a code-behind
  (`.razor.cs`) or a slice logic class.
- **Shared state goes in a scoped DI service**, not cascading parameters.
- **Render mode is declared once, at the root** — see the tech-stack note above. Do not add
  `@rendermode` to a page or layout; it cannot nest inside the interactive root.
- **`Features/_Imports.razor` is load-bearing.** `_Imports.razor` only applies to its own
  folder and below, and the one in `Components/` is a sibling — without the one in
  `Features/`, `EditForm`, `InputText`, `PageTitle` and the render-mode directive are all
  unresolved in slices.
- **Never name a component after a domain record.** Component names derive from the file,
  so `WrapPlan.razor` beside the `WrapPlan` record collides. Hence `WrapPlanView.razor`.
- **Controls are disabled until the circuit is live.** `Plan`, `Add box` and `Remove` render
  disabled while `RendererInfo.IsInteractive` is false, then enable. That is a user-facing
  guarantee, not a test hook: before the circuit attaches a click would either vanish or
  POST the form and navigate away. Read `RendererInfo` during render rather than latching it
  in `OnAfterRenderAsync` — the prerendered markup is replaced by the interactive render,
  which already knows the answer. The suite waits for
  `[data-testid='plan-button']:not([disabled])`.
- **Field rules live in the page's MudForm validation funcs, and only there.** Each field
  carries a `For` expression plus a `Validation` delegate; returning an empty string means
  valid. MudBlazor declares `Validation` as `object`, so the delegate type has to come from a
  typed property on the code-behind — a method group or lambda in markup cannot be inferred.
  The per-field bounds are `const`s on `PlanWrapInput` and are not duplicated anywhere: there is
  deliberately no second copy to drift. `PlanWrapLogic` keeps its own guards for callers that
  bypass the form, and where it needs a limit the form also applies — the box count — it reads
  the same `const` rather than restating the number.
- **Validation reports when a field is left, not on every keystroke.** That is Mud's timing,
  chosen over the old on-change behaviour. Box fields carry `HelperText=" "` and the global
  `.wrapcal-message-slot` rule so the message line is always reserved — without it, reporting a
  problem grows the row and pushes the Plan button down. That rule is global because the class
  lands on markup MudBlazor renders, which a component's scoped CSS cannot reach.
- **Every interactive control carries a `data-testid`**: `roll-width`, `overlap-margin`,
  `roll-options`, `unit-select`, `boxes`, `box-row`, `box-name`, `box-length`, `box-width`,
  `box-height`, `add-box`, `remove-box`, `plan-button`, `plan-error`, `roll-direction`,
  `diagram`, `sections`, `roll-length`, `paper-area`, `waste-area`, `efficiency`,
  `theme-menu`, `theme-system`, `theme-light`, `theme-dark`. MudBlazor puts unmatched attributes
  on the element it renders — for an input that is the `<input>` itself, but for `MudSelect` it
  is a hidden input that cannot be clicked, so the two selects are wrapped in a `<div>` carrying
  the id, and `MudMenu` is wrapped for the same reason (its activator is the button *inside* the
  wrapper, which is what a click has to hit). The functional suite drives the page through
  these, so renaming one breaks tests rather than the app.
- **The diagram never scales to fit.** Its scale comes from the roll width, which does not
  change as boxes are added, so a longer plan draws a longer diagram and the rest is reached
  by scrolling — the scroll axis follows the roll (`is-vertical` / `is-horizontal` on
  `.diagram-scroller`). Scaling the whole plan into the figure is what used to make the
  drawing shrink with every box, and `Layout.feature` measures the overflow to catch its
  return.
- **The roll never touches the diagram's frame.** A gutter of 15% of the roll width frames the
  paper on whichever axis the roll spans — the margins on the other axis already carry the two
  end annotations. Without it the drawing read as zoomed in too far, since the paper sat flush
  against the border. The number is the `Gutter` constant in `WrapPlanDiagram.razor`, tuned by
  eye rather than derived, so change it there and nowhere else.
- **The scroller opens at the origin, with no script.** It is a flex container, and
  `is-reversed` — set for Up and Left, whose origin is at the far end — lays it out from that
  end (`column-reverse` / `row-reverse`), which puts the initial scroll there too. Verified
  in the browser and mutation-checked: forcing `column`/`row` fails the Up and Left examples.
- **The drawing fills the frame and its content hugs the top-left.** The SVG takes
  `min-width`/`min-height: 100%` of the scroller and `preserveAspectRatio="xMinYMin meet"`, so a
  short plan has no dead space beside it for a reversed scroller to pack it against. Without this
  a Left or Up diagram that fits sat against the far edge — right or bottom — with a large empty
  gap, which reads as the drawing being shoved out of place. Do not "restore" `xMidYMid`: that
  re-centres the content in the filled frame. `justify-content: flex-end` and auto margins were
  both tried first and both push overflowing content towards a start the scrollbar cannot reach,
  taking the origin out of view; `safe flex-end` is the right idea but WebKit does not honour it.
- **Re-keying the scroller resets the scroll.** It is keyed on `(Plan, Direction)`, so a new
  plan or a new direction starts at the origin instead of wherever the previous drawing was
  left. Don't "optimise" the key away.
- **The origin dot moves with the drawing.** It sits on the corner where plan X and plan Y
  are both zero: top-left going Down, bottom-left going Up and Right, bottom-right going
  Left. The horizontal layouts also flip X downwards so that corner really is the origin.
- **The diagram transposes for Left/Right, it does not rotate.** A section at `(X, Y)`
  drawn rightwards lands at `(Y, X)` with width and length swapped — a mirror across the
  roll-width axis, not a 90° rotation (which would be `(Y, RollWidth - X - W)`). This is
  deliberate: a true rotation would turn the box labels with the drawing, and Up is 180°,
  so labels would render upside-down. Keeping text upright in all four directions is worth
  the transpose. The plan itself is identical either way.

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
# Build
dotnet build

# Run (hot reload)
dotnet watch --project src/WrapCal

# Run
dotnet run --project src/WrapCal

# Test everything (unit + component + functional)
dotnet test

# Test one project at a time — the normal way to work, since the functional
# suite starts a server and a browser and takes seconds rather than milliseconds
dotnet test --project tests/WrapCal.UnitTests
dotnet test --project tests/WrapCal.FunctionalTests

# Format
dotnet format

# Format check (CI)
dotnet format --verify-no-changes
```

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

- **TUnit** (`1.75.0`); **bUnit** (`2.11.3`) for component rendering.
- bUnit v2's context class is **`BunitContext`** — inherit and call `Render<T>()`.
  (It still exposes `TestContext`, but `BunitContext` avoids a clash with TUnit's own.)
- TUnit auto-configures global usings, so `[Test]` and `Assert.That(...)` need no `using`.
- TUnit assertions are **awaited**: `await Assert.That(cut.Markup).Contains("WrapCal");`
- **`.razor` test files require TUnit's reflection mode.** In the default
  source-generation mode, TUnit and the Razor SDK are both source generators and neither
  can see the other's output, so Razor-generated test classes are invisible to discovery.
  Adding `[assembly: ReflectionMode]` (`TUnit.Core`) — or `TUNIT_EXECUTION_MODE=reflection`
  — makes them discoverable; that is the documented purpose of reflection mode, and it is
  what bUnit's own docs omit when they say TUnit "does not work with razor files".
  **This project stays on the default mode:** our tests are `.cs`, which source generation
  handles, and reflection mode is the slower of the two. Reach for it only to author
  `.razor` test components, which additionally needs `Microsoft.NET.Sdk.Razor`.
  (If you go fully reflection, `<EnableTUnitSourceGeneration>false</EnableTUnitSourceGeneration>`
  skips generation entirely for faster builds.)
- Test files mirror production: `Common/ResultTests.cs`, `Components/MainLayoutTests.cs`,
  `Features/*Tests.cs`.
- Component tests that render an interactive page must call `SetRendererInfo` in the
  constructor, or reading `RendererInfo` throws `MissingRendererInfoException`.

### Functional — `tests/WrapCal.FunctionalTests`

Reqnroll (Gherkin) driving Playwright against the app running for real.

- `Features/*.feature` bind to step definitions in `StepDefinitions/`, split by concern:
  `StepsBase` (shared page helpers), `FormSteps` (setting the form up), `PlanningSteps`
  (planning and what the plan says), `LayoutSteps` (geometry measured in the browser),
  `ThemeSteps` (the cookie and what the page does with it).
- **The theme is split between the two suites, and only `Theme.feature` can test System.**
  `MainLayoutTests` covers which of the three choices is in force — it arranges the browser's
  answer by stubbing `mudThemeProvider.isDarkMode`, since bUnit has no media query and no
  operating system. `Theme.feature` covers what only a real browser can: `EmulateMediaAsync`
  sets `prefers-color-scheme`, and the palette is read off the document root (where a theme is
  visible at all) and compared by luminance rather than against exact colours.
  Mutation-checked: forcing the system preference to light fails "The page follows a dark
  system" and nothing else.
- **The `Then` for a theme waits, and has to.** On a first visit the system preference is not
  knowable until the circuit attaches and can ask the browser, so the palette moves *after* the
  page is interactive. The step waits up to `ThemeSettleTimeout` for the expected side of
  mid-grey and only then reports. A theme assertion that reads once will flake.
- Note what the scenarios do *not* prove: a circuit's first render still sees an HttpContext
  (the SignalR connection's request, cookies included), so the cookie alone also keeps the
  theme right. They are blind to which path delivered it, and removing either leaves them green.
- **Every concrete step class needs `[Binding]`.** Without it Reqnroll finds no steps at all
  and every scenario fails as "undefined", naming no file — not an obvious error to trace
  back to a new class.
- **Scenarios state their own inputs** — roll width, unit, overlap, and every box by name and
  size. None rely on the page's defaults, so a reader can check the expected result without
  knowing them.
- Generated `*.feature.cs` goes to `obj/`, never source control
  (`ReqnrollUseIntermediateOutputPathForCodeBehind`).
- **Reqnroll maps `@tags` to TUnit `[Category]` attributes automatically** — verified in
  the generated code, despite Reqnroll's docs only documenting NUnit/MSTest/xUnit.
- `Support/AppHost.cs` runs the real app as a child process on a free port. Playwright
  drives a real socket, so an in-memory `TestServer` is impossible, and Blazor Server
  needs a real SignalR circuit. `--no-launch-profile` keeps `launchSettings.json` from
  injecting an HTTPS port; with no HTTPS endpoint the `UseHttpsRedirection()` in
  `Program.cs` is a no-op.
- `Hooks/TestRunHooks.cs` starts the app and browser once per run.
  `Hooks/ScenarioHooks.cs` gives each scenario a fresh context and screenshots failures
  to `bin/.../playwright-artifacts/`.
- Steps receive browser state by Reqnroll context injection: `[BeforeScenario]` puts a
  `UiContext` into `ScenarioContext`; step constructors resolve it.

#### Browser selection

Override with `BROWSER=webkit|firefox|chromium`; `HEADED=1` makes the run visible.

| OS | Default | Why |
|----|---------|-----|
| macOS | `webkit` | Firefox cannot launch on macOS 27 — see below |
| Linux / Windows | `firefox` | |

#### Installing browsers without PowerShell

Playwright .NET ships its installer as `playwright.ps1`, which needs PowerShell — not
available here. That script's entire job is to load `Microsoft.Playwright.dll` and call
`Microsoft.Playwright.Program.Main(args)`, so invoke the DLL directly:

```bash
# Requires a prior `dotnet build` — the DLL lives in the build output.
BIN=tests/WrapCal.FunctionalTests/bin/Debug/net10.0
dotnet exec --runtimeconfig "$BIN/WrapCal.FunctionalTests.runtimeconfig.json" \
  "$BIN/Microsoft.Playwright.dll" install webkit firefox
```

Platform-agnostic — no RID-specific paths. Two dead ends worth not repeating:

- The `Microsoft.Playwright.CLI` global tool fails here with *"Couldn't find project
  using Playwright"*, even with `-p` pointing at the project.
- Reaching past `Program.Main` to the bundled Node driver (`$BIN/.playwright/node/...`)
  also works, but only if you hardcode the RID directory (`darwin-arm64`), so it breaks
  on Intel macOS. Prefer the form above.

### Known blocker: Firefox on macOS 27

Firefox **cannot start** on macOS 27, with or without Playwright. The macOS sandbox denies
Firefox's `plugin-container` a sandbox extension, the child is killed on signal 9, and
startup never completes — surfacing as `Could not find profile folder`, which is a red
herring (the profile directory *is* created). Confirmed against stock Firefox Nightly from
mozilla.org with Playwright uninvolved; microsoft/playwright#42082 was closed as *not
planned*, being upstream in Firefox. Chromium and WebKit are unaffected — hence the macOS
default above. Firefox is already the non-macOS default, so nothing needs changing when
this is fixed.

## MCP Tools

`cwm-roslyn-navigator` is installed globally (`dotnet tool list -g` → `cwm.roslynnavigator
0.8.0`) and registered at **user scope by the dotnet-claude-kit plugin**. There is
deliberately **no project-level `.mcp.json`** — adding one registers a second, identical
server that needs its own approval and then shows up as a duplicate.

Solution discovery is automatic: the server scans the working directory (3 levels deep,
skipping `bin`, `obj`, `.git`), so **no `--solution` argument is needed**. Verified working
via its startup log (`Discovered solution: .../WrapCal.slnx`).

Use it to save tokens:

- **Before modifying a type** — `find_symbol` to locate, `get_public_api` for its surface
- **Before adding a reference** — `find_references` for existing usage
- **Interface implementations** — `find_implementations`, not grep
- **After changes** — `get_diagnostics`

### Known limitation: it cannot see Razor-generated types

The workspace loads through `MSBuildWorkspace`, which **does not execute source
generators**. Razor compiles components with the Razor source generator (there are no
`.razor.g.cs` files on disk), so `App`, `Routes`, `PlanWrap`, `WrapPlanView`, and the
`WrapCal.Components` / `WrapCal.Features.PlanWrap` namespaces **do not exist in the
tool's view**. This is expected, not broken config:

- `get_diagnostics` reports phantom `CS0246`/`CS0234` errors for every reference to a
  Razor-generated type — `Program.cs`, and any test that renders a component.
  **`dotnet build` is the source of truth** — it reports 0 errors. Ignore these and
  never "fix" them. (Do not cite specific line numbers here; they drift constantly.)
- Any test that references a component inherits the same phantom errors. Unavoidable,
  and not a defect in the test.
- `find_symbol`, `find_references`, and `get_public_api` return nothing for Razor
  components. Use `grep` or read the `.razor` file directly.
- `get_test_coverage_map` counts only non-Razor types, so its percentage is meaningless
  for this project — treat that dimension as *Not assessed*. It **does** correctly return
  `applicable: false` with a `notApplicableReason`, which is what makes it safely
  ignorable (verified after a clean restart; see below).

### Corrected: the `obj/` "leak" was stale-workspace noise, not a detector bug

An earlier revision of this file claimed the detectors reported generated files under
`obj/`, citing `AP002` in `tests/WrapCal.Tests/obj/.../XunitAutoGeneratedEntryPoint.cs`
and `ValidatableTypeAttribute` in `src/WrapCal/obj/`.

**That was wrong.** Both cases came from a workspace that had never reloaded after the
xUnit → TUnit migration and the project rename. On the same commit, after a restart:

- `detect_antipatterns` → 0 violations, correctly classifying
  `ScannedFiles: 41, ProductionFiles: 5, TestFiles: 7, GeneratedFiles: 29`
- `find_dead_code` → 0 symbols, `ConventionFiltered: 0`
- `get_test_coverage_map` → no `obj/` artifacts at all

**`find_dead_code` now returns hits, and they are all the same false positive.** Once the
pages and components grew members used only from markup — `AddBox`, `RemoveBox`,
`ToggleThemeAsync`, the `*Validation` properties — it reports them as unreferenced, with
`Confidence: high`, because a `.razor` file is not in the tool's view (see the limitation
below, which is the same cause). Every one of them is called from markup. Ignore the list,
and do not "clean up" a member on its say-so: grep the `.razor` file first.

Generated code **is** filtered, as the tool's README promises. The general lesson: a stale
workspace does not merely return old data — it returns materially *wrong* data, including
files and projects that no longer exist, and silently drops response fields like `Summary`
and `Applicable`. Verify the workspace is fresh before concluding a tool is broken.

### Workspace freshness

The workspace loads **once at server start**. After adding or removing a *project*, or
making large structural changes, restart the session so it reloads the solution.
Otherwise tools silently report a stale project list.

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

- **Uses `DateTime.Now` / `DateTime.UtcNow`** — inject `TimeProvider` instead
- **Creates `new HttpClient()`** — use `IHttpClientFactory` or a typed client
- **Uses `async void`** — return `Task`; the only exception is component lifecycle
  event handlers where the framework requires it
- **Blocks on `.Result` or `.Wait()`** — await instead
- **Uses `JSRuntime` where CSS or Blazor suffices** — prefer `@bind`,
  `NavigationManager`, `FocusAsync()`
- **Mixes render modes carelessly** — Interactive Server and WASM components can't
  share state via DI; data crosses the boundary via parameters or an API
- **Puts heavy work in `OnInitializedAsync`** — blocks rendering; stream with
  `[StreamRendering]` or load progressively
- **Calls `StateHasChanged()` liberally** — the framework already calls it after event
  handlers and lifecycle methods
- **Uses `[CascadingParameter]` as general state management** — cascading values
  re-render the whole subtree; use a scoped DI service
- **Catches bare `Exception`** — catch specific types; use `ErrorBoundary` for UI-level
  error handling
- **Interpolates strings into log messages** — use structured logging templates
- **Reintroduces `Microsoft.NET.Test.Sdk`** — this repo runs MTP mode (see above)

## Skills

Load these dotnet-claude-kit skills for context:

- `modern-csharp` — C# 14 features and idioms
- `architecture-advisor` — structuring the app
- `testing` — TUnit, bUnit component testing (the kit's own copy says xUnit; this repo uses TUnit)
- `configuration` — options pattern, secrets
- `dependency-injection` — lifetimes and registration
- `logging` — structured logging, OpenTelemetry
- `error-handling` — Result pattern, error boundaries
- `workflow-mastery` — parallel worktrees, verification loops, subagents
- `wrap-up` — session handoff to `.claude/handoff.md`
