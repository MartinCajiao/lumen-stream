namespace Lumen.Core.Wan;

public readonly record struct GameStreamPort(int Number, string Protocol, string Name);

public static class GameStreamPorts
{
    /// <summary>Sunshine maps these from the HTTP base port (default 47989).</summary>
    public static IReadOnlyList<GameStreamPort> ForBase(int httpPort = 47989) =>
    [
        new(httpPort, "TCP", "HTTP"),
        new(httpPort - 5, "TCP", "HTTPS"),
        new(httpPort + 1, "TCP", "Web"),
        new(httpPort + 9, "UDP", "Video"),
        new(httpPort + 10, "UDP", "Control"),
        new(httpPort + 11, "UDP", "Audio"),
        new(httpPort + 21, "TCP", "RTSP")
    ];
}
