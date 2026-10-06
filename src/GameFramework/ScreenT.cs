namespace GameFramework;

/// <summary>
/// Full-screen view that takes a typed open argument but does not return
/// a typed close result. Use for menu screens, settings, gameplay screens
/// that don't need to communicate anything back to the caller.
/// </summary>
public abstract partial class Screen<TOpenArg> : Screen
{
    internal protected override void InvokeOnShow(object? openArg) => OnShow((TOpenArg)openArg!);

    /// <summary>Called when this screen is shown, with the typed argument.</summary>
    protected virtual void OnShow(TOpenArg arg) { }
}