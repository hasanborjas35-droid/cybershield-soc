using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace CyberShield.Agent.Collectors;

/// <summary>
/// Collects real network information:
/// active interfaces, IP addresses, operational status, DNS servers.
/// Does NOT capture packet payloads or private communications.
/// </summary>
public class NetworkCollector(ILogger<NetworkCollector> log)
{
    public NetworkSnapshot Collect()
    {
        var snap = new NetworkSnapshot { CollectedAt = DateTime.UtcNow };

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var nic in interfaces)
            {
                // Skip loopback and tunnel adapters
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)    continue;

                var info = new NetworkInterfaceInfo
                {
                    Name         = nic.Name,
                    Description  = nic.Description,
                    Status       = nic.OperationalStatus.ToString(),
                    IsUp         = nic.OperationalStatus == OperationalStatus.Up,
                    SpeedMbps    = nic.Speed > 0 ? nic.Speed / 1_000_000 : 0
                };

                var ipProps = nic.GetIPProperties();

                // IPv4 addresses only (skip link-local 169.254.x.x for cleanliness)
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !addr.Address.ToString().StartsWith("169.254"))
                    {
                        info.IpAddresses.Add(addr.Address.ToString());
                    }
                }

                // DNS servers
                foreach (var dns in ipProps.DnsAddresses)
                {
                    if (dns.AddressFamily == AddressFamily.InterNetwork)
                        info.DnsServers.Add(dns.ToString());
                }

                snap.Interfaces.Add(info);

                if (info.IsUp && info.IpAddresses.Count > 0)
                    snap.ActiveInterfaceCount++;
            }

            snap.IsConnected = snap.ActiveInterfaceCount > 0;
        }
        catch (Exception ex)
        {
            log.LogWarning("Network collect error: {Msg}", ex.Message);
            snap.Errors.Add(ex.Message);
        }

        snap.InterfacesJson = JsonSerializer.Serialize(
            snap.Interfaces.Select(i => new
            {
                i.Name, i.Status, i.IsUp, i.SpeedMbps,
                ips  = i.IpAddresses,
                dns  = i.DnsServers
            }),
            new JsonSerializerOptions { WriteIndented = false });

        return snap;
    }
}

public class NetworkSnapshot
{
    public DateTime CollectedAt       { get; set; }
    public bool     IsConnected       { get; set; }
    public int      ActiveInterfaceCount { get; set; }
    public List<NetworkInterfaceInfo> Interfaces { get; set; } = new();
    public string?  InterfacesJson    { get; set; }
    public List<string> Errors        { get; set; } = new();
}

public class NetworkInterfaceInfo
{
    public string        Name        { get; set; } = "";
    public string        Description { get; set; } = "";
    public string        Status      { get; set; } = "";
    public bool          IsUp        { get; set; }
    public long          SpeedMbps   { get; set; }
    public List<string>  IpAddresses { get; set; } = new();
    public List<string>  DnsServers  { get; set; } = new();
}
