using CyberShield.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CyberShield.Api.Controllers;

/// <summary>
/// Network monitoring data — sourced from the Windows Agent telemetry snapshots.
/// Falls back to empty state if no agent is connected.
/// </summary>
[ApiController]
[Route("api/network")]
[Authorize]
public class NetworkController(AppDbContext db) : ControllerBase
{
    // GET /api/network/summary
    // Returns live network interfaces + traffic + firewall status from latest agent snapshot
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        // Get latest snapshot from any registered agent
        var snap = await db.AgentTelemetry
            .Include(s => s.AgentRegistration)
            .OrderByDescending(s => s.CollectedAt)
            .FirstOrDefaultAsync();

        if (snap == null)
            return Ok(new { liveData = false, message = "No agent connected. Start CyberShield.Agent to see real network data." });

        // Parse network interfaces JSON
        var interfaces = new List<object>();
        if (!string.IsNullOrEmpty(snap.NetworkInterfacesJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<JsonElement>>(snap.NetworkInterfacesJson);
                if (parsed != null)
                {
                    foreach (var iface in parsed)
                    {
                        interfaces.Add(new
                        {
                            name    = iface.TryGetProperty("Name",     out var n) ? n.GetString() : "",
                            status  = iface.TryGetProperty("status",   out var st) ? st.GetString() : "",
                            isUp    = iface.TryGetProperty("isUp",     out var up) && up.GetBoolean(),
                            speedMbps = iface.TryGetProperty("speedMbps", out var sp) ? sp.GetInt64() : 0,
                            ips     = iface.TryGetProperty("ips",      out var ip) ? ip : default,
                            dns     = iface.TryGetProperty("dns",      out var dns) ? dns : default
                        });
                    }
                }
            }
            catch { /* malformed JSON — return empty */ }
        }

        // Get recent network-related security events
        var since24h = DateTime.UtcNow.AddHours(-24);
        var netEvents = await db.AgentSecurityEvents
            .Where(e => e.AgentRegistrationId == snap.AgentRegistrationId
                     && e.OccurredAt >= since24h
                     && (e.Category == "FailedLogon" || e.Category == "AccountLockout"))
            .OrderByDescending(e => e.OccurredAt)
            .Take(20)
            .Select(e => new { e.OccurredAt, e.Category, e.Message, Severity = e.Severity.ToString() })
            .ToListAsync();

        // Active connections count from interfaces
        var activeCount = interfaces.Count(i =>
        {
            var obj = (dynamic)i;
            try { return obj.isUp; } catch { return false; }
        });

        return Ok(new
        {
            liveData        = true,
            hostname        = snap.AgentRegistration?.Hostname ?? "",
            collectedAt     = snap.CollectedAt,
            agentOnline     = (DateTime.UtcNow - snap.CollectedAt).TotalMinutes < 2,
            // Interface data
            interfaces      = interfaces,
            activeInterfaces = activeCount,
            totalInterfaces  = interfaces.Count,
            // Security
            firewallEnabled      = snap.FirewallEnabled,
            defenderEnabled      = snap.DefenderEnabled,
            rtpEnabled           = snap.RealTimeProtectionEnabled,
            // Event counts
            failedLogons24h      = snap.FailedLogonCount,
            accountLockouts24h   = snap.AccountLockoutCount,
            recentNetworkEvents  = netEvents
        });
    }

    // GET /api/network/interfaces
    // Just the interface list
    [HttpGet("interfaces")]
    public async Task<IActionResult> Interfaces()
    {
        var snap = await db.AgentTelemetry
            .OrderByDescending(s => s.CollectedAt)
            .FirstOrDefaultAsync();

        if (snap == null || string.IsNullOrEmpty(snap.NetworkInterfacesJson))
            return Ok(new { liveData = false, interfaces = Array.Empty<object>() });

        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(snap.NetworkInterfacesJson);
            return Ok(new { liveData = true, collectedAt = snap.CollectedAt, interfaces = parsed });
        }
        catch
        {
            return Ok(new { liveData = false, interfaces = Array.Empty<object>() });
        }
    }
}
