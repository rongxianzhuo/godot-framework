using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace GameFramework;

/// <summary>
/// UI Manager — autoload (Node). Manages the panel stack, panel-scoped input,
/// and caching. Reachable via <see cref="Instance"/>.
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
/// </summary>
public partial class UIManager : GameService
{
    public static UIManager Instance { get; private set; } = null!;

    public const int UiLayer = 100;

    private CanvasLayer _uiRoot = null!;
    private Control _panelStackContainer = null!;
    private readonly Stack<UIPanelBase> _stack = new();
    private readonly Dictionary<Type, UIPanelBase> _cache = new();

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
        _cache.Clear();
        Instance = null!;
        base._ExitTree();
    }

    // ============================================================
    // PUSH API
    // ============================================================

    /// <summary>Push a no-args/no-result panel by constructing it via <c>new()</c>.</summary>
    public Task PushAsync<TPanel>() where TPanel : UIPanel, new()
        => PushInternalAsync(AcquireCodeOnly<TPanel>(CreatePolicy.TryReuse), null);

    /// <summary>Push a panel that takes an open argument but returns no result.</summary>
    public Task PushAsync<TPanel, TOpenArg>(TOpenArg arg) where TPanel : UIPanel<TOpenArg>, new()
        => PushInternalAsync(AcquireCodeOnly<TPanel>(CreatePolicy.TryReuse), arg);

    /// <summary>Push a panel with both open arg and typed close result.</summary>
    public Task<TCloseArg> PushAsync<TPanel, TOpenArg, TCloseArg>(TOpenArg arg)
        where TPanel : UIPanel<TOpenArg, TCloseArg>, new()
    {
        var panel = AcquireCodeOnly<TPanel>(CreatePolicy.TryReuse);
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
    public UIPanelBase? Current => _stack.Count > 0 ? _stack.Peek() : null;

    /// <summary>Current stack depth.</summary>
    public int Depth => _stack.Count;

    // ============================================================
    // INTERNAL
    // ============================================================

    private TPanel AcquireCodeOnly<TPanel>(CreatePolicy policy) where TPanel : UIPanelBase, new()
    {
        // For MVP we don't reuse cached panels. Each push creates a fresh instance.
        // The cache exists for v0.2 reuse support but is disabled here to avoid
        // stale button handlers firing on detached panels.
        var instance = new TPanel();
        instance.Name = typeof(TPanel).Name;
        return instance;
    }

    private async Task PushInternalAsync(UIPanelBase panel, object? openArg)
    {
        // Initialize once: subscribe to signals, build UI tree.
        InitializePanel(panel);

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

    private void FinalizePop(UIPanelBase panel, object? closeArg)
    {
        // Fire OnClose before detaching.
        try { panel.InvokeOnClose(closeArg); }
        catch (Exception e) { GD.PrintErr($"[UIManager] OnClose threw for {panel.GetType().Name}: {e}"); }

        panel.ClearBindings();
        panel.IsTopOfStack = false;

        // Cache the instance for potential reuse (only if it has a code-only constructor).
        // For MVP, we cache everything; more granular cache control is a v0.2 concern.
        _cache[panel.GetType()] = panel;

        if (panel.GetParent() == _panelStackContainer)
            _panelStackContainer.RemoveChild(panel);

        // Reactivate the new top, if any.
        if (_stack.Count > 0) _stack.Peek().IsTopOfStack = true;
    }

    private static void InitializePanel(UIPanelBase panel)
    {
        // Build the panel's UI tree if not already built. We call OnInitialize via
        // a one-shot marker: panels are responsible for building their children in
        // their constructor (the C# `new TPanel()` path means children are added
        // before _Ready runs).
        //
        // For MVP: panels construct their UI tree in their own constructor.
        // No need to call any framework method here. This method exists as a hook
        // for future expansion (e.g. registering assets or analytics).
    }

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