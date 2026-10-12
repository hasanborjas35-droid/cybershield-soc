using System.Management;

namespace CyberShield.Agent.Collectors;

/// <summary>
/// Collects real Windows security status:
/// - Microsoft Defender antivirus state via WMI SecurityCenter2
/// - Windows Firewall profiles via NetFwTypeLib / WMI
/// - Defender signature date via WMI MSFT_MpComputerStatus
///
/// Permissions: standard user can read SecurityCenter2.
/// MSFT_MpComputerStatus requires elevated rights — handled gracefully.
/// This collector NEVER disables or modifies any security component.
/// </summary>
public class SecurityCollector(ILogger<SecurityCollector> log)
{
    public SecuritySnapshot Collect()
    {
        var snap = new SecuritySnapshot { CollectedAt = DateTime.UtcNow };

        CollectDefenderStatus(snap);
        CollectFirewallStatus(snap);

        return snap;
    }

    // ── Defender via SecurityCenter2 (works without elevation) ───────
    private void CollectDefenderStatus(SecuritySnapshot snap)
    {
        try
        {
            // SecurityCenter2 is the modern WMI namespace for antivirus products
            var scope = new ManagementScope(@"root\SecurityCenter2");
            scope.Connect();

            using var query = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT displayName, productState FROM AntiVirusProduct"));

            foreach (ManagementObject obj in query.Get())
            {
                var name         = obj["displayName"]?.ToString() ?? "";
                var productState = Convert.ToInt32(obj["productState"]);

                // productState is a hex-encoded bitmask:
                // bits 12-19: definition status  (10 = up-to-date, 00 = outdated)
                // bits 4-11:  real-time status   (10 = enabled,    00 = disabled)
                // bits 0-3:   product enabled    (1 = enabled)
                var hexState = productState.ToString("X6");
                bool rtpOn   = hexState.Length >= 4 && hexState[2..4] == "10";
                bool defOn   = productState != 0;

                if (name.Contains("Windows Defender", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Microsoft Defender", StringComparison.OrdinalIgnoreCase))
                {
                    snap.DefenderEnabled              = defOn;
                    snap.RealTimeProtectionEnabled    = rtpOn;
                    snap.AntivirusProductName         = name;
                    break;
                }

                // Accept any AV product if Defender not found
                snap.DefenderEnabled           = defOn;
                snap.RealTimeProtectionEnabled = rtpOn;
                snap.AntivirusProductName      = name;
            }
        }
        catch (Exception ex)
        {
            log.LogWarning("SecurityCenter2 query failed: {Msg}", ex.Message);
            snap.Errors.Add("SecurityCenter2: " + ex.Message);
            // Fallback: try WMI MSFT_MpComputerStatus (needs elevation)
            CollectDefenderViaMpStatus(snap);
        }

        // Try to get signature date (may need elevation)
        TryGetSignatureDate(snap);
    }

    private void CollectDefenderViaMpStatus(SecuritySnapshot snap)
    {
        try
        {
            var scope = new ManagementScope(@"root\Microsoft\Windows\Defender");
            scope.Connect();
            using var query = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT AMRunningMode, AntivirusEnabled, NISEnabled FROM MSFT_MpComputerStatus"));
            foreach (ManagementObject obj in query.Get())
            {
                snap.DefenderEnabled           = Convert.ToBoolean(obj["AntivirusEnabled"] ?? false);
                snap.RealTimeProtectionEnabled = snap.DefenderEnabled;
                break;
            }
        }
        catch (Exception ex)
        {
            log.LogDebug("MSFT_MpComputerStatus unavailable (likely needs elevation): {Msg}", ex.Message);
        }
    }

    private void TryGetSignatureDate(SecuritySnapshot snap)
    {
        try
        {
            var scope = new ManagementScope(@"root\Microsoft\Windows\Defender");
            scope.Connect();
            using var query = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT AntivirusSignatureLastUpdated FROM MSFT_MpComputerStatus"));
            foreach (ManagementObject obj in query.Get())
            {
                var raw = obj["AntivirusSignatureLastUpdated"]?.ToString();
                if (!string.IsNullOrEmpty(raw))
                {
                    // WMI DMTF datetime: "20241010120000.000000+000"
                    if (ManagementDateTimeConverter.ToDateTime(raw) is DateTime dt)
                        snap.DefenderSignatureDate = dt.ToString("yyyy-MM-dd");
                }
                break;
            }
        }
        catch (Exception ex)
        {
            log.LogDebug("Signature date unavailable: {Msg}", ex.Message);
        }
    }

    // ── Windows Firewall via WMI ──────────────────────────────────────
    private void CollectFirewallStatus(SecuritySnapshot snap)
    {
        try
        {
            // HNetCfg.FwPolicy2 via WMI is the most reliable cross-elevation approach
            var scope = new ManagementScope(@"root\StandardCimv2");
            scope.Connect();
            using var query = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT * FROM MSFT_NetFirewallProfile WHERE Name='Domain' OR Name='Private' OR Name='Public'"));

            bool anyEnabled = false;
            foreach (ManagementObject obj in query.Get())
            {
                var enabled = Convert.ToInt32(obj["Enabled"] ?? 0);
                if (enabled == 1) anyEnabled = true;
            }
            snap.FirewallEnabled = anyEnabled;
        }
        catch (Exception ex)
        {
            log.LogWarning("Firewall WMI query failed: {Msg}", ex.Message);
            snap.Errors.Add("Firewall: " + ex.Message);
            // Fallback: assume enabled (fail safe — never report false disabled)
            snap.FirewallEnabled = true;
            snap.FirewallNote    = "Status unconfirmed (WMI unavailable)";
        }
    }
}

public class SecuritySnapshot
{
    public DateTime CollectedAt              { get; set; }
    public bool     DefenderEnabled          { get; set; }
    public bool     RealTimeProtectionEnabled{ get; set; }
    public bool     FirewallEnabled          { get; set; }
    public string   AntivirusProductName     { get; set; } = "";
    public string?  DefenderSignatureDate    { get; set; }
    public string?  FirewallNote             { get; set; }
    public List<string> Errors               { get; set; } = new();
}
