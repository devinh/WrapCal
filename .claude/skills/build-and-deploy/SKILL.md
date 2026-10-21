---
name: build-and-deploy
description: >
  The Cake build script in cake/, the three GitHub workflows, and how the site reaches GitHub
  Pages — including why the cake project is in the solution yet never built. Use when changing
  cake/cake.cs, cake.sh, a .github/workflows file, the solution file, or anything about what
  ships and where.
---

# Building and shipping

Every build and test command lives in `CLAUDE.md`; this is what sits behind them.

## Cake

```bash
./cake.sh                            # Clean → Build → Tests (default target)
./cake.sh --target=UnitTests
./cake.sh --target=FunctionalTests
./cake.sh --target=Publish           # the site, into ./output
./cake.sh --target=Release           # every suite, then publish — what CI runs
./cake.sh --target=InstallBrowsers
```

- **The Cake project in `cake/` is in `WrapCal.slnx` but carries `<Build Project="false" />`.**
  The tooling sees it — `dotnet format` formats `cake.cs` with the rest of the repo — while
  `dotnet build` at the root never builds the build script. That exclusion is load-bearing: Cake
  runs the script it built, so a solution build that rebuilt and rewrote `cake.dll` underneath a
  running one would race itself. Verified: touching `cake/cake.csproj` and building the solution
  leaves `cake/obj` and `cake/bin` untouched — the element excludes it from restore as well as
  from compile.
- It targets `net10.0` via `Cake.Sdk`, and its script runs with the repository root as the
  working directory (`RunWorkingDirectory`), which is why every path inside `cake.cs` is
  relative to the root.
- **`Release` runs `Tests`, so the deploy is gated on the functional suite as well as the unit
  one.** That is deliberate: a dependency bump that changes how the page looks is exactly what
  would otherwise reach the live site unnoticed.
- **`Tests` is not a `dotnet test` of the solution** — it depends on `UnitTests` and
  `FunctionalTests`, one target per test project, so a suite runs the same way alone as it does
  in the pair. Both depend on `Build`, which Cake runs once. Add a third suite as another task
  and a third dependency, not as a branch inside `Tests`.
- `FunctionalTests` depends on `InstallBrowsers`, so a clean machine — a runner especially —
  provisions its own browser; the target is idempotent and takes about two seconds once the
  browser is there.
- **`Publish` never passes `--no-build`**, even straight after `Build`: the `#[.{fingerprint}]`
  placeholders in `index.html` are rewritten during publish, not during build. `AssertSiteIsBootable`
  then checks the published `index.html` for one that survived — the one failure that still
  deploys and still serves, with a blank page as its only symptom — and fails the run.
- `cake.csproj` pins `System.Security.Cryptography.Pkcs` forward: `Cake.Sdk` pulls 10.0.12
  transitively, which trips NU1902 and NU1903. Without the pin the Cake build reports four
  vulnerability warnings, so keep it until `Cake.Sdk` moves to a fixed version.

## Workflows

- `deploy-pages.yml` on push to `main` runs `--target=Release` and uploads `output/wwwroot`.
- `ci.yml` on pull requests runs `--target=Tests`, and is what a Renovate PR waits on before it
  can merge itself.
- `renovate.yaml` runs daily as the `devinh-renovate` App.

**The deploy workflow calls Cake and nothing else.** `Release` is the target it asks for, so what
the artifact is, where it lands and what gates it are all `cake/cake.cs`'s business.
`.github/workflows/deploy-pages.yml` only moves the result onto Pages. Do not add `dotnet` steps
there: a workflow that rebuilds the way it remembers is a second build definition that drifts
from the first.

## Hosting

- The site is a GitHub project page, `https://devinh.github.io/WrapCal/`, so it lives under a
  subpath rather than at an origin root. That is why `wwwroot/index.html` raises its own
  `<base href>` to the directory it was loaded from, and why that script has to run **before**
  the links and scripts below it — a relative URL is resolved against the base in force when its
  element is parsed, not when it is fetched.
- Deploying through Actions skips Jekyll, which is why `_framework/` needs no `.nojekyll`;
  a branch-based deployment would need one.
- Pages has to be turned on for the repository once, with *GitHub Actions* as the source, under
  Settings → Pages.
