using GameFramework;
using Xunit;

namespace ServiceFrameworkTests;

/// <summary>
/// Verifies the "register under derived type" pattern that
/// <c>GameService._Ready</c> uses internally:
/// <c>Game.Instance.Services.Register(this.GetType(), this);</c>
///
/// These tests use plain POCO classes (no <c>Node</c> inheritance) so they
/// can run under plain <c>dotnet test</c> without a Godot engine. The same
/// principle applies identically to <c>GameService</c> subclasses — see
/// README §"Avoiding Common Pitfalls" §1 for the original analysis.
/// </summary>
public class DerivedTypeRegistrationTests
{
    public class BaseService { }
    public class DerivedService : BaseService { }
    public class MoreDerivedService : DerivedService { }

    [Fact]
    public void RegisterUnderDerived_ResolveDerived_Works()
    {
        // The GameService pattern: register by GetType() (which is the
        // runtime-derived type at the call site).
        var reg = new ServiceRegistry();
        var svc = new DerivedService();
        reg.Register(typeof(DerivedService), svc);

        Assert.Same(svc, reg.Resolve<DerivedService>());
    }

    [Fact]
    public void RegisterUnderDerived_ResolveBase_ReturnsNull()
    {
        // Documents the contract: storing under Derived means Base lookups
        // miss. This is intentional — it lets Resolve<UIManager>() find the
        // UIManager singleton (stored under typeof(UIManager)) without
        // colliding with another GameService subclass also registered under
        // the same base type.
        var reg = new ServiceRegistry();
        var svc = new DerivedService();
        reg.Register(typeof(DerivedService), svc);

        Assert.Null(reg.TryGet<BaseService>());
    }

    [Fact]
    public void NaiveGenericRegisterOnBase_ResolveBase_Works()
    {
        // The naive pattern: Register<T>(this) from a base-class method.
        // Here T is the static type at the call site (BaseService), so the
        // service is stored under typeof(BaseService). Base lookups succeed.
        var reg = new ServiceRegistry();
        var svc = new DerivedService();
        reg.Register<BaseService>(svc);

        Assert.Same(svc, reg.Resolve<BaseService>());
    }

    [Fact]
    public void NaiveGenericRegisterOnBase_ResolveDerived_Fails()
    {
        // The bug the naive pattern causes: derived lookups fail because
        // the service was stored under the base type.
        var reg = new ServiceRegistry();
        var svc = new DerivedService();
        reg.Register<BaseService>(svc);

        Assert.Null(reg.TryGet<DerivedService>());
        Assert.Throws<System.InvalidOperationException>(() => reg.Resolve<DerivedService>());
    }

    [Fact]
    public void DerivedTypeRegistration_ThreeLevelChain_OnlyExactKeyMatches()
    {
        // Multi-level inheritance: Base → Derived → MoreDerived.
        // Registration under MoreDerived means neither Derived nor Base
        // lookups find it. ServiceRegistry does no inheritance walk.
        var reg = new ServiceRegistry();
        var svc = new MoreDerivedService();
        reg.Register(typeof(MoreDerivedService), svc);

        Assert.Same(svc, reg.Resolve<MoreDerivedService>());
        Assert.Null(reg.TryGet<DerivedService>());
        Assert.Null(reg.TryGet<BaseService>());
    }

    [Fact]
    public void SiblingTypes_AreIndependentKeys()
    {
        // Two siblings of the same base type, both registered under their
        // own derived type, are independently resolvable.
        var reg = new ServiceRegistry();
        var audio = new DerivedService();
        var input = new SiblingService();
        reg.Register(typeof(DerivedService), audio);
        reg.Register(typeof(SiblingService), input);

        Assert.Same(audio, reg.Resolve<DerivedService>());
        Assert.Same(input, reg.Resolve<SiblingService>());
        Assert.Null(reg.TryGet<BaseService>());
        Assert.Equal(2, reg.Count);
    }

    public class SiblingService : BaseService { }
}
