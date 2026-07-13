using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemetrySchemaV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "metric_definition_discovery_audits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DiscoveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IngestionRecordId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metric_definition_discovery_audits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_metric_definition_discovery_audits_telemetry_ingestion_reco~",
                        column: x => x.IngestionRecordId,
                        principalTable: "telemetry_ingestion_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "metric_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ValueType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SemanticType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CanonicalUnit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ExpectedIntervalSec = table.Column<int>(type: "integer", nullable: true),
                    MinExpectedValue = table.Column<double>(type: "double precision", nullable: true),
                    MaxExpectedValue = table.Column<double>(type: "double precision", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsQueryable = table.Column<bool>(type: "boolean", nullable: false),
                    IsAlertable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metric_definitions", x => x.Id);
                });

            migrationBuilder.Sql("""
CREATE TABLE telemetry_points (
    "Id" uuid NOT NULL, "OccurredAtUtc" timestamptz NOT NULL,
    "TenantId" varchar(120) NOT NULL, "WorkspaceId" uuid NOT NULL, "DeviceId" varchar(160) NOT NULL,
    "MetricDefinitionId" uuid NOT NULL REFERENCES metric_definitions("Id") ON DELETE RESTRICT,
    "NumericValue" double precision NULL, "BooleanValue" boolean NULL, "TextValue" varchar(256) NULL,
    "IngestionRecordId" uuid NOT NULL REFERENCES telemetry_ingestion_records("Id") ON DELETE CASCADE,
    CONSTRAINT "PK_telemetry_points" PRIMARY KEY ("OccurredAtUtc", "Id"),
    CONSTRAINT "CK_telemetry_points_exactly_one_value" CHECK
      ((("NumericValue" IS NOT NULL)::int + ("BooleanValue" IS NOT NULL)::int + ("TextValue" IS NOT NULL)::int) = 1)
) PARTITION BY RANGE ("OccurredAtUtc");
DO $partition$
DECLARE start_month date := (date_trunc('month', CURRENT_DATE) - interval '1 month')::date;
DECLARE month_start date; DECLARE month_end date; DECLARE partition_name text;
BEGIN
  FOR offset_month IN 0..7 LOOP
    month_start := (start_month + make_interval(months => offset_month))::date;
    month_end := (month_start + interval '1 month')::date;
    partition_name := 'telemetry_points_' || to_char(month_start, 'YYYY_MM');
    EXECUTE format('CREATE TABLE IF NOT EXISTS %I PARTITION OF telemetry_points FOR VALUES FROM (%L) TO (%L)', partition_name, month_start, month_end);
  END LOOP;
END $partition$;
""");

            migrationBuilder.CreateIndex(
                name: "IX_metric_definition_discovery_audits_IngestionRecordId",
                table: "metric_definition_discovery_audits",
                column: "IngestionRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_metric_definition_discovery_audits_WorkspaceId_DeviceId_Dis~",
                table: "metric_definition_discovery_audits",
                columns: new[] { "WorkspaceId", "DeviceId", "DiscoveredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_metric_definitions_WorkspaceId_MetricKey",
                table: "metric_definitions",
                columns: new[] { "WorkspaceId", "MetricKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_points_IngestionRecordId",
                table: "telemetry_points",
                column: "IngestionRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_points_MetricDefinitionId",
                table: "telemetry_points",
                column: "MetricDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_points_WorkspaceId_DeviceId_MetricDefinitionId_Oc~",
                table: "telemetry_points",
                columns: new[] { "WorkspaceId", "DeviceId", "MetricDefinitionId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "metric_definition_discovery_audits");

            migrationBuilder.DropTable(
                name: "telemetry_points");

            migrationBuilder.DropTable(
                name: "metric_definitions");
        }
    }
}
