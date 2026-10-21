# PlanWrap

The calculator, and the whole app.

| File | Holds |
|---|---|
| `PlanWrap.razor` | the routable page, `/` |
| `PlanWrap.razor.cs` | form state, field rules, unit conversion |
| `PlanWrapLogic.cs` | `PlanWrapInput` → `Result<WrapPlan>` |
| `ShelfPacker.cs` | shelf packing, and the search over placements |
| `ShelfOptimum.cs` | the exact layout for small lists, and re-partitioning for long ones |
| `PlanWrapModels.cs` | domain records |
| `PlanWrapInput.cs` | form model and the field limits |
| `WrapPlanView.razor` | summary figures and the cut sections table |
| `WrapPlanDiagram.razor` | the SVG cut layout |
| `RollDirectionText.cs` | which corner the origin is, in words and in the diagram |

[The root README](../../../../README.md) explains what the calculator does for a *reader* — the
formulas, what each figure means. This file is what it takes to change it: every point below has
been got wrong once already.

## The maths

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
- **A sheet no other sheet for the same box dominates is the only one considered.**
  `SheetOptions` drops any option that another option for the same box is no wider and no
  longer than: it fits wherever the larger one fits without raising the shelf, so cutting it can
  never shorten the roll — it only spends paper. A 9 × 6 × 1.5 box on a 30 cm roll is never
  offered the 8 × 30.5 sheet, because 8 × 21.5 is the same width and nine shorter. The
  comparison needs one *strict* improvement, or two sheets a hair apart would dominate each
  other and leave the box with no option at all.
- **The note names the box sides the paper goes around**, e.g. `Wrapped around the box's
  4.0 × 22.0 cm sides.` It deliberately never mentions the roll: by the time anyone wraps,
  the sheet is cut off it and the roll is away.
- **`WrapPlan` carries `RollWidth`, not just the length used** — the diagram has no canvas
  without it.

## Packing

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
  sixty. `PlanWrapLogicTests` holds a golden test pinning the output — read its comment before
  touching `Pack` or `SheetOptions`.
- **Above twelve boxes the search's weak point is the partition, so it is re-solved rather than
  nudged.** `ShelfOptimum.Polish` takes each pair of shelves in the best plan, re-solves that
  group exactly, and keeps the result when it is shorter. Box-shuffling moves have no way to see a
  partition that beats the one they are holding; re-solving a pair outright does. Worth 7.5% at
  15 boxes down to 2% at 30, and it beats giving the plain search eight times the compute.
  Groups larger than `MaxBoxes` are skipped — those are the expensive case the limit exists for.
- **The search and the re-solve take turns, `Rounds` times, and the budget is split not added.**
  Polishing improves a plan, and a search that never saw the improved plan cannot build on it.
  Each step only ever improves what the one before produced, so a round cannot undo the last.
  At three rounds: 3.1% shorter at fourteen boxes, 2.6% at thirty, 2.1% at sixty and 18.4% at a
  hundred, for about 1.5× the time of a single pass. Do not add the rounds' iterations on top of
  the budget — `SearchWorkBudget` still bounds the whole run, which is what keeps a long list
  from freezing the page it is drawn on.
- **A shelf's sheets are chosen for the least paper, not the least width.** Once a shelf's height
  is settled, every sheet no longer than the shelf is equally good for the roll, so the choice
  must fall on paper — the same tie-break the search applies between plans. Choosing the narrowest
  sheet instead looks tidier and quietly cut 3% more paper on any shelf with width to spare. Width
  is a constraint, not a preference: it only decides whether the boxes fit on the shelf at all.
- **The search has to be able to return something other than what it started from.** Reinserting
  a ruined box by best fit alone is a fixed point — take a box out and the same plan puts it
  back — and measured **no improvement at all** until both reinsertion *and* the sheet choice for
  a new shelf were randomized. Strict hill climbing was the second half: refusing every worse
  plan cannot walk down into a better valley, hence `UphillChance`.
- **Opening a shelf with a box's shortest sheet can be the wrong default.** It minimizes that
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
  `SearchWorkBudget / boxes.Count` bounds the whole run, so a short list gets every iteration it
  asked for and a long one trades iterations for staying responsive. Do not "fix" it back to a
  flat count — 2000 boxes at 500 restarts measured 25 seconds of a frozen tab. The budget is
  stated in units of boxes × iterations, and a search iteration is cheaper than a repack was, so
  the same count now buys more; raising it is a separate measured decision.
- **`SearchIterations` is a cap whose default is no cap at all.** `null` means "as much as the
  budget allows": the budget already stops a long list blocking the page, so capping below it
  only spends less of the time available. `0` still means "first arrangement and nothing else",
  which tests pinning a specific plan rely on.
- **A bigger iteration count is not reliably a smaller plan.** Within one run nothing goes
  backwards, but across *different* caps the answer moves either way, because the budget is split
  between rounds — a different cap walks a different path rather than extending the same one. At
  sixty boxes a 100-iteration run beat the full budget's plan. The form always passes `null`, so
  this only shows up in tests. Do not "fix" it by making the rounds fixed-size: that would either
  tie the work to nothing or reintroduce the freeze the budget prevents.

## The page

- **A plan holds at most 100 boxes**, `PlanWrapInput.MaxBoxCount`, enforced twice: the Add
  button disables at the limit, and `PlanWrapLogic` refuses more for callers that never saw the
  form. Packing is quadratic in the box count and runs on the browser's only thread, so an
  uncapped list is a way to freeze the page that is drawing it.
- **Changing the unit converts what is already typed**, rounded to 2 decimals, and discards
  any displayed plan, which is stated in the unit just left.
- **Any change to the form discards the displayed plan**, not just a unit change: editing a
  field, adding a box, or removing one. A plan is only ever on screen while it still describes
  the inputs in front of the reader. `DiscardPlan` is the single place that clears it, wired to
  every field with `@bind-Value:after`.
- **Field rules live in the page's MudForm validation funcs, and only there.** Each field
  carries a `For` expression plus a `Validation` delegate; returning an empty string means
  valid. MudBlazor declares `Validation` as `object`, so the delegate type has to come from a
  typed property on the code-behind — a method group or lambda in markup cannot be inferred.
  The per-field bounds are `const`s on `PlanWrapInput` and are not duplicated anywhere: there is
  deliberately no second copy to drift. `PlanWrapLogic` keeps its own guards for callers that
  bypass the form, and where it needs a limit the form also applies — the box count — it reads
  the same `const` rather than restating the number.
- **Validation reports when a field is left, not on every keystroke.** That is Mud's timing,
  chosen over the old on-change behavior. Box fields carry `HelperText=" "` and the global
  `.wrapcal-message-slot` rule so the message line is always reserved — without it, reporting a
  problem grows the row and pushes the Plan button down. That rule is global because the class
  lands on markup MudBlazor renders, which a component's scoped CSS cannot reach.
- **Numbers the reader sees follow their culture; numbers in SVG attributes do not.**
  `WrapPlanView.Format`, `WrapPlanDiagram.F` and the note use `CurrentCulture`, matching the
  form's numeric fields. `WrapPlanDiagram.N` stays invariant — it writes `viewBox` and
  coordinates, which only parse with a decimal point. Tests that assert the wording of a note
  pin the culture for exactly this reason.
- **Every interactive control carries a `data-testid`**: `roll-width`, `overlap-margin`,
  `roll-options`, `unit-select`, `boxes`, `box-row`, `box-name`, `box-length`, `box-width`,
  `box-height`, `add-box`, `remove-box`, `plan-button`, `plan-error`, `roll-direction`,
  `diagram`, `sections`, `roll-length`, `paper-area`, `waste-area`, `efficiency`,
  `plan-orientation`, `zoom-level`, `theme-menu`, `theme-system`, `theme-light`,
  `theme-dark`. MudBlazor puts unmatched attributes on the element it renders — for an input
  that is the `<input>` itself, but for `MudSelect` it is a hidden input that cannot be clicked,
  so the two selects are wrapped in a `<div>` carrying the id, and `MudMenu` is wrapped for the
  same reason (its activator is the button *inside* the wrapper, which is what a click has to
  hit). The functional suite drives the page through these, so renaming one breaks tests rather
  than the app.

## The diagram

- **The diagram never scales to fit.** Its scale comes from the roll width, which does not
  change as boxes are added, so a longer plan draws a longer diagram and the rest is reached
  by scrolling — the scroll axis follows the roll (`is-vertical` / `is-horizontal` on
  `.diagram-scroller`). Scaling the whole plan into the figure is what used to make the
  drawing shrink with every box.
- **`Layout.feature` guards that with the scale, not with the length.** Two scenarios look
  similar and are not: "a long plan scrolls" measures *overflow*, which needs the plan to outgrow
  its frame — better packing shortens plans, so that input has already had to grow twice and can
  be improved away. "Adding boxes does not shrink the drawing" measures the paper's extent across
  the roll, which the CSS pins to the frame and the roll width, so it holds however long the plan
  is and cannot be made vacuous by a better packer. Both are mutation checked: dropping `flex:
  none` fails the vertical one and unclamping the horizontal `height` fails the other.
- **Every color in the drawing comes from the palette, never from a literal.** `WrapPlanDiagram`
  is styled entirely by `--mud-palette-*`, so it is drawn in whichever theme is showing; the two
  fills that need a tint (the paper and the cut sections) are the primary color at 12%
  `fill-opacity` rather than a color of their own, because the same 12% reads as a pale wash on
  light and a deep one on dark. The hatch `<pattern>` lives inside `<defs>` and is styled by
  class like everything else — the scope attribute does reach in there, but it is the one place
  where a scoping failure would leave the band invisible rather than merely off-color, so
  `Theme.feature` holds the hatch to the same computed color as the paper.
- **The roll never touches the diagram's frame.** A gutter of 15% of the roll width frames the
  paper on whichever axis the roll spans — the margins on the other axis already carry the two
  end annotations. Without it the drawing read as zoomed in too far, since the paper sat flush
  against the border. The number is the `Gutter` constant in `WrapPlanDiagram.razor`, tuned by
  eye rather than derived, so change it there and nowhere else.
- **The origin dot moves with the drawing.** It sits on the corner where plan X and plan Y
  are both zero: top-left going Down, bottom-left going Up and Right, bottom-right going
  Left. The horizontal layouts also flip X downwards so that corner really is the origin.
- **The Cut Plan says that corner in words, and the words are derived from the dot's own
  position.** `RollDirectionText.OriginAtFarEnd` / `OriginAtBottom` are what `WrapPlanDiagram`
  places the dot from *and* what the Cut Plan's `plan-orientation` line names the corner from, so
  the sentence cannot drift from the picture. The same helper holds the menu labels and the edge
  word the caption uses. Mutation checked: forcing `OriginAtBottom` true fails the diagram's
  origin test, the view's text test and the orientation outline together — if it only failed the
  text, the two would be parallel switches rather than one source.
- **The scroller opens at the origin from script, not by being laid out backwards.** Up and Left
  put the origin at the far end, and the scroller used to be `column-reverse` / `row-reverse` for
  them so the initial scroll landed there. That reversal cost two things: a reversed row packs its
  content against the *right* edge, so a left-running drawing sat on the wrong side of its frame,
  and its scroll offsets run the other way, which made every scroll correction guess its direction.
  `wrapcalDiagram.openAtOrigin` now sets the offset past the end and lets the browser clamp, once
  per `(Plan, Direction)` — never per render, or a zoom would drag the reader back to the origin.
  The cost is honest: this is the one place the layout no longer opens at the origin without
  script, and the position arrives a frame after the drawing does.
- **The scroller is keyed on `(Plan, Direction)`, and the key is load-bearing.** Re-keying resets
  the scroll, so a new plan or a new direction opens at the origin instead of wherever the last
  drawing was left. A new *direction* also puts the zoom back to 100% — a zoom chosen for one
  orientation is not a zoom chosen for another — while a new *plan* keeps it: the data changed,
  not the view.
- **The drawing hugs the top-left of its frame, and is its own size along the roll.** The vertical
  scroller's svg takes `min-height: 100%` so a short plan has no dead space above it, while the
  horizontal one takes `min-width: 0`: a frame-wide box scrolls while the drawing inside it does
  not, so the far end of a long sideways plan became unreachable — `the drawing should be scrollable
  across its whole width` pins that. Left alignment then comes free from a plain row, which is what
  the reversal used to take away. Do not "restore" `xMidYMid`: that
  re-centers the content in the filled frame. `justify-content: flex-end` and auto margins were
  both tried first and both push overflowing content toward a start the scrollbar cannot reach,
  taking the origin out of view; `safe flex-end` is the right idea but WebKit does not honor it.
- **The diagram transposes for Left/Right, it does not rotate.** A section at `(X, Y)`
  drawn rightwards lands at `(Y, X)` with width and length swapped — a mirror across the
  roll-width axis, not a 90° rotation (which would be `(Y, RollWidth - X - W)`). This is
  deliberate: a true rotation would turn the box labels with the drawing, and Up is 180°,
  so labels would render upside-down. Keeping text upright in all four directions is worth
  the transpose. The plan itself is identical either way.

## Zoom

- **Zoom is gesture-only, continuous, and 100% is the only value that claims anything.** Ctrl/Cmd
  + wheel and pinch everywhere, two fingers on a touchscreen, no on-screen control: a slider was
  tried and removed, because it competed with the drawing for the eye. The wheel scales by a
  **ratio** (`1.5` per 120 of delta), so a mouse notch is one ratio and a trackpad's stream of
  small deltas glides, and nothing accumulates. The ratio is a calibration, not a derivation:
  Safari reports a pinch as the finger spread itself, Chromium and Firefox as ctrl+wheel in pixel
  deltas, and the two only feel alike if this number is tuned. Expect to adjust it by hand on real
  trackpads. Scale is applied by multiplying the pinned dimension through `--diagram-zoom` —
  sizing rather than `transform: scale()`, because a transform leaves the layout box alone and
  the scrollbars would describe the unscaled drawing.
- **There is no keyboard path to zoom, and that is a known gap.** Gestures are the only input, so
  a reader without a wheel or trackpad cannot zoom at all. `+`/`−` buttons are the usual answer
  and are not there. Worth doing if it matters.
- **The scale-invariance scenarios cannot catch a wrong default.** They compare before and after
  adding boxes, so they hold whatever the starting zoom is;
  `Starts_the_zoom_at_one_hundred_percent` and the readout assertions in `Layout.feature` are what
  pin it.
- **Zoom is anchored at the pointer, and the correction does not assume a scroll convention.** The
  fraction of the drawing under the pointer is recorded before the zoom resizes anything; after the
  render, `diagram.js` moves the scroll offsets so that fraction is back under the pointer. Which
  way an offset moves the content depends on the layout — Up and Left lay the scroller out from the
  far end — so instead of encoding four conventions the code tries one direction, measures, and
  takes the other if the error did not shrink. Restoring the exact offsets between attempts matters:
  at the end of the scroll range the browser clamps, so a shift and its "undo" are not equal and
  opposite, and negating moved the drawing the wrong way.
- **Anchor in the drawing's own coordinates, not in fractions of the frame.** The `<svg>` is as big
  as the frame, and a sideways drawing only fills a corner of it, so a fraction of the element is
  not a fraction of the drawing. The point is captured in user space through the SVG's matrix and
  mapped back through it after the zoom. Crucially that matrix must be the **drawing group's**, not
  the `<svg>`'s: a translate on the group is invisible to the svg's matrix, so a drift corrected
  that way never looks corrected and gets rolled back — which is what kept Left and Right failing
  while Up and Down passed.
- **The correction is a scroll correction, and nothing else.** Translating the drawing group looks
  like it works — the point does land under the pointer — but an `<svg>` clips what lies outside its
  own box, so a translated drawing loses the part pushed past the edge and no scrolling can bring it
  back.
- **The horizontal frame needs its own `max-height`.** The vertical scroller's `max-height: 60vh`
  keeps the frame still while the drawing changes size inside it; without the same on the horizontal
  one, zooming made the frame taller and pushed the whole page down under the pointer.
- **Anchoring needs a plan long enough to overflow.** A short sideways drawing fits inside its frame,
  so there is nothing to scroll and nothing to correct — the anchor scenarios therefore plot twelve
  boxes rather than three.
- **`diagram.js` is the second file of JS the app owns, and it exists for passive listeners.**
  Blazor registers `wheel` as passive, so `preventDefault` cannot come from C# and Ctrl+wheel would
  zoom the page along with the drawing. Its listeners sit on the **document** and scope themselves
  by where the pointer is, not by the event's target: the scroller is re-keyed on
  `(Plan, Direction)` and would take a listener with it, and WebKit dispatches a wheel aimed at the
  page root rather than at the element under the cursor. A trackpad pinch arrives as ctrl+wheel on
  Chromium — fractional deltas, hence the accumulator — and as `gesture*` events on Safari. The
  two-finger touch path has **no automated test**: Playwright's multi-touch is Chromium-only through
  CDP, and this suite runs WebKit on macOS and Firefox elsewhere.
- **A pinch in progress silences the wheel.** Safari reports a pinch as gesture events, Chromium and
  Firefox as ctrl+wheel. If a browser ever reports both, honoring both would zoom twice there and
  once elsewhere — which reads as "the zoom is faster on Safari". `ZoomByWheelAsync` therefore
  returns early between `gesturestart` and `gestureend`. Cheap, and a no-op where it does not apply.

## Testing this slice

- **A test that can pass without doing anything is worse than no test, and the way to avoid one is
  to drive the interaction a person uses.** The anchor scenarios first passed while the wheel had
  been broken by an edit — a drawing that never zooms also never moves — so they now assert the
  readout moved to 150% as well as that the point held. And zoom was once on a slider whose tests
  drove it with an arrow key: they passed on Firefox while the mouse drag did nothing, which is
  how a broken control reached a reader. Scroll a control into view before pointing at it, too: an
  element below the fold receives no mouse event at all, so the test exercises nothing and "passes"
  for the wrong reason.
- **The planner's output is pinned by a golden test** in `PlanWrapLogicTests`. Read its comment
  before touching `Pack` or `SheetOptions`.
