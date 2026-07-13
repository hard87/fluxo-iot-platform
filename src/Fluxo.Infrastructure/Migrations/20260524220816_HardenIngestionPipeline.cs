using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenIngestionPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telemetry_ingestion_rejections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Topic = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PayloadRaw = table.Column<string>(type: "text", nullable: false),
                    ErrorType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MessageType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_ingestion_rejections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tir_tenant_workspace_device_occurred_at",
                table: "telemetry_ingestion_records",
                columns: new[] { "TenantId", "WorkspaceId", "DeviceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_telemetry_ingestion_records_tenant_workspace_device_sequence",
                table: "telemetry_ingestion_records",
                columns: new[] { "TenantId", "WorkspaceId", "DeviceId", "Sequence" },
                unique: true,
                filter: "\"Sequence\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_ingestion_rejections_ErrorType",
                table: "telemetry_ingestion_rejections",
                column: "ErrorType");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_ingestion_rejections_ReceivedAtUtc",
                table: "telemetry_ingestion_rejections",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_tirj_tenant_workspace_device_received_at",
                table: "telemetry_ingestion_rejections",
                columns: new[] { "TenantId", "WorkspaceId", "DeviceId", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telemetry_ingestion_rejections");

            migrationBuilder.DropIndex(
                name: "IX_tir_tenant_workspace_device_occurred_at",
                table: "telemetry_ingestion_records");

            migrationBuilder.DropIndex(
                name: "UX_telemetry_ingestion_records_tenant_workspace_device_sequence",
                table: "telemetry_ingestion_records");
        }
    }
}
