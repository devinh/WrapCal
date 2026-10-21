---
name: theme
description: >
  How WrapCal's light/dark/system theme works — the two cookies, the boot script that paints
  the shell before Blazor starts, and the rule that nothing but theme.js writes a cookie. Use
  when changing MainLayout, ThemeChoice, index.html's boot script, app.css, theme.js, or a
  test that asserts a color or a theme choice.
---

# The theme

Three choices — Light, Dark, System — with **System as the default**, picked from a `MudMenu` in
the app bar. `ThemeChoice` lives in `Components/Layout`.

## The first frame already knows the choice

Only the browser can answer what the system is set to, and only a cookie can tell the app what
was chosen last time. `MainLayout.OnInitializedAsync` asks `theme.js` for both cookies through a
single `wrapcalTheme.load` call, so the **first rendered frame already knows the choice** — no
flash on a returning visit. A first visit in System mode still flashes light once, because no
cookie exists yet and `MudThemeProvider` cannot ask the browser until it has rendered.
`GetSystemDarkModeAsync` and `WatchSystemDarkModeAsync` then keep a page left open following the
system as it changes.

## The shell before the app boots is themed too

`index.html` reads the same two cookies and sets `data-boot-theme` on `<html>`; `app.css` paints
that from `--mud-palette-background` with a fallback of MudBlazor's default (#ffffff / #32333d).
Using the palette variable first means the rule keeps following the theme once the app is up,
including a runtime switch, instead of going stale.

The boot script **mirrors `MainLayout` and deliberately ignores `prefers-color-scheme`**: on a
first visit the app has no remembered system preference and draws light, so a shell that were
independently "right" would be dark and the page would flash twice instead of once.
`Theme.feature` holds it there by aborting `_framework/**` so the page stays in its loading
state.

## theme.js owns the cookies

`theme.js` reads them (`load`), writes the choice (`set`) and records what the browser last
reported (`rememberSystem`). A component cannot write a cookie itself, and there is no request
for one to arrive on, so nothing in C# touches `document.cookie`.

Two cookies: `wrapcal-theme` (the choice) and `wrapcal-system-theme` (the last system preference
the browser reported). Both are display preferences rather than application state — the app is
otherwise stateless and stays that way.

## Testing it

The theme is split between the two suites, and only the functional one can test System:

- **`MainLayoutTests`** covers which of the three choices is in force. It arranges the browser's
  answer by stubbing `mudThemeProvider.isDarkMode`, since bUnit has no media query and no
  operating system, and it arranges the cookies through `SetThemePreference(choice, system)`,
  which stubs `wrapcalTheme.load` — the same single interop call the layout makes at runtime.
  With no setup the call answers nothing, which is what a first visit looks like.
- **`Theme.feature`** covers what only a real browser can: `EmulateMediaAsync` sets
  `prefers-color-scheme`, the palette is read off the document root (where a theme is visible at
  all) and compared by luminance rather than against exact colors, and the cookie is a real one
  on a real document. Mutation-checked: forcing the system preference to light fails "The page
  follows a dark system" and nothing else.
- **The `Then` for a theme waits, and has to.** On a first visit the system preference is not
  knowable until the app has booted and can ask the browser, so the palette moves *after* the
  page is on screen. The step waits up to `ThemeSettleTimeout` for the expected side of
  mid-gray and only then reports. A theme assertion that reads once will flake.

## Drawing in the theme

Every color in the diagram comes from the palette, never from a literal: `WrapPlanDiagram` is
styled entirely by `--mud-palette-*`, so it is drawn in whichever theme is showing. The two
fills that need a tint (the paper and the cut sections) are the primary color at 12%
`fill-opacity` rather than a color of their own, because the same 12% reads as a pale wash on
light and a deep one on dark. The hatch `<pattern>` lives inside `<defs>` and is styled by class
like everything else — the scope attribute does reach in there, but it is the one place where a
scoping failure would leave the band invisible rather than merely off-color, so `Theme.feature`
holds the hatch to the same computed color as the paper.
