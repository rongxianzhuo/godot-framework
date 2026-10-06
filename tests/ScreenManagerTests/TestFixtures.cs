using System;
using GameFramework;

namespace ScreenManagerTests;

/// <summary>
/// Test screen classes for <c>ScreenRouterTests</c>. Created via
/// <see cref="System.Runtime.Serialization.FormatterServices.GetUninitializedObject(Type)"/>
/// inside each test so the Control base ctor (which calls Godot native
/// bindings) is skipped — the test only needs the Screen-managed fields
/// (TCS, IsActive, virtual OnShow/OnClose) which work fine without
/// native init.
///
/// Each fixture records its lifecycle calls so tests can assert against
/// state without forcing callers to thread a recording helper through
/// every test.
///
/// Each fixture exposes <c>TriggerClose*</c> helpers — the framework's
/// <c>CloseScreen</c> is <c>protected</c> (only subclasses can call it),
/// so tests use these public wrappers as the canonical way to resolve
/// the pending ShowAsync task.
/// </summary>
public partial class TestScreen : Screen
{
    public bool OnShowCalled { get; set; }
    public bool OnCloseCalled { get; set; }

    protected override void OnShow() => OnShowCalled = true;
    protected override void OnClose() => OnCloseCalled = true;

    /// <summary>Public wrapper to invoke the protected CloseScreen.</summary>
    public void TriggerClose() => CloseScreen();
}

public partial class TestScreenOf<TArg> : Screen<TArg>
{
    public TArg? LastOpenArg { get; set; }
    public bool OnShowCalled { get; set; }

    protected override void OnShow(TArg arg)
    {
        LastOpenArg = arg;
        OnShowCalled = true;
    }

    /// <summary>Public wrapper to invoke the protected CloseScreen.</summary>
    public void TriggerClose() => CloseScreen();
}

public partial class TestScreenOfT<TArg, TResult> : Screen<TArg, TResult>
{
    public TArg? LastOpenArg { get; set; }
    public TResult? LastCloseResult { get; set; }
    public bool OnShowCalled { get; set; }
    public bool OnCloseCalled { get; set; }

    protected override void OnShow(TArg arg)
    {
        LastOpenArg = arg;
        OnShowCalled = true;
    }
    protected override void OnClose(TResult result)
    {
        LastCloseResult = result;
        OnCloseCalled = true;
    }

    /// <summary>Public wrappers to invoke the protected CloseScreen overloads.</summary>
    public void TriggerClose(TResult result) => CloseScreen(result);
    public void TriggerClose() => CloseScreen();
}

/// <summary>OnShow throws — used to test the error logger seam.</summary>
public partial class ThrowingScreen : Screen
{
    public static readonly string ErrorMessage = "test-thrown-by-OnShow";

    protected override void OnShow() => throw new InvalidOperationException(ErrorMessage);

    /// <summary>Public wrapper to invoke the protected CloseScreen.</summary>
    public void TriggerClose() => CloseScreen();
}