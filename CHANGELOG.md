# Changelog

All notable changes to godot-framework will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Framework adheres to **0.x.y** versioning while pre-1.0 — minor bumps
(`0.x.0`) signal breaking changes, patch bumps (`0.x.y`) signal additive
changes.

---

## [v0.5-alpha] - 2026-10-07

> Phase 1 W3 — ScreenManager subsystem + internal `UIPanelBase` rename.
> Released for Match3 `SwapScreen` → `ScreenManager.Instance.ShowAsync<T>()`
> migration (W3 Day 4-5). 4 W3 commits stacked on `feature/screenmanager`
> branch.
>
> **Build baseline**: continues v0.3 / v0.4-alpha's Godot.NET.Sdk 4.7.2 +
> .NET 9 + C# 12. **No SDK upgrade** this release.

### Changed

- **Internal**: `UIPanelBase` → `ManagedNodeBase` class rename. Public
  API (`UIPanel` / `UIPanel<T>` / `UIPanel<T1,T2>` / `Screen` / `Screen<T>`
  / `Screen<T1,T2>`) unchanged. Consumers not affected unless they
  directly subclassed `UIPanelBase` (rare — the doc comments always told
  users to subclass `UIPanel` / `UIPanel<T>` / `UIPanel<T1,T2>` instead).
  Both `UIPanel` and `Screen` hierarchies now share `ManagedNodeBase` as
  their internal base — the shared machinery (`IsTopOfStack`,
  `BindInput` helpers, abstract `InvokeOnClose` + `GetCloseTask`) was
  duplicated-as-`UIPanelBase` only; now lives on `ManagedNodeBase` for
  both. `InvokeOnOpen` is virtual on the new base with a defensive
  `NotSupportedException` default; only `UIPanel` variants override it
  (`Screen` variants use their own `InvokeOnShow` declared on `Screen`).

### Added

- **`ScreenManager` : `GameService`** (`src/GameFramework/ScreenManager.cs`,
  ~120 LOC): thin Node wrapper (autoload). `Instance` static + `Current`
  property + `IsShowing` + `ScreenShown` / `ScreenClosed` events + 3
  `ShowAsync` overloads (no-arg / typed open / typed open + typed
  result). CanvasLayer @ layer 99 — sits below UI panels (layer 100) so
  modal dialogs can overlay the active screen. Force-cleanup on
  `_ExitTree`. POCO split: state machine lives in
  `ScreenRouter` (next bullet) — wrapper just owns the Godot scene-tree
  attachment.

- **`ScreenRouter` POCO** (`src/GameFramework/ScreenRouter.cs`, ~150 LOC):
  show/close state machine. No Godot imports — 4 constructor seams
  (`Func<Type, Screen> screenFactory` / `Action<Screen> attach` /
  `Action<Screen> detach` / `Action<string>? errorLogger`) let the
  manager inject AddChild/RemoveChild + GD.PrintErr while tests inject
  factory + no-op attach/detach + capturing logger. Mirrors the
  `EventDispatcher` POCO + `EventBus` Node-wrapper split.

- **`Screen` / `Screen<TOpenArg>` / `Screen<TOpenArg, TCloseResult>`**
  (`src/GameFramework/Screen.cs` + `ScreenT.cs` + `ScreenT1T2.cs`):
  abstract base classes extending `ManagedNodeBase`. Sibling inheritance
  (each variant declares its own typed `OnShow` / `OnClose` virtuals,
  mirroring the `UIPanel` family pattern). `Screen<T1,T2>.CloseScreen(result)`
  sets the typed `TaskCompletionSource<TCloseResult>` + propagates
  `result` to the typed `ShowAsync<Task<TCloseResult>>` awaiter.

- **`ScreenManagerDemo` sample** (`samples/Demo/TitleScreen.cs` +
  `GameScreen.cs` + `ScreenManagerDemo.cs`, integrated into
  `DemoBootstrap.cs` Step 4 + `project.godot` autoload): exercises
  non-typed `ShowAsync<TitleScreen>()` + typed
  `ShowAsync<GameScreen, string, string>('world1')` returning
  `'score:100'`. The smoke test now prints `[DemoBootstrap] Step 4:
  ScreenManager demo` + the demo output (incl. `OnShow` / `OnClose`
  lifecycle calls on each screen) after the existing UI flow.

- **`ScreenManagerTests` xUnit project** (`tests/ScreenManagerTests/`):
  **30 tests** (30 passed + 0 skipped, ~85ms under `dotnet test`).
  Three fixtures:
  - `ScreenManagerContractTests.cs` (12): structural — `ScreenManager`
    sealed / `GameService` subclass / `Instance` static with private
    setter / `Current` read-only / 3 `ShowAsync` overloads (1/2/3 generic
    args matching §4 draft) / `Screen` abstract `: ManagedNodeBase` /
    `Screen<T>` + `Screen<T1,T2>` abstract + sibling inheritance /
    `CloseScreen` overloads / private `_router` field.
  - `ScreenManagerLifecycleTests.cs` (8): IL-shape reflection —
    `_Ready` / `_ExitTree` are public void no-args overrides rooted at
    `Godot.Node`, calls `GameService._Ready()` / `GameService._ExitTree()`
    / `set_Instance` for singleton registration + cleanup.
  - `ScreenRouterTests.cs` (10): POCO behavior — 3 initial-state, 5
    Mark-runtime (default screen invokes OnShow / typed open passes arg
    / typed close result propagates / sequential lifecycle / throw on
    overlapping ShowAsync), ScreenShown/ScreenClosed events,
    error-logger seam captures OnShow throws. Uses
    `FormatterServices.GetUninitializedObject` to create test Screen
    instances without invoking the `Control` base ctor (which would
    call Godot native bindings unavailable under plain `dotnet test`).

- **`project.godot` autoload**: adds `ScreenManager="*res://src/GameFramework/ScreenManager.cs"`
  (4th autoload entry after `GameFramework` / `UIManager` / `EventBus`).
  Autoload order: `GameFramework` first (so `Game.Instance` exists when
  the others' `_Ready` runs); the other three can be in any order.

### Fixed

- **Day-1 (`00f69b4`) latent bug — `OnClose` never fired on user screens.**
  `ScreenManager.ShowInternalAsync` finally block did NOT invoke
  `InvokeOnClose` before detaching (only `UIPanelManager.FinalizePop`
  did). This was caught by Day-2's `ScreenRouterTests.OnCloseCalled`
  assertion. Day-2 commit `1dabb9b` fixed by adding
  `try { screen.InvokeOnClose(null); } catch …` in `ScreenRouter`'s
  finally block (matches `UIPanel.FinalizePop` pattern). Verified in
  Day-3 smoke output: `[TitleScreen] OnClose` and
  `[GameScreen] OnClose(result='score:100')` both fire.

### Documentation

- **`README.md`**: Status bumped to `v0.5-alpha 🚧`. "What this is"
  adds the ScreenManager module (4th). Repository layout 10 → 15
  `.cs` files (UIPanelBase → ManagedNodeBase + Screen + Screen<T> +
  Screen<T1,T2> + ScreenRouter + ScreenManager).
- **`FORK_CHECKLIST.md`**: Last verified bumped to `7e30ebd`. Stable
  fields adds `ScreenManager` autoload. Volatile fields adds
  `ScreenRouter` + `Screen` public types + `ScreenManagerTests` row.
  New §"Using ScreenManager (since v0.5-alpha)" with 3-step consumer
  integration + ScreenManager internal-fields table (mirror of the
  EventBus section).
- **`tests/README.md`**: Layout updated with `ScreenManagerTests` row.
  Total bumped 51 → 81 tests. New fixture sections for the 3 ScreenManager
  test classes.

---

## [v0.4-alpha] - 2026-10-06

> Phase 1 W2 — EventBus subsystem (the first framework feature
> prioritized because Match3 already reinvented it inline). Released
> for Match3 `PhaseChanged` migration (W2 Day 3-4).
>
> **Build baseline**: continues v0.3's Godot.NET.Sdk 4.7.2 + .NET 9 +
> C# 12. **No SDK upgrade** this release.

### Added

- **`EventDispatcher` + `EventBus`** (commits `576fea4`, `8c81bdd`):
  - `src/GameFramework/EventDispatcher.cs` (new, ~140 LOC): pure POCO
    pub/sub bus. AOT-friendly (no reflection, no source generators).
    Synchronous publish in registration order, error-isolated (a
    throwing subscriber is caught and logged), type-keyed
    (`Dictionary<Type, List<Delegate>>`). Injected `Action<string>
    errorLogger` constructor seam (defaults to `GD.PrintErr`) lets
    tests inject silent or capturing loggers to bypass the native
    dependency under plain `dotnet test`.
  - `src/GameFramework/EventBus.cs` (new, ~70 LOC): thin Node wrapper
    (`sealed partial class EventBus : GameService`). Sets `Instance`
    after `base._Ready()` (so ServiceRegistry registration runs first),
    clears `Instance` in `_ExitTree`. Publish/Subscribe forward to the
    internal `EventDispatcher`. Matches `design_v0.2.md` §3 draft exactly.

- **`EventBusDemo` sample** (`samples/Demo/EventBusDemo.cs`, ~70 LOC +
  integration into `DemoBootstrap.cs` + new autoload entry in
  `project.godot`): exercises Subscribe with `using var` for scope-bound
  auto-unsubscribe, Publish to multiple subscribers in registration order,
  type-keyed routing (two event types share one bus), and diagnostics
  (`EventTypeCount`, `SubscriberCount`). The smoke test now prints
  `[DemoBootstrap] Step 3: EventBus demo` + the demo output after the
  existing UI flow.

- **`EventBusTests` xUnit project** (`tests/EventBusTests/`): **27 tests**
  (27 passed + 0 skipped, ~50ms under `dotnet test`). Three fixtures:
  - `EventBusTests.cs` (12): EventDispatcher POCO surface — Subscribe,
    Publish, Unsubscribe, error isolation, `using var` Dispose,
    multiple-Dispose idempotency, type-keyed isolation, `EventTypeCount`,
    `SubscriberCount`, logger formatting.
  - `EventBusContractTests.cs` (6): EventBus structural — sealed,
    `GameService` subclass, static `Instance` property + private setter,
    `_Ready`/`_ExitTree` overrides, Publish/Subscribe arity match,
    `EventDispatcher` public + sealed.
  - `GameServiceLifecycleTests.cs` (9): EventBus lifecycle IL-shape
    reflection tests — verifies (without instantiating) that `_Ready`
    calls `GameService._Ready()` + `set_Instance`, `_ExitTree` calls
    `GameService._ExitTree()` + `get_Instance` + `set_Instance`, and the
    override chain roots at `Godot.Node`. The IL scanner handles both
    single-byte opcodes and `0xFE` two-byte prefixes (e.g. `ceq`) so it
    walks the full method body correctly.

- **`project.godot` autoload**: adds `EventBus="*res://src/GameFramework/EventBus.cs"`
  in the `[autoload]` section (after `GameFramework` + `UIManager`).

### Fixed

- **EventBus dispatcher tests aren't subject to the Phase 0 native-abort
  pitfall** (`GD.PrintErr` native-crashing under plain `dotnet test`):
  the EventDispatcher's `Action<string> errorLogger` constructor seam
  allows tests to inject a silent/capturing logger, so the
  `SubscriberThrows_*` tests run cleanly under `dotnet test` without
  skipping. (Phase 0's 1 skipped test
  `ServiceRegistryTests.Register_DifferentInstance_ReplacesAndWarns`
  is still skipped — the fix is for new code, not existing
  `ServiceRegistry.Register` warnings. Tracked for v0.3.1 patch.)

### Documentation

- README "Step 4: Use it" now includes a Subscribe/Publish/`using var`
  Dispose token snippet for EventBus (1-2 paragraphs C#).
- README "Quick start (running our sample)" expected output now includes
  the Step 3 EventBusDemo lines.
- README status: `v0.3-prep 🚧` → **`v0.4-alpha 🚧`**.
- README "Explicitly out of scope" — removes "event bus".
- README "Repository layout" — adds `EventDispatcher.cs`, `EventBus.cs`,
  `EventBusDemo.cs`, `tests/EventBusTests/`.
- README "Integration §Step 3" — adds the EventBus autoload line
  (optional, only if consumer uses EventBus).
- `tests/README.md` — updated with full EventBusTests fixture
  documentation (51 tests total across both projects: 50 passed + 1 skipped).
- `FORK_CHECKLIST.md` — updated with EventBus API surface + consumer
  wiring instructions (see FORK_CHECKLIST.md §"Using EventBus
  (since v0.4-alpha)").

### Known Issues

- **Full `GameService._Ready()` / `_ExitTree()` instantiation tests**
  still deferred — require `godot --headless` test runner. EventBus's
  IL-shape reflection tests cover the lifecycle contract from another
  angle (see `tests/EventBusTests/GameServiceLifecycleTests.cs`).
- **Godot headless `--quit` shutdown leak warnings** (~22 CanvasItem
  RIDs + ~77 ObjectDB instances) still appear on `--quit`. Engine
  noise, not a framework regression.

---

## [v0.3] - 2026-10-05

> Maintenance release. Phase 0 + Phase 1 in progress at the time of
> release; Phase 1 W2 (this PR) is the next entry above.
> See [`docs/design_v0.2.md` §11 v0.3 roadmap](docs/design_v0.2.md#v03-路线图)
> for the full tracking table (12 tasks across Phase 0 / 1 / 2).

### Added

- **xUnit test suite** (commit `pending`): `tests/ServiceFrameworkTests/`
  with **24 tests** (23 passed + 1 skipped under `dotnet test`). POCO-only —
  no Godot engine runtime required. Covers `ServiceRegistry` API surface,
  the "register under derived type" principle (README §"Pitfall #1"), and
  structural contract of `GameService`. See `tests/README.md` for scope
  and the rationale for the 1 skipped test (which exercises `GD.PrintErr`).
- **`FORK_CHECKLIST.md`**: consumer-facing documentation of all framework
  fields that may change on submodule sync (`assembly_name`,
  `<AssemblyName>`, `config_version`, `config/features`, `<TargetFramework>`,
  etc.) + the standard `<Compile Remove>` recipe required for consumers
  using SDK default glob.

### Changed (build)

- **SDK upgrade** (commit `cee6c66`): `Godot.NET.Sdk` 4.5.0 → **4.7.2**;
  `.NET` 8.0 → **9.0**. Verified `dotnet build` clean (0 warnings, 0 errors)
  on framework + template (which uses framework via SDK default Compile glob).
- Framework `project.godot`: `config/features` "4.5" → **"4.7"**;
  `config/description` updated to v0.3.
- Trailing newline added to `GodotFramework.csproj` (.editorconfig /
  GitHub-rendering hygiene).
- **Framework `GodotFramework.csproj` default-glob hardening** (commit
  `pending`): added `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>`
  + explicit `<Compile Include="src/GameFramework/**/*.cs" />` +
  `<Compile Include="samples/Demo/**/*.cs" />`. Without this, the SDK's
  default `**/*.cs` Compile glob would silently pull in any new
  subdirectory added to the framework repo (e.g., the new `tests/`).
  See README §"Integration" §"default Compile glob".

### Removed

- **Half-finished panel-cache scaffolding** (commit `c40c030`):
  - `CreatePolicy` enum (was unused — `UIManager.AcquireCodeOnly` ignored the policy parameter)
  - `OnInitialize()` lifecycle hook (empty virtual, never called)
  - `_cache` field + write-only asymmetry (memory leak vector — detached panels were kept alive)
  - `InitializePanel()` static hook (empty method tied to `OnInitialize`)
- All four removal sites marked with `TODO(v0.3)` markers in source for future reintroduction.

### Documentation

- README: new **"Avoiding Common Pitfalls"** section (4 documented gotchas with code examples)
- README: new **default Compile glob warning** in Integration §Step 2 (with `<Compile Remove="addons/godot-framework/samples/**/*.cs" />` recipe)
- README: SDK versions bumped throughout Pattern A / B xml examples + Quick start
- README: status / target / author metadata refreshed
- `docs/design_v0.1.md` → **`docs/design_v0.2.md`** (content sync + §11 v0.3 roadmap + §12 maintenance notes)

### Known Issues

- **Godot headless `--quit` shutdown leak warnings**: ~22 CanvasItem RIDs +
  ~77 ObjectDB instances leak on `--quit`. These are Godot engine shutdown
  noise (not framework bugs) and also reproducible on vanilla Godot headless
  demos. The framework itself produces 0 warnings/errors during normal
  runtime.

---

## [v0.2] - 2026-09-18

### Added

- **Submodule-ready refactor**: `src/GameFramework/` (library code) +
  `samples/Demo/` (sample consumer) separated.
- README integration section covering Pattern A (`<Compile Include>`) and
  Pattern B (`<ProjectReference>`).
- `docs/design_v0.1.md` added with full architecture write-up (candidates
  compared, final design, lessons-learned pitfalls).

### Fixed

- v0.2.1 (commit `cd8439f`): rename `GameFramework` class to `Game` to avoid
  `CS0234` namespace/class name collision. Autoload NODE name stays
  `GameFramework`.
- v0.2.2 (commit `cf10531`): `UIManager._Ready` calls `base._Ready()` so
  `GameService` auto-registration runs.
- v0.2.3 (commit `fc3193d`): `GameService._Ready` registers under the
  derived type via `Register(this.GetType(), this)` — see "Avoiding Common
  Pitfalls" §1 in README.

---

## [v0.1] - 2026-09-18

### Added

- **Initial MVP**:
  - `ServiceRegistry` — AOT-friendly type-keyed service container
    (`Register` / `Register(Type, obj)` / `TryGet` / `Resolve` / `Unregister`)
  - `GameService` — abstract `Node` base for Node-based services with
    auto-registration on `_Ready` / auto-unregistration on `_ExitTree`
  - `UIPanel` / `UIPanel<TOpenArg>` / `UIPanel<TOpenArg, TCloseArg>` —
    three typed UI panel variants with `OnOpen` / `OnClose` / `ClosePanel`
  - `UIManager` — push/pop panel stack with awaitable results, panel-scoped
    input bindings
- 8 .cs files in `src/GameFramework/`, ~900 LOC.
- Sample demo in `samples/Demo/`: `AudioService` (POCO), `MainMenuPanel`,
  `SettingsPanel`, `ConfirmDialog`, `DemoBootstrap` — exercises every
  `UIPanel` variant and the service registry round-trip.
- Headless smoke test infrastructure (`godot --headless --quit-after 300`).
- Documentation: `README.md`, `docs/design_v0.1.md`, this `CHANGELOG.md`.
