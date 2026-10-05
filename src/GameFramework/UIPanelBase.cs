using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace GameFramework;

// TODO(v0.3): reintroduce `CreatePolicy` enum (TryReuse / ForceCreate) when
// implementing panel-instance cache + reuse. The enum existed in v0.2 but
// was unused — see UIManager.AcquireCodeOnly which ignored the policy
// parameter. Removed in v0.3-prep; will be re-added when reuse ships.

/// <summary>
/// Internal base for all UIPanel variants. Holds the panel stack machinery
/// (lifecycle hooks, close completion, panel-scoped input bindings).
///
/// Users should NOT inherit from this directly. Use <see cref="UIPanel"/>,
/// <see cref="UIPanel{TOpenArg}"/>, or <see cref="UIPanel{TOpenArg, TCloseArg}"/>.
/// </summary>
public abstract partial class UIPanelBase : Control
{
    /// <summary>Set by UIManager when this panel becomes the active top.</summary>
    internal bool IsTopOfStack { get; set; }

    // TODO(v0.3): reintroduce `OnInitialize()` lifecycle hook (called once when
    // panel is first instantiated). Needed for cache/reuse to safely re-build
    // the UI tree on a cached panel without stale button handlers firing.
    // Removed in v0.3-prep; will be re-added when reuse ships.

    /// <summary>Called each time the panel is pushed onto the stack (after attach).</summary>
    internal protected abstract void InvokeOnOpen(object? openArg);

    /// <summary>Called each time the panel is popped (before detach).</summary>
    internal protected abstract void InvokeOnClose(object? closeArg);

    /// <summary>Returns the close completion task for UIManager to await.</summary>
    internal abstract Task GetCloseTask();

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
}
