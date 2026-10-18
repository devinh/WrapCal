# Spec 001: MudBlazor style pass

**Status:** Approved (2026-10-18, amended — interactivity declared at the root)
**Date:** 2026-10-18
**Baseline commit:** `cc4a166`

## Problem

WrapCal has never had a style pass. Every surface is the raw Blazor Web App template: Bootstrap 5
classes on a `<table>`, `form-control` inputs, an unstyled header bar, and two small scoped
stylesheets whose only job is to prop up Bootstrap-era layout (reserved space for validation
messages, aligning a Remove button with an input).

The result is an app that works correctly and looks like scaffolding. There is no design system
behind it, so every visual change is a bespoke CSS edit, and the Bootstrap dependency is a thing
to work around rather than a thing to use — the template brought it, nobody chose it.

Success: the calculator looks like a deliberate, designed Material application, built from named
components with a declared theme, with **every existing behaviour intact** — the plan maths, the
diagram geometry, the unit conversion, and the validation rules.

The goal is presentation. This is a style pass, not a feature pass. Nothing in the domain model
changes.

## Scope

### In

- Add **MudBlazor 9.11.0** (has a first-class `net10.0` target).
- Delete **Bootstrap entirely**: the `bootstrap.min.css` link, `wwwroot/lib/bootstrap`.
- Rebuild every surface on MudBlazor components:
  - `MainLayout` → `MudLayout` / `MudAppBar` / `MudMainContent`, with **no navigation added**
    (the app remains a single page; the bar carries the brand and a theme toggle only).
  - `PlanWrap` → `MudPaper` / `MudGrid` / `MudItem` / `MudForm` / `MudTextField` /
    `MudNumericField` / `MudSelect` / `MudButton` / `MudAlert` / `MudCard`.
  - `WrapPlanView` → Mud typography and surfaces; its table may remain semantic.
  - `Error.razor` and `NotFound.razor` → Mud components, both interactive via the root.
  - `WrapPlanDiagram` → Material colours only; geometry frozen (see below).
- Declare a **stock MudBlazor Material theme**, light and dark, with a toggle.
- Self-host the Roboto font files; no third-party requests at runtime.
- Replace the `data-interactive` marker with a **disabled-until-live** readiness guarantee.
- Update the unit and functional suites; delete CSS that becomes dead.

### Out

- Any change to `PlanWrapLogic`, `SheetOptions`, `Pack`, `UnitConverter`, or the domain records.
  The golden test in `PlanWrapLogicTests` must pass untouched.
- Any change to the diagram's **geometry**: `viewBox`, the scale derived from `RollWidth`, the
  `Gutter` constant, the Left/Right transpose, the origin-dot corner, the flex scroller's
  `is-vertical` / `is-horizontal` / `is-reversed` classes, and the `(Plan, Direction)` key.
- New features of any kind — no accounts, saved plans, shareable URLs, printing, export.
- Navigation, routing changes, or a second page.

## Domain Model

**Unchanged.** `GiftBox`, `WrapPlan`, `WrapOptions`, `RollDirection`, `LengthUnit`, `Result<T>` and
`PlanWrapLogic` are not touched by this spec.

The only domain-adjacent surface that moves is **where validation rules are expressed**:

| Rule | Today | After |
|------|-------|-------|
| `RollWidth` ∈ [1, 1000] | `[Range]` on `PlanWrapInput`, via `DataAnnotationsValidator` | MudForm `Validation` func; same bounds |
| `OverlapMargin` ∈ [0, 100] | `[Range]` on `PlanWrapInput` | MudForm `Validation` func; same bounds |
| Box `Length`/`Width`/`Height` ∈ [0.1, 1000] | `PlanWrapInput.ValidateBoxes()` | MudForm per-field `Validation` func; same bounds |
| Box `Name` ≤ 50 chars | `PlanWrapInput.ValidateBoxes()` | MudForm per-field `Validation` func; same bound |

`DataAnnotations` attributes on `PlanWrapInput` are removed once MudForm owns validation. The
**substance** of every rule is preserved: same fields, same bounds, same conditions. Message
wording may change — the page adopts Mud's idiom of helper text that an error replaces.

`PlanWrapLogic` keeps its own guards for callers that bypass the form. Those are not part of the
UI validation path and are untouched.

## Component & Test Contract

There is no HTTP API. The externally-observable contract is the DOM.

### `data-testid`

The existing ids may evolve, but every interactive control that had one keeps a stable, unique
identifier, and the functional suite is updated in the same change. The set today:

`boxes`, `box-name`, `add-box`, `remove-box`, `unit-select`, `plan-button`, `plan-error`,
`roll-direction`, `sections`, `roll-length`, `paper-area`, `waste-area`, `efficiency`.

### Readiness

`data-interactive` and the `form[data-interactive='true']` wait are **removed**. In their place,
`Plan`, `Add box` and `Remove` render **disabled** until `RendererInfo.IsInteractive` is true, then
enable. Tests wait for the Plan button to become enabled.

This is a user-facing guarantee rather than a test-only hook: a click that lands before the circuit
attaches can no longer do anything, because the control is disabled.

### Framework contracts (must not break)

- `ReconnectModal` — the runtime toggles `components-reconnect-show` / `-retrying` / `-failed` /
  `-paused` / `-resume-failed` on `#components-reconnect-modal`, and the JS keys off
  `components-reconnect-*-visible` class names. **No class name in that component may change.**
  It is restyled only by editing its own scoped CSS.
- `#blazor-error-ui` and `.blazor-error-boundary` keep their ids, classes and script wiring.
- `Routes.razor` keeps `NotFoundPage="typeof(Pages.NotFound)"`.

## Authorization

Not applicable. The app has no users, no accounts, and no concept of identity. Nothing in this spec
introduces any.

## Edge Cases & Failure Modes

1. **Interactivity is declared once, at the root.** MudBlazor's popover, dialog and snackbar
   providers **must sit in the same interactive render tree as the components that use them**
   (MudBlazor issues #9757 and #12283). Providers in a static `App.razor` alongside an interactive
   `MudSelect` is precisely the broken combination. Therefore:
   - `App.razor` renders `<Routes @rendermode="InteractiveServer" />`.
   - `MainLayout` hosts `MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider` and
     `MudSnackbarProvider`, and is interactive as a consequence of being inside that root.
   - `PlanWrap` **drops its own `@rendermode`** — it inherits the root's, and nesting an explicit
     rendermode inside an interactive parent is not allowed.
   - `Error.razor` and `NotFound.razor` need no rendermode for the same reason.
   This supersedes the repo's usual "static SSR first, opt in per component" rule, deliberately:
   every route in this app is interactive, so per-component opt-in buys nothing but a broken
   provider tree. `App.razor` itself stays static.
2. **`Error.razor` loses `HttpContext`.** Under `InteractiveServer` the `[CascadingParameter]
   HttpContext` cascade is empty, so the Request ID would vanish. The ID must be captured during
   the **prerender** pass and persisted into the interactive pass — `[PersistentState]` if it is
   available in .NET 10, otherwise `PersistentComponentState`. See Deferred Decisions.
3. **Theme toggle must not discard the plan.** A cookie can be written without JS only by posting
   and reloading, which would destroy the on-screen plan and typed box values. The toggle is
   therefore an ordinary component in the (now interactive) `MainLayout`, writing the cookie
   through a single JS interop call. `App.razor` reads the cookie server-side, so first paint is
   already the right theme — no flash. No interactive island is needed once interactivity is
   declared at the root.
4. **MudSelect and Playwright.** `MudSelect` renders an overlay, not a native `<select>`, so
   `selectOption()` stops working. Steps must open the control and click the item. This affects the
   unit picker and Roll Continues, and therefore `FormSteps`, `PlanningSteps` and `LayoutSteps`.
5. **Validation timing changes.** MudForm validates on blur, not on every keystroke. A bad value is
   reported when the field is left rather than as it is typed. This is chosen deliberately.
6. **Diagram scoped CSS inside new chrome.** `WrapPlanDiagram.razor.css` styles the SVG and its
   scroller. It must keep applying when the component moves inside a `MudCard`; verify the rendered
   output, do not assume scoping survives.
7. **Plan state is not persisted across reloads.** Unchanged from today, and no reload is introduced
   by the toggle (see edge case 3).
8. **Zero boxes.** `Remove` is disabled when only one box remains — unchanged, and now disabled for
   the second reason (pre-circuit) as well. Both reasons must be expressed so the button re-enables
   correctly when the circuit attaches.

## Non-Functional Requirements

- **Bundle.** MudBlazor CSS + JS and two Roboto weights add on the order of 500 KB uncompressed.
  Accepted: served through `MapStaticAssets`, local tool, no CDN.
- **No third-party runtime requests.** Fonts self-hosted under `wwwroot`. Verified by inspecting a
  page load's network requests during the manual check.
- **Build hygiene.** `dotnet build` at **0 warnings**; `dotnet format --verify-no-changes` clean.
- **Test runtime.** The functional suite must not get materially slower; no new server or browser
  startup.

## Integrations

| Integration | Detail |
|-------------|--------|
| **MudBlazor 9.11.0** | NuGet. `net10.0` target, depends on ASP.NET Core 10.0.1. |
| **Assets** | `_content/MudBlazor/MudBlazor.min.css` and `MudBlazor.min.js` referenced from `App.razor`. |
| **Roboto** | Self-hosted woff2 in `wwwroot`; no `fonts.googleapis.com` link. |
| **Bootstrap** | Removed — package files deleted, link removed. |
| **Playwright** | Steps updated for `MudSelect` overlays and the enabled-until-ready wait. |
| **Reqnroll** | Feature files unchanged in intent; only steps and selectors change. |

## Acceptance Criteria

**AC-1 — Bootstrap is gone.** Given the app is running, when any page is loaded, then no request is
made for a Bootstrap asset and `wwwroot/lib/bootstrap` does not exist.

**AC-2 — MudBlazor is in place.** Given the app is running, when `/` is loaded, then the MudBlazor
stylesheet and script are referenced and `MudThemeProvider`, `MudPopoverProvider` and
`MudDialogProvider` are rendered **inside the interactive render tree** — a `MudSelect` opens its
overlay rather than reporting a missing provider.

**AC-3 — The plan is unchanged.** Given the inputs used by the golden test in `PlanWrapLogicTests`,
when a plan is produced, then it is byte-identical to the output pinned before this change.

**AC-4 — Boxes are entered in a MudGrid.** Given the plan page, when a user adds and removes boxes,
then each box is a row of Mud inputs in a `MudPaper`, `Add box` appends a row, and `Remove` deletes
that row and is disabled when only one box remains.

**AC-5 — Units still convert.** Given values typed in inches, when the unit is changed to
centimetres via the MudSelect, then every length is converted to 2 decimal places and any displayed
plan is discarded.

**AC-6 — Validation keeps its rules.** Given a box length of `0`, when the field is left, then a
validation message appears against that box's length field, and it is not the roll's message.
Given a roll width of `2000`, then a message appears against Roll Width.

**AC-7 — Nothing is clickable before the circuit is live.** Given the page has been served but the
SignalR circuit is not yet attached, then `Plan`, `Add box` and `Remove` are disabled. When the
circuit attaches, they enable without a reload.

**AC-8 — The diagram is recoloured but not reshaped.** Given any plan and any of the four roll
directions, when the diagram renders, then its scroll axis, initial scroll position, origin dot and
section positions match the pre-change geometry, and `Layout.feature` passes with only colour
expectations changed.

**AC-9 — Error pages are Material and keep their diagnostic.** Given a request to a missing route,
then the not-found page renders with Mud components. Given an unhandled server error, then the error
page renders with Mud components **and still shows a Request ID**.

**AC-10 — The theme toggles and remembers.** Given the plan page with a plan displayed, when the
theme toggle is used, then the theme changes, the plan and typed inputs are still on screen, and
after a reload the theme is the one chosen, with no flash of the other theme.

**AC-11 — No third-party requests.** Given `/` is loaded, then every asset request goes to the app's
own origin.

**AC-12 — The suites are green.** Given the change, `dotnet build` reports 0 warnings,
`dotnet format --verify-no-changes` passes, `dotnet test --project tests/WrapCal.UnitTests` passes,
and `dotnet test --project tests/WrapCal.FunctionalTests` passes.

**AC-13 — The app is still one page.** Given the finished change, then there is no navigation
element, no second route beyond `/`, `/Error` and `/not-found`, and `MainLayout` carries only the
brand and the theme toggle.

**AC-14 — Reconnect still works.** Given the reconnect modal, then its markup and class names are
unchanged and it styles itself from its own scoped CSS.

## Deferred Decisions

| Decision | Chosen fallback now | Revisit when |
|----------|--------------------|--------------|
| Mechanism for carrying the Request ID across prerender | Try `[PersistentState]` first; if unavailable on .NET 10, use `PersistentComponentState` and `RegisterOnPersisting` | During implementation of AC-9 |
| Exact dark palette values | Use MudBlazor's stock dark palette with no bespoke tuning | After the first visual pass |
| Whether `data-testid` values are renamed | Keep current values wherever the element still exists; rename only where the component genuinely changes shape (e.g. `unit-select`, `roll-direction`) | While updating `FormSteps` |
| Whether `WrapPlanView`'s sections table becomes `MudSimpleTable` or stays a styled `<table>` | Stay a styled semantic `<table>`; it is read-only output | During the visual pass |
| Roboto weights to self-host | 400 and 700 only | If the design needs 500 |

## Open Questions

None.
