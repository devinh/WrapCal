---
name: unit-tests
description: >
  The TUnit + bUnit suite in tests/WrapCal.UnitTests — how to write a test, the BunitContext
  name clash, awaited assertions, and the reflection-mode catch with .razor test files. Use
  when adding or changing anything under tests/WrapCal.UnitTests.
---

# The unit and component suite

```bash
dotnet test --project tests/WrapCal.UnitTests
```

The `dotnet test` runner is Microsoft.Testing.Platform — see the test runner note in `CLAUDE.md`
for what that costs you (`--filter` does not work).

## Writing one

- **TUnit** (`1.75.0`); **bUnit** (`2.11.3`) for component rendering.
- bUnit v2's context class is **`BunitContext`** — inherit and call `Render<T>()`.
  (It still exposes `TestContext`, but `BunitContext` avoids a clash with TUnit's own.)
- TUnit auto-configures global usings, so `[Test]` and `Assert.That(...)` need no `using`.
- TUnit assertions are **awaited**:

  ```csharp
  await Assert.That(cut.Markup).Contains("WrapCal");
  ```
- Test files mirror production: `Common/ResultTests.cs`, `Components/MainLayoutTests.cs`,
  `Features/*Tests.cs`.

## The .razor trap

**`.razor` test files require TUnit's reflection mode.** In the default source-generation mode,
TUnit and the Razor SDK are both source generators and neither can see the other's output, so
Razor-generated test classes are invisible to discovery. Adding `[assembly: ReflectionMode]`
(`TUnit.Core`) — or `TUNIT_EXECUTION_MODE=reflection` — makes them discoverable; that is the
documented purpose of reflection mode, and it is what bUnit's own docs omit when they say TUnit
"does not work with razor files".

**This project stays on the default mode:** our tests are `.cs`, which source generation
handles, and reflection mode is the slower of the two. Reach for it only to author `.razor` test
components, which additionally needs `Microsoft.NET.Sdk.Razor`. (If you go fully reflection,
`<EnableTUnitSourceGeneration>false</EnableTUnitSourceGeneration>` skips generation entirely for
faster builds.)

## Testing what the browser would show

bUnit has no media query and no operating system, so anything that depends on the machine is
arranged by stubbing the single interop call the component makes rather than by faking the
environment — `SetThemePreference(choice, system)` stubs `wrapcalTheme.load`, and with no setup
that call answers nothing, which is what a first visit looks like.

What such a stub cannot prove belongs in the functional suite: `Theme.feature` is the only place
System can be tested, because only a real browser has a `prefers-color-scheme`. The split is
described in the `theme` skill.
