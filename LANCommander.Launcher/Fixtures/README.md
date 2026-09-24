# View fixtures

A fixture is one screen of the launcher in one state, such as the library as a grid, a game mid-install,
or the packaging wizard's file step. It is built from canned data, so it renders the same way every time.
Fixtures are compiled into Debug builds only.

The same fixtures serve two purposes:

- **Visual regression tests.** `FixtureVisualTests` in `LANCommander.Launcher.Tests` renders every
  fixture headlessly with the real theme. It compares each render against `Baselines/<fixture name>.png`.
- **Previewing in the app.** Start a Debug build with `LANCOMMANDER_FIXTURE` set to a fixture's name to
  open that fixture. Set it to anything else (`all`, say) to pick from a list. The preview uses no
  settings, server or database, so it can run beside a normal launcher.

## How a fixture is built

`FixtureContext` provides the launcher's real service registrations, isolated from everything outside:

- a scratch settings file and an empty in-memory database
- no LAN scan, notifications or taskbar progress
- a pinned culture, clock and free-space figure

A fixture builds its view models from those registrations, fills them in directly, and returns what to
show. Nothing is loaded; fixtures never call `Load*`, `Initialize*` or `Start*` methods.

- `context.ShellWindow(page)` gives the main window with the shell around a page. Most fixtures use this.
- `context.MainWindow(view)` gives the main window around a view outside the shell (splash, login).
- Returning any other control hosts it in a plain window of the fixture's `Width` and `Height`.
- `Prepare` runs once the window is shown, for state that only exists in the visual tree: opening an
  overlay, switching a tab, scrolling.

Games, art and other sample data live in `FixtureGames` and `FixtureArt`. Art is drawn from the game's
title and written to the temp folder, so covers, backgrounds, icons and logos look real without shipping
any.

Where a view model keeps the needed state private, a Debug-only `*.Fixture.cs` partial next to it adds a
narrow seeding method, for example `ShellViewModel.InitializeForFixture` or
`GamesCollectionViewModel.SeedFixture`.

`FixtureHost` shows a fixture the same way for the tests and the preview. Before handing the window
back, it:

- waits for asynchronously loaded images (`Helpers/PendingLoads`)
- draws charts
- stops running transitions

## Adding a fixture

1. Add a `ViewFixture` to the matching set in `Sets/`, or add a new set and list it in
   `FixtureCatalog`. Name it `Area.State` (for example `Library.Grid`). The name is also the baseline's
   file name.
2. Keep it deterministic:
   - Take ids, dates and paths from `FixtureGames`, `FixtureContext.Now` and literal strings, not from
     `Guid.NewGuid()` or `DateTime.Now`.
   - Write Windows paths out by hand; `Path.Combine` gives different separators on Linux.
3. Create its baseline (below) and look at it before committing.

`EveryViewAppearsInAFixture` fails when a view under `LANCommander.Launcher.Views` appears in no fixture.
A new view therefore needs a fixture, or an entry in that test's `UncoveredViews` explaining why not.

## Baselines

```
# Compare against the committed baselines
dotnet test LANCommander.Launcher.Tests --filter "FullyQualifiedName~FixtureVisualTests"

# Create or refresh baselines: every capture is written over its baseline
UPDATE_VISUAL_BASELINES=1 dotnet test LANCommander.Launcher.Tests --filter "FullyQualifiedName~FixtureVisualTests|FullyQualifiedName~ViewInteractionTests"
```

On a failure, the run writes the capture to `bin/Debug/net10.0/Screenshots/` and a diff to
`bin/Debug/net10.0/Diffs/`.

Text renders slightly differently on each OS, and CI runs on Linux. Baselines meant for CI should come
from the **LANCommander Launcher Tests — Update Visual Baselines** workflow, which regenerates them all
and commits the result.
