using Lumen.Core.Quality;

namespace Lumen.Core.Runtime;

public static class HostLaunchPlan
{
    public static IReadOnlyList<StreamProfile> Fallbacks(StreamProfile wanted)
    {
        var plans = new List<StreamProfile> { wanted };
        if (!string.Equals(wanted.AddressFamily, "ipv4", StringComparison.OrdinalIgnoreCase))
        {
            plans.Add(wanted with { AddressFamily = "ipv4" });
        }

        plans.Add(wanted with
        {
            PrivacyMode = false,
            AddressFamily = "ipv4",
            Wan = wanted.Wan with { EnableUpnp = false }
        });

        return plans.Distinct().ToArray();
    }
}
