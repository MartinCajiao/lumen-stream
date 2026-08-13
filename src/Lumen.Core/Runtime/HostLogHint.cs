namespace Lumen.Core.Runtime;

public static class HostLogHint
{
    public static string? FromFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return FromText(File.ReadAllText(path));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string? FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var fatal = LastUseful(lines, "Fatal:");
        if (fatal is not null)
        {
            return Clip(fatal);
        }

        var error = LastUseful(lines, "Error:");
        if (error is not null)
        {
            return Clip(error);
        }

        return null;
    }

    private static string? LastUseful(IReadOnlyList<string> lines, string marker)
    {
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.Contains(marker, StringComparison.OrdinalIgnoreCase) && !IsNoise(line))
            {
                return line.Trim();
            }
        }

        return null;
    }

    public static bool IsNoise(string line)
    {
        return line.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)
               || line.Contains("You can safely ignore", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Ignore any errors mentioned above", StringComparison.OrdinalIgnoreCase)
               || line.Contains("gpu doesn't support YUV444", StringComparison.OrdinalIgnoreCase)
               || line.Contains("NvEncUnregisterAsyncEvent", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Found H.264 encoder", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Found HEVC encoder", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Found AV1 encoder", StringComparison.OrdinalIgnoreCase);
    }

    private static string Clip(string line)
    {
        var idx = line.IndexOf("Fatal:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            idx = line.IndexOf("Error:", StringComparison.OrdinalIgnoreCase);
        }

        var useful = idx >= 0 ? line[idx..] : line;
        return useful.Length > 220 ? useful[..220] : useful;
    }
}
