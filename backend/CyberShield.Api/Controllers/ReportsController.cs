using CyberShield.Api.Data;
using CyberShield.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberShield.Api.Controllers;

/// <summary>
/// Security Reports — aggregates real data from all modules.
/// GET /api/reports/summary  → full report snapshot
/// GET /api/reports/trends   → 7-day trend data
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var now     = DateTime.UtcNow;
        var last7d  = now.AddDays(-7);
        var last30d = now.AddDays(-30);

        // ── Security Posture ─────────────────────────────────────────
        var openAlerts   = db.Alerts.AsNoTracking().Where(a => a.Status != AlertStatus.Resolved && a.Status != AlertStatus.Dismissed);
        var critAlerts   = await openAlerts.CountAsync(a => a.Severity == Severity.Critical);
        var highAlerts   = await openAlerts.CountAsync(a => a.Severity == Severity.High);
        var devices      = db.Devices.AsNoTracking();
        var totalDevices = await devices.CountAsync();
        var critDevices  = await devices.CountAsync(d => d.RiskLevel == Severity.Critical);
        var quarantined  = await devices.CountAsync(d => d.Status == DeviceStatus.Quarantined);
        var critInc      = await db.Incidents.CountAsync(i => i.Severity == Severity.Critical && i.Status != IncidentStatus.Resolved);
        var postureScore = Math.Clamp(100 - critAlerts * 3 - highAlerts - critDevices * 2 - quarantined - critInc * 2, 0, 100);

        // ── Alerts Stats ─────────────────────────────────────────────
        var totalAlerts    = await db.Alerts.CountAsync();
        var resolvedAlerts = await db.Alerts.CountAsync(a => a.Status == AlertStatus.Resolved);
        var alerts7d       = await db.Alerts.CountAsync(a => a.CreatedAt >= last7d);
        var alerts30d      = await db.Alerts.CountAsync(a => a.CreatedAt >= last30d);

        var alertsBySev = await db.Alerts.AsNoTracking()
            .GroupBy(a => a.Severity)
            .Select(g => new { Severity = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        // ── Incident Stats ───────────────────────────────────────────
        var totalInc     = await db.Incidents.CountAsync();
        var openInc      = await db.Incidents.CountAsync(i => i.Status == IncidentStatus.Open);
        var investigInc  = await db.Incidents.CountAsync(i => i.Status == IncidentStatus.Investigating);
        var resolvedInc  = await db.Incidents.CountAsync(i => i.Status == IncidentStatus.Resolved);
        var inc7d        = await db.Incidents.CountAsync(i => i.CreatedAt >= last7d);

        var resolvedWithTime = await db.Incidents.AsNoTracking()
            .Where(i => i.ResolvedAt != null)
            .Select(i => new { i.CreatedAt, R = i.ResolvedAt!.Value })
            .ToListAsync();
        var mttr = resolvedWithTime.Count == 0 ? 0
            : Math.Round(resolvedWithTime.Average(x => (x.R - x.CreatedAt).TotalMinutes), 1);

        // ── Device Stats ─────────────────────────────────────────────
        var onlineDevices  = await devices.CountAsync(d => d.Status == DeviceStatus.Online);
        var offlineDevices = await devices.CountAsync(d => d.Status == DeviceStatus.Offline);
        var healthy        = await devices.CountAsync(d => d.RiskLevel == Severity.Low);
        var atRisk         = await devices.CountAsync(d => d.RiskLevel == Severity.High || d.RiskLevel == Severity.Critical);

        var devicesByOs = await db.Devices.AsNoTracking()
            .GroupBy(d => d.OperatingSystem.Contains("Windows") ? "Windows" :
                          d.OperatingSystem.Contains("macOS")   ? "macOS"   :
                          d.OperatingSystem.Contains("Ubuntu") || d.OperatingSystem.Contains("Debian") || d.OperatingSystem.Contains("RHEL") ? "Linux" : "Other")
            .Select(g => new { Os = g.Key, Count = g.Count() })
            .ToListAsync();

        // ── Threat Stats ─────────────────────────────────────────────
        var totalThreats  = await db.Threats.CountAsync();
        var activeThreats = await db.Threats.CountAsync(t => t.Status == ThreatStatus.Active);
        var totalEvents   = await db.Threats.SumAsync(t => t.EventCount);

        var threatsByCategory = await db.Threats.AsNoTracking()
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Count = g.Count(), Events = g.Sum(t => t.EventCount) })
            .OrderByDescending(g => g.Events)
            .Take(5)
            .ToListAsync();

        // ── Agent / Endpoint Stats ────────────────────────────────────
        var agentCount  = await db.AgentRegistrations.CountAsync();
        var onlineAgents = await db.AgentRegistrations
            .CountAsync(r => r.LastHeartbeatAt >= now.AddMinutes(-2));

        var latestSnap = await db.AgentTelemetry
            .OrderByDescending(s => s.CollectedAt)
            .FirstOrDefaultAsync();

        // ── 7-day daily alert trend ───────────────────────────────────
        var dailyAlerts = await db.Alerts.AsNoTracking()
            .Where(a => a.CreatedAt >= last7d)
            .GroupBy(a => a.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(g => g.Date)
            .ToListAsync();

        // Fill missing days with 0
        var trend = Enumerable.Range(0, 7).Select(i =>
        {
            var d = now.AddDays(-6 + i).Date;
            var c = dailyAlerts.FirstOrDefault(x => x.Date == d)?.Count ?? 0;
            return new { date = d.ToString("MMM dd"), count = c };
        }).ToList();

        return Ok(new
        {
            generatedAt  = now,
            // Posture
            postureScore,
            postureGrade = postureScore >= 80 ? "Good" : postureScore >= 60 ? "Fair" : "Poor",
            // Alerts
            alerts = new { totalAlerts, resolvedAlerts, openAlerts = totalAlerts - resolvedAlerts,
                           alerts7d, alerts30d, criticalOpen = critAlerts, highOpen = highAlerts,
                           bySeverity = alertsBySev, dailyTrend = trend },
            // Incidents
            incidents = new { totalInc, openInc, investigInc, resolvedInc, inc7d, mttrMinutes = mttr },
            // Devices
            devices = new { totalDevices, onlineDevices, offlineDevices, quarantined,
                            healthy, atRisk, criticalRisk = critDevices, byOs = devicesByOs },
            // Threats
            threats = new { totalThreats, activeThreats, totalEvents, byCategory = threatsByCategory },
            // Agent
            agent = new
            {
                registeredAgents = agentCount,
                onlineAgents,
                cpuPercent        = latestSnap?.CpuPercent ?? 0,
                defenderEnabled   = latestSnap?.DefenderEnabled ?? false,
                rtpEnabled        = latestSnap?.RealTimeProtectionEnabled ?? false,
                firewallEnabled   = latestSnap?.FirewallEnabled ?? false,
                failedLogons      = latestSnap?.FailedLogonCount ?? 0,
                detections        = latestSnap?.DefenderDetectionCount ?? 0
            }
        });
    }
}
