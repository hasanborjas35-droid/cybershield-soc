using CyberShield.Api.Data;
using CyberShield.Api.Dtos;
using CyberShield.Api.Models;
using CyberShield.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberShield.Api.Controllers;

[ApiController]
[Route("api/incidents")]
[Authorize]
public class IncidentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public IncidentsController(AppDbContext db) => _db = db;

    private IQueryable<Incident> WithUser => _db.Incidents.Include(i => i.AssignedToUser);

    private static IncidentDto ToDto(Incident i) => new(
        i.Id, i.Code, i.Title, i.Severity, i.Status, i.AssignedToUserId, i.AssignedToUser?.FullName,
        i.AffectedAsset, i.CreatedAt, i.UpdatedAt, i.ResolvedAt);

    private static IncidentDetailDto ToDetail(Incident i) => new(
        ToDto(i), i.Description, i.Source, i.MitreTechnique, i.ContainmentStatus, i.AffectedDeviceId,
        i.Timeline.OrderBy(t => t.OccurredAt).Select(t => new TimelineEntryDto(t.Id, t.OccurredAt, t.Title, t.Description)).ToList(),
        i.Notes.OrderBy(n => n.CreatedAt).Select(n => new NoteDto(n.Id, n.AuthorUserId, n.AuthorName, n.Body, n.CreatedAt)).ToList(),
        i.Alerts.Select(a => a.Id).ToList());

    internal static async Task<string> NextCodeAsync(AppDbContext db)
    {
        var codes = await db.Incidents.Select(i => i.Code).ToListAsync();
        var max = codes.Select(c => int.TryParse(c.Replace("INC-", ""), out var n) ? n : 0).DefaultIfEmpty(8000).Max();
        return $"INC-{max + 1}";
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<IncidentDto>>> List(
        [FromQuery] Severity? severity, [FromQuery] IncidentStatus? status, [FromQuery] int? assignedTo,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var q = WithUser.AsNoTracking();
        if (severity is not null) q = q.Where(i => i.Severity == severity);
        if (status is not null) q = q.Where(i => i.Status == status);
        if (assignedTo is not null) q = q.Where(i => i.AssignedToUserId == assignedTo);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(i => i.Title.Contains(search) || i.Code.Contains(search));
        var paged = await q.OrderByDescending(i => i.CreatedAt).ToPagedAsync(page, pageSize);
        return new PagedResult<IncidentDto>(paged.Items.Select(ToDto).ToList(), paged.Total, paged.Page, paged.PageSize);
    }

    /// <summary>Incident Workflow Pipeline: columns Open / Investigating / Contained / Resolved plus headline KPIs.</summary>
    [HttpGet("pipeline")]
    public async Task<IncidentPipelineDto> Pipeline([FromQuery] int perColumn = 10)
    {
        perColumn = Math.Clamp(perColumn, 1, 50);
        var all = _db.Incidents.AsNoTracking();
        var columns = new List<PipelineColumnDto>();
        foreach (var st in Enum.GetValues<IncidentStatus>())
        {
            var count = await all.CountAsync(i => i.Status == st);
            var items = await WithUser.AsNoTracking().Where(i => i.Status == st)
                .OrderByDescending(i => i.CreatedAt).Take(perColumn).ToListAsync();
            columns.Add(new PipelineColumnDto(st, count, items.Select(ToDto).ToList()));
        }
        var resolved = await all.Where(i => i.ResolvedAt != null).Select(i => new { i.CreatedAt, R = i.ResolvedAt!.Value }).ToListAsync();
        var mttr = resolved.Count == 0 ? 0 : Math.Round(resolved.Average(x => (x.R - x.CreatedAt).TotalMinutes), 1);
        var todayCount = await all.CountAsync(i => i.CreatedAt >= DateTime.UtcNow.AddHours(-24));
        return new IncidentPipelineDto(
            await all.CountAsync(i => i.Status != IncidentStatus.Resolved), mttr,
            await all.CountAsync(i => i.Severity == Severity.Critical && i.Status != IncidentStatus.Resolved),
            columns, todayCount);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<IncidentDetailDto>> Get(int id)
    {
        var i = await WithUser.AsNoTracking().Include(x => x.Timeline).Include(x => x.Notes).Include(x => x.Alerts)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);
        return i is null ? NotFound() : ToDetail(i);
    }

    [HttpPost, Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<IncidentDetailDto>> Create(IncidentRequest r)
    {
        var err = await ValidateRefsAsync(r);
        if (err is not null) return BadRequest(new { error = err });
        var i = new Incident
        {
            Code = await NextCodeAsync(_db), Status = IncidentStatus.Open,
            Timeline = { new IncidentTimelineEntry { Title = "Incident opened", Description = r.Title } },
        };
        Apply(i, r);
        _db.Incidents.Add(i);
        await _db.SaveChangesAsync();
        await _db.Entry(i).Reference(x => x.AssignedToUser).LoadAsync();
        return CreatedAtAction(nameof(Get), new { id = i.Id }, ToDetail(i));
    }

    [HttpPut("{id:int}"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<IncidentDto>> Update(int id, IncidentRequest r)
    {
        var err = await ValidateRefsAsync(r);
        if (err is not null) return BadRequest(new { error = err });
        var i = await WithUser.FirstOrDefaultAsync(x => x.Id == id);
        if (i is null) return NotFound();
        Apply(i, r);
        await _db.SaveChangesAsync();
        await _db.Entry(i).Reference(x => x.AssignedToUser).LoadAsync();
        return ToDto(i);
    }

    /// <summary>Move an incident across the pipeline. Stamps ResolvedAt and appends a timeline entry.</summary>
    [HttpPatch("{id:int}/status"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<IncidentDto>> SetStatus(int id, UpdateStatusRequest<IncidentStatus> req)
    {
        var i = await WithUser.FirstOrDefaultAsync(x => x.Id == id);
        if (i is null) return NotFound();
        if (i.Status == req.Status) return ToDto(i);
        var old = i.Status;
        i.Status = req.Status;
        i.UpdatedAt = DateTime.UtcNow;
        i.ResolvedAt = req.Status == IncidentStatus.Resolved ? DateTime.UtcNow : null;
        _db.IncidentTimeline.Add(new IncidentTimelineEntry { IncidentId = i.Id, Title = "Status changed", Description = $"{old} → {req.Status}" });
        await _db.SaveChangesAsync();
        return ToDto(i);
    }

    [HttpPost("{id:int}/assign"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<IncidentDto>> Assign(int id, AssignRequest req)
    {
        var i = await WithUser.FirstOrDefaultAsync(x => x.Id == id);
        if (i is null) return NotFound();
        User? analyst = null;
        if (req.UserId is not null)
        {
            analyst = await _db.Users.FindAsync(req.UserId);
            if (analyst is null || analyst.Status != UserStatus.Active) return BadRequest(new { error = "Analyst not found or not active." });
        }
        i.AssignedToUser = analyst; i.AssignedToUserId = analyst?.Id;
        i.UpdatedAt = DateTime.UtcNow;
        if (analyst is not null && i.Status == IncidentStatus.Open) i.Status = IncidentStatus.Investigating;
        _db.IncidentTimeline.Add(new IncidentTimelineEntry
        {
            IncidentId = i.Id, Title = analyst is null ? "Analyst unassigned" : "Analyst Assigned",
            Description = analyst is null ? "Incident returned to the unassigned queue." : $"{analyst.FullName} claimed {i.Code}.",
        });
        await _db.SaveChangesAsync();
        return ToDto(i);
    }

    [HttpPost("{id:int}/notes"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<NoteDto>> AddNote(int id, NoteRequest r)
    {
        if (!await _db.Incidents.AnyAsync(i => i.Id == id)) return NotFound();
        var note = new IncidentNote
        {
            IncidentId = id, Body = r.Body.Trim(),
            AuthorUserId = int.Parse(User.FindFirst("sub")!.Value), AuthorName = User.FindFirst("name")?.Value ?? "Unknown",
        };
        _db.IncidentNotes.Add(note);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id }, new NoteDto(note.Id, note.AuthorUserId, note.AuthorName, note.Body, note.CreatedAt));
    }

    [HttpPost("{id:int}/timeline"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<TimelineEntryDto>> AddTimeline(int id, TimelineRequest r)
    {
        if (!await _db.Incidents.AnyAsync(i => i.Id == id)) return NotFound();
        var e = new IncidentTimelineEntry
        {
            IncidentId = id, Title = r.Title.Trim(), Description = r.Description ?? "",
            OccurredAt = r.OccurredAt?.ToUniversalTime() ?? DateTime.UtcNow,
        };
        _db.IncidentTimeline.Add(e);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id }, new TimelineEntryDto(e.Id, e.OccurredAt, e.Title, e.Description));
    }

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var i = await _db.Incidents.FindAsync(id);
        if (i is null) return NotFound();
        // Alerts.IncidentId is NoAction in SQL Server: detach linked alerts first.
        await _db.Alerts.Where(a => a.IncidentId == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.IncidentId, (int?)null));
        _db.Incidents.Remove(i);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(Incident i, IncidentRequest r)
    {
        i.Title = r.Title.Trim(); i.Description = r.Description ?? ""; i.Severity = r.Severity;
        i.Source = r.Source ?? i.Source; i.AffectedAsset = r.AffectedAsset ?? i.AffectedAsset;
        i.AffectedDeviceId = r.AffectedDeviceId; i.MitreTechnique = r.MitreTechnique; i.ContainmentStatus = r.ContainmentStatus;
        i.AssignedToUserId = r.AssignedToUserId; i.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<string?> ValidateRefsAsync(IncidentRequest r)
    {
        if (r.AssignedToUserId is not null && !await _db.Users.AnyAsync(u => u.Id == r.AssignedToUserId)) return "AssignedToUserId not found.";
        if (r.AffectedDeviceId is not null && !await _db.Devices.AnyAsync(d => d.Id == r.AffectedDeviceId)) return "AffectedDeviceId not found.";
        return null;
    }
}

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    /// <summary>
    /// Headline numbers for the SOC dashboard. PostureScore = 100 - (open critical alerts × 3) - (open high alerts × 1)
    /// - (critical devices × 2) - (quarantined devices × 1) - (active critical incidents × 2), clamped to 0..100.
    /// </summary>
    [HttpGet("summary")]
    public async Task<DashboardSummaryDto> Summary()
    {
        var openAlerts = _db.Alerts.AsNoTracking().Where(a => a.Status != AlertStatus.Resolved && a.Status != AlertStatus.Dismissed);
        var critAlerts = await openAlerts.CountAsync(a => a.Severity == Severity.Critical);
        var highAlerts = await openAlerts.CountAsync(a => a.Severity == Severity.High);
        var devices = _db.Devices.AsNoTracking();
        var totalDevices = await devices.CountAsync();
        var critDevices = await devices.CountAsync(d => d.RiskLevel == Severity.Critical);
        var quarantined = await devices.CountAsync(d => d.Status == DeviceStatus.Quarantined);
        var healthy = await devices.CountAsync(d => d.RiskLevel == Severity.Low || d.RiskLevel == Severity.Medium);
        var critIncidents = await _db.Incidents.CountAsync(i => i.Severity == Severity.Critical && i.Status != IncidentStatus.Resolved);

        var score = Math.Clamp(100 - critAlerts * 3 - highAlerts - critDevices * 2 - quarantined - critIncidents * 2, 0, 100);

        var sev = await openAlerts.GroupBy(a => a.Severity).Select(g => new { g.Key, C = g.Count() }).ToListAsync();
        var sevTotal = sev.Sum(x => x.C);
        var dist = Enum.GetValues<Severity>().OrderByDescending(s => (int)s)
            .Select(s => { var c = sev.FirstOrDefault(x => x.Key == s)?.C ?? 0; return new NameCount(s.ToString(), c, sevTotal == 0 ? 0 : Math.Round(100.0 * c / sevTotal, 1)); })
            .ToList();

        var recent = await _db.Alerts.AsNoTracking().Include(a => a.AssignedToUser)
            .OrderByDescending(a => a.CreatedAt).Take(4).ToListAsync();

        static AlertDto AlertToDto(Alert a) => new(
            a.Id, a.Code, a.Signature, a.Details, a.Severity, a.Status,
            a.SourceIp, a.DestinationTarget, a.DeviceId, a.ThreatId, a.IncidentId,
            a.AssignedToUserId, a.AssignedToUser?.FullName, a.CreatedAt, a.UpdatedAt);

        return new DashboardSummaryDto(
            score,
            await _db.Threats.CountAsync(t => t.Status == ThreatStatus.Active),
            critAlerts, totalDevices,
            totalDevices == 0 ? 0 : Math.Round(100.0 * healthy / totalDevices, 1),
            await _db.Threats.SumAsync(t => t.EventCount),
            await devices.CountAsync(d => d.RiskLevel == Severity.High || d.RiskLevel == Severity.Critical),
            dist, recent.Select(AlertToDto).ToList());
    }

    /// <summary>
    /// Returns the latest agent telemetry for all registered agents.
    /// Used by the dashboard to display real endpoint health panels.
    /// </summary>
    [HttpGet("agent-status")]
    public async Task<IActionResult> AgentStatus()
    {
        var regs = await _db.AgentRegistrations
            .OrderByDescending(r => r.LastHeartbeatAt)
            .ToListAsync();

        if (regs.Count == 0)
            return Ok(new { agents = Array.Empty<object>(), liveMonitoring = false });

        var result = new List<object>();
        foreach (var reg in regs)
        {
            // Mark offline if no heartbeat in last 2 minutes
            var isOnline = (DateTime.UtcNow - reg.LastHeartbeatAt).TotalMinutes <= 2;

            var snap = await _db.AgentTelemetry
                .Where(s => s.AgentRegistrationId == reg.Id)
                .OrderByDescending(s => s.CollectedAt)
                .FirstOrDefaultAsync();

            // Recent security events (last 24h)
            var since24h = DateTime.UtcNow.AddHours(-24);
            var recentEvents = await _db.AgentSecurityEvents
                .Where(e => e.AgentRegistrationId == reg.Id && e.OccurredAt >= since24h)
                .OrderByDescending(e => e.OccurredAt)
                .Take(10)
                .Select(e => new { e.OccurredAt, e.Category, e.Message, Severity = e.Severity.ToString() })
                .ToListAsync();

            double ramUsedPct = 0, diskUsedPct = 0;
            if (snap != null)
            {
                ramUsedPct  = snap.TotalMemoryBytes  > 0 ? Math.Round(100.0 * (snap.TotalMemoryBytes  - snap.AvailableMemoryBytes)  / snap.TotalMemoryBytes,  1) : 0;
                diskUsedPct = snap.TotalDiskBytes    > 0 ? Math.Round(100.0 * (snap.TotalDiskBytes    - snap.AvailableDiskBytes)    / snap.TotalDiskBytes,    1) : 0;
            }

            result.Add(new
            {
                agentId          = reg.AgentId,
                hostname         = reg.Hostname,
                osVersion        = reg.OsVersion,
                agentVersion     = reg.AgentVersion,
                lastHeartbeatAt  = reg.LastHeartbeatAt,
                isOnline         = isOnline,
                linkedDeviceId   = reg.DeviceId,
                // Health metrics
                cpuPercent            = snap?.CpuPercent       ?? 0,
                ramUsedPercent        = ramUsedPct,
                diskUsedPercent       = diskUsedPct,
                totalMemoryGb         = snap != null ? Math.Round(snap.TotalMemoryBytes  / 1_073_741_824.0, 1) : 0,
                availableMemoryGb     = snap != null ? Math.Round(snap.AvailableMemoryBytes / 1_073_741_824.0, 1) : 0,
                totalDiskGb           = snap != null ? Math.Round(snap.TotalDiskBytes    / 1_073_741_824.0, 1) : 0,
                availableDiskGb       = snap != null ? Math.Round(snap.AvailableDiskBytes / 1_073_741_824.0, 1) : 0,
                // Security status
                defenderEnabled           = snap?.DefenderEnabled           ?? false,
                realTimeProtectionEnabled = snap?.RealTimeProtectionEnabled ?? false,
                firewallEnabled           = snap?.FirewallEnabled           ?? false,
                defenderSignatureDate     = snap?.DefenderSignatureDate,
                // Event counts
                failedLogons          = snap?.FailedLogonCount          ?? 0,
                defenderDetections    = snap?.DefenderDetectionCount    ?? 0,
                accountLockouts       = snap?.AccountLockoutCount       ?? 0,
                // Network
                networkInterfacesJson = snap?.NetworkInterfacesJson,
                // Recent events
                recentSecurityEvents  = recentEvents,
                collectedAt           = snap?.CollectedAt
            });
        }

        return Ok(new { agents = result, liveMonitoring = true });
    }
}
