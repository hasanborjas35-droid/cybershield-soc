using CyberShield.Agent.Collectors;

namespace CyberShield.Agent;

/// <summary>
/// Main background worker — runs collection cycles on a configurable interval.
/// Default: every 30 seconds.
///
/// Each cycle:
/// 1. Collect system health (CPU, RAM, Disk)
/// 2. Collect network interfaces
/// 3. Collect security status (Defender, Firewall)
/// 4. Collect Windows Event Log entries since last cycle
/// 5. Send heartbeat to API
/// 6. Send batched security events to API
/// </summary>
public class MonitoringWorker(
    SystemHealthCollector sysCollector,
    NetworkCollector      netCollector,
    SecurityCollector     secCollector,
    EventLogCollector     evtCollector,
    TelemetrySender       sender,
    IConfiguration        config,
    ILogger<MonitoringWorker> log)
    : BackgroundService
{
    private DateTime _lastEventSince = DateTime.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var intervalSec = config.GetValue<int>("Agent:IntervalSeconds", 30);
        var interval    = TimeSpan.FromSeconds(Math.Max(10, intervalSec));

        log.LogInformation("CyberShield Agent starting. Collection interval: {Interval}s", interval.TotalSeconds);
        log.LogInformation("API endpoint: {Base}", config["Agent:ApiBaseUrl"]);

        // Prime CPU counter — needs ~1 second before first read
        sysCollector.Initialise();
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        while (!ct.IsCancellationRequested)
        {
            await RunCycleAsync(ct);
            await Task.Delay(interval, ct);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            log.LogDebug("Collection cycle started.");

            // ── Collect ───────────────────────────────────────────────
            var sys = sysCollector.Collect();
            var net = netCollector.Collect();
            var sec = secCollector.Collect();
            var evt = evtCollector.Collect(_lastEventSince);

            // Advance the event window
            _lastEventSince = DateTime.UtcNow;

            // ── Register if needed ────────────────────────────────────
            if (!await sender.EnsureRegisteredAsync(sys))
            {
                log.LogWarning("Agent not yet registered — will retry next cycle.");
                return;
            }

            // ── Log summary to console ────────────────────────────────
            double ramUsedPct = sys.TotalMemoryBytes > 0
                ? Math.Round(100.0 * (sys.TotalMemoryBytes - sys.AvailableMemoryBytes) / sys.TotalMemoryBytes, 1)
                : 0;
            double diskUsedPct = sys.TotalDiskBytes > 0
                ? Math.Round(100.0 * (sys.TotalDiskBytes - sys.AvailableDiskBytes) / sys.TotalDiskBytes, 1)
                : 0;

            log.LogInformation(
                "Host={Host} | CPU={Cpu}% | RAM={Ram}% | Disk={Disk}% | " +
                "Defender={Def} | RTP={Rtp} | FW={Fw} | " +
                "FailedLogons={Fail} | Detections={Det} | NetIfaces={Ifaces}",
                sys.Hostname,
                sys.CpuPercent,
                ramUsedPct,
                diskUsedPct,
                sec.DefenderEnabled           ? "ON" : "OFF",
                sec.RealTimeProtectionEnabled ? "ON" : "OFF",
                sec.FirewallEnabled           ? "ON" : "OFF",
                evt.FailedLogonCount,
                evt.DefenderDetectionCount,
                net.ActiveInterfaceCount);

            // Warn on security issues
            if (!sec.DefenderEnabled)
                log.LogWarning("⚠ Microsoft Defender appears to be DISABLED on this endpoint.");
            if (!sec.FirewallEnabled)
                log.LogWarning("⚠ Windows Firewall appears to be DISABLED on this endpoint.");
            if (evt.AccountLockoutCount > 0)
                log.LogWarning("⚠ {Count} account lockout(s) detected this cycle.", evt.AccountLockoutCount);
            if (evt.DefenderDetectionCount > 0)
                log.LogWarning("⚠ {Count} Defender detection(s) this cycle.", evt.DefenderDetectionCount);

            // ── Send to API ───────────────────────────────────────────
            await sender.SendHeartbeatAsync(sys, net, sec, evt);
            await sender.SendEventsAsync(evt);

            log.LogDebug("Collection cycle complete.");
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled error in collection cycle.");
        }
    }
}
