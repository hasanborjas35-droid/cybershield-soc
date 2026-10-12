using CyberShield.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CyberShield.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Threat> Threats => Set<Threat>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertIoc> AlertIocs => Set<AlertIoc>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentTimelineEntry> IncidentTimeline => Set<IncidentTimelineEntry>();
    public DbSet<IncidentNote> IncidentNotes => Set<IncidentNote>();
    public DbSet<AgentRegistration> AgentRegistrations => Set<AgentRegistration>();
    public DbSet<AgentTelemetrySnapshot> AgentTelemetry => Set<AgentTelemetrySnapshot>();
    public DbSet<AgentSecurityEvent> AgentSecurityEvents => Set<AgentSecurityEvent>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---- Users ----
        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(300).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => new { x.Role, x.Status });
            e.HasMany(x => x.Permissions).WithOne(p => p.User).HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserPermission>(e =>
        {
            e.ToTable("UserPermissions");
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.UserId, x.Code }).IsUnique();
        });

        // ---- Devices ----
        b.Entity<Device>(e =>
        {
            e.ToTable("Devices");
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Segment).HasMaxLength(160);
            e.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
            e.Property(x => x.OperatingSystem).HasMaxLength(100);
            e.Property(x => x.AgentVersion).HasMaxLength(40);
            e.Property(x => x.RiskLevel).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.IpAddress);
            e.HasIndex(x => new { x.RiskLevel, x.Status });
        });

        // ---- Threats ----
        b.Entity<Threat>(e =>
        {
            e.ToTable("Threats");
            e.Property(x => x.Category).HasMaxLength(80).IsRequired();
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.TrendPercent).HasPrecision(6, 1);
            e.Property(x => x.SourceIp).HasMaxLength(45);
            e.Property(x => x.SourceAsn).HasMaxLength(80);
            e.Property(x => x.SourceCountry).HasMaxLength(60);
            e.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Category);
            e.HasIndex(x => x.DetectedAt);
        });

        // ---- Alerts ----
        b.Entity<Alert>(e =>
        {
            e.ToTable("Alerts");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Signature).HasMaxLength(200).IsRequired();
            e.Property(x => x.Details).HasMaxLength(2000);
            e.Property(x => x.SourceIp).HasMaxLength(80);
            e.Property(x => x.DestinationTarget).HasMaxLength(160);
            e.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => new { x.Status, x.Severity });
            e.HasIndex(x => x.CreatedAt);

            e.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Threat).WithMany().HasForeignKey(x => x.ThreatId).OnDelete(DeleteBehavior.SetNull);
            // NoAction avoids SQL Server "multiple cascade paths"; controllers null these out explicitly.
            e.HasOne(x => x.Incident).WithMany(i => i.Alerts).HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<AlertIoc>(e =>
        {
            e.ToTable("AlertIocs");
            e.Property(x => x.Type).HasMaxLength(60).IsRequired();
            e.Property(x => x.Value).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Alert).WithMany(a => a.Iocs).HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Incidents ----
        b.Entity<Incident>(e =>
        {
            e.ToTable("Incidents");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Source).HasMaxLength(160);
            e.Property(x => x.AffectedAsset).HasMaxLength(200);
            e.Property(x => x.MitreTechnique).HasMaxLength(120);
            e.Property(x => x.ContainmentStatus).HasMaxLength(120);
            e.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => new { x.Status, x.Severity });
            e.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AffectedDevice).WithMany().HasForeignKey(x => x.AffectedDeviceId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<IncidentTimelineEntry>(e =>
        {
            e.ToTable("IncidentTimeline");
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasOne(x => x.Incident).WithMany(i => i.Timeline).HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<IncidentNote>(e =>
        {
            e.ToTable("IncidentNotes");
            e.Property(x => x.AuthorName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            e.HasOne(x => x.Incident).WithMany(i => i.Notes).HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Agent ----
        b.Entity<AgentRegistration>(e =>
        {
            e.ToTable("AgentRegistrations");
            e.Property(x => x.AgentId).HasMaxLength(36).IsRequired();
            e.Property(x => x.Hostname).HasMaxLength(120).IsRequired();
            e.Property(x => x.OsVersion).HasMaxLength(120);
            e.Property(x => x.AgentVersion).HasMaxLength(40);
            e.HasIndex(x => x.AgentId).IsUnique();
            e.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<AgentTelemetrySnapshot>(e =>
        {
            e.ToTable("AgentTelemetry");
            e.Property(x => x.DefenderSignatureDate).HasMaxLength(30);
            e.Property(x => x.NetworkInterfacesJson).HasMaxLength(4000);
            e.Property(x => x.SecurityEventsJson).HasMaxLength(4000);
            e.Property(x => x.ErrorsJson).HasMaxLength(2000);
            e.HasOne(x => x.AgentRegistration).WithMany().HasForeignKey(x => x.AgentRegistrationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AgentRegistrationId, x.CollectedAt });
        });

        b.Entity<AgentSecurityEvent>(e =>
        {
            e.ToTable("AgentSecurityEvents");
            e.Property(x => x.Source).HasMaxLength(80).IsRequired();
            e.Property(x => x.Category).HasMaxLength(60).IsRequired();
            e.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.AgentRegistration).WithMany().HasForeignKey(x => x.AgentRegistrationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AgentRegistrationId, x.OccurredAt });
            e.HasIndex(x => x.AlertCreated);
        });

        b.Entity<AppSetting>(e =>
        {
            e.ToTable("AppSettings");
            e.Property(x => x.Key).HasMaxLength(100).IsRequired();
            e.Property(x => x.Value).HasMaxLength(2000);
            e.HasIndex(x => x.Key).IsUnique();
        });
    }
}
