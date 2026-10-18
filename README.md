# WrapCal

Plan how much wrapping paper a set of gift boxes needs.

Given box dimensions and the usable width of a paper roll, WrapCal aims to produce a
cut plan: where to cut the roll, how much roll length the boxes consume, and how much
paper is left as waste.

> **Status: early scaffold.** The Blazor shell, build config, and test setup exist.
> No wrap-planning logic is implemented and the only page is a placeholder.

## Status

| | |
|---|---|
| Blazor shell (.NET 10, static SSR) | ✅ Done |
| Build, test, and lint config | ✅ Done |
| Wrap-planning domain logic | ❌ Not started |
| Planning UI | ❌ Not started |
| Persistence | None by design — the app is stateless |

Nothing in the app currently reads or produces wrap plans.

## Requirements

- **.NET SDK 10.0.x** — developed against `10.0.402`
- No database, no external services, no secrets

## Getting started

```bash
# Build
dotnet build

# Run — serves http://localhost:5127 and https://localhost:7002
dotnet run --project src/WrapCal

# Run with hot reload
dotnet watch --project src/WrapCal

# Test
dotnet test
```

## Project layout

```
WrapCal.slnx                 # solution
global.json                  # opts dotnet test into Microsoft.Testing.Platform mode
Directory.Build.props        # shared compiler + analyzer settings
.editorconfig                # code style, enforced at build time
CLAUDE.md                    # conventions and architecture notes

WrappingPaperOptimizer.cs    # domain reference — NOT compiled (see below)

src/
  WrapCal/                   # the app (Blazor Web App)
    Components/
      Layout/                # MainLayout, NavMenu, ReconnectModal
      Pages/                 # Home, Error, NotFound
      App.razor, Routes.razor, _Imports.razor
    wwwroot/                 # static assets (Bootstrap, app.css)
    Program.cs
    WrapCal.csproj

tests/
  WrapCal.Tests/             # xUnit v3 + bUnit, references src/WrapCal
    HomePageTests.cs
```

## Testing

Tests use **xUnit v3** with **bUnit** for component rendering. `global.json` opts
`dotnet test` into .NET 10's **Microsoft.Testing.Platform** mode, which has two
consequences worth knowing before you edit the test project:

- Test projects are **executables** (`<OutputType>Exe</OutputType>`) — that is expected,
  not a mistake.
- `Microsoft.NET.Test.Sdk` is **not** used. Don't add it back, or the test host will
  refuse to run.
- Arguments meant for the test app go after a `--` separator, e.g. `dotnet test -- --report-trx`.

## Domain reference: `WrappingPaperOptimizer.cs`

The repository root holds a standalone, working implementation of the wrapping domain
(namespace `GiftWrapOptimizer`) covering box/roll models, unit conversion, and a
shelf-based packing optimizer with randomised restarts.

**It is not part of the build and is never compiled** — the web project only globs
`src/WrapCal/**/*.cs`. The build will not fail if you break it.

Treat it as a **specification**: port what you need into a feature slice and adapt it,
rather than assuming its types are available.

## Architecture

WrapCal uses **vertical slice architecture** — code is organized by feature, not by
technical layer, and there is no shared `Services/` or `Models/` folder. New work goes
in `src/WrapCal/Features/<FeatureName>/`, containing that feature's page, logic, and
models together.

See [CLAUDE.md](CLAUDE.md) for the full conventions, coding standards, and anti-patterns.
