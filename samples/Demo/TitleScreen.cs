using Godot;
using GameFramework;

namespace GameFramework.Demo;

/// <summary>
/// Demo title screen. Demonstrates:
///   - <see cref="Screen"/> (no open argument, no close result)
///   - Single-screen swap via <see cref="ScreenManager"/>
///   - Calling <c>CloseScreen()</c> from a button handler to resolve the
///     ShowAsync task
/// </summary>
public sealed partial class TitleScreen : Screen
{
    private Button _playButton = null!;

    public TitleScreen()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect
        {
            Name = "Background",
            Color = new Color(0.10f, 0.15f, 0.25f),
            AnchorRight = 1.0f, AnchorBottom = 1.0f,
        };
        AddChild(bg);

        var vbox = new VBoxContainer
        {
            Name = "VBox",
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -200, OffsetRight = 200,
            OffsetTop = -120, OffsetBottom = 120,
        };
        bg.AddChild(vbox);

        var title = new Label
        {
            Text = "Title Screen",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        vbox.AddChild(title);

        var subtitle = new Label
        {
            Text = "W3 Day 3 — ScreenManager demo",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vbox.AddChild(subtitle);

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 24) };
        vbox.AddChild(spacer);

        _playButton = new Button { Name = "PlayButton", Text = "Play" };
        _playButton.Pressed += OnPlayPressed;
        vbox.AddChild(_playButton);
    }

    protected override void OnShow()
    {
        GD.Print("[TitleScreen] OnShow");
        _playButton.GrabFocus();
    }

    protected override void OnClose()
    {
        GD.Print("[TitleScreen] OnClose");
    }

    private void OnPlayPressed()
    {
        GD.Print("[TitleScreen] Play clicked → CloseScreen()");
        CloseScreen();
    }
}