# WrapCal

Conventions and hard-won gotchas for working on this repo. `README.md` says what the app is
and how to run it; this file says how to change it without breaking it.

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
  Deploying through Actions skips Jekyll, which is why `_framework/` needs no `.nojekyll`;
  switching to a branch-based deployment would need one.
- **Three workflows.** `deploy-pages.yml` on push to `main` runs `--target=Release` and uploads
  `output/wwwroot`; `ci.yml` on pull requests runs `--target=Tests`, and is what a Renovate PR
  waits on before it can merge itself; `renovate.yaml` runs daily as the `devinh-renovate` App.
  All three call Cake rather than `dotnet` — a workflow that rebuilds the way it remembers is a
  second build definition that drifts from the first.
- **MudBlazor 9.11** — the entire UI. Bootstrap was removed; there is one styling system and
  no second one to fight it. Its providers (`MudThemeProvider`, `MudPopoverProvider`,
  `MudDialogProvider`, `MudSnackbarProvider`) live in `MainLayout`, which wraps the whole page
  — a `MudSelect` whose popover provider is not an ancestor reports a missing
  `MudPopoverProvider` and cannot open.
- **The theme has three choices and System is the default** — `ThemeChoice` in
  `Components/Layout`, picked from a `MudMenu` in the app bar. Only the browser can answer
  what the system is set to. `MainLayout.OnInitializedAsync` asks `theme.js` for the two
  cookies through a single `wrapcalTheme.load` call, so the **first rendered frame already
  knows the choice** — no flash on a returning visit. A first visit in System mode still
  flashes light once, because no cookie exists yet and `MudThemeProvider` cannot ask the
  browser until it has rendered. `GetSystemDarkModeAsync` and `WatchSystemDarkModeAsync` then
  keep a page left open following the system as it changes.
- **The shell before the app boots is themed too.** `index.html` reads the same two cookies and
  sets `data-boot-theme` on `<html>`; `app.css` paints that from `--mud-palette-background` with
  a fallback of MudBlazor's default (#ffffff / #32333d). Using the palette variable first means
  the rule keeps following the theme once the app is up, including a runtime switch, instead of
  going stale. The boot script **mirrors `MainLayout` and deliberately ignores
  `prefers-color-scheme`**: on a first visit the app has no remembered system preference and
  draws light, so a shell that were independently "right" would be dark and the page would flash
  twice instead of once. `Theme.feature` holds it there by aborting `_framework/**` so the page
  stays in its loading state.
- **`theme.js` is the only owner of the theme cookies** — it reads them (`load`), writes the
  choice (`set`) and records what the browser last reported (`rememberSystem`). A component
  cannot write a cookie itself, and there is no request for one to arrive on, so nothing in C#
  touches `document.cookie`.
- **No persistence** — the app is stateless. No database, no EF Core, no Identity.
  Wrap planning is a pure function of user input. The one exception is the theme, which
  travels in two cookies — `wrapcal-theme` (the choice) and `wrapcal-system-theme` (the last
  system preference the browser reported). Both are display preferences rather than
  application state.
- **No logging library and no configuration** — there is nowhere for a log to go in a
  browser tab, and the app reads no settings, so `appsettings.json` was removed with the
  server.
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
        NotFound.razor
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
        ShelfPacker.cs          # shelf packing + the search over placements
        ShelfOptimum.cs         # the exact layout for small lists, and re-partitioning for long ones
        PlanWrapModels.cs       # domain records
        PlanWrapInput.cs        # form model + the field limits
        WrapPlanView.razor      # summary figures + cut sections table
        WrapPlanView.razor.css  # that table's styling
        WrapPlanDiagram.razor   # SVG cut layout — needs WrapPlan.RollWidth
        WrapPlanDiagram.razor.css
    Program.cs
    wwwroot/
      index.html              # the document — base href, asset links, the boot scripts
      app.css                 # Roboto @font-face, framework furniture, message slot
      theme.js                # the theme cookies (the only JS we own)
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
- **Along-the-box length is `axis + min(p, q) + margin`.** The fold-over closes the end face, a
  `p × q` rectangle, and the paper hangs past it by the same distance all the way round — so
  folding in gives a frame of that width, and the face is covered as soon as the two flaps across
  its **shorter** side meet, at half of it per end. The longer direction needs nothing extra:
  those same flaps already span it. This read `max(p, q)` for a long time, which cut 41% too much
  paper off a book; the test that should have caught it used a cube, where the two sides are
  equal. Without the margin the flaps meet exactly and never lap.
- **Identical boxes are cut from identical paper.** A plan is instructions for someone with
  scissors, and three identical presents cut from three differently sized sheets reads as a
  mistake. `UniformPieces` groups boxes by dimension and reduces each group of two or more to the
  two rotations of one piece — both rotations, because a piece is the paper, not the way it is
  laid down, and turning it is what lets two of them share a shelf. It is not free: measured at
  2% on twelve identical boxes and up to 7% on four sizes repeated, so if that trade ever needs
  revisiting, the place to start is choosing the group's piece by how the copies pack rather than
  by which sheet suits one box best. A group of one keeps every sheet — narrowing those was a
  real bug, and it stopped two differently sized boxes from sharing a strip.
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
  form. Packing is quadratic in the box count and runs on the browser's only thread, so an
  uncapped list is a way to freeze the page that is drawing it.
- **A sheet no other sheet for the same box dominates is the only one considered.**
  `SheetOptions` drops any option that another option for the same box is no wider and no
  longer than: it fits wherever the larger one fits without raising the shelf, so cutting it can
  never shorten the roll — it only spends paper. A 9 × 6 × 1.5 box on a 30 cm roll is never
  offered the 8 × 30.5 sheet, because 8 × 21.5 is the same width and nine shorter. The
  comparison needs one *strict* improvement, or two sheets a hair apart would dominate each
  other and leave the box with no option at all.
- **Lists of twelve boxes or fewer are solved exactly, not searched.** `ShelfOptimum` enumerates
  every partition of the boxes into shelves by dynamic programming over subsets: a shelf's height
  is its tallest sheet, so a layout is a partition and the answer is the one whose heights add up
  to the least. Nothing about that needs a solver or a server, which is why it fits here at all.
  Measured in a Debug build: 22 ms at 12 boxes, 198 ms at 14, 890 ms at 16 — hence `MaxBoxes = 12`,
  the largest count that stays invisible inside a click. Twelve identical 8 × 6 × 4 boxes are now
  126 cm, an answer derived by hand and confirmed independently, rather than the 150 the old
  search reached.
- **Packing is a greedy shelf fit plus a search over placements**, seeded, so a given input
  always produces the same plan. The search keeps its best plan and keeps nudging *it*
  (ruin-and-recreate, reorientation, and occasional full repacks), rather than repacking from
  scratch each iteration. It still carries real weight: on lists where every box differs it
  finds 26% shorter rolls than the greedy first guess at fourteen boxes, 12% at thirty and 4% at
  sixty. (It was 2–15% better than the old random restarts when that was measured; that
  comparison predates the fold-over fix, so treat the figure as history rather than a current
  claim.) `PlanWrapLogicTests` holds a golden test pinning the output — read its comment before
  touching `Pack` or `SheetOptions`.
- **Above twelve boxes the search's weak point is the partition, so it is re-solved rather than
  nudged.** `ShelfOptimum.Polish` takes each pair of shelves in the best plan, re-solves that
  group exactly, and keeps the result when it is shorter. Box-shuffling moves have no way to see a
  partition that beats the one they are holding; re-solving a pair outright does. Worth 7.5% at
  15 boxes down to 2% at 30, and it beats giving the plain search eight times the compute.
  Groups larger than `MaxBoxes` are skipped — those are the expensive case the limit exists for.
- **A shelf's sheets are chosen for the least paper, not the least width.** Once a shelf's height
  is settled, every sheet no longer than the shelf is equally good for the roll, so the choice
  must fall on paper — the same tie-break the search applies between plans. Choosing the narrowest
  sheet instead looks tidier and quietly cut 3% more paper on any shelf with width to spare. Width
  is a constraint, not a preference: it only decides whether the boxes fit on the shelf at all.
- **The search has to be able to return something other than what it started from.** Reinserting
  a ruined box by best fit alone is a fixed point — take a box out and the same plan puts it
  back — and measured **no improvement at all** until both reinsertion *and* the sheet choice for
  a new shelf were randomised. Strict hill climbing was the second half: refusing every worse
  plan cannot walk down into a better valley, hence `UphillChance`.
- **Opening a shelf with a box's shortest sheet can be the wrong default.** It minimises that
  shelf's height but may be too wide for a second box to share, so the box beside it opens a
  shelf of its own. The search therefore sometimes opens with the shortest sheet that still
  leaves half the roll free — measured on mixed-size lists at 151.2 → 146.6 for fourteen boxes
  and 309.2 → 301.9 for thirty. It does nothing for identical boxes any more: the uniform-piece
  rule already settles their shape.
- **Length is the objective; paper cut is only the tie-break.** Roll length is what the reader
  buys and cuts. Ties to within `1e-9` go to the plan cutting less paper, which is what stops an
  orientation change costing paper and buying nothing. Do not make paper area the primary cost:
  at a fixed length that metric always prefers the *largest* legal sheet, rewarding a box
  wrapped in three sheets' worth of paper.
- **The iteration count is capped by a work budget, not just by `SearchIterations`.**
  `SearchWorkBudget / boxes.Count` bounds the whole run; a short list still gets every iteration
  it asked for, a long one trades iterations for staying responsive. Do not "fix" it back to a
  flat count — 2000 boxes at 500 restarts measured 25 seconds of a frozen tab. The budget is
  stated in units of boxes × iterations; a search iteration is cheaper than a repack was, so
  the same count now costs less, and raising it is a separate measured decision.
- **`SearchIterations` is a cap, and its default is no cap at all.** `null` means "as much as the
  budget allows", because the budget is already the thing stopping a long list from blocking the
  page — capping below it only spends less of the time that was available. Measured against a
  flat 500 on mixed-size lists: 8.7% shorter at fourteen boxes, 1.6% at thirty, and a hair
  *worse* at sixty — the re-partitioning pass is not monotone in where the search starts from, so
  a longer search is not reliably a better one. Identical at 100 boxes, where the budget is 500
  anyway. `0` still means "first arrangement and nothing else", which is what tests pinning a
  specific plan rely on.
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
- **`Features/_Imports.razor` is load-bearing.** `_Imports.razor` only applies to its own
  folder and below, and the one in `Components/` is a sibling — without the one in
  `Features/`, `EditForm`, `InputText` and `PageTitle` are unresolved in slices.
- **Never name a component after a domain record.** Component names derive from the file,
  so `WrapPlan.razor` beside the `WrapPlan` record collides. Hence `WrapPlanView.razor`.
- **There is no "until the circuit is live" gate, and adding one would be dead code.** The
  page does not exist until the WebAssembly runtime has booted, so a click cannot arrive
  early — there is nothing to click. `RendererInfo.IsInteractive` is always true here and
  reading it says nothing. The functional suite still waits for
  `[data-testid='plan-button']:not([disabled])`, which now means "the app has rendered" and is
  the same signal after a reload as after a first navigation.
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
- **Every colour in the drawing comes from the palette, never from a literal.** `WrapPlanDiagram`
  is styled entirely by `--mud-palette-*`, so it is drawn in whichever theme is showing; the two
  fills that need a tint (the paper and the cut sections) are the primary colour at 12%
  `fill-opacity` rather than a colour of their own, because the same 12% reads as a pale wash on
  light and a deep one on dark. The hatch `<pattern>` lives inside `<defs>` and is styled by
  class like everything else — the scope attribute does reach in there, but it is the one place
  where a scoping failure would leave the band invisible rather than merely off-colour, so
  `Theme.feature` holds the hatch to the same computed colour as the paper.
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

# Cake — Clean → Build → Tests (the default target), or publish to ./output
./cake.sh
./cake.sh --target=Publish

# One suite at a time
./cake.sh --target=UnitTests
./cake.sh --target=FunctionalTests

# What CI runs: every suite, then publish
./cake.sh --target=Release

# Provision the browser the functional suite drives
./cake.sh --target=InstallBrowsers
```

`Release` runs `Tests`, so the deploy is gated on the functional suite as well as the unit one.
That is deliberate: a dependency bump that changes how the page looks is exactly what would
otherwise reach the live site unnoticed. `FunctionalTests` depends on `InstallBrowsers`, so a
clean machine — a runner especially — provisions its own browser; the target is idempotent and
takes about two seconds once the browser is there.

The Cake project in `cake/` is deliberately **not in `WrapCal.slnx`**, so `dotnet build` at
the root never builds the build script itself. It targets `net10.0` via `Cake.Sdk`, and its
script runs with the repository root as the working directory (`RunWorkingDirectory`), which
is why every path inside `cake.cs` is relative to the root.

`Tests` is not a `dotnet test` of the solution — it depends on `UnitTests` and
`FunctionalTests`, one target per test project, so a suite runs the same way alone as it does
in the pair. Both depend on `Build`, which Cake runs once. Add a third suite as another task
and a third dependency, not as a branch inside `Tests`.

**The deploy workflow calls Cake and nothing else.** `Release` is the target it asks for —
`UnitTests` then `Publish` — so what the artifact is, where it lands and what gates it are all
`cake/cake.cs`'s business. `.github/workflows/deploy-pages.yml` only moves the result onto
Pages. Do not add `dotnet` steps there: a workflow that rebuilds the way it remembers is a
second build definition that drifts from the first.

`cake.csproj` pins `System.Security.Cryptography.Pkcs` forward: `Cake.Sdk` pulls 10.0.12
transitively, which trips NU1902 and NU1903. Without the pin the Cake build reports four
vulnerability warnings, so keep it until `Cake.Sdk` moves to a fixed version.

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
- The theme is arranged through `SetThemePreference(choice, system)`, which stubs
  `wrapcalTheme.load` — the same single interop call the layout makes at runtime. With no
  setup the call answers nothing, which is what a first visit looks like.

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
  sets `prefers-color-scheme`, the palette is read off the document root (where a theme is
  visible at all) and compared by luminance rather than against exact colours, and the cookie
  is a real one on a real document.
  Mutation-checked: forcing the system preference to light fails "The page follows a dark
  system" and nothing else.
- **The `Then` for a theme waits, and has to.** On a first visit the system preference is not
  knowable until the app has booted and can ask the browser, so the palette moves *after* the
  page is on screen. The step waits up to `ThemeSettleTimeout` for the expected side of
  mid-grey and only then reports. A theme assertion that reads once will flake.
- **Match a validation message past its own number.** "Width must be between" is a substring of
  "Roll Width must be between", so a prefix assertion about box width is satisfied by the roll
  width's message and passes with no box-width message on the page at all. `PlanningSteps`
  therefore matches from the label through the first bound, and the component tests assert each
  message whole.
- **The error paths are covered in both suites, split the way the theme is.** `PlanWrapPageTests`
  holds one case per field rule; `PlanWrap.feature` holds what only a real page can prove — a
  rejected field, two rejected at once, a plan still blocked while either is wrong, values
  exactly on each limit, and a plan that appears once a rejected field is corrected. Mutation
  checked: turning `> MaxDimension` into `>=` fails the boundary test, and dropping the name
  trim fails the trim test.
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
- **The suite serves the app from `/WrapCal`, never the root.** That is the only shape it is
  ever deployed in — a GitHub project page — and serving the root as well would let the suite
  pass while `index.html` was failing to raise its base href, which is the one part of the
  deployment that can break silently. `Hosting.feature` does not exist for this: every
  scenario is the proof.
- **`Support/AppHost.cs` publishes the app and serves the output as static files.** Publish,
  not build: a build leaves the app's own assets out of the output folder — only `_framework/`
  lands there — so a built app is not servable, while publish flattens everything into one
  tree. The suite therefore drives the artifact that ships rather than a development host that
  production never runs, and there is no child process, no port to race and no readiness poll.
  Kestrel needs an explicit `application/wasm` mapping or the browser refuses to stream-compile
  the modules.
- **Served uncompressed, deliberately.** Each scenario gets a fresh browser context, so the
  ~19 MB runtime is fetched again for every one of them. Compressing it was measured at 65s
  against 49s for the whole suite — over loopback the transfer is nearly free and the CPU is
  not. Do not "optimise" this back on without measuring.
- **The publish is cached.** `AppHost` publishes into `bin/<config>/<tfm>/app-publish` and
  reuses it while nothing under `src/WrapCal` (or `Directory.Build.props` / `global.json`) is
  newer than the stamp file. Publishing runs the IL linker on every core, and repeating that
  each run is a load burst for no reason.
- **Scenarios are limited to two at a time**, `Support/ScenarioParallelism.cs`. TUnit
  parallelises by default, which here means several browser contexts compiling WebAssembly at
  once and pinning every core — measured at 44s with the machine unusable throughout, against
  31s at two. `WRAPCAL_SCENARIO_PARALLELISM=1` is the gentler setting when something else
  needs the machine; it costs the same 44s the unbounded run did, for far less load. Do not
  remove the limiter to "speed up" the suite.
- Publishing takes a few seconds per run, and a cold first run longer, which is why
  `PageTimeout` is 60s: the first scenario pays for a WebAssembly boot against a cold file
  cache.
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

**Normally you do not need this:** `./cake.sh --target=InstallBrowsers` does it, and the
functional suite depends on that target. The manual form below is what the target runs, and is
worth understanding when it misbehaves.

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
- **Adds `@rendermode`** — there is one render mode and it is the whole app; the attribute
  has nothing left to select
- **Puts heavy work in `OnInitializedAsync`** — blocks the first paint, and here that is the
  only paint there is
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
