# GameFramework

> A Godot 4 C# game framework — singleton container + typed UI panel stack.

**Status:** v0.1 MVP ✅ (demo verified headless). See [`docs/design_v0.1.md`](docs/design_v0.1.md) for the full architecture write-up.

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

**Explicitly out of scope** (v0.2+): state machines, resource loading, event bus,
serialization, hot-reload, networking, tweener, panel cache, editor plugin,
unit tests.

---

## Repository layout

```
godot-framework/
├── docs/
│   └── design_v0.1.md            # Architecture write-up + v0.1 lessons learned
├── scenes/
│   └── demo.tscn                 # Headless smoke-test entry point
├── scripts/
│   ├── framework/                # Core framework (~560 lines, 8 files)
│   │   ├── GameFramework.cs      #   Autoload host
│   │   ├── ServiceRegistry.cs    #   Type-keyed registry
│   │   ├── GameService.cs        #   Node-based service auto-registration
│   │   ├── UIManager.cs          #   Panel stack + UI root
│   │   ├── UIPanelBase.cs        #   Internal panel base + BindInput API
│   │   ├── UIPanel.cs            #   No-args / no-result variant
│   │   ├── UIPanelT.cs           #   Open-arg-only variant
│   │   └── UIPanelT1T2.cs        #   Typed open + typed close result
│   └── demo/                     # Demo (~340 lines, 5 files)
│       ├── AudioService.cs       #   POCO service example
│       ├── MainMenuPanel.cs      #   UIPanel<string, string>
│       ├── SettingsPanel.cs      #   UIPanel<float>
│       ├── ConfirmDialog.cs      #   UIPanel<string, bool>
│       └── DemoBootstrap.cs      #   Auto-driving headless smoke test
├── godot-framework.csproj        # .NET 8 / Godot.NET.Sdk 4.5.0
├── project.godot                 # Godot project + autoloads
├── icon.svg
└── .gitignore
```

---

## Quick start

### 1. Environment

```bash
# .NET 8 SDK
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir /opt/dotnet
apt-get install -y libicu-dev

# Godot 4.5 .NET (mono) build
curl -sSL -o /tmp/godot.zip \
  "https://github.com/godotengine/godot-builds/releases/download/4.5-stable/Godot_v4.5-stable_mono_linux_x86_64.zip"
unzip -d /opt/godot /tmp/godot.zip
apt-get install -y libfontconfig1 unzip
ln -sf /opt/godot/Godot_v4.5-stable_mono_linux_x86_64/Godot_v4.5-stable_mono_linux.x86_64 \
  /usr/local/bin/godot
```

### 2. Build and run the demo

```bash
cd /shared/godot-framework
dotnet build
godot --headless --quit-after 300
```

Expected output (headless smoke test):

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

### 3. Use in your own game

```csharp
// Register a POCO service once at game boot:
GameFramework.Instance.Services.Register(new AudioService());

// Resolve from anywhere — Node-based or POCO:
var audio = GameFramework.Instance.Services.Resolve<AudioService>();

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

## License

MIT — see [`LICENSE`](LICENSE).