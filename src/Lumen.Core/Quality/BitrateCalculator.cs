namespace Lumen.Core.Quality;

/// <summary>
/// Moonlight-compatible default bitrate, then Lumen Sharp LAN floor/ceiling.
/// 144 fps cannot reuse a 60 fps budget.
/// </summary>
public static class BitrateCalculator
{
    public const int SharpLanMinKbps = 80_000;
    public const int SharpLanMaxKbps = 150_000;

    /// <summary>Same table and curve as moonlight-qt StreamingPreferences::getDefaultBitrate.</summary>
    public static int MoonlightDefaultKbps(int width, int height, int fps, bool yuv444)
    {
        var frameRateFactor = (fps <= 60 ? fps : MathF.Sqrt(fps / 60f) * 60f) / 30f;
        var pixels = width * height;
        var resolutionFactor = ResolutionFactor(pixels);
        if (yuv444)
        {
            resolutionFactor *= 2f;
        }

        return (int)MathF.Round(resolutionFactor * frameRateFactor) * 1000;
    }

    public static int ForProfile(int width, int height, int fps, QualityMode quality)
    {
        var yuv444 = quality.UsesYuv444();
        var moonlight = MoonlightDefaultKbps(width, height, fps, yuv444);
        if (quality == QualityMode.Game)
        {
            return Math.Clamp(moonlight, 5_000, SharpLanMaxKbps);
        }

        // Sharp LAN: never below 80 Mbps on 1080p+, cap 150 Mbps as planned.
        var floor = width * height >= 1920 * 1080 ? SharpLanMinKbps : 40_000;
        return Math.Clamp(Math.Max(moonlight, floor), floor, SharpLanMaxKbps);
    }

    private static float ResolutionFactor(int pixels)
    {
        (int Pixels, int Factor)[] table =
        [
            (640 * 360, 1),
            (854 * 480, 2),
            (1280 * 720, 5),
            (1920 * 1080, 10),
            (2560 * 1440, 20),
            (3840 * 2160, 40)
        ];

        if (pixels <= table[0].Pixels)
        {
            return table[0].Factor;
        }

        for (var i = 1; i < table.Length; i++)
        {
            if (pixels == table[i].Pixels)
            {
                return table[i].Factor;
            }

            if (pixels < table[i].Pixels)
            {
                var prev = table[i - 1];
                var next = table[i];
                return ((float)(pixels - prev.Pixels) / (next.Pixels - prev.Pixels))
                    * (next.Factor - prev.Factor) + prev.Factor;
            }
        }

        return table[^1].Factor;
    }
}
