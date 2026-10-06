using Godot;
using GameFramework;

namespace GameFramework.Demo;

/// <summary>
/// Demo game screen. Demonstrates:
///   - <see cref="Screen{TOpenArg, TCloseResult}"/> with typed open + result
///   - Showing a typed open argument via <c>ShowAsync<GameScreen, string, string>(level)</c>
///   - Returning a typed result via <c>CloseScreen(result)</c>
/// </summary>
public sealed partial class GameScreen : Screen<string, string>
{
    private Label _levelLabel = null!;
    private Button _endButton = null!;

    public GameScreen()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect
        {
            Name = "Background",
            Color = new Color(0.15f, 0.25f, 0.15f),
            AnchorRight = 1.0f, AnchorBottom = 1.0f,
        };
        AddChild(bg);

        var vbox = new VBoxContainer
        {
            Name = "VBox",
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -250, OffsetRight = 250,
            OffsetTop = -150, OffsetBottom = 150,
        };
        bg.AddChild(vbox);

        var title = new Label
        {
            Text = "Game Screen",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        vbox.AddChild(title);

        _levelLabel = new Label
        {
            Name = "LevelLabel",
            Text = "Level: ?",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vbox.AddChild(_levelLabel);

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 24) };
        vbox.AddChild(spacer);

        _endButton = new Button
        {
            Name = "EndButton",
            Text = "End Game (returns 'score:100')",
        };
        _endButton.Pressed += OnEndPressed;
        vbox.AddChild(_endButton);
    }

    protected override void OnShow(string level)
    {
        GD.Print($"[GameScreen] OnShow(level='{level}')");
        _levelLabel.Text = $"Level: {level}";
        _endButton.GrabFocus();
    }

    protected override void OnClose(string result)
    {
        GD.Print($"[GameScreen] OnClose(result='{result}')");
    }

    private void OnEndPressed()
    {
        GD.Print("[GameScreen] End clicked → CloseScreen('score:100')");
        CloseScreen("score:100");
    }
}