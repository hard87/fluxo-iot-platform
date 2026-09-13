using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertPortalNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_notification_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_notification_subscriptions", x => x.Id);
                    table.UniqueConstraint("AK_alert_notification_subscriptions_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.CheckConstraint("CK_alert_notification_subscription_channel", "\"Channel\" IN ('Portal')");
                    table.ForeignKey(
                        name: "FK_alert_notification_subscriptions_alert_rules_WorkspaceId_Ru~",
                        columns: x => new { x.WorkspaceId, x.RuleId },
                        principalTable: "alert_rules",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alert_notification_subscriptions_platform_users_MemberId",
                        column: x => x.MemberId,
                        principalTable: "platform_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "portal_notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_notifications", x => x.Id);
                    table.UniqueConstraint("AK_portal_notifications_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.ForeignKey(
                        name: "FK_portal_notifications_alert_event_transitions_WorkspaceId_Tr~",
                        columns: x => new { x.WorkspaceId, x.TransitionId },
                        principalTable: "alert_event_transitions",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_notifications_platform_users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "platform_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_notification_subscriptions_MemberId",
                table: "alert_notification_subscriptions",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_notification_subscriptions_WorkspaceId_RuleId_MemberI~",
                table: "alert_notification_subscriptions",
                columns: new[] { "WorkspaceId", "RuleId", "MemberId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_portal_notifications_RecipientUserId",
                table: "portal_notifications",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_portal_notifications_WorkspaceId_TransitionId_RecipientUser~",
                table: "portal_notifications",
                columns: new[] { "WorkspaceId", "TransitionId", "RecipientUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_notification_subscriptions");

            migrationBuilder.DropTable(
                name: "portal_notifications");
        }
    }
}
