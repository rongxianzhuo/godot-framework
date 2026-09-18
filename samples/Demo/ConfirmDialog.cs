using Godot;

namespace GameFramework.Demo;

/// <summary>
/// Confirm dialog. Demonstrates:
///   - UIPanel&lt;TOpenArg, TCloseArg&gt; with string message and bool result
///   - Centering a small overlay on top of the existing panel stack
///   - Returning a typed result back to the caller
/// </summary>
public sealed partial class ConfirmDialog : UIPanel<string, bool>
{
    private Label _messageLabel = null!;

    public ConfirmDialog()
    {
        // Centered card, not full screen.
        AnchorLeft = 0.5f; AnchorRight = 0.5f;
        AnchorTop = 0.5f; AnchorBottom = 0.5f;
        OffsetLeft = -180; OffsetRight = 180;
        OffsetTop = -60; OffsetBottom = 60;
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new PanelContainer
        {
            Name = "Panel",
            AnchorRight = 1.0f, AnchorBottom = 1.0f,
        };
        AddChild(bg);

        var vbox = new VBoxContainer
        {
            Name = "VBox",
            AnchorRight = 1.0f, AnchorBottom = 1.0f,
            OffsetLeft = 16, OffsetRight = -16,
            OffsetTop = 16, OffsetBottom = -16,
        };
        bg.AddChild(vbox);

        _messageLabel = new Label
        {
            Name = "MessageLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Word,
            CustomMinimumSize = new Vector2(0, 32),
        };
        vbox.AddChild(_messageLabel);

        var hbox = new HBoxContainer
        {
            Name = "HBox",
            Alignment = BoxContainer.AlignmentMode.Center,
            CustomMinimumSize = new Vector2(0, 32),
        };
        vbox.AddChild(hbox);

        var confirm = new Button { Name = "Confirm", Text = "Confirm", CustomMinimumSize = new Vector2(96, 0) };
        var cancel = new Button { Name = "Cancel", Text = "Cancel", CustomMinimumSize = new Vector2(96, 0) };
        hbox.AddChild(confirm);
        hbox.AddChild(cancel);

        confirm.Pressed += () => ClosePanel(true);
        cancel.Pressed += () => ClosePanel(false);
    }

    protected override void OnOpen(string message)
    {
        GD.Print($"[ConfirmDialog] OnOpen(message='{message}')");
        _messageLabel.Text = message;
    }

    protected override void OnClose(bool result)
    {
        GD.Print($"[ConfirmDialog] OnClose(result={result})");
    }
}