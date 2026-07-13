using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemetryRejectionReprocessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReprocessAttemptAtUtc",
                table: "telemetry_ingestion_rejections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReprocessAttempts",
                table: "telemetry_ingestion_rejections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Reprocessed",
                table: "telemetry_ingestion_rejections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRejectionId",
                table: "telemetry_ingestion_rejections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tirj_reprocess_eligibility",
                table: "telemetry_ingestion_rejections",
                columns: new[] { "SourceRejectionId", "Reprocessed", "ErrorType", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tirj_reprocess_eligibility",
                table: "telemetry_ingestion_rejections");

            migrationBuilder.DropColumn(
                name: "LastReprocessAttemptAtUtc",
                table: "telemetry_ingestion_rejections");

            migrationBuilder.DropColumn(
                name: "ReprocessAttempts",
                table: "telemetry_ingestion_rejections");

            migrationBuilder.DropColumn(
                name: "Reprocessed",
                table: "telemetry_ingestion_rejections");

            migrationBuilder.DropColumn(
                name: "SourceRejectionId",
                table: "telemetry_ingestion_rejections");
        }
    }
}
