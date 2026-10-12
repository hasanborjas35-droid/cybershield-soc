using System.Diagnostics.Eventing.Reader;
using System.Text.Json;

namespace CyberShield.Agent.Collectors;

/// <summary>
/// Reads relevant Windows Security Event Log entries since the last collection.
/// Monitored event IDs (documented by Microsoft):
///   4625 — Failed logon
///   4624 — Successful logon (interactive/network only)
///   4740 — Account lockout
///   1116 — Defender malware detected
///   1117 — Defender action taken on malware
///   1121 — Defender controlled folder access block
///   7045 — New service installed (potential persistence)
///
/// Requires: "Log on as a service" or the process must be able to read Security log.
/// Standard users may not read the Security log — handled gracefully.
/// This collector is READ-ONLY. It never modifies the event log.
/// </summary>
public class EventLogCollector(ILogger<EventLogCollector> log)
{
    // Configurable event IDs
    private static readonly int[] FailedLogonIds    = [4625];
    private static readonly int[] SuccessLogonIds   = [4624];
    private static readonly int[] LockoutIds        = [4740];
    private static readonly int[] DefenderIds       = [1116, 1117, 1121];
    private static readonly int[] ServiceChangeIds  = [7045];

    public EventLogSnapshot Collect(DateTime since)
    {
        var snap = new EventLogSnapshot { CollectedAt = DateTime.UtcNow, Since = since };

        TryReadSecurityLog(snap, since);
        TryReadDefenderLog(snap, since);
        TryReadSystemLog(snap, since);

        snap.EventsJson = JsonSerializer.Serialize(
            snap.Events.Select(e => new
            {
                e.OccurredAt, e.EventId, e.Source, e.Category, e.Message, Severity = e.Severity.ToString()
            }),
            new JsonSerializerOptions { WriteIndented = false });

        return snap;
    }

    // ── Security Log (4625, 4624, 4740) ─────────────────────────────
    private void TryReadSecurityLog(EventLogSnapshot snap, DateTime since)
    {
        var watchIds = FailedLogonIds.Concat(SuccessLogonIds).Concat(LockoutIds).ToArray();
        try
        {
            var query = BuildQuery("Security", since, watchIds);
            using var reader = new EventLogReader(query);

            EventRecord? record;
            while ((record = reader.ReadEvent()) != null)
            {
                using (record)
                {
                    var id  = record.Id;
                    var msg = SafeFormatMessage(record);

                    if (FailedLogonIds.Contains(id))
                    {
                        snap.FailedLogonCount++;
                        snap.Events.Add(new SecurityEventItem
                        {
                            OccurredAt = record.TimeCreated?.ToUniversalTime() ?? DateTime.UtcNow,
                            EventId    = id,
                            Source     = "Security",
                            Category   = "FailedLogon",
                            Message    = TrimMessage(msg, 300),
                            Severity   = SecurityEventSeverity.Medium
                        });
                    }
                    else if (SuccessLogonIds.Contains(id))
                    {
                        snap.SuccessfulLogonCount++;
                        // Only store interactive/network logons (type 2, 3, 10)
                        if (msg.Contains("Logon Type:\t\t2")  ||
                            msg.Contains("Logon Type:\t\t3")  ||
                            msg.Contains("Logon Type:\t\t10") ||
                            msg.Contains("Logon Type:		2")   ||
                            msg.Contains("Logon Type:		3"))
                        {
                            snap.Events.Add(new SecurityEventItem
                            {
                                OccurredAt = record.TimeCreated?.ToUniversalTime() ?? DateTime.UtcNow,
                                EventId    = id,
                                Source     = "Security",
                                Category   = "SuccessfulLogon",
                                Message    = TrimMessage(msg, 200),
                                Severity   = SecurityEventSeverity.Low
                            });
                        }
                    }
                    else if (LockoutIds.Contains(id))
                    {
                        snap.AccountLockoutCount++;
                        snap.Events.Add(new SecurityEventItem
                        {
                            OccurredAt = record.TimeCreated?.ToUniversalTime() ?? DateTime.UtcNow,
                            EventId    = id,
                            Source     = "Security",
                            Category   = "AccountLockout",
                            Message    = TrimMessage(msg, 300),
                            Severity   = SecurityEventSeverity.High
                        });
                    }
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            log.LogWarning("Security Event Log: access denied. Run agent as Administrator to read security events.");
            snap.Errors.Add("Security log: access denied (run as Administrator for full monitoring)");
        }
        catch (Exception ex)
        {
            log.LogWarning("Security log read error: {Msg}", ex.Message);
            snap.Errors.Add("Security log: " + ex.Message);
        }
    }

    // ── Defender Log (1116, 1117, 1121) ─────────────────────────────
    private void TryReadDefenderLog(EventLogSnapshot snap, DateTime since)
    {
        try
        {
            var query = BuildQuery("Microsoft-Windows-Windows Defender/Operational", since, DefenderIds);
            using var reader = new EventLogReader(query);

            EventRecord? record;
            while ((record = reader.ReadEvent()) != null)
            {
                using (record)
                {
                    snap.DefenderDetectionCount++;
                    var msg = SafeFormatMessage(record);
                    snap.Events.Add(new SecurityEventItem
                    {
                        OccurredAt = record.TimeCreated?.ToUniversalTime() ?? DateTime.UtcNow,
                        EventId    = record.Id,
                        Source     = "Windows Defender",
                        Category   = "DefenderDetection",
                        Message    = TrimMessage(msg, 400),
                        Severity   = SecurityEventSeverity.Critical
                    });
                }
            }
        }
        catch (Exception ex)
        {
            log.LogDebug("Defender log read error: {Msg}", ex.Message);
            snap.Errors.Add("Defender log: " + ex.Message);
        }
    }

    // ── System Log (7045 — new service) ─────────────────────────────
    private void TryReadSystemLog(EventLogSnapshot snap, DateTime since)
    {
        try
        {
            var query = BuildQuery("System", since, ServiceChangeIds);
            using var reader = new EventLogReader(query);

            EventRecord? record;
            while ((record = reader.ReadEvent()) != null)
            {
                using (record)
                {
                    var msg = SafeFormatMessage(record);
                    snap.Events.Add(new SecurityEventItem
                    {
                        OccurredAt = record.TimeCreated?.ToUniversalTime() ?? DateTime.UtcNow,
                        EventId    = record.Id,
                        Source     = "System",
                        Category   = "ServiceInstalled",
                        Message    = TrimMessage(msg, 300),
                        Severity   = SecurityEventSeverity.Medium
                    });
                }
            }
        }
        catch (Exception ex)
        {
            log.LogDebug("System log read error: {Msg}", ex.Message);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────
    private static EventLogQuery BuildQuery(string logName, DateTime since, int[] eventIds)
    {
        var sinceXml  = since.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var idFilter  = string.Join(" or ", eventIds.Select(id => $"EventID={id}"));
        var xpath     = $"*[System[({idFilter}) and TimeCreated[@SystemTime>='{sinceXml}']]]";
        return new EventLogQuery(logName, PathType.LogName, xpath);
    }

    private static string SafeFormatMessage(EventRecord record)
    {
        try   { return record.FormatDescription() ?? $"EventID {record.Id}"; }
        catch { return $"EventID {record.Id} (message unavailable)"; }
    }

    private static string TrimMessage(string msg, int maxLen) =>
        msg.Length <= maxLen ? msg : msg[..maxLen] + "…";
}

public enum SecurityEventSeverity { Low, Medium, High, Critical }

public class SecurityEventItem
{
    public DateTime              OccurredAt { get; set; }
    public int                   EventId    { get; set; }
    public string                Source     { get; set; } = "";
    public string                Category   { get; set; } = "";
    public string                Message    { get; set; } = "";
    public SecurityEventSeverity Severity   { get; set; }
}

public class EventLogSnapshot
{
    public DateTime CollectedAt           { get; set; }
    public DateTime Since                 { get; set; }
    public int      FailedLogonCount      { get; set; }
    public int      SuccessfulLogonCount  { get; set; }
    public int      AccountLockoutCount   { get; set; }
    public int      DefenderDetectionCount{ get; set; }
    public List<SecurityEventItem> Events { get; set; } = new();
    public string?  EventsJson            { get; set; }
    public List<string> Errors            { get; set; } = new();
}
