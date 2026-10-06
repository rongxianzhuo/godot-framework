using System.Threading.Tasks;
using Godot;

namespace GameFramework;

/// <summary>
/// Full-screen view that takes a typed open argument and returns a typed
/// close result. The ShowAsync Task&lt;TCloseResult&gt; resolves to whatever
/// the screen passes to <c>CloseScreen(result)</c>.
///
/// Usage:
/// <code>
/// var result = await ScreenManager.Instance.ShowAsync&lt;MyScreen, MyOpenArg, MyCloseArg&gt;(arg);
/// // result is the value passed to myScreen.CloseScreen(result)
/// </code>
/// </summary>
public abstract partial class Screen<TOpenArg, TCloseResult> : Screen
{
    private TaskCompletionSource<TCloseResult>? _typedTcs;
    private TaskCompletionSource<TCloseResult> TypedTcs => _typedTcs ??= new TaskCompletionSource<TCloseResult>();

    /// <summary>The typed close result. Set by <c>CloseScreen(TCloseResult)</c>.</summary>
    internal TCloseResult Result { get; private set; } = default!;

    internal protected override void InvokeOnShow(object? openArg) => OnShow((TOpenArg)openArg!);
    internal protected override void InvokeOnClose(object? closeArg) => OnClose((TCloseResult)closeArg!);
    internal override Task GetCloseTask() => TypedTcs.Task;

    /// <summary>Called when this screen is shown, with the typed argument.</summary>
    protected virtual void OnShow(TOpenArg arg) { }

    /// <summary>Called when this screen is closed gracefully, with the typed result.</summary>
    protected virtual void OnClose(TCloseResult result) { }

    /// <summary>
    /// Close this screen with a typed result. The ShowAsync Task&lt;TCloseResult&gt;
    /// resolves to this value. Safe to call multiple times — only the first call
    /// sets the result.
    /// </summary>
    protected void CloseScreen(TCloseResult result)
    {
        Result = result;
        if (!TypedTcs.Task.IsCompleted)
            TypedTcs.TrySetResult(result);
    }

    /// <summary>
    /// Close this screen with a default-constructed result (e.g. for Cancel).
    /// Hides the base <c>Screen.CloseScreen()</c> so the typed TCS is set
    /// rather than the unused non-typed TCS.
    /// </summary>
    protected new void CloseScreen() => CloseScreen(default!);
}