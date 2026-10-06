using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace GameFramework;

/// <summary>
/// Internal base for all UI panel + screen variants. Renamed from
/// <c>UIPanelBase</c> in v0.5-alpha when <see cref="Screen"/> was added —
/// both <see cref="UIPanel"/> (and its typed variants) AND <see cref="Screen"/>
/// (and its typed variants) now inherit from this base.
///
/// Provides:
///   * <see cref="IsTopOfStack"/> — used by <see cref="UIManager"/> to
///     gate panel-scoped input bindings.
///   * <c>BindInput</c> helpers — panel-scoped action bindings.
///   * <see cref="InvokeOnClose"/> abstract — both UIPanel and Screen
///     implement this to dispatch to typed <c>OnClose</c>.
///   * <see cref="GetCloseTask"/> abstract — the manager awaits this
///     to know when the node has resolved its push/show task.
///   * <see cref="InvokeOnOpen"/> virtual (default throws) — only
///     <see cref="UIPanel"/> variants override; <see cref="Screen"/>
///     inherits the throw default because Screen uses <c>InvokeOnShow</c>
///     instead (declared on Screen).
///
/// Public API surface unchanged from v0.4-alpha: only the internal base
/// class name moved. Consumers not affected unless they directly
/// subclassed <c>UIPanelBase</c> (rare — the doc comments told users
/// to subclass UIPanel/UIPanel<T>/UIPanel<T1,T2> instead).
/// </summary>
public abstract partial class ManagedNodeBase : Control
{
    /// <summary>Set by UIManager when this becomes the active top of the panel stack.</summary>
    internal bool IsTopOfStack { get; set; }

    // ---- Panel-scoped input bindings ----
    private readonly List<PanelInputBinding> _bindings = new();

    /// <summary>
    /// Bind an input action to a callback. Only fires while this panel is the
    /// top of the stack. The callback is unsubscribed automatically when the
    /// panel is closed.
    /// </summary>
    protected void BindInput(StringName action, Action<InputEvent> callback)
    {
        _bindings.Add(new PanelInputBinding(action, callback));
    }

    /// <summary>
    /// Convenience: bind an action with no InputEvent arg.
    /// </summary>
    protected void BindInput(StringName action, Action callback)
    {
        _bindings.Add(new PanelInputBinding(action, e => callback()));
    }

    /// <summary>Clear all input bindings (called automatically on close).</summary>
    internal void ClearBindings() => _bindings.Clear();

    internal IReadOnlyList<PanelInputBinding> Bindings => _bindings;

    internal readonly record struct PanelInputBinding(StringName Action, Action<InputEvent> Callback);

    /// <summary>
    /// UIPanel-style "open" dispatch — overridden by UIPanel variants to
    /// call typed <c>OnOpen</c>. Default throws because Screen variants
    /// use <c>InvokeOnShow</c> instead (declared on Screen). Calling
    /// <c>InvokeOnOpen</c> on a Screen is a programming error.
    /// </summary>
    internal protected virtual void InvokeOnOpen(object? openArg)
        => throw new NotSupportedException(
            $"{GetType().Name} does not implement InvokeOnOpen — " +
            "UIPanel variants override this to dispatch to OnOpen(). " +
            "Screen variants use InvokeOnShow instead.");

    /// <summary>
    /// Called when this node is closed (before detach). Both UIPanel
    /// and Screen variants override this to dispatch to typed <c>OnClose</c>.
    /// </summary>
    internal protected abstract void InvokeOnClose(object? closeArg);

    /// <summary>Returns the close completion task for the manager to await.</summary>
    internal abstract Task GetCloseTask();
}