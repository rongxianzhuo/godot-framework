using Godot;

namespace GameFramework.Demo;

/// <summary>
/// Main menu panel. Demonstrates:
///   - UIPanel&lt;TOpenArg, TCloseArg&gt; with string close result ("play" / "quit")
///   - Building UI tree in C# (no .tscn needed)
///   - Returning a typed result back to the caller
///   - Push of another panel from inside this panel's button handler
/// </summary>
public sealed partial class MainMenuPanel : UIPanel<string, string>
{
    private Button _playButton = null!;
    private Button _settingsButton = null!;
    private Button _quitButton = null!;

    public MainMenuPanel()
    {
        // Build the UI tree here. Children get reparented to PanelStackContainer
        // when UIManager.PushInternalAsync calls AddChild on this panel.
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var vbox = new VBoxContainer
        {
            Name = "VBox",
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -200, OffsetRight = 200,
            OffsetTop = -120, OffsetBottom = 120,
        };
        AddChild(vbox);

        var title = new Label { Name = "Title", Text = "GameFramework MVP", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        vbox.AddChild(title);

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 24) };
        vbox.AddChild(spacer);

        _playButton = new Button { Name = "PlayButton", Text = "Play" };
        _settingsButton = new Button { Name = "SettingsButton", Text = "Settings" };
        _quitButton = new Button { Name = "QuitButton", Text = "Quit" };
        vbox.AddChild(_playButton);
        vbox.AddChild(_settingsButton);
        vbox.AddChild(_quitButton);

        _playButton.Pressed += OnPlayPressed;
        _settingsButton.Pressed += OnSettingsPressed;
        _quitButton.Pressed += OnQuitPressed;
    }

    protected override void OnOpen(string arg)
    {
        GD.Print($"[MainMenuPanel] OnOpen(arg='{arg}')");
        _playButton.GrabFocus();
    }

    protected override void OnClose(string result)
    {
        GD.Print($"[MainMenuPanel] OnClose(result='{result}')");
    }

    public override void _Ready()
    {
        GD.Print($"[MainMenuPanel] _Ready called. IsInsideTree: {IsInsideTree()}");
    }

    private void OnPlayPressed()
    {
        GD.Print("[MainMenuPanel] Play clicked → ClosePanel('play')");
        ClosePanel("play");
    }

    private void OnSettingsPressed()
    {
        GD.Print("[MainMenuPanel] Settings clicked → push SettingsPanel");
        // Fire-and-forget push of another panel; this MainMenuPanel stays on stack
        // underneath. When SettingsPanel closes, control returns here.
        _ = UIManager.Instance.PushAsync<SettingsPanel, float>(0f);
    }

    private void OnQuitPressed()
    {
        GD.Print("[MainMenuPanel] Quit clicked → ClosePanel('quit')");
        ClosePanel("quit");
    }
}