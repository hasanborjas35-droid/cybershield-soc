using CyberShield.Api.Data;
using CyberShield.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberShield.Api.Controllers;

/// <summary>
/// Platform settings — stored in the AppSettings table.
/// GET  /api/settings        → load all settings
/// PUT  /api/settings        → save all settings (Admin only)
/// </summary>
[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var settings = await db.AppSettings.ToListAsync();
        var dict = settings.ToDictionary(s => s.Key, s => s.Value);
        return Ok(dict);
    }

    [HttpPut]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Save([FromBody] Dictionary<string, string> updates)
    {
        if (updates == null || updates.Count == 0)
            return BadRequest(new { error = "No settings provided." });

        foreach (var (key, value) in updates)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            var existing = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (existing != null)
                existing.Value = value ?? "";
            else
                db.AppSettings.Add(new AppSetting { Key = key, Value = value ?? "" });
        }

        await db.SaveChangesAsync();
        return Ok(new { saved = updates.Count, savedAt = DateTime.UtcNow });
    }
}
