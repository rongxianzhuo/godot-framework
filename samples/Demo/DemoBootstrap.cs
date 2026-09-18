using Godot;

namespace GameFramework.Demo;

/// <summary>
/// Demo bootstrap. Attached to the root node of demo.tscn.
///
/// Drives the full framework round-trip:
///   1. Register a POCO service (AudioService).
///   2. Push MainMenuPanel → await typed string result.
///   3. Push ConfirmDialog → await typed bool result.
///   4. Quit cleanly.
///
/// For headless smoke testing, button presses are simulated via Timer-driven
/// EmitSignal calls. In a real game, the user clicks for real.
/// </summary>
public sealed partial class DemoBootstrap : Node
{
    public override async void _Ready()
    {
        GD.Print("[DemoBootstrap] _Ready. Waiting for GameFramework autoload...");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (GameFramework.Instance == null)
        {
            GD.PrintErr("[DemoBootstrap] GameFramework autoload not found. Aborting.");
            GetTree().Quit(1);
            return;
        }

        // 1) Register a POCO service.
        GameFramework.Instance.Services.Register(new AudioService());
        GD.Print($"[DemoBootstrap] Services registered: {GameFramework.Instance.Services.Count}");

        // 2) Push MainMenuPanel.
        GD.Print("[DemoBootstrap] Step 1: Push MainMenuPanel(arg='initial')");
        var mainTask = UIManager.Instance.PushAsync<MainMenuPanel, string, string>("initial");

        // Auto-click Play after a brief settle delay.
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        if (UIManager.Instance.Current is MainMenuPanel mp)
        {
            // First click Settings to test UIPanel<float> path, then Play to return.
            GD.Print("[DemoBootstrap] Auto-click MainMenuPanel.SettingsButton (exercises SettingsPanel)");
            mp.GetNode<Button>("VBox/SettingsButton").EmitSignal(BaseButton.SignalName.Pressed);

            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            if (UIManager.Instance.Current is SettingsPanel sp)
            {
                GD.Print("[DemoBootstrap] Auto-click SettingsPanel.Back (exercises UIPanel<float> pop)");
                sp.GetNode<Button>("VBox/BackButton").EmitSignal(BaseButton.SignalName.Pressed);
            }

            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            GD.Print("[DemoBootstrap] Auto-click MainMenuPanel.PlayButton");
            mp.GetNode<Button>("VBox/PlayButton").EmitSignal(BaseButton.SignalName.Pressed);
        }

        var mainResult = await mainTask;
        GD.Print($"[DemoBootstrap] MainMenuPanel returned '{mainResult}'");

        if (mainResult == "play")
        {
            // 3) Push ConfirmDialog.
            GD.Print("[DemoBootstrap] Step 2: Push ConfirmDialog(message='Start game?')");
            var confirmTask = UIManager.Instance.PushAsync<ConfirmDialog, string, bool>("Start game?");

            // Auto-click Confirm.
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            if (UIManager.Instance.Current is ConfirmDialog dlg)
            {
                GD.Print("[DemoBootstrap] Auto-click ConfirmDialog.Confirm");
                dlg.GetNode<Button>("Panel/VBox/HBox/Confirm").EmitSignal(BaseButton.SignalName.Pressed);
            }

            var confirmResult = await confirmTask;
            GD.Print($"[DemoBootstrap] ConfirmDialog returned {confirmResult}");
        }

        // Let any deferred prints flush.
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        GD.Print("[DemoBootstrap] Demo complete. Quitting.");
        GetTree().Quit();
    }
}