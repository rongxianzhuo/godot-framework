using GameFramework;
using Godot;
using Xunit;

namespace ServiceFrameworkTests;

/// <summary>
/// Structural contract tests for <see cref="GameService"/>. These verify the
/// type shape (abstract, Node subclass) without instantiating it — full
/// lifecycle tests (verifying that <c>_Ready()</c> actually registers under
/// the derived type) require a Godot engine runtime and are out of scope for
/// v0.3 POCO-only test suite. See tests/README.md.
/// </summary>
public class GameServiceContractTests
{
    [Fact]
    public void GameService_IsAbstract()
    {
        Assert.True(typeof(GameService).IsAbstract,
            "GameService must be abstract — it has no meaningful default behavior.");
    }

    [Fact]
    public void GameService_IsNodeSubclass()
    {
        Assert.True(typeof(Node).IsAssignableFrom(typeof(GameService)),
            "GameService must inherit Godot.Node so it can be added to the scene tree and use _Ready/_ExitTree lifecycle hooks.");
    }

    [Fact]
    public void GameService_HasReadyHook()
    {
        // _Ready is the entry point that performs auto-registration.
        var method = typeof(GameService).GetMethod(
            "_Ready",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.False(method!.IsAbstract,
            "_Ready must be a concrete override, not abstract (concrete subclasses inherit it).");
    }

    [Fact]
    public void GameService_HasExitTreeHook()
    {
        // _ExitTree must be a concrete override so cleanup runs automatically.
        var method = typeof(GameService).GetMethod(
            "_ExitTree",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.False(method!.IsAbstract);
    }
}
