using System;
using Godot;

namespace GameFramework;

/// <summary>
/// Base class for Node-based services. Auto-registers itself in the global
/// <see cref="ServiceRegistry"/> on _Ready, and unregisters on _ExitTree.
///
/// Inherit from this for any long-lived service that needs Node lifecycle (signals,
/// _Process, child nodes, etc.). For pure data containers, just register a POCO
/// instance manually with <c>Game.Instance.Services.Register(instance)</c>.
/// </summary>
public abstract partial class GameService : Node
{
    public override void _Ready()
    {
        GD.Print($"[GameService] _Ready called on {GetType().Name}; Game.Instance null? {Game.Instance == null}");
        if (Game.Instance == null)
        {
            // Defensive: if the GameService node was added to the tree before Game's
            // _Ready ran (unusual but possible during manual scene swaps), defer one frame.
            CallDeferred(MethodName.RegisterSelfDeferred);
            return;
        }
        Game.Instance.Services.Register(this);
    }

    public override void _ExitTree()
    {
        if (Game.Instance != null)
            Game.Instance.Services.UnregisterAllFromNode(this);
    }

    private void RegisterSelfDeferred()
    {
        if (Game.Instance != null)
            Game.Instance.Services.Register(this);
    }
}