using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using GameFramework;
using Xunit;

namespace ScreenManagerTests;

/// <summary>
/// POCO behavior tests for <see cref="ScreenRouter"/>. No Godot runtime
/// required — tests use
/// <see cref="FormatterServices.GetUninitializedObject(Type)"/> to
/// create uninitialized Screen instances without invoking the Control
/// base ctor (which would call Godot native bindings unavailable under
/// plain <c>dotnet test</c>).
///
/// Covers Mark's W3 Day 2 runtime test plan:
///   1. <c>ShowAsync_DefaultScreen_InvokesOnShow</c>
///   2. <c>ShowAsync_WithOpenArg_PassesArgToScreen</c>
///   3. <c>ShowAsync_WithCloseResult_ResolvesTask</c>
///   4. <c>ShowAsync_TwoScreensSequentially_LifecycleCorrect</c>
///   5. <c>ShowAsync_DuringTransition_Throws</c>
///
/// Plus a few state-machine + event + error-logger sanity tests.
/// </summary>
public class ScreenRouterTests
{
    private static T CreateScreen<T>() where T : Screen
    {
#pragma warning disable SYSLIB0050 // FormatterServices is obsolete but used intentionally here to bypass the Control base ctor (Godot native binding) for unit tests.
        return (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
    }

    // ============================================================
    // INITIAL STATE
    // ============================================================

    [Fact]
    public void DefaultConstructor_DoesNotThrow()
    {
        var router = new ScreenRouter();
        Assert.NotNull(router);
    }

    [Fact]
    public void InitialState_CurrentIsNull()
    {
        var router = new ScreenRouter();
        Assert.Null(router.Current);
    }

    [Fact]
    public void InitialState_IsShowingIsFalse()
    {
        var router = new ScreenRouter();
        Assert.False(router.IsShowing);
    }

    // ============================================================
    // MARK'S 5 RUNTIME TESTS
    // ============================================================

    /// <summary>
    /// Test 1 (was <c>ShowAsync_DefaultScreen_RendersOnRoot</c> in the spec).
    /// In POCO split, "renders on root" becomes "attach callback fires".
    /// The actual AddChild lives in ScreenManager (covered by smoke test
    /// when the demo lands).
    /// </summary>
    [Fact]
    public async Task ShowAsync_DefaultScreen_InvokesOnShow()
    {
        var screen = CreateScreen<TestScreen>();
        bool attachCalled = false;
        bool detachCalled = false;

        var router = new ScreenRouter(
            screenFactory: _ => screen,
            attach: _ => attachCalled = true,
            detach: _ => detachCalled = true);

        var task = router.ShowAsync<TestScreen>();

        Assert.True(screen.OnShowCalled,
            "OnShow should fire synchronously during ShowAsync (before the close-task await).");
        Assert.True(attachCalled,
            "Attach callback should fire before the close-task await.");
        Assert.False(detachCalled,
            "Detach callback should NOT fire yet (screen hasn't been closed).");
        Assert.Same(screen, router.Current);
        Assert.True(router.IsShowing);

        // Resolve the close task and verify post-close state.
        screen.TriggerClose();
        await task;
        Assert.True(task.IsCompletedSuccessfully);
        Assert.True(detachCalled,
            "Detach callback should fire in the finally block after close.");
        Assert.True(screen.OnCloseCalled,
            "OnClose should fire after CloseScreen (finally block invokes InvokeOnClose).");
        Assert.False(router.IsShowing);
        Assert.Null(router.Current);
    }

    /// <summary>
    /// Test 2 (<c>ShowAsync_WithOpenArg_PassesArgToScreen</c>).
    /// </summary>
    [Fact]
    public async Task ShowAsync_WithOpenArg_PassesArgToScreen()
    {
        var screen = CreateScreen<TestScreenOf<string>>();
        var router = new ScreenRouter(_ => screen);

        var task = router.ShowAsync<TestScreenOf<string>, string>("hello");

        Assert.True(screen.OnShowCalled);
        Assert.Equal("hello", screen.LastOpenArg);

        screen.TriggerClose();
        await task;
    }

    /// <summary>
    /// Test 3 (<c>ShowAsync_WithCloseResult_ResolvesTask</c>).
    /// </summary>
    [Fact]
    public async Task ShowAsync_WithCloseResult_ResolvesTask()
    {
        var screen = CreateScreen<TestScreenOfT<string, int>>();
        var router = new ScreenRouter(_ => screen);

        var task = router.ShowAsync<TestScreenOfT<string, int>, string, int>("hello");

        Assert.True(screen.OnShowCalled);
        Assert.Equal("hello", screen.LastOpenArg);
        Assert.False(task.IsCompleted,
            "Task should be pending until the screen calls CloseScreen(result).");

        screen.TriggerClose(42);

        var result = await task;
        Assert.Equal(42, result);
        Assert.Equal(42, screen.LastCloseResult);
    }

    /// <summary>
    /// Test 4 (<c>ShowAsync_TwoScreensSequentially_LifecycleCorrect</c>).
    /// </summary>
    [Fact]
    public async Task ShowAsync_TwoScreensSequentially_LifecycleCorrect()
    {
        var screen1 = CreateScreen<TestScreen>();
        var screen2 = CreateScreen<TestScreen>();

        int callCount = 0;
        var router = new ScreenRouter(_ =>
        {
            callCount++;
            return callCount == 1 ? screen1 : screen2;
        });

        // First show
        var task1 = router.ShowAsync<TestScreen>();
        Assert.Same(screen1, router.Current);
        Assert.True(screen1.OnShowCalled);
        Assert.False(screen2.OnShowCalled);

        screen1.TriggerClose();
        await task1;

        Assert.False(router.IsShowing);
        Assert.Null(router.Current);
        Assert.True(screen1.OnCloseCalled,
            "First screen's OnClose should fire when CloseScreen is called.");

        // Second show — must succeed after first is fully closed.
        var task2 = router.ShowAsync<TestScreen>();
        Assert.Same(screen2, router.Current);
        Assert.True(screen2.OnShowCalled);
        Assert.False(screen2.OnCloseCalled);

        screen2.TriggerClose();
        await task2;

        Assert.False(router.IsShowing);
        Assert.Null(router.Current);
        Assert.True(screen2.OnCloseCalled);
    }

    /// <summary>
    /// Test 5 (<c>ShowAsync_DuringTransition_QueuesOrThrows</c>) —
    /// our implementation chooses <strong>throws</strong>: calling
    /// ShowAsync while another Show is in progress throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public async Task ShowAsync_DuringTransition_Throws()
    {
        var screen1 = CreateScreen<TestScreen>();
        var screen2 = CreateScreen<TestScreen>();

        int callCount = 0;
        var router = new ScreenRouter(_ =>
        {
            callCount++;
            return callCount == 1 ? screen1 : screen2;
        });

        var task1 = router.ShowAsync<TestScreen>();
        Assert.True(router.IsShowing);
        Assert.Same(screen1, router.Current);

        // While task1 is awaiting its screen's close, try to show another — must throw.
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await router.ShowAsync<TestScreen>());

        // First show must still complete cleanly.
        screen1.TriggerClose();
        await task1;
        Assert.False(router.IsShowing);
        Assert.False(screen2.OnShowCalled,
            "Second screen's OnShow must not fire because ShowAsync threw before attaching it.");
    }

    // ============================================================
    // EVENTS + ERROR LOGGING
    // ============================================================

    [Fact]
    public async Task ShowAsync_RaisesScreenShownAndScreenClosed()
    {
        var screen = CreateScreen<TestScreen>();
        var router = new ScreenRouter(_ => screen);

        Screen? shownScreen = null;
        Screen? closedScreen = null;
        router.ScreenShown += s => shownScreen = s;
        router.ScreenClosed += s => closedScreen = s;

        var task = router.ShowAsync<TestScreen>();
        Assert.Same(screen, shownScreen);
        Assert.Null(closedScreen);

        screen.TriggerClose();
        await task;

        Assert.Same(screen, closedScreen);
    }

    [Fact]
    public async Task OnShowThrow_CapturesViaErrorLogger()
    {
        // The error logger seam is the same pattern as EventDispatcher's
        // — default GD.PrintErr would native-abort under plain dotnet test.
        var screen = CreateScreen<ThrowingScreen>();
        var errors = new List<string>();
        var router = new ScreenRouter(
            screenFactory: _ => screen,
            attach: _ => { },
            detach: _ => { },
            errorLogger: errors.Add);

        var task = router.ShowAsync<ThrowingScreen>();
        screen.TriggerClose();
        await task;

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("OnShow threw") && e.Contains(ThrowingScreen.ErrorMessage));
    }
}