using Godot;

namespace GameFramework;

/// <summary>
/// Top-level framework autoload (Node). Hosts the ServiceRegistry and provides
/// the only piece of static state games should reach for: <see cref="Instance"/>.
///
/// Convention:
///   - POCO services: registered manually via <see cref="Services"/>.
///   - Node-based services: inherit <see cref="GameService"/> and they'll auto-register
///     on _Ready / unregister on _ExitTree.
///
/// This autoload is intentionally thin. It is NOT a GameManager. Do not pile business
/// logic here. Each concern (UI, audio, save, settings) is its own service module.
/// </summary>
public partial class GameFramework : Node
{
    /// <summary>Process always, even when the tree is paused (UI keeps working).</summary>
    public static GameFramework Instance { get; private set; } = null!;

    public ServiceRegistry Services { get; } = new();

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        GD.Print("[GameFramework] _Ready. Service registry online.");
    }

    public override void _ExitTree()
    {
        // Tear down any Node-based services that are still registered under this root.
        Services.UnregisterAllFromNode(this);
        Instance = null!;
    }
}