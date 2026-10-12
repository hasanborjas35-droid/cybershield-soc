/* =====================================================================
   CyberShield SOC - full database setup for SQL Server (SSMS)
   ---------------------------------------------------------------------
   1) Creates the database if it does not exist.
   2) Creates all tables, keys and indexes (safe to run more than once).
   3) Inserts the sample data from the Stitch screens (only when Users is empty).

   To use another database name (e.g. CyberNexus) replace "CyberShield"
   in the two lines marked  <-- DATABASE NAME  and run the whole script (F5).
   After running: right-click "Databases" in Object Explorer > Refresh.

   Default sample password for ALL seeded users: ChangeMe!123
   ===================================================================== */
IF DB_ID(N'CyberShield') IS NULL CREATE DATABASE [CyberShield];   -- <-- DATABASE NAME
GO
USE [CyberShield];                                                 -- <-- DATABASE NAME
GO

/* ---------------------------- SCHEMA ---------------------------- */
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE TABLE [Devices] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [Segment] nvarchar(160) NOT NULL,
        [IpAddress] nvarchar(45) NOT NULL,
        [OperatingSystem] nvarchar(100) NOT NULL,
        [RiskLevel] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [AgentVersion] nvarchar(40) NOT NULL,
        [LastSeenAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Devices] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE TABLE [Threats] (
        [Id] int NOT NULL IDENTITY,
        [Category] nvarchar(80) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Severity] nvarchar(20) NOT NULL,
        [EventCount] int NOT NULL,
        [TrendPercent] decimal(6,1) NOT NULL,
        [SourceIp] nvarchar(45) NULL,
        [SourceAsn] nvarchar(80) NULL,
        [SourceCountry] nvarchar(60) NULL,
        [Status] nvarchar(20) NOT NULL,
        [DetectedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Threats] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(120) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [PasswordHash] nvarchar(300) NOT NULL,
        [Role] nvarchar(30) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [MfaEnabled] bit NOT NULL,
        [LastActiveAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE TABLE [Incidents] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(20) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [Severity] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Source] nvarchar(160) NOT NULL,
        [AffectedAsset] nvarchar(200) NOT NULL,
        [AffectedDeviceId] int NULL,
        [MitreTechnique] nvarchar(120) NULL,
        [ContainmentStatus] nvarchar(120) NULL,
        [AssignedToUserId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        CONSTRAINT [PK_Incidents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Incidents_Devices_AffectedDeviceId] FOREIGN KEY ([AffectedDeviceId]) REFERENCES [Devices] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Incidents_Users_AssignedToUserId] FOREIGN KEY ([AssignedToUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE TABLE [UserPermissions] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_UserPermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPermissions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE TABLE [Alerts] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(20) NOT NULL,
        [Signature] nvarchar(200) NOT NULL,
        [Details] nvarchar(2000) NOT NULL,
        [Severity] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [SourceIp] nvarchar(80) NOT NULL,
        [DestinationTarget] nvarchar(160) NOT NULL,
        [DeviceId] int NULL,
        [ThreatId] int NULL,
        [IncidentId] int NULL,
        [AssignedToUserId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Alerts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Alerts_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Alerts_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [Incidents] ([Id]),
        CONSTRAINT [FK_Alerts_Threats_ThreatId] FOREIGN KEY ([ThreatId]) REFERENCES [Threats] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Alerts_Users_AssignedToUserId] FOREIGN KEY ([AssignedToUserId]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE TABLE [IncidentNotes] (
        [Id] int NOT NULL IDENTITY,
        [IncidentId] int NOT NULL,
        [AuthorUserId] int NULL,
        [AuthorName] nvarchar(120) NOT NULL,
        [Body] nvarchar(2000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_IncidentNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IncidentNotes_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [Incidents] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE TABLE [IncidentTimeline] (
        [Id] int NOT NULL IDENTITY,
        [IncidentId] int NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        CONSTRAINT [PK_IncidentTimeline] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IncidentTimeline_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [Incidents] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE TABLE [AlertIocs] (
        [Id] int NOT NULL IDENTITY,
        [AlertId] int NOT NULL,
        [Type] nvarchar(60) NOT NULL,
        [Value] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_AlertIocs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AlertIocs_Alerts_AlertId] FOREIGN KEY ([AlertId]) REFERENCES [Alerts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AlertIocs_AlertId] ON [AlertIocs] ([AlertId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Alerts_AssignedToUserId] ON [Alerts] ([AssignedToUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Alerts_Code] ON [Alerts] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Alerts_CreatedAt] ON [Alerts] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Alerts_DeviceId] ON [Alerts] ([DeviceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Alerts_IncidentId] ON [Alerts] ([IncidentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Alerts_Status_Severity] ON [Alerts] ([Status], [Severity]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Alerts_ThreatId] ON [Alerts] ([ThreatId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Devices_IpAddress] ON [Devices] ([IpAddress]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Devices_Name] ON [Devices] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Devices_RiskLevel_Status] ON [Devices] ([RiskLevel], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_IncidentNotes_IncidentId] ON [IncidentNotes] ([IncidentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_IncidentTimeline_IncidentId] ON [IncidentTimeline] ([IncidentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incidents_AffectedDeviceId] ON [Incidents] ([AffectedDeviceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incidents_AssignedToUserId] ON [Incidents] ([AssignedToUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Incidents_Code] ON [Incidents] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incidents_Status_Severity] ON [Incidents] ([Status], [Severity]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Threats_Category] ON [Threats] ([Category]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Threats_DetectedAt] ON [Threats] ([DetectedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserPermissions_UserId_Code] ON [UserPermissions] ([UserId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_Role_Status] ON [Users] ([Role], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008175203_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008175203_InitialCreate', N'8.0.31');
END;
GO

COMMIT;
GO


GO

/* ------------------------- SAMPLE DATA -------------------------- */
SET XACT_ABORT ON;
IF NOT EXISTS (SELECT 1 FROM [Users])
BEGIN
BEGIN TRAN;
-- Users (6 rows)
SET IDENTITY_INSERT [Users] ON;
INSERT INTO [Users] ([Id], [FullName], [Email], [PasswordHash], [Role], [Status], [MfaEnabled], [LastActiveAt], [CreatedAt]) VALUES (1, N'Marcus Vance', N'm.vance@cybershield.internal', N'v1.210000.0RiTFPljfqk1qGOlhTIzJg==.v8L1d8h+LtHdLg6R3nEZXeBr8/Xtw/XKxM1oVvOB74I=', N'Administrator', N'Active', 1, '2026-10-08T17:58:28.104660', '2026-10-08T17:58:28.161979');
INSERT INTO [Users] ([Id], [FullName], [Email], [PasswordHash], [Role], [Status], [MfaEnabled], [LastActiveAt], [CreatedAt]) VALUES (2, N'Elena Rostova', N'e.rostova@cybershield.internal', N'v1.210000.0RiTFPljfqk1qGOlhTIzJg==.v8L1d8h+LtHdLg6R3nEZXeBr8/Xtw/XKxM1oVvOB74I=', N'SecurityAnalyst', N'Active', 1, '2026-10-08T17:54:28.104660', '2026-10-08T17:58:28.162390');
INSERT INTO [Users] ([Id], [FullName], [Email], [PasswordHash], [Role], [Status], [MfaEnabled], [LastActiveAt], [CreatedAt]) VALUES (3, N'Devon Chen', N'd.chen@cybershield.internal', N'v1.210000.0RiTFPljfqk1qGOlhTIzJg==.v8L1d8h+LtHdLg6R3nEZXeBr8/Xtw/XKxM1oVvOB74I=', N'SecurityAnalyst', N'Suspended', 1, '2026-10-05T17:58:28.104660', '2026-10-08T17:58:28.162418');
INSERT INTO [Users] ([Id], [FullName], [Email], [PasswordHash], [Role], [Status], [MfaEnabled], [LastActiveAt], [CreatedAt]) VALUES (4, N'Sarah Jenkins', N's.jenkins@cybershield.internal', N'v1.210000.0RiTFPljfqk1qGOlhTIzJg==.v8L1d8h+LtHdLg6R3nEZXeBr8/Xtw/XKxM1oVvOB74I=', N'Viewer', N'Pending', 1, NULL, '2026-10-08T17:58:28.162419');
INSERT INTO [Users] ([Id], [FullName], [Email], [PasswordHash], [Role], [Status], [MfaEnabled], [LastActiveAt], [CreatedAt]) VALUES (5, N'Sarah Chen', N's.chen@cybershield.internal', N'v1.210000.0RiTFPljfqk1qGOlhTIzJg==.v8L1d8h+LtHdLg6R3nEZXeBr8/Xtw/XKxM1oVvOB74I=', N'SecurityAnalyst', N'Active', 1, '2026-10-08T17:49:28.104660', '2026-10-08T17:58:28.162420');
INSERT INTO [Users] ([Id], [FullName], [Email], [PasswordHash], [Role], [Status], [MfaEnabled], [LastActiveAt], [CreatedAt]) VALUES (6, N'Alex Mercer', N'a.mercer@cybershield.internal', N'v1.210000.0RiTFPljfqk1qGOlhTIzJg==.v8L1d8h+LtHdLg6R3nEZXeBr8/Xtw/XKxM1oVvOB74I=', N'SecurityAnalyst', N'Active', 1, '2026-10-08T17:43:28.104660', '2026-10-08T17:58:28.162431');
SET IDENTITY_INSERT [Users] OFF;

-- UserPermissions (12 rows)
SET IDENTITY_INSERT [UserPermissions] ON;
INSERT INTO [UserPermissions] ([Id], [UserId], [Code]) VALUES (1, 1, N'IAM:ALL');
INSERT INTO [UserPermissions] ([Id], [UserId], [Code]) VALUES (2, 1, N'NET:CTL');
INSERT INTO [UserPermissions] ([Id], [UserId], [Code]) VALUES (3, 1, N'SIEM:ALL');
INSERT INTO [UserPermissions] ([Id], [UserId], [Code]) VALUES (4, 1, N'INC:ALL');
INSERT INTO [UserPermissions] ([Id], [UserId], [Code]) VALUES (5, 2, N'SIEM:RD');
INSERT INTO [UserPermissions] ([Id], [UserId], [Code]) VALUES (6, 2, N'INC:WR');
INSERT INTO [UserPermissions] ([Id], [UserId], [Code]) VALUES (7, 3, N'REVOKED');
INSERT INTO [UserPermissions] ([Id], [UserId], [Code]) VALUES (8, 4, N'AUDIT:RD');
INSERT INTO [UserPermissions] ([Id], [UserId], [Code]) VALUES (9, 5, N'SIEM:RD');
INSERT INTO [UserPermissions] ([Id], [UserId], [Code]) VALUES (10, 5, N'INC:WR');
INSERT INTO [UserPermissions] ([Id], [UserId], [Code]) VALUES (11, 6, N'SIEM:RD');
INSERT INTO [UserPermissions] ([Id], [UserId], [Code]) VALUES (12, 6, N'INC:WR');
SET IDENTITY_INSERT [UserPermissions] OFF;

-- Devices (10 rows)
SET IDENTITY_INSERT [Devices] ON;
INSERT INTO [Devices] ([Id], [Name], [Segment], [IpAddress], [OperatingSystem], [RiskLevel], [Status], [AgentVersion], [LastSeenAt], [CreatedAt]) VALUES (1, N'CORP-DB-MASTER01', N'Finance Cluster Node', N'10.142.50.12', N'RHEL 9.2 Enterprise', N'Critical', N'Online', N'v4.12.8-RELEASE', '2026-10-08T17:58:28.104660', '2026-10-08T17:58:28.263809');
INSERT INTO [Devices] ([Id], [Name], [Segment], [IpAddress], [OperatingSystem], [RiskLevel], [Status], [AgentVersion], [LastSeenAt], [CreatedAt]) VALUES (2, N'EXEC-MBP-ALEX', N'Executive Leadership', N'10.142.12.88', N'macOS Sonoma 14.2', N'Low', N'Online', N'v4.12.8-RELEASE', '2026-10-08T17:56:28.104660', '2026-10-08T17:58:28.263905');
INSERT INTO [Devices] ([Id], [Name], [Segment], [IpAddress], [OperatingSystem], [RiskLevel], [Status], [AgentVersion], [LastSeenAt], [CreatedAt]) VALUES (3, N'KUBE-WORKER-09', N'US-East Kubernetes Node', N'10.142.88.104', N'Ubuntu 22.04 LTS', N'High', N'Online', N'v4.12.8-RELEASE', '2026-10-08T17:58:28.104660', '2026-10-08T17:58:28.263905');
INSERT INTO [Devices] ([Id], [Name], [Segment], [IpAddress], [OperatingSystem], [RiskLevel], [Status], [AgentVersion], [LastSeenAt], [CreatedAt]) VALUES (4, N'DEV-LAPTOP-SARAH', N'Engineering Remote', N'10.142.19.4', N'Windows 11 Pro', N'Medium', N'Offline', N'v4.11.2-RELEASE', '2026-10-08T13:58:28.104660', '2026-10-08T17:58:28.263906');
INSERT INTO [Devices] ([Id], [Name], [Segment], [IpAddress], [OperatingSystem], [RiskLevel], [Status], [AgentVersion], [LastSeenAt], [CreatedAt]) VALUES (5, N'API-GATEWAY-PROD-02', N'Edge Proxy Layer', N'10.142.4.12', N'Debian 12 Bookworm', N'Low', N'Online', N'v4.12.8-RELEASE', '2026-10-08T17:57:28.104660', '2026-10-08T17:58:28.263906');
INSERT INTO [Devices] ([Id], [Name], [Segment], [IpAddress], [OperatingSystem], [RiskLevel], [Status], [AgentVersion], [LastSeenAt], [CreatedAt]) VALUES (6, N'WORKSTATION-89', N'Finance Workstation', N'10.142.31.89', N'Windows 11 Pro', N'Low', N'Online', N'v4.12.8-RELEASE', '2026-10-08T15:58:28.104660', '2026-10-08T17:58:28.263906');
INSERT INTO [Devices] ([Id], [Name], [Segment], [IpAddress], [OperatingSystem], [RiskLevel], [Status], [AgentVersion], [LastSeenAt], [CreatedAt]) VALUES (7, N'DC02', N'Core Domain Controller', N'10.100.4.12', N'Windows Server 2022', N'Critical', N'Online', N'v4.12.8-RELEASE', '2026-10-08T17:55:28.104660', '2026-10-08T17:58:28.263906');
INSERT INTO [Devices] ([Id], [Name], [Segment], [IpAddress], [OperatingSystem], [RiskLevel], [Status], [AgentVersion], [LastSeenAt], [CreatedAt]) VALUES (8, N'WS-FIN-09', N'Finance Workstation', N'10.0.12.45', N'Windows 11 Pro', N'High', N'Online', N'v4.12.8-RELEASE', '2026-10-08T17:57:28.104660', '2026-10-08T17:58:28.263907');
INSERT INTO [Devices] ([Id], [Name], [Segment], [IpAddress], [OperatingSystem], [RiskLevel], [Status], [AgentVersion], [LastSeenAt], [CreatedAt]) VALUES (9, N'FS-NAS-01', N'File Services', N'10.0.8.99', N'Ubuntu 22.04 LTS', N'Critical', N'Quarantined', N'v4.12.8-RELEASE', '2026-10-08T17:13:28.104660', '2026-10-08T17:58:28.263907');
INSERT INTO [Devices] ([Id], [Name], [Segment], [IpAddress], [OperatingSystem], [RiskLevel], [Status], [AgentVersion], [LastSeenAt], [CreatedAt]) VALUES (10, N'EXCH-02', N'Mail Services', N'10.0.4.18', N'Windows Server 2022', N'High', N'Online', N'v4.11.2-RELEASE', '2026-10-08T17:53:28.104660', '2026-10-08T17:58:28.263907');
SET IDENTITY_INSERT [Devices] OFF;

-- Threats (6 rows)
SET IDENTITY_INSERT [Threats] ON;
INSERT INTO [Threats] ([Id], [Category], [Name], [Description], [Severity], [EventCount], [TrendPercent], [SourceIp], [SourceAsn], [SourceCountry], [Status], [DetectedAt]) VALUES (1, N'Malware & Ransomware', N'Ransomware Payload', N'Payload signatures & polymorphic strains detected.', N'Critical', 428, 8.2, N'185.220.101.5', N'AS24940 (Hetzner Online)', N'Germany', N'Active', '2026-10-08T17:58:16.104660');
INSERT INTO [Threats] ([Id], [Category], [Name], [Description], [Severity], [EventCount], [TrendPercent], [SourceIp], [SourceAsn], [SourceCountry], [Status], [DetectedAt]) VALUES (2, N'Phishing Campaigns', N'Credential Harvester Domains', N'Spear-phishing domains & credential harvesters.', N'High', 312, 4.1, N'45.33.22.156', N'AS14061 (DigitalOcean)', N'United States', N'Active', '2026-10-08T17:38:28.104660');
INSERT INTO [Threats] ([Id], [Category], [Name], [Description], [Severity], [EventCount], [TrendPercent], [SourceIp], [SourceAsn], [SourceCountry], [Status], [DetectedAt]) VALUES (3, N'Brute Force', N'Brute Force Attempt', N'Credential stuffing & automated SSH hammering.', N'Medium', 245, -2.5, N'45.33.32.156', N'AS12389 (Rostelecom)', N'Russia', N'Active', '2026-10-08T17:57:43.104660');
INSERT INTO [Threats] ([Id], [Category], [Name], [Description], [Severity], [EventCount], [TrendPercent], [SourceIp], [SourceAsn], [SourceCountry], [Status], [DetectedAt]) VALUES (4, N'DDoS Flooding', N'Volumetric Layer 7 / SYN Flood', N'Volumetric Layer 7 & SYN flood attacks intercepted.', N'Critical', 190, 15.0, N'203.0.113.77', N'AS4837 (China Unicom)', N'China', N'Active', '2026-10-08T17:50:28.104660');
INSERT INTO [Threats] ([Id], [Category], [Name], [Description], [Severity], [EventCount], [TrendPercent], [SourceIp], [SourceAsn], [SourceCountry], [Status], [DetectedAt]) VALUES (5, N'Suspicious Logins', N'Impossible Travel', N'Impossible travel & anomalous session tokens.', N'Medium', 154, -1.2, N'198.51.100.14', NULL, NULL, N'Active', '2026-10-08T16:58:28.104660');
INSERT INTO [Threats] ([Id], [Category], [Name], [Description], [Severity], [EventCount], [TrendPercent], [SourceIp], [SourceAsn], [SourceCountry], [Status], [DetectedAt]) VALUES (6, N'Data Exfiltration', N'C2 Beaconing', N'Unusual outbound data spikes & C2 beaconing.', N'Critical', 99, 5.8, N'185.220.101.5', N'AS24940 (Hetzner Online)', N'Germany', N'Active', '2026-10-08T17:53:28.104660');
SET IDENTITY_INSERT [Threats] OFF;

-- Incidents (6 rows)
SET IDENTITY_INSERT [Incidents] ON;
INSERT INTO [Incidents] ([Id], [Code], [Title], [Description], [Severity], [Status], [Source], [AffectedAsset], [AffectedDeviceId], [MitreTechnique], [ContainmentStatus], [AssignedToUserId], [CreatedAt], [UpdatedAt], [ResolvedAt]) VALUES (1, N'INC-8921', N'Unauthorized RDP Brute-Force Wave', N'Sustained RDP authentication failures from a rotating IP pool targeting the jump-host subnet.', N'Critical', N'Open', N'Firewall / Windows Security Log', N'JUMPHOST-RDP-01', NULL, NULL, NULL, NULL, '2026-10-08T17:46:28.104660', '2026-10-08T17:52:28.104660', NULL);
INSERT INTO [Incidents] ([Id], [Code], [Title], [Description], [Severity], [Status], [Source], [AffectedAsset], [AffectedDeviceId], [MitreTechnique], [ContainmentStatus], [AssignedToUserId], [CreatedAt], [UpdatedAt], [ResolvedAt]) VALUES (2, N'INC-8920', N'Suspicious Outbound DNS Tunneling', N'High-entropy TXT queries to a newly registered domain from an internal resolver.', N'High', N'Open', N'DNS Telemetry', N'DNS-RESOLVER-02', NULL, NULL, NULL, NULL, '2026-10-08T17:30:28.104660', '2026-10-08T17:44:28.104660', NULL);
INSERT INTO [Incidents] ([Id], [Code], [Title], [Description], [Severity], [Status], [Source], [AffectedAsset], [AffectedDeviceId], [MitreTechnique], [ContainmentStatus], [AssignedToUserId], [CreatedAt], [UpdatedAt], [ResolvedAt]) VALUES (3, N'INC-8915', N'Privilege Escalation on Core DC02', N'An anomalous process execution was detected spawning an interactive cmd.exe shell from an unquoted service path vulnerability on domain controller DC02 (10.100.4.12). Lateral movement indicators point to credential dumping activity via Mimikatz in memory.', N'Critical', N'Investigating', N'EDR Sensor / Windows-Sysmon', N'DC02.corp.cybershield.internal', 7, N'T1574.009 - Unquoted Path', N'Pending Network Quarantine', 5, '2026-10-08T16:58:28.104660', '2026-10-08T17:28:28.104660', NULL);
INSERT INTO [Incidents] ([Id], [Code], [Title], [Description], [Severity], [Status], [Source], [AffectedAsset], [AffectedDeviceId], [MitreTechnique], [ContainmentStatus], [AssignedToUserId], [CreatedAt], [UpdatedAt], [ResolvedAt]) VALUES (4, N'INC-8912', N'Malware Beacon Detected: APT29 Sig', N'Beaconing pattern matching APT29 signature set.', N'High', N'Investigating', N'EDR Sensor', N'EXCH-02', 10, NULL, NULL, 6, '2026-10-08T15:58:28.104660', '2026-10-08T16:58:28.104660', NULL);
INSERT INTO [Incidents] ([Id], [Code], [Title], [Description], [Severity], [Status], [Source], [AffectedAsset], [AffectedDeviceId], [MitreTechnique], [ContainmentStatus], [AssignedToUserId], [CreatedAt], [UpdatedAt], [ResolvedAt]) VALUES (5, N'INC-8908', N'Phishing Credential Harvester Sinkhole', N'Harvester domain sinkholed; affected mailboxes under review.', N'Medium', N'Contained', N'Email Security Gateway', N'Mail Gateway', NULL, NULL, NULL, 2, '2026-10-08T13:58:28.104660', '2026-10-08T15:58:28.104660', NULL);
INSERT INTO [Incidents] ([Id], [Code], [Title], [Description], [Severity], [Status], [Source], [AffectedAsset], [AffectedDeviceId], [MitreTechnique], [ContainmentStatus], [AssignedToUserId], [CreatedAt], [UpdatedAt], [ResolvedAt]) VALUES (6, N'INC-8901', N'SQL Injection Attempt on API Gateway', N'Blocked SQLi payloads against /api/v2/search; WAF rule tightened.', N'Low', N'Resolved', N'WAF', N'API-GATEWAY-PROD-02', 5, NULL, NULL, 1, '2026-10-08T11:58:28.104660', '2026-10-08T14:58:28.104660', '2026-10-08T14:58:28.104660');
SET IDENTITY_INSERT [Incidents] OFF;

-- IncidentTimeline (3 rows)
SET IDENTITY_INSERT [IncidentTimeline] ON;
INSERT INTO [IncidentTimeline] ([Id], [IncidentId], [OccurredAt], [Title], [Description]) VALUES (1, 3, '2026-10-08T16:58:28.104660', N'EDR Alert Triggered', N'Sysmon event ID 1 recorded anomalous process spawning from unquoted path.');
INSERT INTO [IncidentTimeline] ([Id], [IncidentId], [OccurredAt], [Title], [Description]) VALUES (2, 3, '2026-10-08T17:02:28.104660', N'Analyst Assigned', N'Sarah Chen claimed INC-8915 for tier-2 forensic review.');
INSERT INTO [IncidentTimeline] ([Id], [IncidentId], [OccurredAt], [Title], [Description]) VALUES (3, 3, '2026-10-08T17:10:28.104660', N'YARA Rule Match Verified', N'Memory signature confirmed Mimikatz payload injected into LSASS.');
SET IDENTITY_INSERT [IncidentTimeline] OFF;

-- IncidentNotes (2 rows)
SET IDENTITY_INSERT [IncidentNotes] ON;
INSERT INTO [IncidentNotes] ([Id], [IncidentId], [AuthorUserId], [AuthorName], [Body], [CreatedAt]) VALUES (1, 3, 5, N'Sarah Chen', N'Verified administrative accounts active on DC02. Initiating token revocation now.', '2026-10-08T17:13:28.104660');
INSERT INTO [IncidentNotes] ([Id], [IncidentId], [AuthorUserId], [AuthorName], [Body], [CreatedAt]) VALUES (2, 3, 6, N'Alex Mercer', N'Firewall rules updated to block C2 IP 185.220.101.5. Good catch team.', '2026-10-08T17:19:28.104660');
SET IDENTITY_INSERT [IncidentNotes] OFF;

-- Alerts (9 rows)
SET IDENTITY_INSERT [Alerts] ON;
INSERT INTO [Alerts] ([Id], [Code], [Signature], [Details], [Severity], [Status], [SourceIp], [DestinationTarget], [DeviceId], [ThreatId], [IncidentId], [AssignedToUserId], [CreatedAt], [UpdatedAt]) VALUES (1, N'ALT-9821', N'Cobalt Strike Beacon C2 Traffic', N'EDR sensor detected outbound HTTPS beaconing to known malicious infrastructure utilizing randomized jitter and HTTP GET request patterns signature-matched to Cobalt Strike default malleable C2 profiles.', N'Critical', N'New', N'192.168.4.112', N'185.220.101.5 (External)', NULL, 6, NULL, NULL, '2026-10-08T17:58:28.104660', NULL);
INSERT INTO [Alerts] ([Id], [Code], [Signature], [Details], [Severity], [Status], [SourceIp], [DestinationTarget], [DeviceId], [ThreatId], [IncidentId], [AssignedToUserId], [CreatedAt], [UpdatedAt]) VALUES (2, N'ALT-9820', N'Suspicious PowerShell Execution (Encoded)', N'Proc: powershell.exe -enc JABh...', N'High', N'Investigating', N'10.0.12.45 (WS-FIN-09)', N'Internal DC-01', 8, NULL, 3, NULL, '2026-10-08T17:46:28.104660', NULL);
INSERT INTO [Alerts] ([Id], [Code], [Signature], [Details], [Severity], [Status], [SourceIp], [DestinationTarget], [DeviceId], [ThreatId], [IncidentId], [AssignedToUserId], [CreatedAt], [UpdatedAt]) VALUES (3, N'ALT-9819', N'Ransomware Behavior: Mass File Encryption', N'Extension rename storm detected (.locked)', N'Critical', N'Blocked', N'10.0.8.99 (FS-NAS-01)', N'Local Volumes', 9, 1, NULL, NULL, '2026-10-08T17:13:28.104660', NULL);
INSERT INTO [Alerts] ([Id], [Code], [Signature], [Details], [Severity], [Status], [SourceIp], [DestinationTarget], [DeviceId], [ThreatId], [IncidentId], [AssignedToUserId], [CreatedAt], [UpdatedAt]) VALUES (4, N'ALT-9818', N'Brute Force Attack (SSH Multiple Failures)', N'142 failed attempts in 60s', N'Medium', N'Resolved', N'45.33.32.156 (External)', N'10.0.1.5 (Bastion Host)', NULL, 3, NULL, NULL, '2026-10-08T15:58:28.104660', NULL);
INSERT INTO [Alerts] ([Id], [Code], [Signature], [Details], [Severity], [Status], [SourceIp], [DestinationTarget], [DeviceId], [ThreatId], [IncidentId], [AssignedToUserId], [CreatedAt], [UpdatedAt]) VALUES (5, N'ALT-9817', N'Unauthorized Data Exfiltration Attempt', N'Outbound transfer > 4.2 GB via HTTPS', N'High', N'Investigating', N'10.0.4.18 (EXCH-02)', N'198.51.100.42 (Mega.nz)', 10, 6, 4, NULL, '2026-10-08T14:58:28.104660', NULL);
INSERT INTO [Alerts] ([Id], [Code], [Signature], [Details], [Severity], [Status], [SourceIp], [DestinationTarget], [DeviceId], [ThreatId], [IncidentId], [AssignedToUserId], [CreatedAt], [UpdatedAt]) VALUES (6, N'ALT-9816', N'Inbound Exploit Attempt: Prod Gateway', N'Known CVE exploit signature on prod-gateway-us-east', N'Critical', N'Investigating', N'185.220.101.5', N'prod-gateway-us-east', 5, 1, NULL, NULL, '2026-10-08T17:56:28.104660', NULL);
INSERT INTO [Alerts] ([Id], [Code], [Signature], [Details], [Severity], [Status], [SourceIp], [DestinationTarget], [DeviceId], [ThreatId], [IncidentId], [AssignedToUserId], [CreatedAt], [UpdatedAt]) VALUES (7, N'ALT-9815', N'Auth Service Credential Stuffing', N'Mitigation active on auth-service-node-4', N'High', N'Investigating', N'45.33.22.156', N'auth-service-node-4', NULL, 3, NULL, NULL, '2026-10-08T17:44:28.104660', NULL);
INSERT INTO [Alerts] ([Id], [Code], [Signature], [Details], [Severity], [Status], [SourceIp], [DestinationTarget], [DeviceId], [ThreatId], [IncidentId], [AssignedToUserId], [CreatedAt], [UpdatedAt]) VALUES (8, N'ALT-9814', N'Internal Workstation Anomaly', N'Unusual lateral SMB enumeration', N'Medium', N'Blocked', N'192.168.1.104', N'internal-workstation-9', 6, NULL, NULL, NULL, '2026-10-08T17:17:28.104660', NULL);
INSERT INTO [Alerts] ([Id], [Code], [Signature], [Details], [Severity], [Status], [SourceIp], [DestinationTarget], [DeviceId], [ThreatId], [IncidentId], [AssignedToUserId], [CreatedAt], [UpdatedAt]) VALUES (9, N'ALT-9813', N'Replica Lag Warning', N'Replication lag threshold exceeded', N'Low', N'Resolved', N'10.0.4.12', N'db-cluster-replica-2', NULL, NULL, NULL, NULL, '2026-10-08T15:58:28.104660', NULL);
SET IDENTITY_INSERT [Alerts] OFF;

-- AlertIocs (3 rows)
SET IDENTITY_INSERT [AlertIocs] ON;
INSERT INTO [AlertIocs] ([Id], [AlertId], [Type], [Value]) VALUES (1, 1, N'SHA256 File Hash', N'4e907621c8901b2a92c019283fbc81fd2a38910b');
INSERT INTO [AlertIocs] ([Id], [AlertId], [Type], [Value]) VALUES (2, 1, N'Destination IP / Port', N'185.220.101.5:443 (Tor Exit Node)');
INSERT INTO [AlertIocs] ([Id], [AlertId], [Type], [Value]) VALUES (3, 1, N'Malicious Process Name', N'svchost_update.exe (PID: 4912)');
SET IDENTITY_INSERT [AlertIocs] OFF;
COMMIT;
PRINT 'CyberShield sample data inserted.';
END
ELSE PRINT 'Users table is not empty - sample data skipped.';
GO

SELECT 'Users' AS [Table], COUNT(*) AS [Rows] FROM [Users]
UNION ALL SELECT 'Devices', COUNT(*) FROM [Devices]
UNION ALL SELECT 'Threats', COUNT(*) FROM [Threats]
UNION ALL SELECT 'Alerts', COUNT(*) FROM [Alerts]
UNION ALL SELECT 'Incidents', COUNT(*) FROM [Incidents];
GO
