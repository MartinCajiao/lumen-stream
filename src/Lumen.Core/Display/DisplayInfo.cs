namespace Lumen.Core.Display;

public sealed record DisplayInfo(
    int Width,
    int Height,
    int RefreshHz,
    string DeviceName)
{
    public static DisplayInfo Fallback { get; } = new(1920, 1080, 60, "unknown");

    public string ResolutionLabel => $"{Width}x{Height}@{RefreshHz}Hz";
}

public interface IDisplayProbe
{
    DisplayInfo Primary { get; }
}
