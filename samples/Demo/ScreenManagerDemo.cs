using System.Threading.Tasks;
using Godot;
using GameFramework;

namespace GameFramework.Demo;

/// <summary>
/// Minimal runnable demo of <see cref="ScreenManager"/>. Demonstrates:
///   - Single-screen swap pattern (one Screen at a time)
///   - <c>ShowAsync<Screen>()</c> (no arg, no result)
///   - <c>ShowAsync<Screen<T>, T>(arg)</c> (typed open, no result) — for
///     <see cref="GameScreen"/> which has both typed open and result
///   - <c>ShowAsync<Screen<T1, T2>, T1, T2>(arg)</c> → <c>Task<T2></c>
///     returning the typed close result
///
/// Run via <see cref="DemoBootstrap"/> after the existing UI flow:
/// <code>
///   await ScreenManagerDemo.Run(ScreenManager.Instance);
/// </code>
///
/// Design notes:
///   - The manager is passed as a parameter (not pulled from
///     <see cref="ScreenManager.Instance"/>) so the demo could be tested
///     against a plain <see cref="ScreenRouter"/> if needed.
///   - Two screens exercise both the non-typed (<see cref="TitleScreen"/>)
///     and the typed-open + typed-result (<see cref="GameScreen"/>) variants.
///   - Auto-clicking for headless smoke test is handled by
///     <see cref="DemoBootstrap"/> (it reaches into the screen via the
///     scene tree and emits <c>Pressed</c> on the button).
/// </summary>
public static class ScreenManagerDemo
{
    public static async Task Run(ScreenManager manager)
    {
        GD.Print("[ScreenManagerDemo] Starting");

        // Step 1: Non-typed screen — ShowAsync<TitleScreen>() returns Task
        //         (no typed result). Resolves when TitleScreen calls
        //         CloseScreen() from its Play button.
        GD.Print("[ScreenManagerDemo] Step 1: ShowAsync<TitleScreen>()");
        await manager.ShowAsync<TitleScreen>();
        GD.Print("[ScreenManagerDemo] TitleScreen closed");

        // Step 2: Typed-open + typed-result screen.
        //         ShowAsync<GameScreen, string, string>(arg) returns Task<string>
        //         resolving to whatever GameScreen passes to CloseScreen(result).
        GD.Print("[ScreenManagerDemo] Step 2: ShowAsync<GameScreen, string, string>('world1')");
        var score = await manager.ShowAsync<GameScreen, string, string>("world1");
        GD.Print($"[ScreenManagerDemo] GameScreen returned '{score}'");

        GD.Print("[ScreenManagerDemo] Done");
    }
}