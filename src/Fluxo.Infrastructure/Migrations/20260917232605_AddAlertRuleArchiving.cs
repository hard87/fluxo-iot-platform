using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertRuleArchiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAtUtc",
                table: "alert_rule_revisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_alert_revision_archive",
                table: "alert_rule_revisions",
                sql: "\"ArchivedAtUtc\" IS NULL OR NOT \"Enabled\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_alert_revision_archive",
                table: "alert_rule_revisions");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "alert_rule_revisions");
        }
    }
}
