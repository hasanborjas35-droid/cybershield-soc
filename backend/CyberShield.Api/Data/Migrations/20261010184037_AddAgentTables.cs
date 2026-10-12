using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberShield.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Hostname = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DeviceId = table.Column<int>(type: "int", nullable: true),
                    OsVersion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AgentVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastHeartbeatAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsOnline = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRegistrations_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AgentSecurityEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentRegistrationId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AlertCreated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSecurityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentSecurityEvents_AgentRegistrations_AgentRegistrationId",
                        column: x => x.AgentRegistrationId,
                        principalTable: "AgentRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentTelemetry",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentRegistrationId = table.Column<int>(type: "int", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CpuPercent = table.Column<double>(type: "float", nullable: false),
                    TotalMemoryBytes = table.Column<long>(type: "bigint", nullable: false),
                    AvailableMemoryBytes = table.Column<long>(type: "bigint", nullable: false),
                    TotalDiskBytes = table.Column<long>(type: "bigint", nullable: false),
                    AvailableDiskBytes = table.Column<long>(type: "bigint", nullable: false),
                    DefenderEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RealTimeProtectionEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FirewallEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DefenderSignatureDate = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FailedLogonCount = table.Column<int>(type: "int", nullable: false),
                    SuccessfulLogonCount = table.Column<int>(type: "int", nullable: false),
                    AccountLockoutCount = table.Column<int>(type: "int", nullable: false),
                    DefenderDetectionCount = table.Column<int>(type: "int", nullable: false),
                    NetworkInterfacesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SecurityEventsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ErrorsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTelemetry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentTelemetry_AgentRegistrations_AgentRegistrationId",
                        column: x => x.AgentRegistrationId,
                        principalTable: "AgentRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRegistrations_AgentId",
                table: "AgentRegistrations",
                column: "AgentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentRegistrations_DeviceId",
                table: "AgentRegistrations",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSecurityEvents_AgentRegistrationId_OccurredAt",
                table: "AgentSecurityEvents",
                columns: new[] { "AgentRegistrationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentSecurityEvents_AlertCreated",
                table: "AgentSecurityEvents",
                column: "AlertCreated");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTelemetry_AgentRegistrationId_CollectedAt",
                table: "AgentTelemetry",
                columns: new[] { "AgentRegistrationId", "CollectedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentSecurityEvents");

            migrationBuilder.DropTable(
                name: "AgentTelemetry");

            migrationBuilder.DropTable(
                name: "AgentRegistrations");
        }
    }
}
