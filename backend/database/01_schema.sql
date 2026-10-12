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

