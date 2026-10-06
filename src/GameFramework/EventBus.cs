using System;
using Godot;

namespace GameFramework;

/// <summary>
/// Process-wide synchronous event bus for game events. Wraps an
/// <see cref="EventDispatcher"/> with the autoload lifecycle: the node
/// auto-registers with <see cref="Game.Instance"/>.<see cref="Game.Services"/>
/// on <c>_Ready</c> (inherited from <see cref="GameService"/>), and
/// <see cref="Instance"/> is set after that registration completes.
///
/// Usage:
/// <code>
/// // Subscribe (returns IDisposable token for scope-bound usage):
/// using var sub = EventBus.Instance.Subscribe&lt;GamePhaseChanged&gt;(evt =&gt;
///     GD.Print($"phase changed to {evt.NewPhase}"));
///
/// // Publish (synchronous, error-isolated):
/// EventBus.Instance.Publish(new GamePhaseChanged("Playing"));
/// </code>
///
/// Design notes:
/// - All dispatch logic lives in <see cref="EventDispatcher"/> (POCO).
/// - <see cref="EventBus"/> is a thin wrapper that handles Node lifecycle
///   and exposes the static <see cref="Instance"/> pointer for consumer
///   convenience.
/// - Tests target <see cref="EventDispatcher"/> directly — see
///   <c>tests/EventBusTests/EventBusTests.cs</c>. The wrapper itself is
///   covered by structural reflection tests in
///   <c>EventBusContractTests.cs</c>.
/// </summary>
public sealed partial class EventBus : GameService
{
    public static EventBus Instance { get; private set; } = null!;

    private readonly EventDispatcher _dispatcher = new();

    public override void _Ready()
    {
        // base._Ready() registers us with Game.Instance.Services.
        // After registration, set Instance so consumers can find us.
        base._Ready();
        Instance = this;
    }

    public override void _ExitTree()
    {
        // Clear Instance first so any late publishers see null and skip
        // (they'd hit a NullReferenceException otherwise).
        if (Instance == this) Instance = null!;
        base._ExitTree();
    }

    /// <summary>Forwards to the internal <see cref="EventDispatcher"/>.</summary>
    public void Publish<TEvent>(TEvent evt) where TEvent : notnull
        => _dispatcher.Publish(evt);

    /// <summary>Forwards to the internal <see cref="EventDispatcher"/>.</summary>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
        => _dispatcher.Subscribe(handler);

    /// <summary>Number of distinct event types with active subscribers.</summary>
    public int EventTypeCount => _dispatcher.EventTypeCount;

    /// <summary>Total active subscribers across all event types.</summary>
    public int SubscriberCount => _dispatcher.SubscriberCount;
}
