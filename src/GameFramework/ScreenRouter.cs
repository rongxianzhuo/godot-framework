using System;
using System.Threading.Tasks;

namespace GameFramework;

/// <summary>
/// POCO state machine for showing one <see cref="Screen"/> at a time. No
/// Godot types are imported here — the attach/detach callbacks are
/// injected by the caller (typically <see cref="ScreenManager"/>, which
/// passes <c>AddChild</c>/<c>RemoveChild</c> on its screen container).
///
/// Usage:
/// <code>
/// var router = new ScreenRouter(
///     screenFactory: t => (Screen)Activator.CreateInstance(t)!,
///     attach: screen => container.AddChild(screen),
///     detach: screen => container.RemoveChild(screen)
/// );
/// await router.ShowAsync<MyScreen>();
/// </code>
///
/// Threading: not thread-safe. Call from the Godot main thread (or any
/// single-threaded caller).
///
/// This is the POCO twin to <see cref="ScreenManager"/> (the Node wrapper),
/// just like <see cref="EventDispatcher"/> is the POCO twin to
/// <see cref="EventBus"/>. Tests can exercise the state machine directly
/// without a Godot engine runtime — see <c>ScreenRouterTests</c>.
/// </summary>
public sealed class ScreenRouter
{
    private readonly Func<Type, Screen> _screenFactory;
    private readonly Action<Screen> _attach;
    private readonly Action<Screen> _detach;
    private readonly Action<string> _errorLogger;

    private Screen? _current;
    private bool _isShowing;

    /// <summary>Default constructor — no-op attach/detach, default Activator-based factory.</summary>
    public ScreenRouter()
        : this(DefaultFactory, _ => { }, _ => { }, DefaultErrorLogger)
    {
    }

    /// <summary>Factory injection only — no-op attach/detach. Useful for tests that want to control screen creation.</summary>
    public ScreenRouter(Func<Type, Screen> screenFactory)
        : this(screenFactory, _ => { }, _ => { }, DefaultErrorLogger)
    {
    }

    /// <summary>
    /// Full constructor. Pass <paramref name="errorLogger"/> to capture errors
    /// instead of letting them surface through <c>GD.PrintErr</c> (which would
    /// native-abort the host under plain <c>dotnet test</c>).
    /// </summary>
    public ScreenRouter(
        Func<Type, Screen> screenFactory,
        Action<Screen> attach,
        Action<Screen> detach,
        Action<string>? errorLogger = null)
    {
        _screenFactory = screenFactory ?? throw new ArgumentNullException(nameof(screenFactory));
        _attach = attach ?? throw new ArgumentNullException(nameof(attach));
        _detach = detach ?? throw new ArgumentNullException(nameof(detach));
        _errorLogger = errorLogger ?? DefaultErrorLogger;
    }

    private static Screen DefaultFactory(Type t)
    {
        var instance = (Screen)Activator.CreateInstance(t)!;
        instance.Name = t.Name;
        return instance;
    }

    private static void DefaultErrorLogger(string msg)
    {
        Console.Error.WriteLine($"[ScreenRouter] {msg}");
    }

    /// <summary>The currently-shown screen, or null if none.</summary>
    public Screen? Current => _current;

    /// <summary>True while a ShowAsync task is awaiting its screen's close.</summary>
    public bool IsShowing => _isShowing;

    /// <summary>Raised after a screen is attached and OnShow has fired.</summary>
    public event Action<Screen>? ScreenShown;

    /// <summary>Raised after a screen is detached (via CloseScreen).</summary>
    public event Action<Screen>? ScreenClosed;

    // ============================================================
    // SHOW API
    // ============================================================

    public Task ShowAsync<TScreen>() where TScreen : Screen, new()
        => ShowInternalAsync(_screenFactory(typeof(TScreen)), null);

    public Task ShowAsync<TScreen, TOpenArg>(TOpenArg arg)
        where TScreen : Screen<TOpenArg>, new()
        => ShowInternalAsync(_screenFactory(typeof(TScreen)), arg);

    public Task<TCloseResult> ShowAsync<TScreen, TOpenArg, TCloseResult>(TOpenArg arg)
        where TScreen : Screen<TOpenArg, TCloseResult>, new()
    {
        var screen = (Screen<TOpenArg, TCloseResult>)_screenFactory(typeof(TScreen));
        return ShowAndReturnResultAsync(screen, arg);
    }

    // ============================================================
    // INTERNAL
    // ============================================================

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
                $"[ScreenRouter] Cannot show '{screen.GetType().Name}' while another screen is active " +
                $"('{_current?.GetType().Name ?? "<none>"}'). Close the current screen first " +
                "(await its ShowAsync task) before showing a new one.");
        }
        _isShowing = true;
        try
        {
            _attach(screen);
            _current = screen;

            try { screen.InvokeOnShow(openArg); }
            catch (Exception ex) { _errorLogger($"OnShow threw for {screen.GetType().Name}: {ex}"); }

            ScreenShown?.Invoke(screen);

            try { await screen.GetCloseTask(); }
            catch (Exception ex) { _errorLogger($"Close task threw for {screen.GetType().Name}: {ex}"); }
        }
        finally
        {
            // Fire OnClose before detaching (matches UIPanel.FinalizePop).
            // For Screen<T1, T2> this reads Result and passes it to typed OnClose.
            try { screen.InvokeOnClose(null); }
            catch (Exception ex) { _errorLogger($"OnClose threw for {screen.GetType().Name}: {ex}"); }

            _detach(screen);
            _current = null;
            ScreenClosed?.Invoke(screen);
            _isShowing = false;
        }
    }
}