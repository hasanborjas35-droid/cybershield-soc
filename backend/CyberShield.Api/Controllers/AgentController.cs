using CyberShield.Api.Data;
using CyberShield.Api.Dtos;
using CyberShield.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CyberShield.Api.Controllers;

/// <summary>
/// Receives telemetry from the CyberShield Windows Monitoring Agent.
/// Registration uses a shared API key (header X-Agent-Key).
/// Heartbeat and events require the same key.
/// </summary>
[ApiController]
[Route("api/agent")]
public class AgentController(AppDbContext db, IConfiguration config, ILogger<AgentController> log)
    : ControllerBase
{
    private bool ValidateAgentKey()
    {
        var expected = config["Agent:ApiKey"];
        if (string.IsNullOrEmpty(expected)) return true; // key not configured → open (dev mode)
        Request.Headers.TryGetValue("X-Agent-Key", out var provided);
        return provided == expected;
    }

    // ────────────────────────────────────────────────────────────────
    // POST /api/agent/register
    // Called once by the agent on first run.
    // ────────────────────────────────────────────────────────────────
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] AgentRegisterRequest req)
    {
        if (!ValidateAgentKey()) return Unauthorized(new { error = "Invalid agent key." });

        var existing = await db.AgentRegistrations.FirstOrDefaultAsync(a => a.AgentId == req.AgentId);
        if (existing != null)
        {
            existing.Hostname = req.Hostname;
            existing.OsVersion = req.OsVersion ?? existing.OsVersion;
            existing.AgentVersion = req.AgentVersion ?? existing.AgentVersion;
            existing.IsOnline = true;
            existing.LastHeartbeatAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Ok(new AgentRegisterResponse(existing.Id, existing.AgentId, existing.DeviceId));
        }

        // Try to auto-link to an existing Device record by hostname
        var device = await db.Devices.FirstOrDefaultAsync(d =>
            d.Name.ToLower() == req.Hostname.ToLower());

        var reg = new AgentRegistration
        {
            AgentId = req.AgentId,
            Hostname = req.Hostname,
            OsVersion = req.OsVersion ?? "",
            AgentVersion = req.AgentVersion ?? "",
            DeviceId = device?.Id,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeatAt = DateTime.UtcNow,
            IsOnline = true
        };
        db.AgentRegistrations.Add(reg);

        // If no device exists, create one automatically
        if (device == null)
        {
            var newDevice = new Device
            {
                Name = req.Hostname,
                Segment = "Agent-Monitored",
                IpAddress = "127.0.0.1",
                OperatingSystem = req.OsVersion ?? "Windows",
                Status = DeviceStatus.Online,
                AgentVersion = req.AgentVersion ?? "agent",
                RiskLevel = Severity.Low,
                LastSeenAt = DateTime.UtcNow
            };
            db.Devices.Add(newDevice);
            await db.SaveChangesAsync();
            reg.DeviceId = newDevice.Id;
        }

        await db.SaveChangesAsync();
        log.LogInformation("Agent registered: {AgentId} / {Hostname}", reg.AgentId, reg.Hostname);
        return Ok(new AgentRegisterResponse(reg.Id, reg.AgentId, reg.DeviceId));
    }

    // ────────────────────────────────────────────────────────────────
    // POST /api/agent/heartbeat
    // Sent every collection cycle (default 30 s) with a full snapshot.
    // ────────────────────────────────────────────────────────────────
    [HttpPost("heartbeat")]
    [AllowAnonymous]
    public async Task<IActionResult> Heartbeat([FromBody] AgentHeartbeatRequest req)
    {
        if (!ValidateAgentKey()) return Unauthorized(new { error = "Invalid agent key." });

        var reg = await db.AgentRegistrations.FirstOrDefaultAsync(a => a.AgentId == req.AgentId);
        if (reg == null) return NotFound(new { error = "Agent not registered. Call /api/agent/register first." });

        // Update registration status
        reg.LastHeartbeatAt = DateTime.UtcNow;
        reg.IsOnline = true;

        // Update linked device's LastSeen + status
        if (reg.DeviceId.HasValue)
        {
            var dev = await db.Devices.FindAsync(reg.DeviceId.Value);
            if (dev != null && dev.Status != DeviceStatus.Quarantined)
            {
                dev.LastSeenAt = DateTime.UtcNow;
                dev.Status = DeviceStatus.Online;
                // Elevate risk if security issues detected
                if (!req.DefenderEnabled || !req.RealTimeProtectionEnabled || !req.FirewallEnabled)
                    dev.RiskLevel = Severity.High;
                else if (req.FailedLogonCount >= 5)
                    dev.RiskLevel = Severity.Medium;
                else
                    dev.RiskLevel = Severity.Low;
            }
        }

        // Persist snapshot
        var snap = new AgentTelemetrySnapshot
        {
            AgentRegistrationId = reg.Id,
            CollectedAt = req.CollectedAt == default ? DateTime.UtcNow : req.CollectedAt,
            CpuPercent = req.CpuPercent,
            TotalMemoryBytes = req.TotalMemoryBytes,
            AvailableMemoryBytes = req.AvailableMemoryBytes,
            TotalDiskBytes = req.TotalDiskBytes,
            AvailableDiskBytes = req.AvailableDiskBytes,
            DefenderEnabled = req.DefenderEnabled,
            RealTimeProtectionEnabled = req.RealTimeProtectionEnabled,
            FirewallEnabled = req.FirewallEnabled,
            DefenderSignatureDate = req.DefenderSignatureDate,
            FailedLogonCount = req.FailedLogonCount,
            SuccessfulLogonCount = req.SuccessfulLogonCount,
            AccountLockoutCount = req.AccountLockoutCount,
            DefenderDetectionCount = req.DefenderDetectionCount,
            NetworkInterfacesJson = req.NetworkInterfacesJson,
            ErrorsJson = req.ErrorsJson
        };
        db.AgentTelemetry.Add(snap);

        // Auto-raise alert if critical security issues detected
        await RaiseSecurityAlertsIfNeeded(reg, req);

        await db.SaveChangesAsync();
        return Ok(new { received = true, serverTime = DateTime.UtcNow });
    }

    // ────────────────────────────────────────────────────────────────
    // POST /api/agent/events
    // Batched Windows security events.
    // ────────────────────────────────────────────────────────────────
    [HttpPost("events")]
    [AllowAnonymous]
    public async Task<IActionResult> Events([FromBody] AgentSecurityEventRequest req)
    {
        if (!ValidateAgentKey()) return Unauthorized(new { error = "Invalid agent key." });

        var reg = await db.AgentRegistrations.FirstOrDefaultAsync(a => a.AgentId == req.AgentId);
        if (reg == null) return NotFound(new { error = "Agent not registered." });

        foreach (var ev in req.Events)
        {
            var secEvent = new AgentSecurityEvent
            {
                AgentRegistrationId = reg.Id,
                OccurredAt = ev.OccurredAt,
                EventId = ev.EventId,
                Source = ev.Source,
                Category = ev.Category,
                Message = ev.Message,
                Severity = ev.Severity,
                AlertCreated = false
            };
            db.AgentSecurityEvents.Add(secEvent);

            // Auto-create Alert in the SOC for high-severity events
            if (ev.Severity >= Severity.High)
            {
                await CreateAlertFromEvent(reg, secEvent);
                secEvent.AlertCreated = true;
            }
        }

        await db.SaveChangesAsync();
        return Ok(new { stored = req.Events.Count });
    }

    // ────────────────────────────────────────────────────────────────
    // GET /api/agent/status
    // Returns latest snapshot for all registered agents — used by dashboard.
    // ────────────────────────────────────────────────────────────────
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> Status()
    {
        var regs = await db.AgentRegistrations
            .OrderByDescending(r => r.LastHeartbeatAt)
            .ToListAsync();

        var result = new List<AgentStatusDto>();
        foreach (var reg in regs)
        {
            var snap = await db.AgentTelemetry
                .Where(s => s.AgentRegistrationId == reg.Id)
                .OrderByDescending(s => s.CollectedAt)
                .FirstOrDefaultAsync();

            // Mark offline if no heartbeat in last 2 minutes
            if ((DateTime.UtcNow - reg.LastHeartbeatAt).TotalMinutes > 2)
                reg.IsOnline = false;

            result.Add(new AgentStatusDto(
                AgentId: reg.AgentId,
                Hostname: reg.Hostname,
                OsVersion: reg.OsVersion,
                AgentVersion: reg.AgentVersion,
                LastHeartbeatAt: reg.LastHeartbeatAt,
                IsOnline: reg.IsOnline,
                LinkedDeviceId: reg.DeviceId,
                CpuPercent: snap?.CpuPercent ?? 0,
                TotalMemoryBytes: snap?.TotalMemoryBytes ?? 0,
                AvailableMemoryBytes: snap?.AvailableMemoryBytes ?? 0,
                TotalDiskBytes: snap?.TotalDiskBytes ?? 0,
                AvailableDiskBytes: snap?.AvailableDiskBytes ?? 0,
                DefenderEnabled: snap?.DefenderEnabled ?? false,
                RealTimeProtectionEnabled: snap?.RealTimeProtectionEnabled ?? false,
                FirewallEnabled: snap?.FirewallEnabled ?? false,
                DefenderSignatureDate: snap?.DefenderSignatureDate,
                RecentFailedLogons: snap?.FailedLogonCount ?? 0,
                RecentDefenderDetections: snap?.DefenderDetectionCount ?? 0,
                NetworkInterfacesJson: snap?.NetworkInterfacesJson
            ));
        }

        await db.SaveChangesAsync(); // persist IsOnline changes
        return Ok(result);
    }

    // ────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────
    private async Task RaiseSecurityAlertsIfNeeded(AgentRegistration reg, AgentHeartbeatRequest req)
    {
        var issues = new List<(string sig, string detail, Severity sev)>();

        if (!req.DefenderEnabled)
            issues.Add(("Defender Disabled on " + reg.Hostname, "Microsoft Defender antivirus is disabled on the monitored endpoint.", Severity.Critical));
        else if (!req.RealTimeProtectionEnabled)
            issues.Add(("Defender Real-Time Protection Off — " + reg.Hostname, "Real-time protection is turned off. The endpoint may be exposed.", Severity.High));

        if (!req.FirewallEnabled)
            issues.Add(("Windows Firewall Disabled — " + reg.Hostname, "All firewall profiles appear to be disabled on the monitored endpoint.", Severity.High));

        if (req.AccountLockoutCount > 0)
            issues.Add(("Account Lockout Detected — " + reg.Hostname, $"{req.AccountLockoutCount} account lockout(s) detected in this collection window.", Severity.High));

        if (req.DefenderDetectionCount > 0)
            issues.Add(("Defender Threat Detection — " + reg.Hostname, $"Microsoft Defender detected {req.DefenderDetectionCount} threat(s) in this collection window.", Severity.Critical));

        foreach (var (sig, detail, sev) in issues)
        {
            // Avoid duplicate open alerts for the same signature
            var alreadyOpen = await db.Alerts.AnyAsync(a =>
                a.Signature == sig &&
                a.Status == AlertStatus.New &&
                a.CreatedAt > DateTime.UtcNow.AddHours(-1));
            if (alreadyOpen) continue;

            var code = "AGT-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            db.Alerts.Add(new Alert
            {
                Code = code,
                Signature = sig,
                Details = detail,
                Severity = sev,
                Status = AlertStatus.New,
                SourceIp = reg.Hostname,
                DestinationTarget = reg.Hostname,
                DeviceId = reg.DeviceId,
                CreatedAt = DateTime.UtcNow
            });
            log.LogWarning("Auto-alert created: {Sig}", sig);
        }
    }

    private async Task CreateAlertFromEvent(AgentRegistration reg, AgentSecurityEvent ev)
    {
        var alreadyOpen = await db.Alerts.AnyAsync(a =>
            a.Signature.Contains(ev.Category) &&
            a.SourceIp == reg.Hostname &&
            a.Status == AlertStatus.New &&
            a.CreatedAt > DateTime.UtcNow.AddMinutes(-30));
        if (alreadyOpen) return;

        var code = "AGT-EV-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        db.Alerts.Add(new Alert
        {
            Code = code,
            Signature = $"{ev.Category} — {reg.Hostname} (EventID {ev.EventId})",
            Details = ev.Message,
            Severity = ev.Severity,
            Status = AlertStatus.New,
            SourceIp = reg.Hostname,
            DestinationTarget = reg.Hostname,
            DeviceId = reg.DeviceId,
            CreatedAt = ev.OccurredAt
        });
    }
}
