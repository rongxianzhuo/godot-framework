using System.Threading.Tasks;
using Godot;

namespace GameFramework;

/// <summary>
/// Base class for full-screen views managed by <see cref="ScreenManager"/>.
/// Only one Screen is visible at a time — calling <c>ShowAsync</c> replaces
/// the previous screen. The previous screen is force-removed from the tree
/// without invoking its <c>OnClose</c> (use the screen's <c>_ExitTree</c> for
/// cleanup that must survive graceful close skips).
///
/// To pass a typed open argument, inherit from <see cref="Screen{TOpenArg}"/>.
/// To also get a typed close result, inherit from
/// <see cref="Screen{TOpenArg, TCloseResult}"/>.
///
/// To close a screen with no result, call <see cref="CloseScreen(object?)"/>
/// from anywhere within the screen.
/// </summary>
public abstract partial class Screen : ManagedNodeBase
{
    /// <summary>Set by ScreenManager when this screen becomes the active one.</summary>
    internal bool IsActive { get; set; }

    private TaskCompletionSource? _tcs;
    private TaskCompletionSource Tcs => _tcs ??= new TaskCompletionSource();

    /// <summary>Called by ScreenManager after attach. Default: calls no-arg OnShow.</summary>
    internal protected virtual void InvokeOnShow(object? openArg) => OnShow();

    /// <summary>Called by ScreenManager when this screen is closed (graceful path).</summary>
    internal protected override void InvokeOnClose(object? closeArg) => OnClose();

    /// <summary>Returns the close completion task for ScreenManager to await.</summary>
    internal override Task GetCloseTask() => Tcs.Task;

    /// <summary>Called when this screen is shown. Override for setup logic.</summary>
    protected virtual void OnShow() { }

    /// <summary>Called when this screen is closed gracefully. Override for teardown logic.</summary>
    protected virtual void OnClose() { }

    /// <summary>
    /// Close this screen. The ShowAsync Task completes normally (without a
    /// typed result — the non-typed ShowAsync overload returns Task).
    ///
    /// For typed-result screens (inheriting
    /// <see cref="Screen{TOpenArg, TCloseResult}"/>), use the typed
    /// <c>CloseScreen(TCloseResult)</c> overload instead. Calling
    /// <c>CloseScreen()</c> on a typed screen would only set the non-typed
    /// TCS (which no caller awaits); use the typed overload to actually
    /// resolve the ShowAsync task with a result.
    /// </summary>
    protected void CloseScreen()
    {
        if (!Tcs.Task.IsCompleted)
            Tcs.TrySetResult();
    }
}