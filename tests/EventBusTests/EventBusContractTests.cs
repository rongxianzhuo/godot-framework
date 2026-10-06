using System.Reflection;
using GameFramework;
using Godot;
using Xunit;

namespace EventBusTests;

/// <summary>
/// Structural contract tests for <see cref="EventBus"/> — the Node wrapper
/// around <see cref="EventDispatcher"/>. Verifies the type shape without
/// instantiating (full lifecycle tests require a Godot engine runtime and
/// are deferred until a `godot --headless` test runner is available).
/// </summary>
public class EventBusContractTests
{
    [Fact]
    public void EventBus_IsSealed()
    {
        Assert.True(typeof(EventBus).IsSealed,
            "EventBus must be sealed — it owns the static Instance and there should be no subclasses.");
    }

    [Fact]
    public void EventBus_IsGameServiceSubclass()
    {
        // EventBus inherits GameService → GameService inherits Node.
        // This makes EventBus autoload-eligible: Game's autoload setup
        // adds it to the scene tree, GameService._Ready registers it with
        // ServiceRegistry, then EventBus._Ready sets Instance.
        Assert.True(typeof(GameService).IsAssignableFrom(typeof(EventBus)),
            "EventBus must inherit GameService so it auto-registers with Game.Instance.Services.");
        Assert.True(typeof(Node).IsAssignableFrom(typeof(EventBus)),
            "EventBus must be a Node so it can be an autoload.");
    }

    [Fact]
    public void EventBus_HasInstanceStaticProperty()
    {
        var prop = typeof(EventBus).GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(prop);
        Assert.Equal(typeof(EventBus), prop!.PropertyType);

        // Setter must be private (consumers should never reassign).
        var setter = prop.SetMethod;
        Assert.NotNull(setter);
        Assert.True(setter!.IsPrivate,
            "EventBus.Instance setter must be private — only _Ready should assign it.");
    }

    [Fact]
    public void EventBus_HasReadyAndExitTreeOverrides()
    {
        // _Ready must be a concrete override so Instance gets set after
        // GameService registration.
        var ready = typeof(EventBus).GetMethod(
            "_Ready",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(ready);
        Assert.False(ready!.IsAbstract);

        // _ExitTree must clear Instance before teardown.
        var exit = typeof(EventBus).GetMethod(
            "_ExitTree",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(exit);
        Assert.False(exit!.IsAbstract);
    }

    [Fact]
    public void EventBus_PublishAndSubscribeMatchDispatcherSignature()
    {
        // The wrapper must forward Publish/Subscribe to the dispatcher with
        // matching shape (same generic arity, same parameter count). The
        // actual `where TEvent : notnull` constraint is enforced by the
        // compiler (EventBus.Subscribe forwards directly to dispatcher.Subscribe).
        var busPublish = typeof(EventBus).GetMethod("Publish")!;
        var dispPublish = typeof(EventDispatcher).GetMethod("Publish")!;
        Assert.Single(busPublish.GetGenericArguments());
        Assert.Single(dispPublish.GetGenericArguments());
        Assert.Single(busPublish.GetParameters());
        Assert.Single(dispPublish.GetParameters());

        var busSub = typeof(EventBus).GetMethod("Subscribe")!;
        var dispSub = typeof(EventDispatcher).GetMethod("Subscribe")!;
        Assert.Single(busSub.GetGenericArguments());
        Assert.Single(dispSub.GetGenericArguments());
        Assert.Single(busSub.GetParameters());
        Assert.Single(dispSub.GetParameters());
    }

    [Fact]
    public void EventDispatcher_IsSealedAndPublic()
    {
        // EventDispatcher is exposed publicly so it can be used standalone
        // (e.g., in tests, or in non-Node contexts like tooling).
        Assert.True(typeof(EventDispatcher).IsPublic);
        Assert.True(typeof(EventDispatcher).IsSealed,
            "EventDispatcher is sealed — no inheritance hierarchy needed.");
    }
}
