using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace GameFramework;

/// <summary>
/// UI Manager — autoload (Node). Manages the panel stack and panel-scoped
/// input. Reachable via <see cref="Instance"/>.
///
/// Scene tree layout (created by this manager in _Ready):
///   UIManager (Node, autoload)
///     └── UI Root (CanvasLayer, layer=100)
///           └── Panel Stack Container (Control, full-rect anchors)
///                 └── [top panel here]
///
/// Panel-scoped input: BindInput calls on a panel only fire while that panel
/// is the top of the stack. The manager routes _UnhandledInput events to the
/// top panel's registered bindings.
///
/// v0.3-prep note: the half-finished `_cache` / `InitializePanel` plumbing
/// from v0.2 has been removed (see TODO(v0.3) markers). Cache/reuse will be
/// reintroduced as a coherent feature in a later v0.3 PR — see CHANGELOG.md.
/// </summary>
public partial class UIManager : GameService
{
    public static UIManager Instance { get; private set; } = null!;

    public const int UiLayer = 100;

    private CanvasLayer _uiRoot = null!;
    private Control _panelStackContainer = null!;
    private readonly Stack<ManagedNodeBase> _stack = new();

    // TODO(v0.3): reintroduce `private readonly Dictionary<Type, ManagedNodeBase> _cache`
    // when implementing panel-instance reuse. Removed in v0.3-prep — every push
    // currently creates a fresh `new TPanel()`. See CHANGELOG.md v0.3 entry.

    public override void _Ready()
    {
        base._Ready(); // GameService auto-registers with Game.Instance.Services
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        // Build the UI root on the fly. No .tscn needed.
        _uiRoot = new CanvasLayer { Name = "UIRoot", Layer = UiLayer };
        AddChild(_uiRoot);

        _panelStackContainer = new Control
        {
            Name = "PanelStackContainer",
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            MouseFilter = Control.MouseFilterEnum.Stop, // catch clicks so unfocused UI doesn't leak
        };
        _uiRoot.AddChild(_panelStackContainer);

        GD.Print("[UIManager] _Ready. UI Root created (CanvasLayer @ layer 100).");
    }

    public override void _ExitTree()
    {
        // Forceful shutdown — just remove from tree without invoking OnClose
        // (we don't know the right typed default for each panel's TCloseArg).
        while (_stack.Count > 0)
        {
            var p = _stack.Pop();
            if (p.GetParent() == _panelStackContainer)
                _panelStackContainer.RemoveChild(p);
        }
        Instance = null!;
        base._ExitTree();
    }

    // ============================================================
    // PUSH API
    // ============================================================

    /// <summary>Push a no-args/no-result panel by constructing it via <c>new()</c>.</summary>
    public Task PushAsync<TPanel>() where TPanel : UIPanel, new()
        => PushInternalAsync(AcquireCodeOnly<TPanel>(), null);

    /// <summary>Push a panel that takes an open argument but returns no result.</summary>
    public Task PushAsync<TPanel, TOpenArg>(TOpenArg arg) where TPanel : UIPanel<TOpenArg>, new()
        => PushInternalAsync(AcquireCodeOnly<TPanel>(), arg);

    /// <summary>Push a panel with both open arg and typed close result.</summary>
    public Task<TCloseArg> PushAsync<TPanel, TOpenArg, TCloseArg>(TOpenArg arg)
        where TPanel : UIPanel<TOpenArg, TCloseArg>, new()
    {
        var panel = AcquireCodeOnly<TPanel>();
        return PushAndReturnResultAsync(panel, arg);
    }

    private async Task<TCloseArg> PushAndReturnResultAsync<TOpenArg, TCloseArg>(
        UIPanel<TOpenArg, TCloseArg> typedPanel, TOpenArg arg)
    {
        await PushInternalAsync(typedPanel, arg);
        return typedPanel.Result;
    }

    // ============================================================
    // POP API
    // ============================================================

    /// <summary>Pop the top panel (no result).</summary>
    public void Pop()
    {
        if (_stack.Count == 0) { GD.PrintErr("[UIManager] Pop called on empty stack."); return; }
        FinalizePop(_stack.Pop(), default!);
    }

    /// <summary>Pop the top panel with a typed result.</summary>
    public void Pop<TCloseArg>(TCloseArg result)
    {
        if (_stack.Count == 0) { GD.PrintErr("[UIManager] Pop<T> called on empty stack."); return; }
        FinalizePop(_stack.Pop(), result);
    }

    /// <summary>Pop all panels, optionally notifying them in reverse order.</summary>
    public void PopAll()
    {
        while (_stack.Count > 0) FinalizePop(_stack.Pop(), default!);
    }

    /// <summary>Current top panel (or null if stack empty). Useful for diagnostics.</summary>
    public ManagedNodeBase? Current => _stack.Count > 0 ? _stack.Peek() : null;

    /// <summary>Current stack depth.</summary>
    public int Depth => _stack.Count;

    // ============================================================
    // INTERNAL
    // ============================================================

    private TPanel AcquireCodeOnly<TPanel>() where TPanel : ManagedNodeBase, new()
    {
        // Each push creates a fresh instance. Reuse/cache is planned for v0.3 —
        // see CHANGELOG.md. Until then, do NOT cache panel instances here: a
        // detached panel with bound button handlers would fire stale callbacks.
        var instance = new TPanel();
        instance.Name = typeof(TPanel).Name;
        return instance;
    }

    private async Task PushInternalAsync(ManagedNodeBase panel, object? openArg)
    {
        // If there's a current top, mark it inactive (its input bindings become inert).
        if (_stack.Count > 0) _stack.Peek().IsTopOfStack = false;

        _panelStackContainer.AddChild(panel);
        _stack.Push(panel);
        panel.IsTopOfStack = true;

        // Fire OnOpen with the typed arg.
        try { panel.InvokeOnOpen(openArg); }
        catch (Exception e) { GD.PrintErr($"[UIManager] OnOpen threw for {panel.GetType().Name}: {e}"); }

        // Await the panel's close task. When it completes, finalize.
        try { await panel.GetCloseTask(); }
        catch (Exception e) { GD.PrintErr($"[UIManager] Close task threw for {panel.GetType().Name}: {e}"); }
    }

    private void FinalizePop(ManagedNodeBase panel, object? closeArg)
    {
        // Fire OnClose before detaching.
        try { panel.InvokeOnClose(closeArg); }
        catch (Exception e) { GD.PrintErr($"[UIManager] OnClose threw for {panel.GetType().Name}: {e}"); }

        panel.ClearBindings();
        panel.IsTopOfStack = false;

        if (panel.GetParent() == _panelStackContainer)
            _panelStackContainer.RemoveChild(panel);

        // Reactivate the new top, if any.
        if (_stack.Count > 0) _stack.Peek().IsTopOfStack = true;
    }

    // TODO(v0.3): reintroduce `InitializePanel(ManagedNodeBase panel)` static hook
    // when implementing panel-instance reuse. The hook should call
    // `panel.OnInitialize()` to re-build the UI tree for cached panels. Removed
    // in v0.3-prep along with `OnInitialize` (was in UIPanelBase.cs, renamed
    // to ManagedNodeBase.cs in v0.5-alpha). See CHANGELOG.md.

    // ============================================================
    // INPUT ROUTING
    // ============================================================

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_stack.Count == 0) return;
        var top = _stack.Peek();
        if (!top.IsTopOfStack) return;

        // Only route action events for now (key bindings are action-based).
        // We're looking at events that have been mapped to a named action in the InputMap.
        if (@event is InputEventAction actionEvent && actionEvent.IsPressed())
        {
            var actionName = new StringName(actionEvent.Action);
            foreach (var binding in top.Bindings)
            {
                if (binding.Action == actionName)
                {
                    binding.Callback(@event);
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
        }
    }
}

// ===========================================================================
// (No extension helpers needed; UIPanel<T1,T2>.Result is the typed result channel.)
// ===========================================================================
