using Lumen.Core.Runtime;
using Lumen.Core.Settings;
using Xunit;

namespace Lumen.Tests;

public sealed class PcAndInstallerTests
{
    [Fact]
    public void Power_labels()
    {
        Assert.Equal("No está", PcPowerState.Off.Label());
        Assert.Equal("Este PC", PcPowerState.On.Label());
        Assert.Equal("En línea", PcPowerState.Sharing.Label());
    }

    [Fact]
    public void Host_autostart_command_uses_user_session()
    {
        var cmd = UserSessionHost.CommandLine(@"C:\Lumen\Lumen.exe");
        Assert.Equal("\"C:\\Lumen\\Lumen.exe\" --host", cmd);
        Assert.Contains("--host", cmd);
    }

    [Fact]
    public void Picks_apollo_and_moonlight_installers()
    {
        Assert.Equal("Apollo-0.4.6.exe", GitHubAssetPicker.PickInstaller(
            ["Apollo-0.4.6.exe", "Apollo-debug.pdb"], "Apollo"));
        Assert.Equal("MoonlightSetup-6.1.0.exe", GitHubAssetPicker.PickInstaller(
            ["Moonlight-6.1.0-x86_64.AppImage", "MoonlightSetup-6.1.0.exe"], "Moonlight"));
        Assert.Equal("MoonlightPortable-x64-6.1.0.zip", GitHubAssetPicker.PickPortableZip(
            ["MoonlightPortable-arm64-6.1.0.zip", "MoonlightPortable-x64-6.1.0.zip"], "MoonlightPortable"));
    }

    [Fact]
    public void Finds_apollo_when_binary_is_named_sunshine()
    {
        var known = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LumenStream", "deps", "Apollo-0.4.6", "sunshine.exe");
        if (!File.Exists(known))
        {
            return;
        }

        var host = ProcessLocator.FindHost();
        Assert.NotNull(host);
        Assert.True(
            host.Path.EndsWith("sunshine.exe", StringComparison.OrdinalIgnoreCase)
            || host.Path.EndsWith("apollo.exe", StringComparison.OrdinalIgnoreCase));
        Assert.True(ProcessLocator.LooksLikeCompleteHost(host.Path));
    }

    [Fact]
    public void Incomplete_host_exe_is_not_treated_as_apollo()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-host-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var exe = Path.Combine(dir, "sunshine.exe");
            File.WriteAllBytes(exe, [0]);
            Assert.False(ProcessLocator.LooksLikeCompleteHost(exe));
            File.WriteAllBytes(Path.Combine(dir, "sunshine.dll"), [0]);
            Assert.True(ProcessLocator.LooksLikeCompleteHost(exe));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Host_liveness_check_does_not_throw()
    {
        _ = HostProcess.IsLive();
        _ = HostProcess.IsListening();
        Assert.False(HostProcess.IsRunning(null));
    }

    [Fact]
    public void Manual_pc_stays_connectable()
    {
        var pc = new KnownComputer { Address = "203.0.113.9", Manual = true, LastSeenUtc = DateTimeOffset.UtcNow.AddHours(-2) };
        Assert.True(KnownComputerStore.IsOnline(pc, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Known_pc_online_window()
    {
        var now = DateTimeOffset.UtcNow;
        var pc = new KnownComputer { LastSeenUtc = now.AddSeconds(-5) };
        Assert.True(KnownComputerStore.IsOnline(pc, now));
        pc.LastSeenUtc = now.AddMinutes(-5);
        Assert.False(KnownComputerStore.IsOnline(pc, now));
    }
}
