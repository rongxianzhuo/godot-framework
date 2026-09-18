using System;
using Godot;

namespace GameFramework;

/// <summary>
/// Base class for Node-based services. Auto-registers itself in the global
/// <see cref="ServiceRegistry"/> on _Ready, and unregisters on _ExitTree.
///
/// Inherit from this for any long-lived service that needs Node lifecycle (signals,
/// _Process, child nodes, etc.). For pure data containers, just register a POCO
/// instance manually with <c>GameFramework.Instance.Services.Register(instance)</c>.
/// </summary>
public abstract partial class GameService : Node
{
    public override void _Ready()
    {
        if (GameFramework.Instance == null)
        {
            // Defensive: if the GameService node was added to the tree before GameFramework's
            // _Ready ran (unusual but possible during manual scene swaps), defer one frame.
            CallDeferred(MethodName.RegisterSelfDeferred);
            return;
        }
        GameFramework.Instance.Services.Register(this);
    }

    public override void _ExitTree()
    {
        if (GameFramework.Instance != null)
            GameFramework.Instance.Services.UnregisterAllFromNode(this);
    }

    private void RegisterSelfDeferred()
    {
        if (GameFramework.Instance != null)
            GameFramework.Instance.Services.Register(this);
    }
}