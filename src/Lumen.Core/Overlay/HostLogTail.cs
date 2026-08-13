using Lumen.Core.Paths;
using Lumen.Core.Quality;

namespace Lumen.Core.Overlay;

public sealed class HostLogTail
{
    public int? LastCaptureHz { get; private set; }
    public int? LastRequestedFps { get; private set; }

    public void Poll()
    {
        var path = LumenPaths.HostLogFile;
        if (!File.Exists(path))
        {
            return;
        }

        string chunk;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length > 64_000)
            {
                stream.Seek(-64_000, SeekOrigin.End);
            }

            using var reader = new StreamReader(stream);
            chunk = reader.ReadToEnd();
        }
        catch (IOException)
        {
            return;
        }

        LastCaptureHz = HostLogStatsParser.TryCaptureHz(chunk) ?? LastCaptureHz;
        LastRequestedFps = HostLogStatsParser.TryRequestedFps(chunk) ?? LastRequestedFps;
    }

    public StreamStats ToStats(StreamProfile profile) =>
        OverlayAdvisor.Compose(profile, LastCaptureHz, LastRequestedFps);
}
