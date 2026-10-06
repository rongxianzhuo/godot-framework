using System;
using System.Threading.Tasks;
using Godot;

namespace GameFramework;

/// <summary>
/// Screen manager — autoload (Node). Manages the single-screen swap pattern.
/// Reachable via <see cref="Instance"/>.
///
/// Scene tree layout (created by this manager in <c>_Ready</c>):
/// <code>
///   ScreenManager (Node, autoload)
///     └── Screen Layer (CanvasLayer, layer=99 — below UI panels @ layer 100)
///             └── Screen Container (Control, full-rect anchors)
///                     └── [current screen here]
/// </code>
///
/// POCO split: the show/close state machine lives in
/// <see cref="ScreenRouter"/>. This class is a thin Node wrapper that
/// owns the Godot scene-tree attachment (AddChild/RemoveChild) and
/// forwards ShowAsync calls to the router.
///
/// Only one Screen is visible at a time. Calling <c>ShowAsync</c> while
/// another screen is active throws <see cref="InvalidOperationException"/>
/// (close the current screen first — await its ShowAsync task).
///
/// Layer ordering: Screen (99) sits below UI panels (100) so dialogs can
/// overlay the active screen.
/// </summary>
public sealed partial class ScreenManager : GameService
{
    public static ScreenManager Instance { get; private set; } = null!;

    /// <summary>CanvasLayer index for the screen layer. Below UI panels (100).</summary>
    public const int ScreenLayer = 99;

    private CanvasLayer? _screenLayer;
    private Control? _screenContainer;
    private readonly ScreenRouter _router;

    public ScreenManager()
    {
        // Default factory: Activator.CreateInstance + Name assignment.
        // Attach/detach callbacks close over the container via lazy init —
        // the container is built in _Ready, but ShowAsync calls work as
        // long as the user awaits ShowAsync only after _Ready.
        _router = new ScreenRouter(
            screenFactory: t =>
            {
                var s = (Screen)Activator.CreateInstance(t)!;
                s.Name = t.Name;
                return s;
            },
            attach: AttachScreen,
            detach: DetachScreen,
            errorLogger: msg => GD.PrintErr($"[ScreenManager] {msg}")
        );
    }

    /// <summary>The currently-shown screen, or null if none.</summary>
    public Screen? Current => _router.Current;

    /// <summary>True while a ShowAsync task is awaiting its screen's close.</summary>
    public bool IsShowing => _router.IsShowing;

    /// <summary>Raised after a screen is attached and OnShow has fired.</summary>
    public event Action<Screen>? ScreenShown
    {
        add => _router.ScreenShown += value;
        remove => _router.ScreenShown -= value;
    }

    /// <summary>Raised after a screen is detached (via CloseScreen).</summary>
    public event Action<Screen>? ScreenClosed
    {
        add => _router.ScreenClosed += value;
        remove => _router.ScreenClosed -= value;
    }

    public override void _Ready()
    {
        base._Ready();
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        EnsureContainer();
        GD.Print("[ScreenManager] _Ready. Screen layer created (CanvasLayer @ layer 99).");
    }

    public override void _ExitTree()
    {
        // Forceful shutdown — just remove from tree without invoking OnClose
        // (we don't know the right typed default for each screen's TCloseResult).
        var current = _router.Current;
        if (current != null && _screenContainer != null && current.GetParent() == _screenContainer)
            _screenContainer.RemoveChild(current);
        if (Instance == this) Instance = null!;
        base._ExitTree();
    }

    // ============================================================
    // SHOW API — all delegate to _router
    // ============================================================

    public Task ShowAsync<TScreen>() where TScreen : Screen, new()
        => _router.ShowAsync<TScreen>();

    public Task ShowAsync<TScreen, TOpenArg>(TOpenArg arg)
        where TScreen : Screen<TOpenArg>, new()
        => _router.ShowAsync<TScreen, TOpenArg>(arg);

    public Task<TCloseResult> ShowAsync<TScreen, TOpenArg, TCloseResult>(TOpenArg arg)
        where TScreen : Screen<TOpenArg, TCloseResult>, new()
        => _router.ShowAsync<TScreen, TOpenArg, TCloseResult>(arg);

    // ============================================================
    // INTERNAL — attach/detach bridge for ScreenRouter
    // ============================================================

    private void EnsureContainer()
    {
        if (_screenContainer != null) return;

        _screenLayer = new CanvasLayer { Name = "ScreenLayer", Layer = ScreenLayer };
        AddChild(_screenLayer);

        _screenContainer = new Control
        {
            Name = "ScreenContainer",
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            MouseFilter = Control.MouseFilterEnum.Stop, // catch clicks so unfocused UI doesn't leak
        };
        _screenLayer.AddChild(_screenContainer);
    }

    private void AttachScreen(Screen screen)
    {
        EnsureContainer();
        _screenContainer!.AddChild(screen);
        screen.IsActive = true;
    }

    private void DetachScreen(Screen screen)
    {
        screen.IsActive = false;
        if (_screenContainer != null && screen.GetParent() == _screenContainer)
            _screenContainer.RemoveChild(screen);
    }
}