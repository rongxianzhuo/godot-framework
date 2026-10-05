using System;
using GameFramework;
using Xunit;

namespace ServiceFrameworkTests;

/// <summary>
/// Unit tests for <see cref="ServiceRegistry"/>. Pure POCO tests — no Godot
/// engine runtime required, because <see cref="ServiceRegistry"/> doesn't
/// inherit any Godot type. Runs under plain `dotnet test`.
/// </summary>
public class ServiceRegistryTests
{
    private sealed class FakeAudioService
    {
        public int Volume { get; set; }
    }

    private sealed class FakeInputService
    {
        public string LastKey { get; set; } = string.Empty;
    }

    [Fact]
    public void Register_Generic_StoresUnderInstanceType()
    {
        var reg = new ServiceRegistry();
        var svc = new FakeAudioService { Volume = 7 };
        reg.Register(svc);

        Assert.Same(svc, reg.Resolve<FakeAudioService>());
        Assert.Equal(7, reg.Resolve<FakeAudioService>().Volume);
    }

    [Fact]
    public void Resolve_UnregisteredType_ThrowsInvalidOperation()
    {
        var reg = new ServiceRegistry();
        var ex = Assert.Throws<InvalidOperationException>(() => reg.Resolve<FakeAudioService>());
        Assert.Contains("FakeAudioService", ex.Message);
        Assert.Contains("not registered", ex.Message);
    }

    [Fact]
    public void TryGet_UnregisteredType_ReturnsNull()
    {
        var reg = new ServiceRegistry();
        Assert.Null(reg.TryGet<FakeAudioService>());
    }

    [Fact]
    public void TryGet_RegisteredType_ReturnsInstance()
    {
        var reg = new ServiceRegistry();
        var svc = new FakeAudioService();
        reg.Register(svc);
        Assert.Same(svc, reg.TryGet<FakeAudioService>());
    }

    [Fact]
    public void Unregister_ExistingType_RemovesAndReturnsTrue()
    {
        var reg = new ServiceRegistry();
        reg.Register(new FakeAudioService());
        Assert.True(reg.Unregister<FakeAudioService>());
        Assert.Null(reg.TryGet<FakeAudioService>());
        Assert.Equal(0, reg.Count);
    }

    [Fact]
    public void Unregister_MissingType_ReturnsFalse()
    {
        var reg = new ServiceRegistry();
        Assert.False(reg.Unregister<FakeAudioService>());
    }

    [Fact]
    public void Register_SameInstanceTwice_DoesNotReplaceOrWarn()
    {
        // Re-registering with the SAME instance is a no-op (no warning, no
        // replacement, Count stays at 1). This is the testable branch — the
        // "different instance replaces" path triggers GD.PrintErr which
        // requires a Godot engine runtime (see skipped test below).
        var reg = new ServiceRegistry();
        var svc = new FakeAudioService();
        reg.Register(svc);
        reg.Register(svc);

        Assert.Same(svc, reg.Resolve<FakeAudioService>());
        Assert.Equal(1, reg.Count);
    }

    [Fact(Skip = "Requires Godot engine runtime: ServiceRegistry.Register calls GD.PrintErr when re-registering with a different instance, which crashes the test host under plain `dotnet test`. See tests/README.md §'Out of scope'.")]
    public void Register_DifferentInstance_ReplacesAndWarns()
    {
        // Documents the implicit contract: when a service of the same Type is
        // registered with a different instance, the new instance replaces the
        // old (Count unchanged) and a warning is emitted via GD.PrintErr.
        // Not runnable under `dotnet test` because GD.PrintErr requires a
        // Godot engine process. Tracked as a future `godot --headless` test.
        var reg = new ServiceRegistry();
        var svc1 = new FakeAudioService { Volume = 1 };
        var svc2 = new FakeAudioService { Volume = 2 };
        reg.Register(svc1);
        reg.Register(svc2);

        Assert.Same(svc2, reg.Resolve<FakeAudioService>());
        Assert.Equal(1, reg.Count);
    }

    [Fact]
    public void Register_DistinctTypes_AllResolvable()
    {
        var reg = new ServiceRegistry();
        var audio = new FakeAudioService();
        var input = new FakeInputService { LastKey = "W" };
        reg.Register(audio);
        reg.Register(input);

        Assert.Equal(2, reg.Count);
        Assert.Same(audio, reg.Resolve<FakeAudioService>());
        Assert.Same(input, reg.Resolve<FakeInputService>());
    }

    [Fact]
    public void Register_NullGenericInstance_Throws()
    {
        var reg = new ServiceRegistry();
        Assert.Throws<ArgumentNullException>(() => reg.Register<FakeAudioService>(null!));
    }

    [Fact]
    public void Register_NullTypeKey_Throws()
    {
        var reg = new ServiceRegistry();
        Assert.Throws<ArgumentNullException>(() => reg.Register(null!, new FakeAudioService()));
    }

    [Fact]
    public void Register_NullExplicitInstance_Throws()
    {
        var reg = new ServiceRegistry();
        Assert.Throws<ArgumentNullException>(() => reg.Register(typeof(FakeAudioService), null!));
    }

    [Fact]
    public void Count_InitiallyZero()
    {
        var reg = new ServiceRegistry();
        Assert.Equal(0, reg.Count);
    }

    [Fact]
    public void Resolve_TypeWithGenericConstraint_OnlyResolvesByExactKey()
    {
        // Resolving by base type when registered under derived type should NOT
        // work. ServiceRegistry does no inheritance walk - exact Type key only.
        // This is the principle tested more thoroughly in
        // DerivedTypeRegistrationTests; here we just confirm the basic case
        // for two unrelated POCO types.
        var reg = new ServiceRegistry();
        reg.Register<FakeAudioService>(new FakeAudioService());
        Assert.Null(reg.TryGet<FakeInputService>());
    }
}
