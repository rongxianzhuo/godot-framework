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
/// Only one Screen is visible at a time. Calling <c>ShowAsync</c> while
/// another screen is active throws <see cref="InvalidOperationException"/>
/// (close the current screen first — await its ShowAsync task). The previous
/// screen is **not** force-removed; this is intentional to prevent accidental
/// stack corruption from concurrent swap requests.
///
/// Layer ordering: Screen (99) sits below UI panels (100) so dialogs can
/// overlay the active screen.
/// </summary>
public sealed partial class ScreenManager : GameService
{
    public static ScreenManager Instance { get; private set; } = null!;

    /// <summary>CanvasLayer index for the screen layer. Below UI panels (100).</summary>
    public const int ScreenLayer = 99;

    private CanvasLayer _screenLayer = null!;
    private Control _screenContainer = null!;
    private Screen? _current;
    private bool _isShowing;

    /// <summary>The currently-shown screen, or null if none.</summary>
    public Screen? Current => _current;

    /// <summary>True while a ShowAsync task is awaiting its screen's close. Used to throw on overlapping ShowAsync.</summary>
    public bool IsShowing => _isShowing;

    /// <summary>Raised after a screen is attached and OnShow has fired.</summary>
    public event Action<Screen>? ScreenShown;

    /// <summary>Raised after a screen is detached (via CloseScreen).</summary>
    public event Action<Screen>? ScreenClosed;

    public override void _Ready()
    {
        base._Ready();
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

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

        GD.Print("[ScreenManager] _Ready. Screen layer created (CanvasLayer @ layer 99).");
    }

    public override void _ExitTree()
    {
        // Forceful shutdown — just remove from tree without invoking OnClose
        // (we don't know the right typed default for each screen's TCloseResult).
        if (_current != null && _current.GetParent() == _screenContainer)
            _screenContainer.RemoveChild(_current);
        _current = null;
        if (Instance == this) Instance = null!;
        base._ExitTree();
    }

    // ============================================================
    // SHOW API
    // ============================================================

    /// <summary>Show a screen with no open argument and no close result.</summary>
    public Task ShowAsync<TScreen>() where TScreen : Screen, new()
        => ShowInternalAsync(Acquire<TScreen>(), null);

    /// <summary>Show a screen with a typed open argument but no close result.</summary>
    public Task ShowAsync<TScreen, TOpenArg>(TOpenArg arg) where TScreen : Screen<TOpenArg>, new()
        => ShowInternalAsync(Acquire<TScreen>(), arg);

    /// <summary>Show a screen with both open arg and typed close result.</summary>
    public Task<TCloseResult> ShowAsync<TScreen, TOpenArg, TCloseResult>(TOpenArg arg)
        where TScreen : Screen<TOpenArg, TCloseResult>, new()
    {
        var screen = Acquire<TScreen>();
        return ShowAndReturnResultAsync(screen, arg);
    }

    // ============================================================
    // INTERNAL
    // ============================================================

    private TScreen Acquire<TScreen>() where TScreen : Screen, new()
    {
        var instance = new TScreen();
        instance.Name = typeof(TScreen).Name;
        return instance;
    }

    private async Task<TCloseResult> ShowAndReturnResultAsync<TOpenArg, TCloseResult>(
        Screen<TOpenArg, TCloseResult> typedScreen, TOpenArg arg)
    {
        await ShowInternalAsync(typedScreen, arg);
        return typedScreen.Result;
    }

    private async Task ShowInternalAsync(Screen screen, object? openArg)
    {
        if (_isShowing || _current != null)
        {
            throw new InvalidOperationException(
                $"[ScreenManager] Cannot show '{screen.GetType().Name}' while another screen is active " +
                $"('{_current?.GetType().Name ?? "<none>"}'). Close the current screen first " +
                "(await its ShowAsync task) before showing a new one.");
        }
        _isShowing = true;
        try
        {
            _screenContainer.AddChild(screen);
            _current = screen;
            screen.IsActive = true;

            // Fire OnShow with the typed arg.
            try { screen.InvokeOnShow(openArg); }
            catch (Exception ex) { GD.PrintErr($"[ScreenManager] OnShow threw for {screen.GetType().Name}: {ex}"); }

            ScreenShown?.Invoke(screen);

            // Await the screen's close task. When it completes, finalize.
            try { await screen.GetCloseTask(); }
            catch (Exception ex) { GD.PrintErr($"[ScreenManager] Close task threw for {screen.GetType().Name}: {ex}"); }
        }
        finally
        {
            screen.IsActive = false;
            if (screen.GetParent() == _screenContainer)
                _screenContainer.RemoveChild(screen);
            _current = null;
            ScreenClosed?.Invoke(screen);
            _isShowing = false;
        }
    }
}