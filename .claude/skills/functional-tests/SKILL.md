---
name: functional-tests
description: >
  The Reqnroll + Playwright browser suite in tests/WrapCal.FunctionalTests — feature files,
  step definitions, the AppHost that publishes and serves the site, scenario parallelism, and
  the rules that keep assertions from passing vacuously. Use when writing or changing a
  .feature file, a step definition, a hook, or anything under tests/WrapCal.FunctionalTests.
---

# The functional suite

Reqnroll (Gherkin) driving Playwright against the app running for real.

```bash
dotnet test --project tests/WrapCal.FunctionalTests
BROWSER=webkit|firefox|chromium   HEADED=1   WRAPCAL_SCENARIO_PARALLELISM=1
```

Browser selection, installing browsers, and the Firefox-on-macOS-27 blocker are in the root
`README.md` — the macOS default is WebKit for that reason.

## Structure

- `Features/*.feature` bind to step definitions in `StepDefinitions/`, split by concern:
  `StepsBase` (shared page helpers), `FormSteps` (setting the form up), `PlanningSteps`
  (planning and what the plan says), `LayoutSteps` (geometry measured in the browser),
  `ThemeSteps` (the cookie and what the page does with it).
- `Hooks/TestRunHooks.cs` starts the app and browser once per run.
  `Hooks/ScenarioHooks.cs` gives each scenario a fresh context and screenshots failures
  to `bin/.../playwright-artifacts/`.
- Steps receive browser state by Reqnroll context injection: `[BeforeScenario]` puts a
  `UiContext` into `ScenarioContext`; step constructors resolve it.
- Generated `*.feature.cs` goes to `obj/`, never source control
  (`ReqnrollUseIntermediateOutputPathForCodeBehind`).
- **Reqnroll maps `@tags` to TUnit `[Category]` attributes automatically** — verified in
  the generated code, despite Reqnroll's docs only documenting NUnit/MSTest/xUnit.

## Reqnroll traps

- **Every concrete step class needs `[Binding]`.** Without it Reqnroll finds no steps at all
  and every scenario fails as "undefined", naming no file — not an obvious error to trace
  back to a new class.
- **A binding is keyword-specific unless it says otherwise.** `[Given]`/`[When]`/`[Then]` match
  only their own keyword, and an `And` inherits the one before it — so a `Given` step whose
  definition is `[When]` fails as undefined, naming no file. `[StepDefinition]` is the escape
  hatch and matches all three. It is not used here on purpose: every step's *meaning* depends on
  where it sits. "I set the roll orientation" is the tempting candidate, but its control only
  exists inside `@if (_plan is not null)` (`PlanWrap.razor`), so moving it before a plan would
  bind and then wait out the locator timeout — a worse failure than the undefined step it replaces.
- **Scenarios state their own inputs** — roll width, unit, overlap, and every box by name and
  size. None rely on the page's defaults, so a reader can check the expected result without
  knowing them.

## Hosting

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
  not. Do not "optimize" this back on without measuring.
- **The publish is cached.** `AppHost` publishes into `bin/<config>/<tfm>/app-publish` and
  reuses it while nothing under `src/WrapCal` (or `Directory.Build.props` / `global.json`) is
  newer than the stamp file. Publishing runs the IL linker on every core, and repeating that
  each run is a load burst for no reason.
- Publishing takes a few seconds per run, and a cold first run longer, which is why
  `PageTimeout` is 60s: the first scenario pays for a WebAssembly boot against a cold file
  cache.
- **Scenarios are limited to two at a time**, `Support/ScenarioParallelism.cs`. TUnit
  parallelizes by default, which here means several browser contexts compiling WebAssembly at
  once and pinning every core — measured at 44s with the machine unusable throughout, against
  31s at two. `WRAPCAL_SCENARIO_PARALLELISM=1` is the gentler setting when something else
  needs the machine. Do not remove the limiter to "speed up" the suite.

## Assertions that would otherwise pass vacuously

- **The `Then` for a theme waits, and has to.** On a first visit the system preference is not
  knowable until the app has booted and can ask the browser, so the palette moves *after* the
  page is on screen. The step waits up to `ThemeSettleTimeout` for the expected side of
  mid-gray and only then reports. A theme assertion that reads once will flake.
- **Match a validation message past its own number.** "Width must be between" is a substring of
  "Roll Width must be between", so a prefix assertion about box width is satisfied by the roll
  width's message and passes with no box-width message on the page at all. `PlanningSteps`
  therefore matches from the label through the first bound, and the component tests assert each
  message whole.
- **A test that can pass without doing anything is worse than no test, and the way to avoid one
  is to drive the interaction a person uses.** The zoom-anchor scenarios first passed while the
  wheel was broken by an edit — a drawing that never zooms also never moves — so they now assert
  the readout moved to 150% as well as that the point held. Scroll a control into view before
  pointing at it: an element below the fold receives no mouse event at all, so the test
  exercises nothing and "passes" for the wrong reason.
- **`Layout.feature` guards the diagram's scale with the scale, not with the length.** Two
  scenarios look similar and are not: "a long plan scrolls" measures *overflow*, which needs the
  plan to outgrow its frame — better packing shortens plans, so that input has already had to
  grow twice and can be improved away. "Adding boxes does not shrink the drawing" measures the
  paper's extent across the roll, which the CSS pins to the frame and the roll width, so it
  holds however long the plan is and cannot be made vacuous by a better packer. Both are
  mutation checked: dropping `flex: none` fails the vertical one and unclamping the horizontal
  `height` fails the other.

## The split with the unit suite

- **The theme is split between the two suites, and only `Theme.feature` can test System.**
  `MainLayoutTests` covers which of the three choices is in force — it arranges the browser's
  answer by stubbing `mudThemeProvider.isDarkMode`, since bUnit has no media query and no
  operating system. `Theme.feature` covers what only a real browser can: `EmulateMediaAsync`
  sets `prefers-color-scheme`, the palette is read off the document root (where a theme is
  visible at all) and compared by luminance rather than against exact colors, and the cookie
  is a real one on a real document.
  Mutation-checked: forcing the system preference to light fails "The page follows a dark
  system" and nothing else.
- **The error paths are covered in both suites, split the way the theme is.** `PlanWrapPageTests`
  holds one case per field rule; `PlanWrap.feature` holds what only a real page can prove — a
  rejected field, two rejected at once, a plan still blocked while either is wrong, values
  exactly on each limit, and a plan that appears once a rejected field is corrected. Mutation
  checked: turning `> MaxDimension` into `>=` fails the boundary test, and dropping the name
  trim fails the trim test.
