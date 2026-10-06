using Godot;
using GameFramework;

namespace GameFramework.Demo;

/// <summary>
/// Minimal runnable demo of <see cref="EventBus"/>. Demonstrates:
///   - Subscribe with <c>using var</c> for scope-bound auto-unsubscribe
///   - Publish to multiple subscribers in registration order
///   - Event-type-keyed routing (multiple event types share one bus)
///   - Diagnostics: <see cref="EventBus.EventTypeCount"/> and
///     <see cref="EventBus.SubscriberCount"/>
///
/// Run via <see cref="DemoBootstrap"/> after the existing UI flow:
/// <code>
///   EventBusDemo.Run(EventBus.Instance);
/// </code>
///
/// Design notes:
///   - The bus is passed as a parameter (not pulled from
///     <see cref="EventBus.Instance"/>) so the demo can be tested
///     against a plain <see cref="EventDispatcher"/> if needed.
///   - Two unrelated event types (<see cref="HelloEvent"/> and
///     <see cref="CounterEvent"/>) exercise the type-keyed routing.
///   - Subscribers are scoped to <see cref="Run"/> via <c>using var</c>,
///     so they auto-unsubscribe when Run() returns.
/// </summary>
public static class EventBusDemo
{
    /// <summary>A simple string-carrying event.</summary>
    public record HelloEvent(string Message);

    /// <summary>An int-carrying event used for arithmetic aggregation.</summary>
    public record CounterEvent(int Value);

    public static void Run(EventBus bus)
    {
        GD.Print("[EventBusDemo] Starting");

        int helloCount = 0;
        int counterSum = 0;

        using var helloSub = bus.Subscribe<HelloEvent>(evt =>
        {
            helloCount++;
            GD.Print($"[EventBusDemo] HelloEvent received: '{evt.Message}' (total: {helloCount})");
        });

        using var counterSub = bus.Subscribe<CounterEvent>(evt =>
        {
            counterSum += evt.Value;
            GD.Print($"[EventBusDemo] CounterEvent received: {evt.Value} (sum: {counterSum})");
        });

        GD.Print($"[EventBusDemo] After subscribe: EventTypeCount={bus.EventTypeCount}, SubscriberCount={bus.SubscriberCount}");

        bus.Publish(new HelloEvent("first"));
        bus.Publish(new CounterEvent(10));
        bus.Publish(new HelloEvent("second"));
        bus.Publish(new CounterEvent(32));

        GD.Print($"[EventBusDemo] After publish: helloCount={helloCount}, counterSum={counterSum}");
        // ↑ At this point the `using var` subscriptions are still alive
        // (they dispose when Run() returns). Counters reflect this state.
        GD.Print("[EventBusDemo] Done (subscribers will unsubscribe on Run() return)");
    }
}
