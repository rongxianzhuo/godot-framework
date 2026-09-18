using Godot;

namespace GameFramework.Demo;

/// <summary>
/// POCO service demo. Demonstrates that the registry handles plain C# objects
/// just as well as Node-based services.
///
/// In a real game this would wrap AudioServer buses; for MVP we just store a
/// float and print changes so the demo can show the framework plumbing.
/// </summary>
public sealed class AudioService
{
    public float MusicVolume { get; private set; } = 0.8f;

    public void SetMusicVolume(float v)
    {
        MusicVolume = Mathf.Clamp(v, 0.0f, 1.0f);
        GD.Print($"[AudioService] Music volume set to {MusicVolume:F2}");
    }
}