using Lumen.Core.Paths;

namespace Lumen.Core.Runtime;

public sealed record LocatedBinary(string Path, string Kind);

public static class ProcessLocator
{
    public static LocatedBinary? FindHost()
    {
        LocatedBinary? incomplete = null;
        foreach (var candidate in HostCandidates())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            var found = new LocatedBinary(candidate, "apollo");
            if (LooksLikeCompleteHost(candidate))
            {
                return found;
            }

            incomplete ??= found;
        }

        return incomplete;
    }

    public static LocatedBinary? FindClient()
    {
        foreach (var candidate in ClientCandidates())
        {
            if (File.Exists(candidate))
            {
                return new LocatedBinary(candidate, "moonlight");
            }
        }

        return null;
    }

    public static IEnumerable<string> HostCandidates()
    {
        var names = new[] { "sunshine.exe", "apollo.exe" };
        var roots = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "vendor", "apollo"),
            LumenPaths.DepsDir,
            Path.Combine(AppContext.BaseDirectory, "host"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Apollo"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Apollo"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Apollo"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Apollo"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Sunshine")
        };

        foreach (var root in roots)
        {
            foreach (var name in names)
            {
                foreach (var path in Existing(root, name))
                {
                    yield return path;
                }
            }
        }
    }

    public static IEnumerable<string> ClientCandidates()
    {
        var roots = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "vendor", "moonlight"),
            Path.Combine(LumenPaths.DepsDir, "moonlight"),
            LumenPaths.DepsDir,
            Path.Combine(AppContext.BaseDirectory, "client"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Moonlight Game Streaming"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Moonlight"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Moonlight")
        };

        foreach (var root in roots)
        {
            foreach (var path in Existing(root, "Moonlight.exe"))
            {
                yield return path;
            }
        }
    }

    public static bool LooksLikeCompleteHost(string exePath)
    {
        var dir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(dir))
        {
            return false;
        }

        return File.Exists(Path.Combine(dir, "sunshine.dll"))
               || File.Exists(Path.Combine(dir, "apollo.dll"))
               || Directory.Exists(Path.Combine(dir, "assets"))
               || File.Exists(Path.Combine(dir, "zlib1.dll"));
    }

    private static IEnumerable<string> Existing(string root, string exe)
    {
        var direct = Path.Combine(root, exe);
        if (File.Exists(direct))
        {
            yield return direct;
        }

        if (!Directory.Exists(root))
        {
            yield break;
        }

        IEnumerable<string> nested = [];
        try
        {
            nested = Directory.EnumerateFiles(root, exe, SearchOption.AllDirectories);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        foreach (var file in nested)
        {
            yield return file;
        }
    }
}
