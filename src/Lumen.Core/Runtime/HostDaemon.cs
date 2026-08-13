using Lumen.Core.Discovery;
using Lumen.Core.Display;
using Lumen.Core.Host;
using Lumen.Core.Paths;
using Lumen.Core.Quality;
using Lumen.Core.Settings;
using Lumen.Core.Wan;

namespace Lumen.Core.Runtime;

public static class HostDaemon
{
    public static int Run()
    {
        var settings = LumenSettingsStore.Load();
        if (!settings.AutoShareOnBoot && string.IsNullOrWhiteSpace(settings.Username))
        {
            return 0;
        }

        var probe = new WindowsDisplayProbe();
        var profile = StreamProfile.From(settings, probe.Primary);
        SunshineConfigWriter.Write(profile);
        AppsJsonWriter.Write(profile);

        var host = ProcessLocator.FindHost();
        if (host is null)
        {
            return 2;
        }

        try
        {
            SessionLauncher.StartHost(profile, host, settings.Username);
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var ip = NetworkAddresses.LocalIpv4() ?? "127.0.0.1";
            using var beacon = LanBeacon.StartAnnouncing(
                new DiscoveredHost(Environment.MachineName, ip, profile.Wan.HostPort, probe.Primary.RefreshHz, settings.Username),
                cts.Token);

            cts.Token.WaitHandle.WaitOne();
            return 0;
        }
        finally
        {
            HostProcess.StopAll();
        }
    }
}
