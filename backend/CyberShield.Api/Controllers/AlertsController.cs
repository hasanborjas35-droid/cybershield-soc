using CyberShield.Api.Data;
using CyberShield.Api.Dtos;
using CyberShield.Api.Models;
using CyberShield.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberShield.Api.Controllers;

public static class AlertMapping
{
    public static AlertDto ToDto(this Alert a) => new(
        a.Id, a.Code, a.Signature, a.Details, a.Severity, a.Status, a.SourceIp, a.DestinationTarget,
        a.DeviceId, a.ThreatId, a.IncidentId, a.AssignedToUserId, a.AssignedToUser?.FullName, a.CreatedAt, a.UpdatedAt);
}

[ApiController]
[Route("api/alerts")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AlertsController(AppDbContext db) => _db = db;

    private IQueryable<Alert> WithUser => _db.Alerts.Include(a => a.AssignedToUser);

    /// <param name="range">24h | 7d | 30d (alternative to from/to)</param>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AlertDto>>> List(
        [FromQuery] Severity? severity, [FromQuery] AlertStatus? status, [FromQuery] string? search,
        [FromQuery] string? range, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var q = WithUser.AsNoTracking();
        if (severity is not null) q = q.Where(a => a.Severity == severity);
        if (status is not null) q = q.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(a => a.Signature.Contains(search) || a.Code.Contains(search) || a.SourceIp.Contains(search) || a.DestinationTarget.Contains(search));
        var now = DateTime.UtcNow;
        var since = range?.ToLowerInvariant() switch
        {
            "24h" => now.AddHours(-24), "7d" => now.AddDays(-7), "30d" => now.AddDays(-30), _ => from,
        };
        if (since is not null) q = q.Where(a => a.CreatedAt >= since);
        if (to is not null) q = q.Where(a => a.CreatedAt <= to);
        var paged = await q.OrderByDescending(a => a.CreatedAt).ToPagedAsync(page, pageSize);
        return new PagedResult<AlertDto>(paged.Items.Select(a => a.ToDto()).ToList(), paged.Total, paged.Page, paged.PageSize);
    }

    [HttpGet("summary")]
    public async Task<AlertSummaryDto> Summary()
    {
        var a = _db.Alerts.AsNoTracking();
        var open = a.Where(x => x.Status != AlertStatus.Resolved && x.Status != AlertStatus.Dismissed);
        return new AlertSummaryDto(
            await open.CountAsync(),
            await open.CountAsync(x => x.Severity == Severity.Critical),
            await a.CountAsync(x => x.Status == AlertStatus.Investigating),
            await a.CountAsync(x => x.Status == AlertStatus.Blocked));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AlertDetailDto>> Get(int id)
    {
        var a = await WithUser.AsNoTracking().Include(x => x.Iocs).FirstOrDefaultAsync(x => x.Id == id);
        return a is null ? NotFound() : new AlertDetailDto(a.ToDto(), a.Iocs.Select(i => new IocDto(i.Id, i.Type, i.Value)).ToList());
    }

    [HttpPost, Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<AlertDetailDto>> Create(CreateAlertRequest r)
    {
        if (r.DeviceId is not null && !await _db.Devices.AnyAsync(d => d.Id == r.DeviceId)) return BadRequest(new { error = "DeviceId not found." });
        if (r.ThreatId is not null && !await _db.Threats.AnyAsync(t => t.Id == r.ThreatId)) return BadRequest(new { error = "ThreatId not found." });

        var alert = new Alert
        {
            Code = await NextCodeAsync(), Signature = r.Signature.Trim(), Details = r.Details ?? "", Severity = r.Severity,
            Status = AlertStatus.New, SourceIp = r.SourceIp, DestinationTarget = r.DestinationTarget,
            DeviceId = r.DeviceId, ThreatId = r.ThreatId,
            Iocs = (r.Iocs ?? Array.Empty<IocRequest>()).Select(i => new AlertIoc { Type = i.Type, Value = i.Value }).ToList(),
        };
        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = alert.Id },
            new AlertDetailDto(alert.ToDto(), alert.Iocs.Select(i => new IocDto(i.Id, i.Type, i.Value)).ToList()));
    }

    [HttpPatch("{id:int}/status"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<AlertDto>> SetStatus(int id, UpdateStatusRequest<AlertStatus> req)
    {
        var a = await WithUser.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        a.Status = req.Status; a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return a.ToDto();
    }

    [HttpPost("{id:int}/assign"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<AlertDto>> Assign(int id, AssignRequest req)
    {
        var a = await WithUser.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        if (req.UserId is not null)
        {
            var u = await _db.Users.FindAsync(req.UserId);
            if (u is null) return BadRequest(new { error = "UserId not found." });
            a.AssignedToUser = u;
        }
        else { a.AssignedToUser = null; a.AssignedToUserId = null; }
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return a.ToDto();
    }

    /// <summary>Response action: Dismiss.</summary>
    [HttpPost("{id:int}/dismiss"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public Task<ActionResult<AlertDto>> Dismiss(int id) => SetStatus(id, new(AlertStatus.Dismissed));

    /// <summary>Response action: Isolate Host. Quarantines the linked device and marks the alert Blocked.</summary>
    [HttpPost("{id:int}/isolate"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<AlertDto>> IsolateHost(int id)
    {
        var a = await WithUser.Include(x => x.Device).FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        if (a.Device is null) return BadRequest(new { error = "This alert is not linked to a managed device." });
        a.Device.Status = DeviceStatus.Quarantined;
        a.Status = AlertStatus.Blocked; a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return a.ToDto();
    }

    /// <summary>Response action: Escalate. Opens an incident from the alert and links them.</summary>
    [HttpPost("{id:int}/escalate"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<IncidentDto>> Escalate(int id)
    {
        var a = await WithUser.Include(x => x.Device).FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        if (a.IncidentId is not null) return Conflict(new { error = "Alert is already linked to an incident.", incidentId = a.IncidentId });

        var incident = new Incident
        {
            Code = await IncidentsController.NextCodeAsync(_db), Title = a.Signature, Description = a.Details,
            Severity = a.Severity, Status = IncidentStatus.Open, Source = $"Escalated from {a.Code}",
            AffectedAsset = a.Device?.Name ?? a.DestinationTarget, AffectedDeviceId = a.DeviceId,
            Timeline = { new IncidentTimelineEntry { Title = "Incident opened", Description = $"Escalated from alert {a.Code}." } },
        };
        incident.Alerts.Add(a);
        a.Status = AlertStatus.Investigating; a.UpdatedAt = DateTime.UtcNow;
        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();
        return CreatedAtAction("Get", "Incidents", new { id = incident.Id },
            new IncidentDto(incident.Id, incident.Code, incident.Title, incident.Severity, incident.Status, null, null,
                incident.AffectedAsset, incident.CreatedAt, incident.UpdatedAt, null));
    }

    [HttpPost("{id:int}/iocs"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<IocDto>> AddIoc(int id, IocRequest r)
    {
        if (!await _db.Alerts.AnyAsync(a => a.Id == id)) return NotFound();
        var ioc = new AlertIoc { AlertId = id, Type = r.Type, Value = r.Value };
        _db.AlertIocs.Add(ioc);
        await _db.SaveChangesAsync();
        return new IocDto(ioc.Id, ioc.Type, ioc.Value);
    }

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await _db.Alerts.FindAsync(id);
        if (a is null) return NotFound();
        _db.Alerts.Remove(a);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> NextCodeAsync()
    {
        var codes = await _db.Alerts.Select(a => a.Code).ToListAsync();
        var max = codes.Select(c => int.TryParse(c.Replace("ALT-", ""), out var n) ? n : 0).DefaultIfEmpty(9000).Max();
        return $"ALT-{max + 1}";
    }
}
