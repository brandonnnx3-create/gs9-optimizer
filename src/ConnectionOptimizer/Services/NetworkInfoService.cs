using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ConnectionOptimizer.Services;

/// <summary>Real data of the adapter Windows uses for Internet traffic. Nothing here is estimated.</summary>
public sealed record ConnectionInfo
{
    public static ConnectionInfo None { get; } = new() { Found = false };

    public required bool Found { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Medium { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsUp { get; init; }

    /// <summary>Link speed in bits per second, or -1 when Windows does not report it.</summary>
    public long SpeedBitsPerSecond { get; init; } = -1;

    public string? IPv4 { get; init; }
    public string? Gateway { get; init; }
    public IReadOnlyList<string> Dns { get; init; } = [];
    public string InterfaceGuid { get; init; } = string.Empty;
}

public sealed class NetworkInfoService
{
    // Same destination Network_Tweaks uses with Find-NetRoute: it only queries the routing table, nothing is sent.
    private static readonly IPAddress RouteProbe = IPAddress.Parse("1.1.1.1");

    public Task<ConnectionInfo> GetActiveConnectionAsync() => Task.Run(GetActiveConnection);

    private static ConnectionInfo GetActiveConnection()
    {
        NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
        NetworkInterface? active = FindByRoute(interfaces) ?? FindFallback(interfaces);
        return active is null ? ConnectionInfo.None : Describe(active);
    }

    /// <summary>The interface Windows would route Internet traffic through.</summary>
    private static NetworkInterface? FindByRoute(NetworkInterface[] interfaces)
    {
        // GetBestInterface expects the address in network byte order, which is the byte layout IPAddress returns.
        uint destination = BitConverter.ToUInt32(RouteProbe.GetAddressBytes(), 0);
        if (GetBestInterface(destination, out uint index) != 0)
        {
            return null;
        }

        return interfaces.FirstOrDefault(ni => GetIPv4Index(ni) == (int)index);
    }

    /// <summary>Used only when there is no route to the Internet: the first connected, non-virtual adapter.</summary>
    private static NetworkInterface? FindFallback(NetworkInterface[] interfaces) =>
        interfaces
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                && ni.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .OrderByDescending(ni => GetIPv4Gateway(ni.GetIPProperties()) is not null)
            .FirstOrDefault();

    private static ConnectionInfo Describe(NetworkInterface ni)
    {
        IPInterfaceProperties properties = ni.GetIPProperties();

        string? ipv4 = properties.UnicastAddresses
            .Select(a => a.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?.ToString();

        List<string> dns = properties.DnsAddresses
            .OrderBy(a => a.AddressFamily == AddressFamily.InterNetworkV6 ? 1 : 0)
            .Select(a => a.ToString())
            .ToList();

        return new ConnectionInfo
        {
            Found = true,
            Name = ni.Name,
            Description = ni.Description,
            Medium = DescribeMedium(ni.NetworkInterfaceType),
            Status = DescribeStatus(ni.OperationalStatus),
            IsUp = ni.OperationalStatus == OperationalStatus.Up,
            SpeedBitsPerSecond = ni.Speed,
            IPv4 = ipv4,
            Gateway = GetIPv4Gateway(properties)?.ToString(),
            Dns = dns,
            InterfaceGuid = ni.Id,
        };
    }

    private static IPAddress? GetIPv4Gateway(IPInterfaceProperties properties) =>
        properties.GatewayAddresses
            .Select(g => g.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));

    private static int? GetIPv4Index(NetworkInterface ni)
    {
        try
        {
            return ni.GetIPProperties().GetIPv4Properties()?.Index;
        }
        catch (NetworkInformationException)
        {
            return null; // IPv4 not enabled on this interface.
        }
    }

    private static string DescribeMedium(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Wireless80211 => "WI-FI",
        NetworkInterfaceType.Ethernet
            or NetworkInterfaceType.Ethernet3Megabit
            or NetworkInterfaceType.FastEthernetT
            or NetworkInterfaceType.FastEthernetFx
            or NetworkInterfaceType.GigabitEthernet => "ETHERNET",
        NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => "MOBILE",
        NetworkInterfaceType.Ppp => "PPP",
        NetworkInterfaceType.Tunnel => "TUNNEL",
        _ => "OTHER",
    };

    private static string DescribeStatus(OperationalStatus status) => status switch
    {
        OperationalStatus.Up => "CONNECTED",
        OperationalStatus.Down => "DISCONNECTED",
        _ => status.ToString().ToUpperInvariant(),
    };

    [DllImport("iphlpapi.dll")]
    private static extern int GetBestInterface(uint destinationAddress, out uint bestInterfaceIndex);
}
