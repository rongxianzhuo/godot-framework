using System;
using System.Collections.Generic;
using Godot;

namespace GameFramework;

/// <summary>
/// Synchronous, in-process pub/sub event bus. Pure POCO — no Node
/// inheritance, no Godot types in its API surface.
///
/// Design principles:
/// - AOT-friendly: no reflection, no source generators, no expression trees.
/// - Synchronous: Publish invokes subscribers in registration order, on
///   the calling thread.
/// - Error-isolated: a throwing subscriber is caught (and logged via the
///   injected error logger; defaults to GD.PrintErr); remaining subscribers
///   still receive.
/// - Type-keyed: each <c>TEvent</c> type has its own subscriber list.
///
/// Thread-safety: NOT thread-safe. The bus is designed for single-threaded
/// game-loop dispatch. If multi-threaded dispatch is needed, callers must
/// synchronize externally.
///
/// This class is the implementation behind <see cref="EventBus"/>. Kept
/// as a standalone POCO so it can be unit-tested without a Godot engine.
/// The error-logger constructor seam lets tests inject a silent or
/// capturing logger (avoiding GD.PrintErr's native dependency under
/// <c>dotnet test</c>).
/// </summary>
public sealed class EventDispatcher
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly Action<string> _errorLogger;

    /// <summary>
    /// Default constructor: uses <see cref="GD.PrintErr(string)"/> for
    /// error logging. Production code should use this.
    /// </summary>
    public EventDispatcher() : this(DefaultErrorLogger) { }

    /// <summary>
    /// Test seam: inject a custom error logger. The injected delegate is
    /// called once per throwing subscriber, with a formatted message
    /// including the event type name and exception.
    ///
    /// Pass a no-op (`_ => { }`) to silence logging under
    /// <c>dotnet test</c> (where <see cref="GD.PrintErr(string)"/> would
    /// native-abort the test host because no Godot engine is running).
    /// </summary>
    public EventDispatcher(Action<string> errorLogger)
    {
        _errorLogger = errorLogger ?? throw new ArgumentNullException(nameof(errorLogger));
    }

    private static void DefaultErrorLogger(string message)
    {
        GD.PrintErr(message);
    }

    /// <summary>
    /// Publishes <paramref name="evt"/> to all current subscribers of
    /// <typeparamref name="TEvent"/>, in subscription order. A throwing
    /// subscriber is caught (logged) so remaining subscribers still
    /// receive. Publish with no subscribers is a no-op.
    /// </summary>
    public void Publish<TEvent>(TEvent evt) where TEvent : notnull
    {
        if (evt == null) throw new ArgumentNullException(nameof(evt));

        if (!_subscribers.TryGetValue(typeof(TEvent), out var handlers))
            return;

        // Snapshot so subscribers that mutate the list during dispatch
        // (e.g. subscribe or unsubscribe from within a handler) don't
        // break iteration.
        var snapshot = handlers.ToArray();
        foreach (var handler in snapshot)
        {
            try
            {
                ((Action<TEvent>)handler)(evt);
            }
            catch (Exception ex)
            {
                _errorLogger($"[EventDispatcher] Subscriber threw while handling {typeof(TEvent).Name}: {ex}");
            }
        }
    }

    /// <summary>
    /// Subscribes <paramref name="handler"/> to events of type
    /// <typeparamref name="TEvent"/>. Returns an <see cref="IDisposable"/>
    /// token — disposing it unsubscribes. Use with <c>using var sub = ...</c>
    /// for scope-bound subscriptions.
    ///
    /// Dispose is idempotent and safe to call multiple times.
    /// </summary>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        if (!_subscribers.TryGetValue(typeof(TEvent), out var handlers))
        {
            handlers = new List<Delegate>();
            _subscribers[typeof(TEvent)] = handlers;
        }
        handlers.Add(handler);

        return new SubscriptionToken(this, typeof(TEvent), handler);
    }

    /// <summary>Number of distinct event types with at least one subscriber.</summary>
    public int EventTypeCount => _subscribers.Count;

    /// <summary>Total active subscribers across all event types.</summary>
    public int SubscriberCount
    {
        get
        {
            int count = 0;
            foreach (var list in _subscribers.Values) count += list.Count;
            return count;
        }
    }

    private void UnsubscribeInternal(Type eventType, Delegate handler)
    {
        if (!_subscribers.TryGetValue(eventType, out var handlers))
            return;
        handlers.Remove(handler);
        if (handlers.Count == 0)
            _subscribers.Remove(eventType);
    }

    private sealed class SubscriptionToken : IDisposable
    {
        private EventDispatcher? _dispatcher;
        private readonly Type _eventType;
        private readonly Delegate _handler;

        public SubscriptionToken(EventDispatcher dispatcher, Type eventType, Delegate handler)
        {
            _dispatcher = dispatcher;
            _eventType = eventType;
            _handler = handler;
        }

        public void Dispose()
        {
            // Idempotent: only the first Dispose actually unsubscribes.
            // Interlocked.Exchange ensures thread-safety on the dispose
            // flag itself, even though the bus is otherwise not thread-safe.
            var d = System.Threading.Interlocked.Exchange(ref _dispatcher, null);
            d?.UnsubscribeInternal(_eventType, _handler);
        }
    }
}
