using System.Net.Http.Json;
using System.Text.Json;
using CyberShield.Agent.Collectors;

namespace CyberShield.Agent;

/// <summary>
/// Handles all HTTP communication with the CyberShield API.
/// Responsibilities:
/// - Register the agent on first run (persists AgentId to disk)
/// - Send heartbeat snapshots every collection cycle
/// - Batch-send security events
/// </summary>
public class TelemetrySender(
    HttpClient http,
    ILogger<TelemetrySender> log)
{
    private static readonly string AgentIdFile =
        Path.Combine(AppContext.BaseDirectory, "agent-id.txt");

    private string? _agentId;
    private bool    _registered;

    public string AgentId => _agentId ??= LoadOrCreateAgentId();

    // ── Registration ─────────────────────────────────────────────────
    public async Task<bool> EnsureRegisteredAsync(SystemHealthSnapshot sys)
    {
        if (_registered) return true;
        try
        {
            var req = new
            {
                agentId      = AgentId,
                hostname     = sys.Hostname,
                osVersion    = sys.OsVersion,
                agentVersion = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"
            };
            var resp = await http.PostAsJsonAsync("/api/agent/register", req);
            if (resp.IsSuccessStatusCode)
            {
                _registered = true;
                log.LogInformation("Agent registered. ID={AgentId}", AgentId);
                return true;
            }
            var body = await resp.Content.ReadAsStringAsync();
            log.LogWarning("Registration failed {Code}: {Body}", resp.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            log.LogWarning("Registration error: {Msg}", ex.Message);
            return false;
        }
    }

    // ── Heartbeat ─────────────────────────────────────────────────────
    public async Task SendHeartbeatAsync(
        SystemHealthSnapshot sys,
        NetworkSnapshot      net,
        SecuritySnapshot     sec,
        EventLogSnapshot     evt)
    {
        var allErrors = sys.Errors
            .Concat(net.Errors)
            .Concat(sec.Errors)
            .Concat(evt.Errors)
            .ToList();

        var payload = new
        {
            agentId                   = AgentId,
            collectedAt               = sys.CollectedAt,
            cpuPercent                = sys.CpuPercent,
            totalMemoryBytes          = sys.TotalMemoryBytes,
            availableMemoryBytes      = sys.AvailableMemoryBytes,
            totalDiskBytes            = sys.TotalDiskBytes,
            availableDiskBytes        = sys.AvailableDiskBytes,
            defenderEnabled           = sec.DefenderEnabled,
            realTimeProtectionEnabled = sec.RealTimeProtectionEnabled,
            firewallEnabled           = sec.FirewallEnabled,
            defenderSignatureDate     = sec.DefenderSignatureDate,
            failedLogonCount          = evt.FailedLogonCount,
            successfulLogonCount      = evt.SuccessfulLogonCount,
            accountLockoutCount       = evt.AccountLockoutCount,
            defenderDetectionCount    = evt.DefenderDetectionCount,
            networkInterfacesJson     = net.InterfacesJson,
            errorsJson                = allErrors.Count > 0
                ? JsonSerializer.Serialize(allErrors)
                : null
        };

        try
        {
            var resp = await http.PostAsJsonAsync("/api/agent/heartbeat", payload);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                log.LogWarning("Heartbeat rejected {Code}: {Body}", resp.StatusCode, body);
            }
            else
            {
                log.LogDebug("Heartbeat sent. CPU={Cpu}% RAM={RamPct}%",
                    sys.CpuPercent,
                    sys.TotalMemoryBytes > 0
                        ? Math.Round(100.0 * (sys.TotalMemoryBytes - sys.AvailableMemoryBytes) / sys.TotalMemoryBytes, 1)
                        : 0);
            }
        }
        catch (Exception ex)
        {
            log.LogWarning("Heartbeat send error: {Msg}", ex.Message);
        }
    }

    // ── Security Events ──────────────────────────────────────────────
    public async Task SendEventsAsync(EventLogSnapshot evt)
    {
        if (evt.Events.Count == 0) return;

        // Only send Medium+ severity events to avoid flooding
        var toSend = evt.Events
            .Where(e => e.Severity >= SecurityEventSeverity.Medium)
            .Select(e => new
            {
                occurredAt = e.OccurredAt,
                eventId    = e.EventId,
                source     = e.Source,
                category   = e.Category,
                message    = e.Message,
                severity   = MapSeverity(e.Severity)
            })
            .ToList();

        if (toSend.Count == 0) return;

        var payload = new { agentId = AgentId, events = toSend };
        try
        {
            var resp = await http.PostAsJsonAsync("/api/agent/events", payload);
            if (!resp.IsSuccessStatusCode)
                log.LogWarning("Events send failed {Code}", resp.StatusCode);
            else
                log.LogInformation("Sent {Count} security event(s) to API.", toSend.Count);
        }
        catch (Exception ex)
        {
            log.LogWarning("Events send error: {Msg}", ex.Message);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────
    private static string MapSeverity(SecurityEventSeverity s) => s switch
    {
        SecurityEventSeverity.Critical => "Critical",
        SecurityEventSeverity.High     => "High",
        SecurityEventSeverity.Medium   => "Medium",
        _                              => "Low"
    };

    private static string LoadOrCreateAgentId()
    {
        if (File.Exists(AgentIdFile))
        {
            var stored = File.ReadAllText(AgentIdFile).Trim();
            if (Guid.TryParse(stored, out _)) return stored;
        }
        var id = Guid.NewGuid().ToString();
        File.WriteAllText(AgentIdFile, id);
        return id;
    }
}
