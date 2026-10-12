using CyberShield.Api.Data;
using CyberShield.Api.Dtos;
using CyberShield.Api.Models;
using CyberShield.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberShield.Api.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    public DevicesController(AppDbContext db) => _db = db;

    private static DeviceDto ToDto(Device d) => new(
        d.Id, d.Name, d.Segment, d.IpAddress, d.OperatingSystem, d.RiskLevel, d.Status, d.AgentVersion, d.LastSeenAt);

    /// <param name="sort">lastSeen (default) | risk | name</param>
    [HttpGet]
    public async Task<ActionResult<PagedResult<DeviceDto>>> List(
        [FromQuery] Severity? risk, [FromQuery] DeviceStatus? status, [FromQuery] string? search,
        [FromQuery] string sort = "lastSeen", [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var q = _db.Devices.AsNoTracking().AsQueryable();
        if (risk is not null) q = q.Where(d => d.RiskLevel == risk);
        if (status is not null) q = q.Where(d => d.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(d => d.Name.Contains(search) || d.IpAddress.Contains(search) || d.OperatingSystem.Contains(search));
        q = sort.ToLowerInvariant() switch
        {
            "risk" => q.OrderByDescending(d => d.RiskLevel == Severity.Critical ? 3 : d.RiskLevel == Severity.High ? 2 : d.RiskLevel == Severity.Medium ? 1 : 0).ThenBy(d => d.Name),
            "name" => q.OrderBy(d => d.Name),
            _ => q.OrderByDescending(d => d.LastSeenAt),
        };
        var paged = await q.ToPagedAsync(page, pageSize);
        return new PagedResult<DeviceDto>(paged.Items.Select(ToDto).ToList(), paged.Total, paged.Page, paged.PageSize);
    }

    [HttpGet("summary")]
    public async Task<DeviceSummaryDto> Summary()
    {
        var devices = await _db.Devices.AsNoTracking().ToListAsync();
        var total = devices.Count;
        static double Health(Severity s) => s switch { Severity.Critical => 45, Severity.High => 70, Severity.Medium => 88, _ => 99 };
        static string Family(string os) =>
            os.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows Server / Pro" :
            os.Contains("mac", StringComparison.OrdinalIgnoreCase) ? "macOS Apple Silicon" : "Linux (RHEL / Ubuntu / Debian)";
        var dist = devices.GroupBy(d => Family(d.OperatingSystem))
            .Select(g => new NameCount(g.Key, g.Count(), total == 0 ? 0 : Math.Round(100.0 * g.Count() / total, 1)))
            .OrderByDescending(x => x.Count).ToList();
        return new DeviceSummaryDto(
            total,
            devices.Count(d => d.RiskLevel is Severity.Critical or Severity.High),
            devices.Count(d => d.Status == DeviceStatus.Quarantined),
            total == 0 ? 0 : Math.Round(devices.Average(d => Health(d.RiskLevel)), 1),
            dist,
            devices.Count(d => d.AgentVersion != "v4.12.8-RELEASE"));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DeviceDto>> Get(int id)
    {
        var d = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return d is null ? NotFound() : ToDto(d);
    }

    [HttpPost, Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<DeviceDto>> Create(DeviceRequest r)
    {
        if (await _db.Devices.AnyAsync(d => d.Name == r.Name))
            return Conflict(new { error = "A device with this name already exists." });
        var d = new Device
        {
            Name = r.Name.Trim(), Segment = r.Segment ?? "", IpAddress = r.IpAddress.Trim(),
            OperatingSystem = r.OperatingSystem ?? "", RiskLevel = r.RiskLevel, Status = r.Status,
            AgentVersion = r.AgentVersion ?? "v4.12.8-RELEASE",
        };
        _db.Devices.Add(d);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = d.Id }, ToDto(d));
    }

    [HttpPut("{id:int}"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<DeviceDto>> Update(int id, DeviceRequest r)
    {
        var d = await _db.Devices.FindAsync(id);
        if (d is null) return NotFound();
        if (await _db.Devices.AnyAsync(x => x.Name == r.Name && x.Id != id))
            return Conflict(new { error = "A device with this name already exists." });
        d.Name = r.Name.Trim(); d.Segment = r.Segment ?? ""; d.IpAddress = r.IpAddress.Trim();
        d.OperatingSystem = r.OperatingSystem ?? ""; d.RiskLevel = r.RiskLevel; d.Status = r.Status;
        if (r.AgentVersion is not null) d.AgentVersion = r.AgentVersion;
        await _db.SaveChangesAsync();
        return ToDto(d);
    }

    /// <summary>Quarantine Controls → Isolate.</summary>
    [HttpPost("{id:int}/isolate"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<DeviceDto>> Isolate(int id)
    {
        var d = await _db.Devices.FindAsync(id);
        if (d is null) return NotFound();
        d.Status = DeviceStatus.Quarantined;
        await _db.SaveChangesAsync();
        return ToDto(d);
    }

    [HttpPost("{id:int}/release"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<DeviceDto>> Release(int id)
    {
        var d = await _db.Devices.FindAsync(id);
        if (d is null) return NotFound();
        d.Status = DeviceStatus.Online;
        d.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(d);
    }

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var d = await _db.Devices.FindAsync(id);
        if (d is null) return NotFound();
        _db.Devices.Remove(d);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/threats")]
[Authorize]
public class ThreatsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ThreatsController(AppDbContext db) => _db = db;

    private static ThreatDto ToDto(Threat t) => new(
        t.Id, t.Category, t.Name, t.Description, t.Severity, t.EventCount, t.TrendPercent,
        t.SourceIp, t.SourceAsn, t.SourceCountry, t.Status, t.DetectedAt);

    [HttpGet]
    public async Task<ActionResult<PagedResult<ThreatDto>>> List(
        [FromQuery] string? category, [FromQuery] Severity? severity, [FromQuery] ThreatStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var q = _db.Threats.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(t => t.Category == category);
        if (severity is not null) q = q.Where(t => t.Severity == severity);
        if (status is not null) q = q.Where(t => t.Status == status);
        var paged = await q.OrderByDescending(t => t.DetectedAt).ToPagedAsync(page, pageSize);
        return new PagedResult<ThreatDto>(paged.Items.Select(ToDto).ToList(), paged.Total, paged.Page, paged.PageSize);
    }

    /// <summary>Category breakdown, origin ranking and headline numbers for the Threats screen.</summary>
    [HttpGet("summary")]
    public async Task<ThreatSummaryDto> Summary()
    {
        var threats = await _db.Threats.AsNoTracking().ToListAsync();
        var totalEvents = threats.Sum(t => t.EventCount);
        var cats = threats.GroupBy(t => t.Category)
            .Select(g => new ThreatCategoryDto(g.Key, g.Max(x => x.Severity), g.Sum(x => x.EventCount), Math.Round(g.Average(x => x.TrendPercent), 1)))
            .OrderByDescending(c => c.Events).ToList();
        var withAsn = threats.Where(t => !string.IsNullOrEmpty(t.SourceAsn)).ToList();
        var asnTotal = withAsn.Sum(t => t.EventCount);
        var origins = withAsn.GroupBy(t => t.SourceAsn!)
            .Select(g => new
            {
                Asn = g.Key, Events = g.Sum(x => x.EventCount),
                Top = g.OrderByDescending(x => x.EventCount).First().Category,
            })
            .OrderByDescending(x => x.Events)
            .Select((x, i) => new ThreatOriginDto(i + 1, x.Asn, x.Top, x.Events, asnTotal == 0 ? 0 : Math.Round(100.0 * x.Events / asnTotal, 1)))
            .ToList();
        return new ThreatSummaryDto(
            totalEvents,
            threats.Count(t => t.Status == ThreatStatus.Active),
            threats.Count(t => t.Severity == Severity.Critical && t.Status == ThreatStatus.Active),
            cats, origins);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ThreatDto>> Get(int id)
    {
        var t = await _db.Threats.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return t is null ? NotFound() : ToDto(t);
    }

    [HttpPost, Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<ThreatDto>> Create(ThreatRequest r)
    {
        var t = new Threat();
        Apply(t, r);
        _db.Threats.Add(t);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = t.Id }, ToDto(t));
    }

    [HttpPut("{id:int}"), Authorize(Roles = Roles.AdminOrAnalyst)]
    public async Task<ActionResult<ThreatDto>> Update(int id, ThreatRequest r)
    {
        var t = await _db.Threats.FindAsync(id);
        if (t is null) return NotFound();
        Apply(t, r);
        await _db.SaveChangesAsync();
        return ToDto(t);
    }

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await _db.Threats.FindAsync(id);
        if (t is null) return NotFound();
        _db.Threats.Remove(t);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(Threat t, ThreatRequest r)
    {
        t.Category = r.Category.Trim(); t.Name = r.Name.Trim(); t.Description = r.Description ?? "";
        t.Severity = r.Severity; t.EventCount = r.EventCount; t.TrendPercent = r.TrendPercent;
        t.SourceIp = r.SourceIp; t.SourceAsn = r.SourceAsn; t.SourceCountry = r.SourceCountry; t.Status = r.Status;
    }
}
