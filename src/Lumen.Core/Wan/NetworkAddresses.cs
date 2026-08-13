using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Lumen.Core.Wan;

public static class NetworkAddresses
{
    public static string? LocalIpv4()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            return socket.LocalEndPoint is IPEndPoint ep ? ep.Address.ToString() : null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    public static string? TailscaleIpv4()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (var address in nic.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (IsTailscaleIpv4(address.Address.ToString()))
                {
                    return address.Address.ToString();
                }
            }
        }

        return null;
    }

    public static string? GlobalIpv6()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var address in nic.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetworkV6 || address.Address.IsIPv6LinkLocal)
                {
                    continue;
                }

                var bytes = address.Address.GetAddressBytes();
                if (bytes.Length == 16 && (bytes[0] & 0xE0) == 0x20)
                {
                    return address.Address.ToString();
                }
            }
        }

        return null;
    }

    public static bool IsPrivateIpv4(string ip)
    {
        if (!IPAddress.TryParse(ip, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var b = parsed.GetAddressBytes();
        return b[0] == 10
               || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
               || (b[0] == 192 && b[1] == 168)
               || IsTailscaleIpv4(ip)
               || b[0] == 127;
    }

    public static bool IsTailscaleIpv4(string ip)
    {
        if (!IPAddress.TryParse(ip, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var b = parsed.GetAddressBytes();
        return b[0] == 100 && b[1] >= 64 && b[1] <= 127;
    }
}
