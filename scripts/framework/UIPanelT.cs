using System.Threading.Tasks;
using Godot;

namespace GameFramework;

/// <summary>
/// Panel that takes a typed open argument but does not return a result.
/// </summary>
public abstract partial class UIPanel<TOpenArg> : UIPanelBase
{
    private TaskCompletionSource? _tcs;

    private TaskCompletionSource Tcs => _tcs ??= new TaskCompletionSource();

    internal protected override void InvokeOnOpen(object? openArg) => OnOpen((TOpenArg)openArg!);
    internal protected override void InvokeOnClose(object? closeArg) => OnClose();

    /// <summary>Called each time this panel is pushed, with the typed argument.</summary>
    protected virtual void OnOpen(TOpenArg arg) { }

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