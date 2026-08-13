using Lumen.Core.Quality;

namespace Lumen.Core.Client;

public interface IKeyValueStore
{
    void Set(string key, object value);
}

public sealed class MemoryKeyValueStore : IKeyValueStore
{
    public Dictionary<string, object> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string key, object value) => Values[key] = value;
}

/// <summary>
/// Writes Moonlight Qt preferences (registry on Windows, INI elsewhere).
/// FPS/bitrate/4:4:4 are requested by the client; Apollo matches the virtual display to them.
/// </summary>
public static class MoonlightSettingsWriter
{
    public const string Organization = "Moonlight Game Streaming Project";
    public const string Application = "Moonlight";

    public static void Apply(StreamProfile profile, IKeyValueStore store)
    {
        store.Set("width", profile.Width);
        store.Set("height", profile.Height);
        store.Set("fps", profile.Fps);
        store.Set("bitrate", profile.BitrateKbps);
        store.Set("unlockbitrate", profile.BitrateKbps > 80_000);
        store.Set("autoadjustbitrate", profile.Quality == QualityMode.Game);
        store.Set("yuv444", profile.EnableYuv444);
        store.Set("hdr", profile.EnableHdr);
        store.Set("videocfg", profile.Quality == QualityMode.SharpLan ? 2 : 0); // 0 auto, 2 HEVC
        store.Set("vsync", true);
        store.Set("framepacing", profile.FramePacing);
        store.Set("gameopts", profile.GameOptimizations);
        store.Set("showperfoverlay", profile.ShowPerformanceOverlay);
        store.Set("mdns", true);
        store.Set("quitAppAfter", false);
        store.Set("hostaudio", false);
        store.Set("capturesyskeys", 1); // fullscreen
        store.Set("keepawake", true);
        store.Set("videodec", 1); // force hardware
        store.Set("windowmode", 2); // windowed so errors are visible
        store.Set("abstouchmode", true);
        store.Set("mouseacceleration", profile.NativePenTouch); // absolute mouse helps tablets
    }

    public static string RenderIni(StreamProfile profile)
    {
        var mem = new MemoryKeyValueStore();
        Apply(profile, mem);
        var lines = new List<string> { "[General]" };
        foreach (var pair in mem.Values)
        {
            lines.Add($"{pair.Key}={Format(pair.Value)}");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string Format(object value) => value switch
    {
        bool b => b ? "true" : "false",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
    };
}
