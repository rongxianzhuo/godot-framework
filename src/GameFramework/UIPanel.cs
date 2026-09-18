using System.Threading.Tasks;
using Godot;

namespace GameFramework;

/// <summary>
/// Panel with no open arguments and no close result. Use for stateless screens
/// that don't need to communicate anything back to the caller.
/// </summary>
public abstract partial class UIPanel : UIPanelBase
{
    private TaskCompletionSource? _tcs;

    private TaskCompletionSource Tcs => _tcs ??= new TaskCompletionSource();

    internal protected override void InvokeOnOpen(object? openArg) => OnOpen();
    internal protected override void InvokeOnClose(object? closeArg) => OnClose();

    /// <summary>Called each time this panel is pushed.</summary>
    protected virtual void OnOpen() { }

    /// <summary>Called each time this panel is popped.</summary>
    protected virtual void OnClose() { }

    /// <summary>
    /// Close this panel. The push caller's Task will complete normally.
    /// </summary>
    protected void ClosePanel()
    {
        if (!Tcs.Task.IsCompleted)
            Tcs.TrySetResult();
        UIManager.Instance?.Pop();
    }

    internal override Task GetCloseTask() => Tcs.Task;
}