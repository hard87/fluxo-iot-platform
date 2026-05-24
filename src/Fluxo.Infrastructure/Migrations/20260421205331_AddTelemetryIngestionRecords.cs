using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemetryIngestionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telemetry_ingestion_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Topic = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FirmwareVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: true),
                    Humidity = table.Column<double>(type: "double precision", nullable: true),
                    Battery = table.Column<double>(type: "double precision", nullable: true),
                    Rssi = table.Column<int>(type: "integer", nullable: true),
                    UptimeSec = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_ingestion_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_ingestion_records_DeviceId_OccurredAtUtc",
                table: "telemetry_ingestion_records",
                columns: new[] { "DeviceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_ingestion_records_ReceivedAtUtc",
                table: "telemetry_ingestion_records",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_ingestion_records_TenantId",
                table: "telemetry_ingestion_records",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_ingestion_records_TenantId_WorkspaceId",
                table: "telemetry_ingestion_records",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_ingestion_records_WorkspaceId_DeviceId",
                table: "telemetry_ingestion_records",
                columns: new[] { "WorkspaceId", "DeviceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telemetry_ingestion_records");
        }
    }
}
