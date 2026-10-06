using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameFramework;
using Xunit;

namespace ScreenManagerTests;

/// <summary>
/// Structural contract tests for <see cref="ScreenManager"/>, <see cref="Screen"/>,
/// and the typed <see cref="Screen{T}"/> / <see cref="Screen{T1, T2}"/> variants.
///
/// Pure reflection tests — no instantiation, no Godot engine runtime required.
/// Runtime tests (actual ShowAsync behavior) are deferred to a godot --headless
/// runner — see CHANGELOG.md v0.4-alpha Known Issues.
/// </summary>
public class ScreenManagerContractTests
{
    [Fact]
    public void ScreenManager_IsSealed()
    {
        Assert.True(typeof(ScreenManager).IsSealed,
            "ScreenManager must be sealed — it owns the static Instance pointer.");
    }

    [Fact]
    public void ScreenManager_IsGameServiceSubclass()
    {
        Assert.True(typeof(GameService).IsAssignableFrom(typeof(ScreenManager)),
            "ScreenManager must inherit GameService so it auto-registers with Game.Instance.Services.");
        Assert.True(typeof(Godot.Node).IsAssignableFrom(typeof(ScreenManager)),
            "ScreenManager must be a Node so it can be an autoload.");
    }

    [Fact]
    public void ScreenManager_HasInstanceStaticProperty_WithPrivateSetter()
    {
        var prop = typeof(ScreenManager).GetProperty(
            "Instance",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(prop);
        Assert.Equal(typeof(ScreenManager), prop!.PropertyType);

        var setter = prop.SetMethod;
        Assert.NotNull(setter);
        Assert.True(setter!.IsPrivate,
            "ScreenManager.Instance setter must be private — only _Ready should assign it.");
    }

    [Fact]
    public void ScreenManager_HasCurrentReadOnlyProperty_OfTypeScreen()
    {
        var prop = typeof(ScreenManager).GetProperty(
            "Current",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(prop);
        Assert.Equal(typeof(Screen), prop!.PropertyType);
        Assert.Null(prop.SetMethod);
    }

    [Fact]
    public void ScreenManager_HasScreenShownAndScreenClosedEvents()
    {
        var shownEvent = typeof(ScreenManager).GetEvent("ScreenShown");
        Assert.NotNull(shownEvent);
        Assert.Equal(typeof(Action<Screen>), shownEvent!.EventHandlerType);

        var closedEvent = typeof(ScreenManager).GetEvent("ScreenClosed");
        Assert.NotNull(closedEvent);
        Assert.Equal(typeof(Action<Screen>), closedEvent!.EventHandlerType);
    }

    [Fact]
    public void ScreenManager_HasThreeShowAsyncOverloads()
    {
        var showAsyncMethods = typeof(ScreenManager)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name == "ShowAsync" && m.IsGenericMethodDefinition)
            .OrderBy(m => m.GetGenericArguments().Length)
            .ToArray();

        Assert.Equal(3, showAsyncMethods.Length);

        // ShowAsync<TScreen>()  — 1 generic argument
        var m1 = showAsyncMethods[0];
        Assert.Single(m1.GetGenericArguments());
        Assert.Empty(m1.GetParameters());
        var t1 = m1.GetGenericArguments()[0].GetGenericParameterConstraints();
        Assert.Contains(t1, c => typeof(Screen).IsAssignableFrom(c));

        // ShowAsync<TScreen, TOpenArg>(arg)  — 2 generic arguments
        var m2 = showAsyncMethods[1];
        Assert.Equal(2, m2.GetGenericArguments().Length);
        Assert.Single(m2.GetParameters());
        var t2 = m2.GetGenericArguments()[1].GetGenericParameterConstraints();
        Assert.Empty(t2); // TOpenArg is unconstrained

        // ShowAsync<TScreen, TOpenArg, TCloseResult>(arg)  — 3 generic arguments
        var m3 = showAsyncMethods[2];
        Assert.Equal(3, m3.GetGenericArguments().Length);
        Assert.Single(m3.GetParameters());
    }

    [Fact]
    public void Screen_IsAbstractControl()
    {
        // Screen : Control (not just a marker interface) — it needs to be a Node
        // for AddChild/RemoveChild in ScreenManager.
        Assert.True(typeof(Screen).IsAbstract,
            "Screen is the base class — consumers subclass it.");
        Assert.True(typeof(Godot.Control).IsAssignableFrom(typeof(Screen)),
            "Screen must inherit Godot.Control so ScreenManager can AddChild it.");
    }

    [Fact]
    public void Screen_GenericVariantsAreAbstract()
    {
        Assert.True(typeof(Screen<>).IsAbstract,
            "Screen<T> is abstract — consumers subclass it.");
        Assert.True(typeof(Screen<,>).IsAbstract,
            "Screen<T1, T2> is abstract — consumers subclass it.");
    }

    [Fact]
    public void Screen_GenericVariantsInheritFromScreen()
    {
        Assert.True(typeof(Screen).IsAssignableFrom(typeof(Screen<>)),
            "Screen<T> must inherit Screen.");
        Assert.True(typeof(Screen).IsAssignableFrom(typeof(Screen<,>)),
            "Screen<T1, T2> must inherit Screen (sibling to Screen<T>, not nested).");
    }

    [Fact]
    public void Screen_HasProtectedCloseScreenMethod()
    {
        var method = typeof(Screen).GetMethod(
            "CloseScreen",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            Type.EmptyTypes,
            null);
        Assert.NotNull(method);
        Assert.True(method!.IsFamily || method.IsFamilyOrAssembly || method.IsPublic,
            "CloseScreen must be accessible to subclasses.");
    }

    [Fact]
    public void ScreenT1T2_HasTypedCloseScreenOverloads()
    {
        // Use DeclaredOnly so we only see methods on Screen<T1, T2> itself,
        // not the inherited Screen.CloseScreen(). (Both CloseScreen()
        // overloads coexist — the derived one hides the base via `new`.)
        var closeScreenMethods = typeof(Screen<,>)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "CloseScreen")
            .ToArray();

        // Expect two: CloseScreen(TCloseResult) and CloseScreen() (default).
        Assert.Equal(2, closeScreenMethods.Length);

        Assert.Contains(closeScreenMethods, m =>
            m.GetParameters().Length == 1);
        Assert.Contains(closeScreenMethods, m =>
            m.GetParameters().Length == 0);
    }
}