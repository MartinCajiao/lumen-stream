using System.Text.Json;
using System.Text.Json.Nodes;
using Lumen.Core.Paths;
using Lumen.Core.Quality;

namespace Lumen.Core.Host;

public static class AppsJsonWriter
{
    public static string Render(StreamProfile profile)
    {
        var apps = new JsonArray();
        for (var i = 1; i <= profile.MonitorCount; i++)
        {
            var name = profile.MonitorCount == 1 ? "Desktop" : $"Desktop {i}";
            apps.Add(new JsonObject
            {
                ["name"] = name,
                ["image-path"] = "desktop.png",
                ["virtual-display"] = true,
                ["allow-client-commands"] = false,
                ["auto-detach"] = true
            });
        }

        var root = new JsonObject
        {
            ["env"] = new JsonObject(),
            ["apps"] = apps
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static void Write(StreamProfile profile)
    {
        LumenPaths.EnsureLayout();
        File.WriteAllText(LumenPaths.HostAppsFile, Render(profile));
    }
}
