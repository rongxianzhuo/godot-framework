# GameFramework

> A Godot 4 C# game framework — singleton container + typed UI panel stack.
> Designed as a **git submodule** for Godot 4 game projects.

**Status:** v0.4-alpha 🚧 (see [CHANGELOG.md](CHANGELOG.md) for what's done). Architecture in [`docs/design_v0.2.md`](docs/design_v0.2.md).

**Author:** Johnni — Framework Engineer, MagicStudio (v0.1 / v0.2 design); Francisco — Framework Engineer, MagicStudio (v0.3 / v0.4 maintenance)
**Target:** Godot 4.7+ / .NET 9 / C# 12+

---

## What this is

A minimal, AOT-friendly game framework for Godot 4 C# projects, built from scratch
(not a port of any Unity/Godot prior work). Three modules:

1. **Singleton container** — type-safe service registry hosted by a Godot autoload.
   Supports both Node-based services (auto-registered on `_Ready`) and plain POCO services.

2. **UI framework** — push/pop panel stack on a dedicated CanvasLayer, with
   three typed variants: `UIPanel`, `UIPanel<TOpenArg>`, `UIPanel<TOpenArg, TCloseArg>`.
   Awaitable push with typed open args and typed close results.

3. **Event bus** (v0.4-alpha) — synchronous in-process pub/sub. `EventBus` is
   the autoload Node (auto-registered with the service registry); `EventDispatcher`
   is the pure POCO underneath it. Type-keyed, error-isolated, AOT-friendly
   (no reflection, no source generators).

**Explicitly out of scope** (v0.4+): state machines, resource loading,
serialization, hot-reload, networking, tweener, panel cache, editor plugin.

---

## Repository layout

```
godot-framework/
├── GodotFramework.csproj          # Library + sample (for our own smoke testing)
├── project.godot                  # Our test project's Godot config
├── src/
│   └── GameFramework/             # LIBRARY CODE (10 .cs files, namespace GameFramework)
│       ├── Game.cs                  # The autoload class (note: NOT GameFramework.cs; see below)
│       ├── ServiceRegistry.cs
│       ├── GameService.cs
│       ├── UIManager.cs
│       ├── UIPanelBase.cs
│       ├── UIPanel.cs
│       ├── UIPanelT.cs
│       ├── UIPanelT1T2.cs
│       ├── EventDispatcher.cs      # POCO pub/sub (added v0.4-alpha)
│       └── EventBus.cs             # Node wrapper autoload (added v0.4-alpha)
├── samples/
│   └── Demo/                      # SAMPLE: exercises every UIPanel variant + service resolution + EventBus
│       ├── AudioService.cs
│       ├── MainMenuPanel.cs
│       ├── SettingsPanel.cs
│       ├── ConfirmDialog.cs
│       ├── EventBusDemo.cs         # Added v0.4-alpha (invoked by DemoBootstrap as Step 3)
│       └── DemoBootstrap.cs
├── scenes/
│   └── demo.tscn                  # The main scene run by our test project
├── icon.svg
├── README.md
├── CHANGELOG.md                   # Version history (v0.1 / v0.2 / v0.3 / v0.4-alpha)
├── docs/
│   └── design_v0.2.md             # Architecture + lessons learned (see §11 for v0.3 roadmap)
├── LICENSE
└── .gitignore
```

---

## Integration (as a git submodule)

The library is designed to be dropped into any Godot 4 C# game project at
`addons/godot-framework/` via `git submodule add`. After cloning, your consumer
project needs three small wiring changes.

### Step 1: Add the submodule

```bash
cd /path/to/your/godot-game
git submodule add git@github.com:rongxianzhuo/godot-framework.git addons/godot-framework
git submodule update --init --recursive
```

After this, `addons/godot-framework/src/GameFramework/` contains the library
source files. `addons/godot-framework/GodotFramework.csproj` is the project
file. `samples/`, `scenes/`, `project.godot` are internal to the library and
not used by your game.

### Step 2: Wire up C# (your game's `.csproj`)

Two integration patterns — pick the one that matches your needs:

#### Pattern A — `<Compile Include>` (recommended, no separate assembly)

Pulls the library source files directly into your game's main assembly. No
external DLL dependency, cleanest IDE experience, the library code is compiled
together with your game code (so it can't accidentally drift out of sync).

```xml
<Project Sdk="Godot.NET.Sdk/4.7.2">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Godot.NET.Sdk" Version="4.7.2" />
  </ItemGroup>
  <ItemGroup>
    <!-- GameFramework library — only the src/ folder, NOT samples/. -->
    <Compile Include="addons/godot-framework/src/GameFramework/**/*.cs" />
  </ItemGroup>
</Project>
```

#### Pattern B — `<ProjectReference>` (separate assembly)

If you want the library compiled as a separate assembly your game references
(less common — usually only useful if you want hot-swap of the library DLL
during development):

```xml
<Project Sdk="Godot.NET.Sdk/4.7.2">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Godot.NET.Sdk" Version="4.7.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="addons/godot-framework/GodotFramework.csproj" />
  </ItemGroup>
</Project>
```

> The shipped `GodotFramework.csproj` compiles both `src/GameFramework/` and
> `samples/Demo/`. Pattern B will pull sample classes into your reference chain.
> If that bothers you, fork the repo and add `<Compile Remove="samples/**/*.cs" />`
> to `GodotFramework.csproj` before building.

> ⚠️ **Avoid the SDK default Compile glob.**
> If your consumer `.csproj` is `<Project Sdk="Godot.NET.Sdk/X.Y.Z">` without
> an explicit `<Compile Include>` (Pattern A) or `<ProjectReference>` (Pattern B),
> the SDK's default Compile glob walks the entire project tree — including
> `addons/godot-framework/samples/Demo/*.cs` AND `addons/godot-framework/tests/**/*.cs`
> (added in v0.3). You'll then have phantom types like
> `GameFramework.Demo.AudioService` (and friends) compiled into your game's
> main assembly, unused but polluting type lookup.
>
> Add this to your consumer `.csproj` to opt out:
>
> ```xml
> <ItemGroup>
>   <Compile Remove="addons/godot-framework/samples/**/*.cs" />
>   <Compile Remove="addons/godot-framework/tests/**/*.cs" />
> </ItemGroup>
> ```
>
> The framework itself uses `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>`
> + explicit `<Compile Include="src/GameFramework/**/*.cs" />` +
> `<Compile Include="samples/Demo/**/*.cs" />` in `GodotFramework.csproj`, so
> its own build does not need this workaround.
>
> **Related (Android consumers):** godot-template's README documents a
> packaging pitfall where Godot 4.7+ `.tpz` Android templates must be
> extracted to `android/build/` (the `.tpz` contains an internal
> `templates/` subdirectory that confuses Gradle's APK packager). See
> `godot-template/README.md` "踩过的坑" #1.

### Step 3: Wire up autoloads (your game's `project.godot`)

Add two autoload entries pointing into the submodule:

```ini
[autoload]
GameFramework="*res://addons/godot-framework/src/GameFramework/Game.cs"
UIManager="*res://addons/godot-framework/src/GameFramework/UIManager.cs"
```

Add a third line if you also use `EventBus`:

```ini
[autoload]
GameFramework="*res://addons/godot-framework/src/GameFramework/Game.cs"
UIManager="*res://addons/godot-framework/src/GameFramework/UIManager.cs"
EventBus="*res://addons/godot-framework/src/GameFramework/EventBus.cs"
```

You only need `UIManager` if you actually use the UI panel stack — drop the
line if you're only using `ServiceRegistry`. Order matters: `GameFramework`
must be listed **first**; `UIManager` and `EventBus` (both `GameService`s)
auto-register with `Game.Instance.Services` and need `Game.Instance` to
exist when their own `_Ready` fires.

> **Class naming note:** the autoload class is named `Game` (not
> `GameFramework`) because the C# namespace and class cannot share a name
> without causing `CS0234` ("X does not exist in namespace X") errors
> for consumers. The autoload NODE name in `project.godot` is still
> `GameFramework` — that's just an identifier inside Godot, not a C#
> identifier. So: autoload name `GameFramework`, class `Game`.

### Step 4: Use it

```csharp
// Register a POCO service once at game boot:
Game.Instance.Services.Register(new AudioService());

// Resolve from anywhere — Node-based or POCO:
var audio = Game.Instance.Services.Resolve<AudioService>();

// Push a typed panel and await its result:
var choice = await UIManager.Instance.PushAsync<ConfirmDialog, string, bool>(
    "Start a new game?");
if (choice) StartGame();
```

Define a panel:

```csharp
public sealed partial class MyPanel : UIPanel<MyOpenArg, MyCloseResult>
{
    public MyPanel() {
        // Build your UI tree in C# (no .tscn needed for code-only panels).
        // ...
        confirmButton.Pressed += () => ClosePanel(MyCloseResult.Ok);
    }

    protected override void OnOpen(MyOpenArg arg) { /* refresh UI */ }
    protected override void OnClose(MyCloseResult result) { /* cleanup */ }
}
```

Publish and subscribe to game-wide events with `EventBus` (v0.4-alpha). The
`using var` pattern gives you scope-bound auto-unsubscribe — the token's
`Dispose()` runs at the end of the enclosing scope:

```csharp
// Publish from anywhere — synchronous, error-isolated:
EventBus.Instance.Publish(new GamePhaseChanged("playing"));

// Subscribe with auto-cleanup at scope exit:
using var sub = EventBus.Instance.Subscribe<GamePhaseChanged>(evt =>
    GD.Print($"phase is now {evt.NewPhase}"));
// sub.Dispose() runs at end of method/scope → unsubscribes
```

For tooling or tests where a Godot Node lifecycle is overkill, use the
underlying POCO `EventDispatcher` directly (no autoload required):

```csharp
var bus = new EventDispatcher();           // production: GD.PrintErr on subscriber throws
var silentBus = new EventDispatcher(_ => { });  // tests: silent error logger
using var sub = silentBus.Subscribe<MyEvent>(handler);
bus.Publish(new MyEvent(...));
```

---

## Quick start (running our sample)

```bash
# 1. .NET 9 SDK
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0 --install-dir /opt/dotnet
apt-get install -y libicu-dev

# 2. Godot 4.7 .NET build
curl -sSL -o /tmp/godot.zip \
  "https://github.com/godotengine/godot-builds/releases/download/4.7-stable/Godot_v4.7-stable_mono_linux_x86_64.zip"
unzip -d /opt/godot /tmp/godot.zip
apt-get install -y libfontconfig1 unzip
ln -sf /opt/godot/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 \
  /usr/local/bin/godot

# 3. Build + run sample headless
cd /root/godot-framework
dotnet build GodotFramework.csproj
godot --headless --quit-after 300
```

Expected output:

```
[GameFramework] _Ready. Service registry online.
[UIManager] _Ready. UI Root created (CanvasLayer @ layer 100).
[DemoBootstrap] Services registered: 3     ← 1 added v0.4-alpha (EventBus autoload)
[DemoBootstrap] Step 1: Push MainMenuPanel(arg='initial')
[MainMenuPanel] OnOpen(arg='initial')
[MainMenuPanel] Settings clicked → push SettingsPanel
[SettingsPanel] OnOpen(arg='0')
[AudioService] Music volume set to 0.00
[SettingsPanel] Back clicked → ClosePanel()
[MainMenuPanel] Play clicked → ClosePanel('play')
[DemoBootstrap] MainMenuPanel returned 'play'
[DemoBootstrap] Step 2: Push ConfirmDialog(message='Start game?')
[ConfirmDialog] OnOpen(message='Start game?')
[ConfirmDialog] OnClose(result=True)
[DemoBootstrap] ConfirmDialog returned True
[DemoBootstrap] Step 3: EventBus demo              ← added v0.4-alpha
[EventBusDemo] Starting
[EventBusDemo] After subscribe: EventTypeCount=2, SubscriberCount=2
[EventBusDemo] HelloEvent received: 'first' (total: 1)
[EventBusDemo] CounterEvent received: 10 (sum: 10)
[EventBusDemo] HelloEvent received: 'second' (total: 2)
[EventBusDemo] CounterEvent received: 32 (sum: 42)
[EventBusDemo] After publish: helloCount=2, counterSum=42
[EventBusDemo] Done (subscribers will unsubscribe on Run() return)
[DemoBootstrap] Demo complete. Quitting.
```

---

## GDScript compatibility

**No** — the library is pure C# and uses the Godot C# binding (`GodotSharp.dll`)
heavily. The `ServiceRegistry`, `UIPanel<T1,T2>` etc. all require C# consumer
code. A GDScript game cannot use this library directly in v0.2.

(Future: a thin GDScript wrapper around the C# autoloads is possible but not
planned.)

---

## Avoiding Common Pitfalls

These four gotchas are easy to hit when extending the framework. Each has
been debugged during v0.1 / v0.2 development.

### 1. `GameService` registers under the **derived** type, not the base

If `GameService._Ready` used `Register<T>(this)`, the generic parameter `T`
would be the static base type `GameService` — not the actual derived type
(e.g. `UIManager`). `Resolve<UIManager>()` would then return null. The
framework solves this with `Register(this.GetType(), this)`:

```csharp
// ✅ Correct (what GameService._Ready does internally):
public override void _Ready()
{
    Game.Instance.Services.Register(this.GetType(), this);
}

// ❌ Wrong — locks the key to GameService, derived lookups fail:
Game.Instance.Services.Register<GameService>(this);
```

If you write a custom service base class, **don't** replicate the naive pattern.

### 2. Namespace and class can't share a name (CS0234)

If you ever create a class named the same as its namespace, C# resolution
prefers the namespace over the type and you get `CS0234`:

```csharp
namespace GameFramework { public class GameFramework { } }
//                      ^^^^^^^^^^^^^^^^ CS0234: 'GameFramework' does not
//                                         exist in namespace 'GameFramework'
```

The framework's autoload class is named `Game` (not `GameFramework`) for
exactly this reason. If you fork the framework, keep the class name distinct
from the namespace.

### 3. `OnClose` is NOT called on `_ExitTree` / forced shutdown

When `UIManager` exits the tree (scene change, app quit, force-unload), it
removes panels from the stack **without** invoking `OnClose`. Reason: typed
panels (`UIPanel<T1, T2>`) need a default value for `TCloseArg`, and any
default we pick could throw `InvalidCastException` for non-trivial types
(reference types, structs with required fields).

If your panel needs cleanup that must survive `OnClose` being skipped (timers,
external subscriptions, file handles), put it in `_ExitTree()`:

```csharp
public sealed partial class MyPanel : UIPanel<MyArg, MyResult>
{
    protected override void OnClose(MyResult result) { /* normal cleanup */ }
    public override void _ExitTree() { /* always-runs cleanup */ }
}
```

### 4. Panel UI tree must be built in the **constructor**, not `_Ready`

`UIManager.PushInternalAsync` adds the panel to the tree immediately after
construction. Any UI children added in `_Ready` arrive **after** the panel is
already attached, leaving a frame of empty UI (and possibly missing focus
handlers):

```csharp
public sealed partial class MyPanel : UIPanel<MyArg>
{
    private Button _btn = null!;
    public MyPanel()  // ✅ children added here, before AddChild
    {
        _btn = new Button { Text = "OK" };
        AddChild(_btn);
        _btn.Pressed += () => ClosePanel();
    }

    // ❌ Too late — panel is already in the tree:
    public override void _Ready() { AddChild(_btn); }
}
```

---

## License

MIT — see [`LICENSE`](LICENSE).