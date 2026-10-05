# Changelog

All notable changes to godot-framework will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Framework adheres to **0.x.y** versioning while pre-1.0 — minor bumps
(`0.x.0`) signal breaking changes, patch bumps (`0.x.y`) signal additive
changes.

---

## [v0.3] - WIP

> Phase 0 + Phase 1 in progress. See [`docs/design_v0.2.md` §11 v0.3 roadmap](docs/design_v0.2.md#v03-路线图)
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
