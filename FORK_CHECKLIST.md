# Framework FORK_CHECKLIST

This document lists the **fields that may change when godot-framework
upgrades** and need to be re-verified (or updated) by consumer repos that
have this framework as a git submodule. Use it as a pre-flight / post-sync
checklist whenever you bump the framework's submodule HEAD.

Last verified against: `7e30ebd` (v0.5-alpha, on branch `feature/screenmanager`).

---

## Stable framework-side fields

These are unlikely to change between minor versions, but listed here so
consumers can verify them after a sync.

| Field | Current value | Location | Notes |
|---|---|---|---|
| Submodule HEAD | `8c81bdd` (no pin — every PR reviewed manually) | `git -C addons/godot-framework log --oneline -1` | Branch `feature/eventbus` until merged to main. |
| `assembly_name` | `GodotFramework` | `project.godot` (line `project/assembly_name="..."`) | Must match `<AssemblyName>` in csproj. |
| `<AssemblyName>` | `GodotFramework` | `GodotFramework.csproj` | Compiled DLL filename. |
| `<RootNamespace>` | `GameFramework` | `GodotFramework.csproj` | C# namespace all framework code lives in. |
| `config_version` | `5` | `project.godot` (line `config_version=5`) | Godot project format version. |
| `config/features` | `("4.7", "C#", "Forward Plus")` | `project.godot` (line `config/features=PackedStringArray(...)`) | Godot version + capabilities. |
| `<TargetFramework>` | `net9.0` | `GodotFramework.csproj` | .NET TFM. |
| `<Godot.NET.Sdk>` version | `4.7.2` | `GodotFramework.csproj` (both `Sdk="..."` and `<PackageReference>`) | Tracks Godot version. |
| `<EnableDefaultCompileItems>` | `false` | `GodotFramework.csproj` | **Always false** — framework uses explicit Compile whitelist. |
| `<EnableDynamicLoading>` | `true` | `GodotFramework.csproj` | Hot-swap DLL loading for dev iteration. |
| Compile whitelist | `src/GameFramework/**/*.cs` + `samples/Demo/**/*.cs` | `GodotFramework.csproj` (two `<Compile Include>` lines) | Anything else in framework repo (e.g., `tests/`, future `tools/`) is **excluded** from the framework assembly. |
| Autoload class name | `Game` (NOT `GameFramework`) | `src/GameFramework/Game.cs` | See README §"Avoiding Common Pitfalls" §2 — namespace and class can't share a name (CS0234). The **autoload NODE name** in `project.godot` is `GameFramework` — that's just a Godot-side identifier. |
| Autoload class names (additional) | `UIManager`, `EventBus`, `ScreenManager` | `src/GameFramework/UIManager.cs`, `src/GameFramework/EventBus.cs`, `src/GameFramework/ScreenManager.cs` | Added in v0.3 (UIManager), v0.4-alpha (EventBus), and v0.5-alpha (ScreenManager). All three are `GameService` subclasses. Their corresponding autoload NODE names are `UIManager`, `EventBus`, and `ScreenManager` in `project.godot`. |
| EventDispatcher public types | `EventDispatcher`, `EventBus`, `ScreenRouter`, `ScreenManager`, `Screen` / `Screen<T>` / `Screen<T1,T2>` | `src/GameFramework/EventDispatcher.cs`, `src/GameFramework/EventBus.cs`, `src/GameFramework/ScreenRouter.cs`, `src/GameFramework/ScreenManager.cs`, `src/GameFramework/Screen.cs` + `ScreenT.cs` + `ScreenT1T2.cs` | Added v0.4-alpha (EventDispatcher/EventBus) and v0.5-alpha (ScreenRouter/ScreenManager/Screen variants). Both POCO + Node-wrapper pairs follow the same split pattern: a pure POCO (`EventDispatcher`, `ScreenRouter`) + a Node wrapper (`EventBus`, `ScreenManager`) that owns the static `Instance` pointer. The 3 Screen variants mirror the 3 UIPanel variants (no-arg / typed open / typed open + result). |

## Volatile framework-side fields

These may change between minor versions. Consumers should diff the
framework repo on every sync and check for impact.

| Field | Range / pattern | Notes |
|---|---|---|
| Public API surface | 15 .cs files in `src/GameFramework/` (namespace `GameFramework`) | Public types + methods: `ServiceRegistry`, `GameService`, `Game`, `UIManager`, `ManagedNodeBase` (internal), `UIPanel*`, `EventBus`, `EventDispatcher`, `ScreenManager`, `ScreenRouter`, `Screen*`. Check `git diff` for added / removed / renamed. |
| Subfolder layout | `src/GameFramework/` (library) + `samples/Demo/` (sample consumer) | `samples/` is for framework's own smoke test, **not** meant to ship in consumer builds. |
| Tests subfolder | `tests/ServiceFrameworkTests/` (v0.3-prep) + `tests/EventBusTests/` (v0.4-alpha) + `tests/ScreenManagerTests/` (v0.5-alpha) | xUnit POCO tests. Not compiled into framework assembly. May grow. Total now **81 tests** (80 passed + 1 skipped) — see `tests/README.md`. |
| `samples/Demo/` content | Mirrors new framework features | Updated alongside library additions. `EventBusDemo.cs` added v0.4-alpha (invoked from `DemoBootstrap.cs` Step 3). v0.5-alpha adds `TitleScreen.cs` + `GameScreen.cs` + `ScreenManagerDemo.cs` (invoked from Step 4). |

## Consumer-side actions required on framework upgrade

After bumping the submodule HEAD, do these in the consumer repo:

### 1. Default-glob `<Compile Remove>` (only for Pattern C consumers)

If your consumer `.csproj` is `<Project Sdk="Godot.NET.Sdk/X.Y.Z">` with
**no explicit `<Compile Include>` and no `<ProjectReference>`** (i.e., the
SDK default Compile glob is in effect), add this to opt out of
non-library framework subdirectories:

```xml
<ItemGroup>
  <Compile Remove="addons/godot-framework/samples/**/*.cs" />
  <Compile Remove="addons/godot-framework/tests/**/*.cs" />
</ItemGroup>
```

Pattern A (`<Compile Include="addons/godot-framework/src/GameFramework/**/*.cs" />`)
and Pattern B (`<ProjectReference Include="addons/godot-framework/GodotFramework.csproj" />`)
are **immune** — they only pull in what they ask for. See README
§"Integration" for the three patterns.

> **This was added in v0.3-prep** because the new `tests/` subfolder is
> now part of the framework repo. Earlier v0.2.x consumers using
> default-glob only needed `samples/` removal — they now also need
> `tests/` removal.

### 2. Smoke test must pass

```bash
cd /path/to/consumer
godot --headless --quit-after 300
```

Expected: exit code 0, demo UI flow completes (or your own game's
bootstrap prints success). Framework's own demo is in
`addons/godot-framework/scenes/demo.tscn` — but it's only invoked if your
project.godot points to it.

### 3. Check CHANGELOG.md for breaking changes

`CHANGELOG.md` at the repo root lists breaking changes under
**`### Removed`** and **`### Changed`** per version. Common breakage:

- Public type / method renames
- Autoload order changes (`project.godot`)
- Required csproj properties (e.g., `<EnableDefaultCompileItems>` if a
  future release flips the default)

### 4. Re-run `dotnet build` + your own tests

```bash
dotnet build YourProject.csproj
dotnet test YourTests.csproj
```

Both must be clean (0 warnings, 0 errors). Framework's own tests
(`addons/godot-framework/tests/ServiceFrameworkTests/`) can be re-run
as a sanity check:

```bash
cd addons/godot-framework/tests/ServiceFrameworkTests
dotnet test
```

Expected: 23 passed, 1 skipped (24 total). The 1 skipped test is the
`GD.PrintErr` warning path which requires Godot runtime.

For `EventBusTests`:

```bash
cd addons/godot-framework/tests/EventBusTests
dotnet test
```

Expected: 27 passed, 0 skipped (27 total). None skipped because the
dispatcher's `errorLogger` constructor seam (defaults to `GD.PrintErr`)
lets tests inject a silent or capturing logger, sidestepping the
`GD.PrintErr` native-abort under plain `dotnet test`.

---

## Using EventBus (since v0.4-alpha)

EventBus is **opt-in**. Your consumer repo doesn't need to change
anything if you don't use it. If you do, three small additions:

### 1. Add the `EventBus` autoload to your `project.godot`

```ini
[autoload]

GameFramework="*res://addons/godot-framework/src/GameFramework/Game.cs"
UIManager="*res://addons/godot-framework/src/GameFramework/UIManager.cs"
EventBus="*res://addons/godot-framework/src/GameFramework/EventBus.cs"
```

The autoload **order matters**: `GameFramework` must come first;
`UIManager` and `EventBus` (both `GameService` subclasses) auto-register
with `Game.Instance.Services` on `_Ready` and need `Game.Instance` to
exist at that point. List `EventBus` **after** `GameFramework` (and
**after** `UIManager` if you use both — order between UIManager and
EventBus doesn't matter).

### 2. Publish / subscribe from anywhere

```csharp
// Publish a game-wide event (synchronous, error-isolated):
EventBus.Instance.Publish(new GamePhaseChanged("playing"));

// Subscribe with scope-bound auto-unsubscribe:
using var sub = EventBus.Instance.Subscribe<GamePhaseChanged>(evt =>
    GD.Print($"phase is now {evt.NewPhase}"));
// sub.Dispose() runs at end of enclosing scope → unsubscribes
```

The `IDisposable` token is idempotent — calling `Dispose()` multiple
times is safe.

### 3. (Optional) Use `EventDispatcher` directly for non-Node contexts

`EventDispatcher` is a pure POCO (not a `Node`). Use it for tooling,
unit tests, or any context where a full Godot lifecycle is overkill:

```csharp
var bus = new EventDispatcher();                  // production: GD.PrintErr on subscriber throws
var silentBus = new EventDispatcher(_ => { });   // tests: silent error logger
using var sub = silentBus.Subscribe<MyEvent>(handler);
bus.Publish(new MyEvent(...));
```

### EventBus internal fields (may change between versions)

Listed here for visibility — consumers should treat these as private
implementation details, not as stable API:

| Field | Type | Visibility | Notes |
|---|---|---|---|
| `EventDispatcher._subscribers` | `Dictionary<Type, List<Delegate>>` | `private readonly` | Type-keyed subscriber registry. One list per event type. Snapshotted before iteration so handlers that subscribe/unsubscribe during dispatch don't break the loop. |
| `EventDispatcher._errorLogger` | `Action<string>` | `private readonly` | Injected error logger (defaults to `GD.PrintErr`). Construct with `_ => { }` for silent logging in tests. |
| `EventBus._dispatcher` | `EventDispatcher` | `private readonly` | Composition — the `EventBus` Node delegates Publish/Subscribe to this. |
| `EventBus.Instance` | `EventBus` (static property) | `public get, private set` | Set in `_Ready`, cleared in `_ExitTree` (only if `Instance == this` to avoid clobbering a successor autoload). |

## Using ScreenManager (since v0.5-alpha)

ScreenManager is **opt-in**. Your consumer repo doesn't need to change
anything if you don't use it. If you do, three small additions:

### 1. Add the `ScreenManager` autoload to your `project.godot`

```ini
[autoload]

GameFramework="*res://addons/godot-framework/src/GameFramework/Game.cs"
UIManager="*res://addons/godot-framework/src/GameFramework/UIManager.cs"
EventBus="*res://addons/godot-framework/src/GameFramework/EventBus.cs"
ScreenManager="*res://addons/godot-framework/src/GameFramework/ScreenManager.cs"
```

Autoload order: `GameFramework` first (so `Game.Instance` exists when the
others' `_Ready` runs). The other three can be in any order among themselves.

### 2. Define your screens + show them

```csharp
// Non-typed screen (no open arg, no close result):
public sealed partial class TitleScreen : Screen
{
    protected override void OnShow() { /* setup */ }
    protected override void OnClose() { /* teardown */ }

    // Button click handler:
    private void OnPlayPressed() => CloseScreen();
}

// Typed-open + typed-result screen:
public sealed partial class GameScreen : Screen<string, string>
{
    protected override void OnShow(string level) { /* setup with arg */ }
    protected override void OnClose(string result) { /* teardown with result */ }

    // Button click handler:
    private void OnEndPressed() => CloseScreen("score:100");
}

// Caller:
await ScreenManager.Instance.ShowAsync<TitleScreen>();
var score = await ScreenManager.Instance.ShowAsync<GameScreen, string, string>("world1");
```

Only one `Screen` is visible at a time. Calling `ShowAsync` while another
screen is active throws `InvalidOperationException` — close the current
screen first (await its `ShowAsync` task).

### 3. (Optional) Use `ScreenRouter` directly for non-Node contexts

`ScreenRouter` is a pure POCO (not a `Node`). Use it for tooling,
unit tests, or any context where a full Godot lifecycle is overkill:

```csharp
var router = new ScreenRouter(
    screenFactory: t => (Screen)Activator.CreateInstance(t)!,
    attach: screen => container.AddChild(screen),
    detach: screen => container.RemoveChild(screen)
);
await router.ShowAsync<MyScreen>();
```

The 4 constructor seams (`screenFactory` / `attach` / `detach` /
`errorLogger`) let you swap in no-op attach/detach + silent logger for
tests — see `tests/ScreenManagerTests/ScreenRouterTests.cs`.

### ScreenManager internal fields (may change between versions)

Listed here for visibility — consumers should treat these as private
implementation details, not as stable API:

| Field | Type | Visibility | Notes |
|---|---|---|---|
| `ScreenRouter._screenFactory` | `Func<Type, Screen>` | `private readonly` | Injected screen factory. Default `Activator.CreateInstance`. Override to inject pre-created Screen instances in tests (via `FormatterServices.GetUninitializedObject`). |
| `ScreenRouter._attach` / `ScreenRouter._detach` | `Action<Screen>` (each) | `private readonly` | Injected attach/detach callbacks. ScreenManager wires them to `AddChild`/`RemoveChild`; tests inject no-op stubs. |
| `ScreenRouter._errorLogger` | `Action<string>` | `private readonly` | Injected error logger (defaults to `Console.Error.WriteLine`). ScreenManager wires it to `GD.PrintErr`. Pass `_ => { }` for silent in tests. |
| `ScreenManager._router` | `ScreenRouter` | `private readonly` | Composition — the `ScreenManager` Node delegates Show/ShowAsync to this. |
| `ScreenManager.Instance` | `ScreenManager` (static property) | `public get, private set` | Set in `_Ready`, cleared in `_ExitTree` (only if `Instance == this` to avoid clobbering a successor autoload). |
| `ScreenManager._screenLayer` / `_screenContainer` | `CanvasLayer` / `Control` | `private` | Built lazily in `_Ready`. Screen layer @ 99, container full-rect with `MouseFilter.Stop`. |

## Submodule HEAD tracking policy

This repo is consumed as a git submodule. There is **no pinned SHA** —
every PR's `git diff` is reviewed manually for breaking changes (see
`docs/design_v0.2.md` §11 v0.3 roadmap for the rollout plan).

Rationale: framework evolution is fast (event bus, screen manager, more
tests coming in v0.3+). Pinning to a stable commit would slow the
feedback loop.

Trade-off: every consumer must re-verify after sync. This FORK_CHECKLIST
is the verification aid.
