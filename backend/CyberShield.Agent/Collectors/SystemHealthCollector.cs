using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace CyberShield.Agent.Collectors;

/// <summary>
/// Collects real system health metrics:
/// CPU utilisation, RAM, disk capacity, hostname, and OS version.
/// Uses PerformanceCounter + WMI (Windows only).
/// </summary>
public class SystemHealthCollector(ILogger<SystemHealthCollector> log)
{
    private PerformanceCounter? _cpuCounter;

    public void Initialise()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // first call always returns 0 — prime it
        }
        catch (Exception ex)
        {
            log.LogWarning("CPU counter unavailable: {Msg}", ex.Message);
        }
    }

    public SystemHealthSnapshot Collect()
    {
        var snapshot = new SystemHealthSnapshot
        {
            Hostname     = Environment.MachineName,
            OsVersion    = GetOsVersion(),
            CollectedAt  = DateTime.UtcNow
        };

        // ── CPU ──────────────────────────────────────────────────────
        try
        {
            if (_cpuCounter != null)
            {
                snapshot.CpuPercent = Math.Round(_cpuCounter.NextValue(), 1);
            }
        }
        catch (Exception ex) { log.LogWarning("CPU read error: {Msg}", ex.Message); }

        // ── Memory ───────────────────────────────────────────────────
        try
        {
            using var query = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in query.Get())
            {
                snapshot.TotalMemoryBytes     = Convert.ToInt64(obj["TotalVisibleMemorySize"]) * 1024L;
                snapshot.AvailableMemoryBytes = Convert.ToInt64(obj["FreePhysicalMemory"])      * 1024L;
                break;
            }
        }
        catch (Exception ex) { log.LogWarning("RAM read error: {Msg}", ex.Message); }

        // ── Disk (C: drive) ──────────────────────────────────────────
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d =>
                d.IsReady && d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase));
            if (drive != null)
            {
                snapshot.TotalDiskBytes     = drive.TotalSize;
                snapshot.AvailableDiskBytes = drive.AvailableFreeSpace;
            }
        }
        catch (Exception ex) { log.LogWarning("Disk read error: {Msg}", ex.Message); }

        return snapshot;
    }

    private static string GetOsVersion()
    {
        try
        {
            using var query = new ManagementObjectSearcher("SELECT Caption, BuildNumber FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in query.Get())
                return $"{obj["Caption"]} (Build {obj["BuildNumber"]})";
        }
        catch { /* fallback */ }
        return RuntimeInformation.OSDescription;
    }
}

public class SystemHealthSnapshot
{
    public string Hostname            { get; set; } = "";
    public string OsVersion           { get; set; } = "";
    public double CpuPercent          { get; set; }
    public long   TotalMemoryBytes    { get; set; }
    public long   AvailableMemoryBytes{ get; set; }
    public long   TotalDiskBytes      { get; set; }
    public long   AvailableDiskBytes  { get; set; }
    public DateTime CollectedAt       { get; set; }
    public List<string> Errors        { get; set; } = new();
}
