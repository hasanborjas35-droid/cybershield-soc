using System.ComponentModel.DataAnnotations;
using CyberShield.Api.Models;

namespace CyberShield.Api.Dtos;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

// ---------- Auth ----------
public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);

// ---------- Users ----------
public record UserDto(
    int Id, string FullName, string Email, UserRole Role, UserStatus Status,
    bool MfaEnabled, string[] Permissions, DateTime? LastActiveAt, DateTime CreatedAt);

public record CreateUserRequest(
    [Required, StringLength(120, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(200)] string Email,
    UserRole Role,
    string[]? Permissions);

public record UpdateUserRequest(
    [Required, StringLength(120, MinimumLength = 2)] string FullName,
    UserRole Role,
    bool MfaEnabled,
    string[]? Permissions);

public record UpdateStatusRequest<TStatus>(TStatus Status);

public record CreateUserResponse(UserDto User, string TemporaryPassword);

public record UserSummaryDto(int TotalIdentities, int Administrators, int ActiveAnalysts, int PendingOrSuspended);

// ---------- Devices ----------
public record DeviceDto(
    int Id, string Name, string Segment, string IpAddress, string OperatingSystem,
    Severity RiskLevel, DeviceStatus Status, string AgentVersion, DateTime LastSeenAt);

public record DeviceRequest(
    [Required, StringLength(120)] string Name,
    [StringLength(160)] string? Segment,
    [Required, StringLength(45)] string IpAddress,
    [StringLength(100)] string? OperatingSystem,
    Severity RiskLevel,
    DeviceStatus Status,
    [StringLength(40)] string? AgentVersion);

public record NameCount(string Name, int Count, double Percent);

public record DeviceSummaryDto(
    int TotalEndpoints, int AtRiskOrCompromised, int QuarantinedNodes, double AvgHealthIndex,
    IReadOnlyList<NameCount> OsDistribution, int PendingAgentUpdates);

// ---------- Threats ----------
public record ThreatDto(
    int Id, string Category, string Name, string Description, Severity Severity, int EventCount,
    decimal TrendPercent, string? SourceIp, string? SourceAsn, string? SourceCountry,
    ThreatStatus Status, DateTime DetectedAt);

public record ThreatRequest(
    [Required, StringLength(80)] string Category,
    [Required, StringLength(160)] string Name,
    [StringLength(500)] string? Description,
    Severity Severity,
    [Range(0, int.MaxValue)] int EventCount,
    decimal TrendPercent,
    [StringLength(45)] string? SourceIp,
    [StringLength(80)] string? SourceAsn,
    [StringLength(60)] string? SourceCountry,
    ThreatStatus Status);

public record ThreatCategoryDto(string Category, Severity Severity, int Events, decimal AvgTrendPercent);
public record ThreatOriginDto(int Rank, string Asn, string TopCategory, int Events, double Percent);
public record ThreatSummaryDto(
    int TotalEvents, int ActiveThreats, int CriticalThreats,
    IReadOnlyList<ThreatCategoryDto> Categories, IReadOnlyList<ThreatOriginDto> Origins);

// ---------- Alerts ----------
public record AlertDto(
    int Id, string Code, string Signature, string Details, Severity Severity, AlertStatus Status,
    string SourceIp, string DestinationTarget, int? DeviceId, int? ThreatId, int? IncidentId,
    int? AssignedToUserId, string? AssignedToName, DateTime CreatedAt, DateTime? UpdatedAt);

public record IocDto(int Id, string Type, string Value);
public record IocRequest([Required, StringLength(60)] string Type, [Required, StringLength(500)] string Value);

public record AlertDetailDto(AlertDto Alert, IReadOnlyList<IocDto> Iocs);

public record CreateAlertRequest(
    [Required, StringLength(200)] string Signature,
    [StringLength(2000)] string? Details,
    Severity Severity,
    [Required, StringLength(80)] string SourceIp,
    [Required, StringLength(160)] string DestinationTarget,
    int? DeviceId,
    int? ThreatId,
    IocRequest[]? Iocs);

public record AssignRequest(int? UserId);

public record AlertSummaryDto(int ActiveAlerts, int CriticalSeverity, int UnderInvestigation, int AutoBlocked);

// ---------- Incidents ----------
public record IncidentDto(
    int Id, string Code, string Title, Severity Severity, IncidentStatus Status,
    int? AssignedToUserId, string? AssignedToName, string AffectedAsset,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime? ResolvedAt);

public record TimelineEntryDto(int Id, DateTime OccurredAt, string Title, string Description);
public record NoteDto(int Id, int? AuthorUserId, string AuthorName, string Body, DateTime CreatedAt);

public record IncidentDetailDto(
    IncidentDto Incident, string Description, string Source, string? MitreTechnique,
    string? ContainmentStatus, int? AffectedDeviceId,
    IReadOnlyList<TimelineEntryDto> Timeline, IReadOnlyList<NoteDto> Notes, IReadOnlyList<int> AlertIds);

public record IncidentRequest(
    [Required, StringLength(200)] string Title,
    [StringLength(4000)] string? Description,
    Severity Severity,
    [StringLength(160)] string? Source,
    [StringLength(200)] string? AffectedAsset,
    int? AffectedDeviceId,
    [StringLength(120)] string? MitreTechnique,
    [StringLength(120)] string? ContainmentStatus,
    int? AssignedToUserId);

public record NoteRequest([Required, StringLength(2000)] string Body);
public record TimelineRequest([Required, StringLength(200)] string Title, [StringLength(1000)] string? Description, DateTime? OccurredAt);

public record PipelineColumnDto(IncidentStatus Status, int Count, IReadOnlyList<IncidentDto> Items);
public record IncidentPipelineDto(
    int ActiveIncidents, double MeanTimeToResolveMinutes, int CriticalSeverity,
    IReadOnlyList<PipelineColumnDto> Columns, int TodayCount = 0);

// ---------- Dashboard ----------
public record DashboardSummaryDto(
    int PostureScore, int ActiveThreats, int CriticalAlerts, int ProtectedDevices,
    double HealthyDevicePercent, int NetworkEvents, int OpenVulnerabilitiesEstimate,
    IReadOnlyList<NameCount> SeverityDistribution, IReadOnlyList<AlertDto> RecentAlerts);

// ---------- Agent Telemetry ----------

/// <summary>Sent by the agent on first run or when AgentId is unknown.</summary>
public record AgentRegisterRequest(
    [Required, StringLength(36, MinimumLength = 36)] string AgentId,
    [Required, StringLength(120)] string Hostname,
    [StringLength(120)] string? OsVersion,
    [StringLength(40)] string? AgentVersion);

public record AgentRegisterResponse(int RegistrationId, string AgentId, int? LinkedDeviceId);

/// <summary>Periodic heartbeat + full telemetry snapshot.</summary>
public record AgentHeartbeatRequest(
    [Required, StringLength(36)] string AgentId,
    DateTime CollectedAt,
    double CpuPercent,
    long TotalMemoryBytes,
    long AvailableMemoryBytes,
    long TotalDiskBytes,
    long AvailableDiskBytes,
    bool DefenderEnabled,
    bool RealTimeProtectionEnabled,
    bool FirewallEnabled,
    string? DefenderSignatureDate,
    int FailedLogonCount,
    int SuccessfulLogonCount,
    int AccountLockoutCount,
    int DefenderDetectionCount,
    string? NetworkInterfacesJson,
    string? ErrorsJson);

/// <summary>Individual security events (batched).</summary>
public record AgentSecurityEventRequest(
    [Required, StringLength(36)] string AgentId,
    IReadOnlyList<AgentEventItem> Events);

public record AgentEventItem(
    DateTime OccurredAt,
    int EventId,
    [Required, StringLength(80)] string Source,
    [Required, StringLength(60)] string Category,
    [StringLength(1000)] string Message,
    Severity Severity);

/// <summary>Latest agent status for the dashboard.</summary>
public record AgentStatusDto(
    string AgentId,
    string Hostname,
    string OsVersion,
    string AgentVersion,
    DateTime LastHeartbeatAt,
    bool IsOnline,
    int? LinkedDeviceId,
    // latest snapshot values
    double CpuPercent,
    long TotalMemoryBytes,
    long AvailableMemoryBytes,
    long TotalDiskBytes,
    long AvailableDiskBytes,
    bool DefenderEnabled,
    bool RealTimeProtectionEnabled,
    bool FirewallEnabled,
    string? DefenderSignatureDate,
    int RecentFailedLogons,
    int RecentDefenderDetections,
    string? NetworkInterfacesJson);
