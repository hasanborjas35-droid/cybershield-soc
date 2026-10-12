namespace CyberShield.Api.Models;

public enum Severity { Low = 0, Medium = 1, High = 2, Critical = 3 }

public enum UserRole { Viewer = 0, SecurityAnalyst = 1, Administrator = 2 }

public enum UserStatus { Pending = 0, Active = 1, Suspended = 2 }

public enum DeviceStatus { Online = 0, Offline = 1, Quarantined = 2 }

public enum ThreatStatus { Active = 0, Monitoring = 1, Mitigated = 2 }

public enum AlertStatus { New = 0, Investigating = 1, Resolved = 2, Blocked = 3, Dismissed = 4 }

public enum IncidentStatus { Open = 0, Investigating = 1, Contained = 2, Resolved = 3 }

/// <summary>SOC personnel account (Users screen).</summary>
public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Viewer;
    public UserStatus Status { get; set; } = UserStatus.Pending;
    public bool MfaEnabled { get; set; } = true;
    public DateTime? LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<UserPermission> Permissions { get; set; } = new();
}

/// <summary>Permission badge such as IAM:ALL, NET:CTL, SIEM:RD.</summary>
public class UserPermission
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Code { get; set; } = "";
    public User? User { get; set; }
}

/// <summary>Monitored endpoint (Devices screen).</summary>
public class Device
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Segment { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string OperatingSystem { get; set; } = "";
    public Severity RiskLevel { get; set; } = Severity.Low;
    public DeviceStatus Status { get; set; } = DeviceStatus.Online;
    public string AgentVersion { get; set; } = "v4.12.8-RELEASE";
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Threat category / intelligence record (Threats screen).</summary>
public class Threat
{
    public int Id { get; set; }
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Severity Severity { get; set; } = Severity.Medium;
    public int EventCount { get; set; }
    public decimal TrendPercent { get; set; }
    public string? SourceIp { get; set; }
    public string? SourceAsn { get; set; }
    public string? SourceCountry { get; set; }
    public ThreatStatus Status { get; set; } = ThreatStatus.Active;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Security alert (Alerts screen).</summary>
public class Alert
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Signature { get; set; } = "";
    public string Details { get; set; } = "";
    public Severity Severity { get; set; } = Severity.Medium;
    public AlertStatus Status { get; set; } = AlertStatus.New;
    public string SourceIp { get; set; } = "";
    public string DestinationTarget { get; set; } = "";
    public int? DeviceId { get; set; }
    public int? ThreatId { get; set; }
    public int? IncidentId { get; set; }
    public int? AssignedToUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Device? Device { get; set; }
    public Threat? Threat { get; set; }
    public Incident? Incident { get; set; }
    public User? AssignedToUser { get; set; }
    public List<AlertIoc> Iocs { get; set; } = new();
}

/// <summary>Indicator of compromise attached to an alert.</summary>
public class AlertIoc
{
    public int Id { get; set; }
    public int AlertId { get; set; }
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
    public Alert? Alert { get; set; }
}

/// <summary>Security incident (Incidents screen).</summary>
public class Incident
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Severity Severity { get; set; } = Severity.Medium;
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public string Source { get; set; } = "";
    public string AffectedAsset { get; set; } = "";
    public int? AffectedDeviceId { get; set; }
    public string? MitreTechnique { get; set; }
    public string? ContainmentStatus { get; set; }
    public int? AssignedToUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public User? AssignedToUser { get; set; }
    public Device? AffectedDevice { get; set; }
    public List<Alert> Alerts { get; set; } = new();
    public List<IncidentTimelineEntry> Timeline { get; set; } = new();
    public List<IncidentNote> Notes { get; set; } = new();
}

public class IncidentTimelineEntry
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Incident? Incident { get; set; }
}

public class IncidentNote
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public int? AuthorUserId { get; set; }
    public string AuthorName { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Incident? Incident { get; set; }
}

/// <summary>
/// Registered monitoring agent instance running on a Windows endpoint.
/// One agent maps to one Device record.
/// </summary>
public class AgentRegistration
{
    public int Id { get; set; }

    /// <summary>Stable GUID generated once by the agent and stored in its config.</summary>
    public string AgentId { get; set; } = "";

    /// <summary>Hostname of the monitored machine.</summary>
    public string Hostname { get; set; } = "";

    /// <summary>Optional link to an existing Device record.</summary>
    public int? DeviceId { get; set; }
    public Device? Device { get; set; }

    public string OsVersion { get; set; } = "";
    public string AgentVersion { get; set; } = "";
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastHeartbeatAt { get; set; } = DateTime.UtcNow;
    public bool IsOnline { get; set; } = true;
}

/// <summary>
/// A single telemetry snapshot sent by the agent every collection cycle.
/// Stores health, network, security, and event data as JSON blobs
/// so the schema stays flexible without migrations per new metric.
/// </summary>
public class AgentTelemetrySnapshot
{
    public long Id { get; set; }
    public int AgentRegistrationId { get; set; }
    public AgentRegistration? AgentRegistration { get; set; }

    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    // System Health
    public double CpuPercent { get; set; }
    public long TotalMemoryBytes { get; set; }
    public long AvailableMemoryBytes { get; set; }
    public long TotalDiskBytes { get; set; }
    public long AvailableDiskBytes { get; set; }

    // Security flags
    public bool DefenderEnabled { get; set; }
    public bool RealTimeProtectionEnabled { get; set; }
    public bool FirewallEnabled { get; set; }
    public string? DefenderSignatureDate { get; set; }

    // Event counts from Windows Event Log (since last snapshot)
    public int FailedLogonCount { get; set; }
    public int SuccessfulLogonCount { get; set; }
    public int AccountLockoutCount { get; set; }
    public int DefenderDetectionCount { get; set; }

    // JSON blobs for flexible extra data
    public string? NetworkInterfacesJson { get; set; }   // list of {name, ip, status}
    public string? SecurityEventsJson { get; set; }      // recent security events summary
    public string? ErrorsJson { get; set; }              // collector errors
}

/// <summary>A security event read from Windows Event Log, stored individually.</summary>
public class AgentSecurityEvent
{
    public long Id { get; set; }
    public int AgentRegistrationId { get; set; }
    public AgentRegistration? AgentRegistration { get; set; }

    public DateTime OccurredAt { get; set; }
    public int EventId { get; set; }
    public string Source { get; set; } = "";
    public string Category { get; set; } = "";   // FailedLogon, Lockout, DefenderDetection, etc.
    public string Message { get; set; } = "";
    public Severity Severity { get; set; } = Severity.Low;
    public bool AlertCreated { get; set; } = false;
}

/// <summary>Key-value store for platform settings (saved from the Settings page).</summary>
public class AppSetting
{
    public int    Id    { get; set; }
    public string Key   { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
