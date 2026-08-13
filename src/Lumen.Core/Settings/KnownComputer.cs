using System.Text.Json;
using Lumen.Core.Paths;

namespace Lumen.Core.Settings;

public sealed class KnownComputer
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Owner { get; set; } = "";
    public int Port { get; set; } = 47989;
    public int PanelHz { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public bool Manual { get; set; }
    public bool ReadyToStream { get; set; }
}

public static class KnownComputerStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static List<KnownComputer> Load()
    {
        LumenPaths.EnsureLayout();
        if (!File.Exists(LumenPaths.ComputersFile))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<KnownComputer>>(File.ReadAllText(LumenPaths.ComputersFile), Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static void Save(IEnumerable<KnownComputer> computers)
    {
        LumenPaths.EnsureLayout();
        File.WriteAllText(LumenPaths.ComputersFile, JsonSerializer.Serialize(computers, Json));
    }

    public static bool IsOnline(KnownComputer pc, DateTimeOffset now) =>
        pc.Manual || now - pc.LastSeenUtc < TimeSpan.FromSeconds(20);
}
