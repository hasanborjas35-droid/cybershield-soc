using CyberShield.Api.Models;
using CyberShield.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CyberShield.Api.Data;

/// <summary>Seeds the same sample records shown in the Stitch screens. Runs only on an empty database.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, string seedPassword)
    {
        if (await db.Users.AnyAsync()) return;
        var now = DateTime.UtcNow;
        var hash = PasswordHasher.Hash(seedPassword);

        User U(string name, string email, UserRole role, UserStatus st, DateTime? last, params string[] perms) => new()
        {
            FullName = name, Email = email, Role = role, Status = st, PasswordHash = hash,
            LastActiveAt = last, MfaEnabled = true,
            Permissions = perms.Select(p => new UserPermission { Code = p }).ToList(),
        };

        // ---------- Users ----------
        var marcus = U("Marcus Vance", "m.vance@cybershield.internal", UserRole.Administrator, UserStatus.Active, now, "IAM:ALL", "NET:CTL", "SIEM:ALL", "INC:ALL");
        var elena = U("Elena Rostova", "e.rostova@cybershield.internal", UserRole.SecurityAnalyst, UserStatus.Active, now.AddMinutes(-4), "SIEM:RD", "INC:WR");
        var devon = U("Devon Chen", "d.chen@cybershield.internal", UserRole.SecurityAnalyst, UserStatus.Suspended, now.AddDays(-3), "REVOKED");
        var sarahJ = U("Sarah Jenkins", "s.jenkins@cybershield.internal", UserRole.Viewer, UserStatus.Pending, null, "AUDIT:RD");
        var sarahC = U("Sarah Chen", "s.chen@cybershield.internal", UserRole.SecurityAnalyst, UserStatus.Active, now.AddMinutes(-9), "SIEM:RD", "INC:WR");
        var alex = U("Alex Mercer", "a.mercer@cybershield.internal", UserRole.SecurityAnalyst, UserStatus.Active, now.AddMinutes(-15), "SIEM:RD", "INC:WR");
        db.Users.AddRange(marcus, elena, devon, sarahJ, sarahC, alex);

        // ---------- Devices ----------
        Device D(string n, string seg, string ip, string os, Severity r, DeviceStatus s, TimeSpan seen, string agent = "v4.12.8-RELEASE") => new()
        { Name = n, Segment = seg, IpAddress = ip, OperatingSystem = os, RiskLevel = r, Status = s, LastSeenAt = now - seen, AgentVersion = agent };

        var dbMaster = D("CORP-DB-MASTER01", "Finance Cluster Node", "10.142.50.12", "RHEL 9.2 Enterprise", Severity.Critical, DeviceStatus.Online, TimeSpan.Zero);
        var mbp = D("EXEC-MBP-ALEX", "Executive Leadership", "10.142.12.88", "macOS Sonoma 14.2", Severity.Low, DeviceStatus.Online, TimeSpan.FromMinutes(2));
        var kube = D("KUBE-WORKER-09", "US-East Kubernetes Node", "10.142.88.104", "Ubuntu 22.04 LTS", Severity.High, DeviceStatus.Online, TimeSpan.Zero);
        var laptop = D("DEV-LAPTOP-SARAH", "Engineering Remote", "10.142.19.4", "Windows 11 Pro", Severity.Medium, DeviceStatus.Offline, TimeSpan.FromHours(4), "v4.11.2-RELEASE");
        var gateway = D("API-GATEWAY-PROD-02", "Edge Proxy Layer", "10.142.4.12", "Debian 12 Bookworm", Severity.Low, DeviceStatus.Online, TimeSpan.FromMinutes(1));
        var ws89 = D("WORKSTATION-89", "Finance Workstation", "10.142.31.89", "Windows 11 Pro", Severity.Low, DeviceStatus.Online, TimeSpan.FromHours(2));
        var dc02 = D("DC02", "Core Domain Controller", "10.100.4.12", "Windows Server 2022", Severity.Critical, DeviceStatus.Online, TimeSpan.FromMinutes(3));
        var ws09 = D("WS-FIN-09", "Finance Workstation", "10.0.12.45", "Windows 11 Pro", Severity.High, DeviceStatus.Online, TimeSpan.FromMinutes(1));
        var nas = D("FS-NAS-01", "File Services", "10.0.8.99", "Ubuntu 22.04 LTS", Severity.Critical, DeviceStatus.Quarantined, TimeSpan.FromMinutes(45));
        var exch = D("EXCH-02", "Mail Services", "10.0.4.18", "Windows Server 2022", Severity.High, DeviceStatus.Online, TimeSpan.FromMinutes(5), "v4.11.2-RELEASE");
        db.Devices.AddRange(dbMaster, mbp, kube, laptop, gateway, ws89, dc02, ws09, nas, exch);

        // ---------- Threats ----------
        Threat T(string cat, string name, string desc, Severity sev, int ev, decimal trend, string? ip, string? asn, string? country, TimeSpan ago) => new()
        { Category = cat, Name = name, Description = desc, Severity = sev, EventCount = ev, TrendPercent = trend, SourceIp = ip, SourceAsn = asn, SourceCountry = country, DetectedAt = now - ago, Status = ThreatStatus.Active };

        var tMal = T("Malware & Ransomware", "Ransomware Payload", "Payload signatures & polymorphic strains detected.", Severity.Critical, 428, 8.2m, "185.220.101.5", "AS24940 (Hetzner Online)", "Germany", TimeSpan.FromSeconds(12));
        var tPhish = T("Phishing Campaigns", "Credential Harvester Domains", "Spear-phishing domains & credential harvesters.", Severity.High, 312, 4.1m, "45.33.22.156", "AS14061 (DigitalOcean)", "United States", TimeSpan.FromMinutes(20));
        var tBrute = T("Brute Force", "Brute Force Attempt", "Credential stuffing & automated SSH hammering.", Severity.Medium, 245, -2.5m, "45.33.32.156", "AS12389 (Rostelecom)", "Russia", TimeSpan.FromSeconds(45));
        var tDdos = T("DDoS Flooding", "Volumetric Layer 7 / SYN Flood", "Volumetric Layer 7 & SYN flood attacks intercepted.", Severity.Critical, 190, 15.0m, "203.0.113.77", "AS4837 (China Unicom)", "China", TimeSpan.FromMinutes(8));
        var tLogin = T("Suspicious Logins", "Impossible Travel", "Impossible travel & anomalous session tokens.", Severity.Medium, 154, -1.2m, "198.51.100.14", null, null, TimeSpan.FromHours(1));
        var tExfil = T("Data Exfiltration", "C2 Beaconing", "Unusual outbound data spikes & C2 beaconing.", Severity.Critical, 99, 5.8m, "185.220.101.5", "AS24940 (Hetzner Online)", "Germany", TimeSpan.FromMinutes(5));
        db.Threats.AddRange(tMal, tPhish, tBrute, tDdos, tLogin, tExfil);

        await db.SaveChangesAsync();

        // ---------- Incidents ----------
        Incident I(string code, string title, string desc, Severity sev, IncidentStatus st, User? who, string asset, Device? dev, TimeSpan ago, string src = "EDR Sensor") => new()
        {
            Code = code, Title = title, Description = desc, Severity = sev, Status = st, AssignedToUserId = who?.Id,
            AffectedAsset = asset, AffectedDeviceId = dev?.Id, Source = src, CreatedAt = now - ago, UpdatedAt = now - ago / 2,
            ResolvedAt = st == IncidentStatus.Resolved ? now - ago / 2 : null,
        };

        var i8921 = I("INC-8921", "Unauthorized RDP Brute-Force Wave", "Sustained RDP authentication failures from a rotating IP pool targeting the jump-host subnet.", Severity.Critical, IncidentStatus.Open, null, "JUMPHOST-RDP-01", null, TimeSpan.FromMinutes(12), "Firewall / Windows Security Log");
        var i8920 = I("INC-8920", "Suspicious Outbound DNS Tunneling", "High-entropy TXT queries to a newly registered domain from an internal resolver.", Severity.High, IncidentStatus.Open, null, "DNS-RESOLVER-02", null, TimeSpan.FromMinutes(28), "DNS Telemetry");
        var i8915 = I("INC-8915", "Privilege Escalation on Core DC02",
            "An anomalous process execution was detected spawning an interactive cmd.exe shell from an unquoted service path vulnerability on domain controller DC02 (10.100.4.12). Lateral movement indicators point to credential dumping activity via Mimikatz in memory.",
            Severity.Critical, IncidentStatus.Investigating, sarahC, "DC02.corp.cybershield.internal", dc02, TimeSpan.FromHours(1), "EDR Sensor / Windows-Sysmon");
        i8915.MitreTechnique = "T1574.009 - Unquoted Path";
        i8915.ContainmentStatus = "Pending Network Quarantine";
        var i8912 = I("INC-8912", "Malware Beacon Detected: APT29 Sig", "Beaconing pattern matching APT29 signature set.", Severity.High, IncidentStatus.Investigating, alex, "EXCH-02", exch, TimeSpan.FromHours(2));
        var i8908 = I("INC-8908", "Phishing Credential Harvester Sinkhole", "Harvester domain sinkholed; affected mailboxes under review.", Severity.Medium, IncidentStatus.Contained, elena, "Mail Gateway", null, TimeSpan.FromHours(4), "Email Security Gateway");
        var i8901 = I("INC-8901", "SQL Injection Attempt on API Gateway", "Blocked SQLi payloads against /api/v2/search; WAF rule tightened.", Severity.Low, IncidentStatus.Resolved, marcus, "API-GATEWAY-PROD-02", gateway, TimeSpan.FromHours(6), "WAF");
        db.Incidents.AddRange(i8921, i8920, i8915, i8912, i8908, i8901);
        await db.SaveChangesAsync();

        var t0 = i8915.CreatedAt;
        db.IncidentTimeline.AddRange(
            new IncidentTimelineEntry { IncidentId = i8915.Id, OccurredAt = t0, Title = "EDR Alert Triggered", Description = "Sysmon event ID 1 recorded anomalous process spawning from unquoted path." },
            new IncidentTimelineEntry { IncidentId = i8915.Id, OccurredAt = t0.AddMinutes(4), Title = "Analyst Assigned", Description = "Sarah Chen claimed INC-8915 for tier-2 forensic review." },
            new IncidentTimelineEntry { IncidentId = i8915.Id, OccurredAt = t0.AddMinutes(12), Title = "YARA Rule Match Verified", Description = "Memory signature confirmed Mimikatz payload injected into LSASS." });
        db.IncidentNotes.AddRange(
            new IncidentNote { IncidentId = i8915.Id, AuthorUserId = sarahC.Id, AuthorName = "Sarah Chen", CreatedAt = t0.AddMinutes(15), Body = "Verified administrative accounts active on DC02. Initiating token revocation now." },
            new IncidentNote { IncidentId = i8915.Id, AuthorUserId = alex.Id, AuthorName = "Alex Mercer", CreatedAt = t0.AddMinutes(21), Body = "Firewall rules updated to block C2 IP 185.220.101.5. Good catch team." });

        // ---------- Alerts ----------
        Alert A(string code, string sig, string details, Severity sev, AlertStatus st, string src, string dst, Device? dev, Threat? thr, TimeSpan ago, Incident? inc = null) => new()
        { Code = code, Signature = sig, Details = details, Severity = sev, Status = st, SourceIp = src, DestinationTarget = dst, DeviceId = dev?.Id, ThreatId = thr?.Id, IncidentId = inc?.Id, CreatedAt = now - ago };

        var a9821 = A("ALT-9821", "Cobalt Strike Beacon C2 Traffic", "EDR sensor detected outbound HTTPS beaconing to known malicious infrastructure utilizing randomized jitter and HTTP GET request patterns signature-matched to Cobalt Strike default malleable C2 profiles.", Severity.Critical, AlertStatus.New, "192.168.4.112", "185.220.101.5 (External)", null, tExfil, TimeSpan.Zero);
        a9821.Iocs = new()
        {
            new AlertIoc { Type = "SHA256 File Hash", Value = "4e907621c8901b2a92c019283fbc81fd2a38910b" },
            new AlertIoc { Type = "Destination IP / Port", Value = "185.220.101.5:443 (Tor Exit Node)" },
            new AlertIoc { Type = "Malicious Process Name", Value = "svchost_update.exe (PID: 4912)" },
        };
        var a9820 = A("ALT-9820", "Suspicious PowerShell Execution (Encoded)", "Proc: powershell.exe -enc JABh...", Severity.High, AlertStatus.Investigating, "10.0.12.45 (WS-FIN-09)", "Internal DC-01", ws09, null, TimeSpan.FromMinutes(12), i8915);
        var a9819 = A("ALT-9819", "Ransomware Behavior: Mass File Encryption", "Extension rename storm detected (.locked)", Severity.Critical, AlertStatus.Blocked, "10.0.8.99 (FS-NAS-01)", "Local Volumes", nas, tMal, TimeSpan.FromMinutes(45));
        var a9818 = A("ALT-9818", "Brute Force Attack (SSH Multiple Failures)", "142 failed attempts in 60s", Severity.Medium, AlertStatus.Resolved, "45.33.32.156 (External)", "10.0.1.5 (Bastion Host)", null, tBrute, TimeSpan.FromHours(2));
        var a9817 = A("ALT-9817", "Unauthorized Data Exfiltration Attempt", "Outbound transfer > 4.2 GB via HTTPS", Severity.High, AlertStatus.Investigating, "10.0.4.18 (EXCH-02)", "198.51.100.42 (Mega.nz)", exch, tExfil, TimeSpan.FromHours(3), i8912);
        var a9816 = A("ALT-9816", "Inbound Exploit Attempt: Prod Gateway", "Known CVE exploit signature on prod-gateway-us-east", Severity.Critical, AlertStatus.Investigating, "185.220.101.5", "prod-gateway-us-east", gateway, tMal, TimeSpan.FromMinutes(2));
        var a9815 = A("ALT-9815", "Auth Service Credential Stuffing", "Mitigation active on auth-service-node-4", Severity.High, AlertStatus.Investigating, "45.33.22.156", "auth-service-node-4", null, tBrute, TimeSpan.FromMinutes(14));
        var a9814 = A("ALT-9814", "Internal Workstation Anomaly", "Unusual lateral SMB enumeration", Severity.Medium, AlertStatus.Blocked, "192.168.1.104", "internal-workstation-9", ws89, null, TimeSpan.FromMinutes(41));
        var a9813 = A("ALT-9813", "Replica Lag Warning", "Replication lag threshold exceeded", Severity.Low, AlertStatus.Resolved, "10.0.4.12", "db-cluster-replica-2", null, null, TimeSpan.FromHours(2));
        db.Alerts.AddRange(a9821, a9820, a9819, a9818, a9817, a9816, a9815, a9814, a9813);

        await db.SaveChangesAsync();
    }
}
