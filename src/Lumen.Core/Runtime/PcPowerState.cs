namespace Lumen.Core.Runtime;

public enum PcPowerState
{
    Off,
    On,
    Sharing
}

public static class PcPowerStateExtensions
{
    public static string Label(this PcPowerState state) => state switch
    {
        PcPowerState.Sharing => "En línea",
        PcPowerState.On => "Este PC",
        _ => "No está"
    };

    public static string Color(this PcPowerState state) => state switch
    {
        PcPowerState.Sharing => "#3DFF9A",
        PcPowerState.On => "#4D8CFF",
        _ => "#6B5A80"
    };
}
