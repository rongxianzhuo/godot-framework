using System;
using System.Collections.Generic;
using GameFramework;
using Xunit;

namespace EventBusTests;

/// <summary>
/// Unit tests for <see cref="EventDispatcher"/>, the POCO implementation
/// behind <see cref="EventBus"/>. Pure POCO tests — no Godot engine runtime
/// required to run via <c>dotnet test</c>.
///
/// The dispatcher's <see cref="Godot.GD.PrintErr(string)"/> call is wrapped
/// in a defensive try/catch in source, so even the error-isolation test
/// runs cleanly under plain <c>dotnet test</c> (no native abort from a
/// missing engine).
///
/// Tests cover the 6 cases from the W2 Day 1 spec + 5 extras:
///   1. Subscribe_Publish_ReceivesEvent            (Mark #1)
///   2. Subscribe_MultipleSubscribers_AllReceive   (Mark #2)
///   3. Unsubscribe_NoLongerReceives               (Mark #3)
///   4. Publish_NoSubscribers_DoesNotThrow         (Mark #4)
///   5. SubscriberThrows_OtherSubscribersStillReceive (Mark #5, error isolation)
///   6. DisposeToken_AutoUnsubscribes              (Mark #6)
///   7. Publish_WithNoMatchingEventType_DoesNotThrow (bonus)
///   8. DisposeToken_MultipleDispose_IsIdempotent  (bonus)
///   9. DifferentEventTypes_AreIndependentSubscribers (bonus)
///  10. EventTypeCount_ReflectsSubscriptions       (bonus)
///  11. SubscriberCount_ReflectsAllSubscribers     (bonus)
/// </summary>
public class EventDispatcherTests
{
    // Test event types — record classes give us immutability + value equality.
    public record PlayerScored(int Points);
    public record GamePhaseChanged(string NewPhase);

    [Fact]
    public void Subscribe_Publish_ReceivesEvent()
    {
        var bus = new EventDispatcher();
        PlayerScored? received = null;
        using var sub = bus.Subscribe<PlayerScored>(evt => received = evt);

        bus.Publish(new PlayerScored(100));

        Assert.NotNull(received);
        Assert.Equal(100, received.Points);
    }

    [Fact]
    public void Subscribe_MultipleSubscribers_AllReceive()
    {
        var bus = new EventDispatcher();
        int r1 = 0, r2 = 0, r3 = 0;
        using var s1 = bus.Subscribe<PlayerScored>(_ => r1++);
        using var s2 = bus.Subscribe<PlayerScored>(_ => r2++);
        using var s3 = bus.Subscribe<PlayerScored>(_ => r3++);

        bus.Publish(new PlayerScored(50));

        Assert.Equal(1, r1);
        Assert.Equal(1, r2);
        Assert.Equal(1, r3);
    }

    [Fact]
    public void Unsubscribe_NoLongerReceives()
    {
        var bus = new EventDispatcher();
        int received = 0;
        var sub = bus.Subscribe<PlayerScored>(_ => received++);

        bus.Publish(new PlayerScored(1));
        Assert.Equal(1, received);

        sub.Dispose();
        bus.Publish(new PlayerScored(2));
        Assert.Equal(1, received); // still 1, not 2
    }

    [Fact]
    public void Publish_NoSubscribers_DoesNotThrow()
    {
        var bus = new EventDispatcher();
        var ex = Record.Exception(() => bus.Publish(new PlayerScored(100)));
        Assert.Null(ex);
    }

    [Fact]
    public void Publish_WithNoMatchingEventType_DoesNotThrow()
    {
        var bus = new EventDispatcher();
        using var sub = bus.Subscribe<GamePhaseChanged>(_ => { });

        // Publish to PlayerScored; only GamePhaseChanged is subscribed.
        var ex = Record.Exception(() => bus.Publish(new PlayerScored(100)));
        Assert.Null(ex);
    }

    [Fact]
    public void SubscriberThrows_OtherSubscribersStillReceive()
    {
        // Error isolation: a throwing subscriber must not break the chain.
        // We inject a silent logger so GD.PrintErr isn't called (which
        // would native-abort the test host without a Godot engine).
        var bus = new EventDispatcher(_ => { });
        int r2 = 0, r3 = 0;
        using var s1 = bus.Subscribe<PlayerScored>(_ =>
        {
            throw new InvalidOperationException("boom");
        });
        using var s2 = bus.Subscribe<PlayerScored>(_ => r2++);
        using var s3 = bus.Subscribe<PlayerScored>(_ => r3++);

        bus.Publish(new PlayerScored(42));

        Assert.Equal(1, r2);
        Assert.Equal(1, r3);
        // s1 threw, but the error-isolation caught the exception and the
        // remaining subscribers received the event.
    }

    [Fact]
    public void SubscriberThrows_LoggerReceivesFormattedMessage()
    {
        // The injected logger receives the formatted error message,
        // including the event type name and exception details.
        var logged = new List<string>();
        var bus = new EventDispatcher(msg => logged.Add(msg));
        using var s1 = bus.Subscribe<PlayerScored>(_ =>
        {
            throw new InvalidOperationException("boom");
        });

        bus.Publish(new PlayerScored(99));

        Assert.Single(logged);
        Assert.Contains("PlayerScored", logged[0]);
        Assert.Contains("boom", logged[0]);
        Assert.Contains("InvalidOperationException", logged[0]);
    }

    [Fact]
    public void DisposeToken_AutoUnsubscribes()
    {
        var bus = new EventDispatcher();
        int received = 0;

        using (bus.Subscribe<PlayerScored>(_ => received++))
        {
            bus.Publish(new PlayerScored(1));
            Assert.Equal(1, received);
        }

        // Token disposed at end of using block.
        bus.Publish(new PlayerScored(2));
        Assert.Equal(1, received);
    }

    [Fact]
    public void DisposeToken_MultipleDispose_IsIdempotent()
    {
        var bus = new EventDispatcher();
        var sub = bus.Subscribe<PlayerScored>(_ => { });

        sub.Dispose();
        var ex = Record.Exception(() => sub.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void DifferentEventTypes_AreIndependentSubscribers()
    {
        var bus = new EventDispatcher();
        int playerReceived = 0, phaseReceived = 0;
        using var s1 = bus.Subscribe<PlayerScored>(_ => playerReceived++);
        using var s2 = bus.Subscribe<GamePhaseChanged>(_ => phaseReceived++);

        bus.Publish(new PlayerScored(10));
        Assert.Equal(1, playerReceived);
        Assert.Equal(0, phaseReceived);

        bus.Publish(new GamePhaseChanged("Playing"));
        Assert.Equal(1, playerReceived);
        Assert.Equal(1, phaseReceived);
    }

    [Fact]
    public void EventTypeCount_ReflectsSubscriptions()
    {
        var bus = new EventDispatcher();
        Assert.Equal(0, bus.EventTypeCount);

        var s1 = bus.Subscribe<PlayerScored>(_ => { });
        Assert.Equal(1, bus.EventTypeCount);

        var s2 = bus.Subscribe<GamePhaseChanged>(_ => { });
        Assert.Equal(2, bus.EventTypeCount);

        // A second subscriber to the same type doesn't increase the count.
        var s3 = bus.Subscribe<PlayerScored>(_ => { });
        Assert.Equal(2, bus.EventTypeCount);

        // Dispose all PlayerScored subscribers → that type's count drops to 0
        // → EventTypeCount drops.
        s1.Dispose();
        s3.Dispose();
        Assert.Equal(1, bus.EventTypeCount);
    }

    [Fact]
    public void SubscriberCount_ReflectsAllSubscribers()
    {
        var bus = new EventDispatcher();
        Assert.Equal(0, bus.SubscriberCount);

        var s1 = bus.Subscribe<PlayerScored>(_ => { });
        var s2 = bus.Subscribe<PlayerScored>(_ => { });
        var s3 = bus.Subscribe<GamePhaseChanged>(_ => { });
        Assert.Equal(3, bus.SubscriberCount);

        s1.Dispose();
        Assert.Equal(2, bus.SubscriberCount);
    }
}
