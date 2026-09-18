using Godot;

namespace GameFramework.Demo;

/// <summary>
/// Settings panel. Demonstrates:
///   - UIPanel&lt;TOpenArg&gt; (open argument, no close result)
///   - Accessing a registered service (AudioService) to mutate game state
///   - Wiring UI controls (HSlider) to live-update a service
/// </summary>
public sealed partial class SettingsPanel : UIPanel<float>
{
    private HSlider _volumeSlider = null!;
    private Label _volumeLabel = null!;

    public SettingsPanel()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var vbox = new VBoxContainer
        {
            Name = "VBox",
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -200, OffsetRight = 200,
            OffsetTop = -80, OffsetBottom = 80,
        };
        AddChild(vbox);

        var title = new Label { Text = "Settings", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        vbox.AddChild(title);

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 16) };
        vbox.AddChild(spacer);

        _volumeLabel = new Label { Text = "Music Volume: 0.80", HorizontalAlignment = HorizontalAlignment.Center };
        vbox.AddChild(_volumeLabel);

        _volumeSlider = new HSlider
        {
            MinValue = 0, MaxValue = 1, Step = 0.01,
            Value = 0.8f,
            CustomMinimumSize = new Vector2(0, 24),
        };
        vbox.AddChild(_volumeSlider);
        _volumeSlider.ValueChanged += OnVolumeChanged;

        var spacer2 = new Control { CustomMinimumSize = new Vector2(0, 16) };
        vbox.AddChild(spacer2);

        var back = new Button { Name = "BackButton", Text = "Back" };
        back.Pressed += () => { GD.Print("[SettingsPanel] Back clicked → ClosePanel()"); ClosePanel(); };
        vbox.AddChild(back);
    }

    protected override void OnOpen(float initialVolume)
    {
        GD.Print($"[SettingsPanel] OnOpen(arg='{initialVolume}')");
        _volumeSlider.Value = initialVolume;
        _volumeLabel.Text = $"Music Volume: {initialVolume:F2}";
    }

    private void OnVolumeChanged(double value)
    {
        var v = (float)value;
        _volumeLabel.Text = $"Music Volume: {v:F2}";
        var audio = GameFramework.Instance.Services.TryGet<AudioService>();
        audio?.SetMusicVolume(v);
    }
}