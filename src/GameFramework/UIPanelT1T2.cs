using System.Threading.Tasks;
using Godot;

namespace GameFramework;

/// <summary>
/// Panel that takes a typed open argument and returns a typed close result.
/// The push caller's Task&lt;TCloseArg&gt; resolves to whatever the panel passes to ClosePanel.
/// </summary>
public abstract partial class UIPanel<TOpenArg, TCloseArg> : UIPanelBase
{
    private TaskCompletionSource<TCloseArg>? _tcs;

    /// <summary>Lazily initialized close TCS. Subclasses overriding _Ready don't need to call base.</summary>
    private TaskCompletionSource<TCloseArg> Tcs => _tcs ??= new TaskCompletionSource<TCloseArg>();

    /// <summary>The typed close result. Set just before Pop is called.
    /// Read by UIManager to satisfy PushAsync&lt;...,TCloseArg&gt;'s return value.</summary>
    internal TCloseArg Result { get; private set; } = default!;

    internal protected override void InvokeOnOpen(object? openArg) => OnOpen((TOpenArg)openArg!);
    internal protected override void InvokeOnClose(object? closeArg) => OnClose((TCloseArg)closeArg!);

    /// <summary>Called each time this panel is pushed, with the typed argument.</summary>
    protected virtual void OnOpen(TOpenArg arg) { }

    /// <summary>Called each time this panel is popped, with the typed result.</summary>
    protected virtual void OnClose(TCloseArg result) { }

    /// <summary>
    /// Close this panel with a typed result. The push caller's Task&lt;TCloseArg&gt;
    /// resolves to this value.
    /// </summary>
    protected void ClosePanel(TCloseArg result)
    {
        Result = result;
        if (!Tcs.Task.IsCompleted)
            Tcs.TrySetResult(result);
        UIManager.Instance?.Pop<TCloseArg>(result);
    }

    /// <summary>
    /// Close this panel with a default-constructed result (e.g. for Cancel).
    /// </summary>
    protected void ClosePanel() => ClosePanel(default(TCloseArg)!);

    internal override Task GetCloseTask() => Tcs.Task;
}