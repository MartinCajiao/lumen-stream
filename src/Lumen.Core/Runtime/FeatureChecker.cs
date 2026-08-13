using Lumen.Core.Quality;

namespace Lumen.Core.Runtime;

public sealed record FeatureCheck(string Id, string Title, bool Ready, string Detail);

/// <summary>
/// What actually works today: config+UI are real. Streaming needs Apollo/Moonlight once.
/// </summary>
public static class FeatureChecker
{
    public static IReadOnlyList<FeatureCheck> Evaluate(
        StreamProfile profile,
        bool hostInstalled,
        bool clientInstalled)
    {
        var hzReady = profile.Fps > 0 && profile.PanelHz > 0;
        var hzDetail = profile.Fps > profile.PanelHz
            ? $"Tu pantalla es {profile.PanelHz} Hz. Pediste {profile.Fps}: se verá a {profile.PanelHz}."
            : $"Stream a {profile.Fps} Hz (tu panel: {profile.PanelHz}). El host crea un monitor virtual a ese Hz.";

        return
        [
            new("host", "Compartir este PC", hostInstalled,
                hostInstalled ? "Pulsa Compartir. En el otro PC pulsa Conectar." : "Preparando este PC…"),
            new("client", "Entrar a otro PC", clientInstalled,
                clientInstalled ? "Pulsa Conectar en el PC de la lista." : "Preparando la conexión…"),
            new("hz", "Hz reales", hzReady, hzDetail),
            new("privacy", "Pantalla privada", profile.PrivacyMode,
                profile.PrivacyMode ? "Al compartir se apagan los monitores físicos." : "Los monitores físicos siguen encendidos."),
            new("sharp", "Imagen nítida 4:4:4", true,
                profile.EnableYuv444
                    ? $"Nítido LAN activo · {profile.BitrateKbps / 1000} Mbps."
                    : "Modo juego (más FPS). El nítido se activa solo si lo necesitas."),
            new("wacom", "Lápiz / Wacom", profile.NativePenTouch,
                profile.NativePenTouch ? "Pressure y tilt van al host." : "Lápiz desactivado."),
            new("monitors", "Hasta 3 pantallas", profile.MonitorCount is >= 1 and <= 3,
                $"{profile.MonitorCount} pantalla(s) virtual(es).")
        ];
    }

    public static string Headline(IReadOnlyList<FeatureCheck> checks)
    {
        var host = checks.First(c => c.Id == "host");
        var client = checks.First(c => c.Id == "client");
        if (host.Ready && client.Ready)
        {
            return "Listo. Comparte este PC o entra a otro.";
        }

        if (!host.Ready && !client.Ready)
        {
            return "Pulsa Compartir aquí. En el otro PC pulsa Conectar.";
        }

        return host.Ready ? client.Detail : host.Detail;
    }
}
