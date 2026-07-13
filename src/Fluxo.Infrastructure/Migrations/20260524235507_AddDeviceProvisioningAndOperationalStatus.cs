using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceProvisioningAndOperationalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devices_WorkspaceId_Identifier",
                table: "devices");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastContactAtUtc",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTelemetryOccurredAtUtc",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastTelemetryPayloadJson",
                table: "devices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTelemetryReceivedAtUtc",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastTelemetrySequence",
                table: "devices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "devices",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "default");

            migrationBuilder.CreateTable(
                name: "device_credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SecretSalt = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_credentials_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_devices_LastContactAtUtc",
                table: "devices",
                column: "LastContactAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_devices_TenantId_WorkspaceId_Identifier",
                table: "devices",
                columns: new[] { "TenantId", "WorkspaceId", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_WorkspaceId_Identifier",
                table: "devices",
                columns: new[] { "WorkspaceId", "Identifier" });

            migrationBuilder.CreateIndex(
                name: "IX_device_credentials_Username",
                table: "device_credentials",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_device_credentials_device_active",
                table: "device_credentials",
                column: "DeviceId",
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.Sql(
                "COMMENT ON TABLE telemetry_ingestion_records IS 'Pilot note: partition by RANGE (\"ReceivedAtUtc\") before production-scale rollout.';");

            migrationBuilder.Sql(
                "COMMENT ON COLUMN telemetry_ingestion_records.\"ReceivedAtUtc\" IS 'Retention anchor and future partition key.';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "COMMENT ON COLUMN telemetry_ingestion_records.\"ReceivedAtUtc\" IS NULL;");

            migrationBuilder.Sql(
                "COMMENT ON TABLE telemetry_ingestion_records IS NULL;");

            migrationBuilder.DropTable(
                name: "device_credentials");

            migrationBuilder.DropIndex(
                name: "IX_devices_LastContactAtUtc",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_TenantId_WorkspaceId_Identifier",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_WorkspaceId_Identifier",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "LastContactAtUtc",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "LastTelemetryOccurredAtUtc",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "LastTelemetryPayloadJson",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "LastTelemetryReceivedAtUtc",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "LastTelemetrySequence",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "devices");

            migrationBuilder.CreateIndex(
                name: "IX_devices_WorkspaceId_Identifier",
                table: "devices",
                columns: new[] { "WorkspaceId", "Identifier" },
                unique: true);
        }
    }
}
