# GameFramework

> A Godot 4 C# game framework — singleton container + typed UI panel stack.
> Designed as a **git submodule** for Godot 4 game projects.

**Status:** v0.2 ✅ (submodule-ready, demo verified headless). See [`docs/design_v0.1.md`](docs/design_v0.1.md) for the architecture write-up.

**Author:** Johnni — Framework Engineer, MagicStudio  
**Target:** Godot 4.5+ / .NET 8 / C# 12

---

## What this is

A minimal, AOT-friendly game framework for Godot 4 C# projects, built from scratch
(not a port of any Unity/Godot prior work). Two modules:

1. **Singleton container** — type-safe service registry hosted by a Godot autoload.
   Supports both Node-based services (auto-registered on `_Ready`) and plain POCO services.

2. **UI framework** — push/pop panel stack on a dedicated CanvasLayer, with
   three typed variants: `UIPanel`, `UIPanel<TOpenArg>`, `UIPanel<TOpenArg, TCloseArg>`.
   Awaitable push with typed open args and typed close results.

**Explicitly out of scope** (v0.3+): state machines, resource loading, event bus,
serialization, hot-reload, networking, tweener, panel cache, editor plugin,
unit tests.

---

## Repository layout

```
godot-framework/
├── GodotFramework.csproj          # Library + sample (for our own smoke testing)
├── project.godot                  # Our test project's Godot config
├── src/
│   └── GameFramework/             # LIBRARY CODE (8 .cs files, namespace GameFramework)
│       ├── Game.cs                  # The autoload class (note: NOT GameFramework.cs; see below)
│       ├── ServiceRegistry.cs
│       ├── GameService.cs
│       ├── UIManager.cs
│       ├── UIPanelBase.cs
│       ├── UIPanel.cs
│       ├── UIPanelT.cs
│       └── UIPanelT1T2.cs
├── samples/
│   └── Demo/                      # SAMPLE: exercises every UIPanel variant + service resolution
│       ├── AudioService.cs
│       ├── MainMenuPanel.cs
│       ├── SettingsPanel.cs
│       ├── ConfirmDialog.cs
│       └── DemoBootstrap.cs
├── scenes/
│   └── demo.tscn                  # The main scene run by our test project
├── icon.svg
├── README.md
├── docs/
│   └── design_v0.1.md             # Architecture + lessons learned
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
<Project Sdk="Godot.NET.Sdk/4.5.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Godot.NET.Sdk" Version="4.5.0" />
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
<Project Sdk="Godot.NET.Sdk/4.5.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Godot.NET.Sdk" Version="4.5.0" />
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

### Step 3: Wire up autoloads (your game's `project.godot`)

Add two autoload entries pointing into the submodule:

```ini
[autoload]
GameFramework="*res://addons/godot-framework/src/GameFramework/GameFramework.cs"
UIManager="*res://addons/godot-framework/src/GameFramework/UIManager.cs"
```

You only need `UIManager` if you actually use the UI panel stack — drop the
line if you're only using `ServiceRegistry`. Order matters: `GameFramework`
must be listed before `UIManager` (UIManager is a `GameService` that
auto-registers with `Game.Instance.Services`).

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

---

## Quick start (running our sample)

```bash
# 1. .NET 8 SDK
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir /opt/dotnet
apt-get install -y libicu-dev

# 2. Godot 4.5 .NET build
curl -sSL -o /tmp/godot.zip \
  "https://github.com/godotengine/godot-builds/releases/download/4.5-stable/Godot_v4.5-stable_mono_linux_x86_64.zip"
unzip -d /opt/godot /tmp/godot.zip
apt-get install -y libfontconfig1 unzip
ln -sf /opt/godot/Godot_v4.5-stable_mono_linux_x86_64/Godot_v4.5-stable_mono_linux.x86_64 \
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
[DemoBootstrap] Services registered: 1
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

## License

MIT — see [`LICENSE`](LICENSE).