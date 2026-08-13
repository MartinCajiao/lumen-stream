using Lumen.Core.Discovery;
using Lumen.Core.Runtime;
using Lumen.Core.Settings;

namespace Lumen.Launcher;

public sealed class ComputerItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string Address { get; init; }
    public bool IsThisPc { get; init; }
    public required PcPowerState State { get; init; }
    public string Badge => State.Label();
    public bool ShowConnect => !IsThisPc && State != PcPowerState.Off;
    public bool ShowOffline => !IsThisPc && State == PcPowerState.Off;
    public bool IsOff => State == PcPowerState.Off;
    public bool IsLit => State == PcPowerState.On;
    public bool IsShared => State == PcPowerState.Sharing;
    public string ActionLabel => IsThisPc ? "" : (ShowConnect ? "Conectar" : "No está");

    public static ComputerItem FromThisPc(string machine, string address, int hz, bool sharing) => new()
    {
        Id = "this",
        Title = "Este PC",
        Subtitle = sharing
            ? "Se está compartiendo. En el otro pulsa Conectar."
            : "Pulsa Compartir para que el otro PC pueda entrar.",
        Address = address,
        IsThisPc = true,
        State = sharing ? PcPowerState.Sharing : PcPowerState.On
    };

    public static ComputerItem FromKnown(KnownComputer pc, DateTimeOffset now)
    {
        var online = KnownComputerStore.IsOnline(pc, now);
        var name = string.IsNullOrWhiteSpace(pc.Owner) ? pc.Name : pc.Owner;
        return new ComputerItem
        {
            Id = $"{pc.Address}:{pc.Port}",
            Title = name,
            Subtitle = online ? "Pulsa Conectar para entrar." : "No está encendido o no está en la red.",
            Address = pc.Address,
            IsThisPc = false,
            State = online ? PcPowerState.Sharing : PcPowerState.Off
        };
    }

    public static ComputerItem FromHost(DiscoveredHost host) => new()
    {
        Id = $"{host.Address}:{host.Port}",
        Title = string.IsNullOrWhiteSpace(host.Owner) ? host.Name : host.Owner,
        Subtitle = "Pulsa Conectar para entrar.",
        Address = host.Address,
        IsThisPc = false,
        State = PcPowerState.Sharing
    };
}
