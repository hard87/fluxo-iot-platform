using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertBackend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION fluxo_alert_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Alert evidence is append-only' USING ERRCODE = '23514'; END $$;
                CREATE FUNCTION fluxo_alert_event_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Alert occurrence cannot be deleted' USING ERRCODE = '23514'; END IF;
                  IF (to_jsonb(NEW) - 'Status' - 'Ordinal') IS DISTINCT FROM (to_jsonb(OLD) - 'Status' - 'Ordinal')
                    OR OLD."Status" <> 'Firing' OR NEW."Status" NOT IN ('Resolved','Closed') OR NEW."Ordinal" <> OLD."Ordinal" + 1
                  THEN RAISE EXCEPTION 'Invalid alert occurrence mutation' USING ERRCODE = '23514'; END IF;
                  RETURN NEW;
                END $$;
                """);
            migrationBuilder.AddUniqueConstraint(
                name: "AK_telemetry_ingestion_records_WorkspaceId_DeviceId_Id",
                table: "telemetry_ingestion_records",
                columns: new[] { "WorkspaceId", "DeviceId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_telemetry_ingestion_records_WorkspaceId_Id",
                table: "telemetry_ingestion_records",
                columns: new[] { "WorkspaceId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_metric_definitions_WorkspaceId_Id",
                table: "metric_definitions",
                columns: new[] { "WorkspaceId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_devices_WorkspaceId_Identifier",
                table: "devices",
                columns: new[] { "WorkspaceId", "Identifier" });

            migrationBuilder.CreateTable(
                name: "alert_device_coordination",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NextOrder = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_device_coordination", x => new { x.WorkspaceId, x.DeviceIdentifier });
                });

            migrationBuilder.CreateTable(
                name: "alert_evaluation_work_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "character varying(120)", nullable: false),
                    IngestionRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    QueueOrder = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_evaluation_work_items", x => x.Id);
                    table.UniqueConstraint("AK_alert_evaluation_work_items_WorkspaceId_DeviceIdentifier_Qu~", x => new { x.WorkspaceId, x.DeviceIdentifier, x.QueueOrder, x.Id });
                    table.UniqueConstraint("AK_alert_evaluation_work_items_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.ForeignKey(
                        name: "FK_alert_evaluation_work_items_alert_device_coordination_Works~",
                        columns: x => new { x.WorkspaceId, x.DeviceIdentifier },
                        principalTable: "alert_device_coordination",
                        principalColumns: new[] { "WorkspaceId", "DeviceIdentifier" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alert_evaluation_work_items_telemetry_ingestion_records_Wor~",
                        columns: x => new { x.WorkspaceId, x.DeviceIdentifier, x.IngestionRecordId },
                        principalTable: "telemetry_ingestion_records",
                        principalColumns: new[] { "WorkspaceId", "DeviceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alert_acknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_acknowledgements", x => x.Id);
                    table.UniqueConstraint("AK_alert_acknowledgements_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.ForeignKey(
                        name: "FK_alert_acknowledgements_platform_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "platform_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alert_delivery_intents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_delivery_intents", x => x.Id);
                    table.UniqueConstraint("AK_alert_delivery_intents_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "alert_evaluation_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "character varying(120)", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QueueOrder = table.Column<long>(type: "bigint", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    ClaimedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_evaluation_attempts", x => x.Id);
                    table.UniqueConstraint("AK_alert_evaluation_attempts_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.CheckConstraint("CK_alert_attempt_status", "\"Status\" IN ('Pending','Claimed','Completed','Failed','DeadLetter','Skipped')");
                    table.ForeignKey(
                        name: "FK_alert_evaluation_attempts_alert_evaluation_work_items_Works~",
                        columns: x => new { x.WorkspaceId, x.DeviceIdentifier, x.QueueOrder, x.WorkItemId },
                        principalTable: "alert_evaluation_work_items",
                        principalColumns: new[] { "WorkspaceId", "DeviceIdentifier", "QueueOrder", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alert_event_transitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IngestionRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    NumericValue = table.Column<double>(type: "double precision", nullable: true),
                    BooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    EvaluatorVersion = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_event_transitions", x => x.Id);
                    table.UniqueConstraint("AK_alert_event_transitions_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.CheckConstraint("CK_alert_transition_kind", "\"Kind\" IN ('Firing','Resolved','Closed')");
                    table.ForeignKey(
                        name: "FK_alert_event_transitions_telemetry_ingestion_records_Workspa~",
                        columns: x => new { x.WorkspaceId, x.IngestionRecordId },
                        principalTable: "telemetry_ingestion_records",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alert_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "character varying(120)", nullable: false),
                    TriggeredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_events", x => x.Id);
                    table.UniqueConstraint("AK_alert_events_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.UniqueConstraint("AK_alert_events_WorkspaceId_RuleId_DeviceIdentifier_Id", x => new { x.WorkspaceId, x.RuleId, x.DeviceIdentifier, x.Id });
                    table.CheckConstraint("CK_alert_event_status", "\"Status\" IN ('Firing','Resolved','Closed')");
                    table.ForeignKey(
                        name: "FK_alert_events_devices_WorkspaceId_DeviceIdentifier",
                        columns: x => new { x.WorkspaceId, x.DeviceIdentifier },
                        principalTable: "devices",
                        principalColumns: new[] { "WorkspaceId", "Identifier" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alert_rule_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    MetricDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "character varying(120)", nullable: true),
                    ValueType = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    Operator = table.Column<string>(type: "text", nullable: false),
                    Threshold = table.Column<double>(type: "double precision", nullable: true),
                    ThresholdHigh = table.Column<double>(type: "double precision", nullable: true),
                    Hysteresis = table.Column<double>(type: "double precision", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "integer", nullable: false),
                    ExpectedIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rule_revisions", x => x.Id);
                    table.UniqueConstraint("AK_alert_rule_revisions_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.UniqueConstraint("AK_alert_rule_revisions_WorkspaceId_RuleId_Id", x => new { x.WorkspaceId, x.RuleId, x.Id });
                    table.CheckConstraint("CK_alert_revision_parameters", "\"DurationSeconds\" >= 0 AND \"CooldownSeconds\" >= 0 AND \"ExpectedIntervalSeconds\" > 0 AND \"Hysteresis\" >= 0 AND \"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_alert_rule_revisions_devices_WorkspaceId_DeviceIdentifier",
                        columns: x => new { x.WorkspaceId, x.DeviceIdentifier },
                        principalTable: "devices",
                        principalColumns: new[] { "WorkspaceId", "Identifier" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alert_rule_revisions_metric_definitions_WorkspaceId_MetricD~",
                        columns: x => new { x.WorkspaceId, x.MetricDefinitionId },
                        principalTable: "metric_definitions",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alert_rule_revisions_platform_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "platform_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alert_rule_states",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "character varying(120)", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstViolationAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastObservedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false),
                    LastIngestionRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastNumericValue = table.Column<double>(type: "double precision", nullable: true),
                    LastBooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    LastTriggeredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActiveEventId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rule_states", x => new { x.WorkspaceId, x.RuleId, x.DeviceIdentifier });
                    table.ForeignKey(
                        name: "FK_alert_rule_states_alert_events_WorkspaceId_RuleId_DeviceIde~",
                        columns: x => new { x.WorkspaceId, x.RuleId, x.DeviceIdentifier, x.ActiveEventId },
                        principalTable: "alert_events",
                        principalColumns: new[] { "WorkspaceId", "RuleId", "DeviceIdentifier", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alert_rule_states_alert_rule_revisions_WorkspaceId_RuleId_R~",
                        columns: x => new { x.WorkspaceId, x.RuleId, x.RevisionId },
                        principalTable: "alert_rule_revisions",
                        principalColumns: new[] { "WorkspaceId", "RuleId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alert_rule_states_devices_WorkspaceId_DeviceIdentifier",
                        columns: x => new { x.WorkspaceId, x.DeviceIdentifier },
                        principalTable: "devices",
                        principalColumns: new[] { "WorkspaceId", "Identifier" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rules", x => x.Id);
                    table.UniqueConstraint("AK_alert_rules_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.ForeignKey(
                        name: "FK_alert_rules_alert_rule_revisions_WorkspaceId_Id_CurrentRevi~",
                        columns: x => new { x.WorkspaceId, x.Id, x.CurrentRevisionId },
                        principalTable: "alert_rule_revisions",
                        principalColumns: new[] { "WorkspaceId", "RuleId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alert_rules_workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_acknowledgements_AuthorId",
                table: "alert_acknowledgements",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_acknowledgements_EventId_AuthorId",
                table: "alert_acknowledgements",
                columns: new[] { "EventId", "AuthorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_acknowledgements_WorkspaceId_EventId",
                table: "alert_acknowledgements",
                columns: new[] { "WorkspaceId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_delivery_intents_TransitionId",
                table: "alert_delivery_intents",
                column: "TransitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_delivery_intents_WorkspaceId_TransitionId",
                table: "alert_delivery_intents",
                columns: new[] { "WorkspaceId", "TransitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_evaluation_attempts_Status_NextAttemptAtUtc_LeaseUnti~",
                table: "alert_evaluation_attempts",
                columns: new[] { "Status", "NextAttemptAtUtc", "LeaseUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_evaluation_attempts_WorkItemId_RuleId_RevisionId",
                table: "alert_evaluation_attempts",
                columns: new[] { "WorkItemId", "RuleId", "RevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_evaluation_attempts_WorkspaceId_DeviceIdentifier_Queu~",
                table: "alert_evaluation_attempts",
                columns: new[] { "WorkspaceId", "DeviceIdentifier", "QueueOrder", "WorkItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_evaluation_attempts_WorkspaceId_DeviceIdentifier_Rule~",
                table: "alert_evaluation_attempts",
                columns: new[] { "WorkspaceId", "DeviceIdentifier", "RuleId", "QueueOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_evaluation_attempts_WorkspaceId_RuleId_RevisionId",
                table: "alert_evaluation_attempts",
                columns: new[] { "WorkspaceId", "RuleId", "RevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_evaluation_work_items_WorkspaceId_DeviceIdentifier_In~",
                table: "alert_evaluation_work_items",
                columns: new[] { "WorkspaceId", "DeviceIdentifier", "IngestionRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_evaluation_work_items_WorkspaceId_DeviceIdentifier_Qu~",
                table: "alert_evaluation_work_items",
                columns: new[] { "WorkspaceId", "DeviceIdentifier", "QueueOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_evaluation_work_items_WorkspaceId_IngestionRecordId",
                table: "alert_evaluation_work_items",
                columns: new[] { "WorkspaceId", "IngestionRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_event_transitions_EventId_Ordinal",
                table: "alert_event_transitions",
                columns: new[] { "EventId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_event_transitions_WorkspaceId_EventId",
                table: "alert_event_transitions",
                columns: new[] { "WorkspaceId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_event_transitions_WorkspaceId_IngestionRecordId",
                table: "alert_event_transitions",
                columns: new[] { "WorkspaceId", "IngestionRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_events_WorkspaceId_DeviceIdentifier",
                table: "alert_events",
                columns: new[] { "WorkspaceId", "DeviceIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_events_WorkspaceId_RuleId_DeviceIdentifier",
                table: "alert_events",
                columns: new[] { "WorkspaceId", "RuleId", "DeviceIdentifier" },
                unique: true,
                filter: "\"Status\" = 'Firing'");

            migrationBuilder.CreateIndex(
                name: "IX_alert_events_WorkspaceId_RuleId_RevisionId",
                table: "alert_events",
                columns: new[] { "WorkspaceId", "RuleId", "RevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_rule_revisions_AuthorId",
                table: "alert_rule_revisions",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_rule_revisions_WorkspaceId_DeviceIdentifier",
                table: "alert_rule_revisions",
                columns: new[] { "WorkspaceId", "DeviceIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_rule_revisions_WorkspaceId_MetricDefinitionId",
                table: "alert_rule_revisions",
                columns: new[] { "WorkspaceId", "MetricDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_rule_revisions_WorkspaceId_RuleId_Version",
                table: "alert_rule_revisions",
                columns: new[] { "WorkspaceId", "RuleId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_rule_states_WorkspaceId_DeviceIdentifier",
                table: "alert_rule_states",
                columns: new[] { "WorkspaceId", "DeviceIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_rule_states_WorkspaceId_RuleId_DeviceIdentifier_Activ~",
                table: "alert_rule_states",
                columns: new[] { "WorkspaceId", "RuleId", "DeviceIdentifier", "ActiveEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_rule_states_WorkspaceId_RuleId_RevisionId",
                table: "alert_rule_states",
                columns: new[] { "WorkspaceId", "RuleId", "RevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_rules_WorkspaceId_Id_CurrentRevisionId",
                table: "alert_rules",
                columns: new[] { "WorkspaceId", "Id", "CurrentRevisionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_alert_acknowledgements_alert_events_WorkspaceId_EventId",
                table: "alert_acknowledgements",
                columns: new[] { "WorkspaceId", "EventId" },
                principalTable: "alert_events",
                principalColumns: new[] { "WorkspaceId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_alert_delivery_intents_alert_event_transitions_WorkspaceId_~",
                table: "alert_delivery_intents",
                columns: new[] { "WorkspaceId", "TransitionId" },
                principalTable: "alert_event_transitions",
                principalColumns: new[] { "WorkspaceId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_alert_evaluation_attempts_alert_rule_revisions_WorkspaceId_~",
                table: "alert_evaluation_attempts",
                columns: new[] { "WorkspaceId", "RuleId", "RevisionId" },
                principalTable: "alert_rule_revisions",
                principalColumns: new[] { "WorkspaceId", "RuleId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_alert_event_transitions_alert_events_WorkspaceId_EventId",
                table: "alert_event_transitions",
                columns: new[] { "WorkspaceId", "EventId" },
                principalTable: "alert_events",
                principalColumns: new[] { "WorkspaceId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_alert_events_alert_rule_revisions_WorkspaceId_RuleId_Revisi~",
                table: "alert_events",
                columns: new[] { "WorkspaceId", "RuleId", "RevisionId" },
                principalTable: "alert_rule_revisions",
                principalColumns: new[] { "WorkspaceId", "RuleId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_alert_rule_revisions_alert_rules_WorkspaceId_RuleId",
                table: "alert_rule_revisions",
                columns: new[] { "WorkspaceId", "RuleId" },
                principalTable: "alert_rules",
                principalColumns: new[] { "WorkspaceId", "Id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                CREATE TRIGGER alert_revisions_immutable BEFORE UPDATE OR DELETE ON alert_rule_revisions FOR EACH ROW EXECUTE FUNCTION fluxo_alert_append_only();
                CREATE TRIGGER alert_transitions_immutable BEFORE UPDATE OR DELETE ON alert_event_transitions FOR EACH ROW EXECUTE FUNCTION fluxo_alert_append_only();
                CREATE TRIGGER alert_acknowledgements_immutable BEFORE UPDATE OR DELETE ON alert_acknowledgements FOR EACH ROW EXECUTE FUNCTION fluxo_alert_append_only();
                CREATE TRIGGER alert_intents_immutable BEFORE UPDATE OR DELETE ON alert_delivery_intents FOR EACH ROW EXECUTE FUNCTION fluxo_alert_append_only();
                CREATE TRIGGER alert_occurrence_guard BEFORE UPDATE OR DELETE ON alert_events FOR EACH ROW EXECUTE FUNCTION fluxo_alert_event_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_alert_rules_alert_rule_revisions_WorkspaceId_Id_CurrentRevi~",
                table: "alert_rules");

            migrationBuilder.DropTable(
                name: "alert_acknowledgements");

            migrationBuilder.DropTable(
                name: "alert_delivery_intents");

            migrationBuilder.DropTable(
                name: "alert_evaluation_attempts");

            migrationBuilder.DropTable(
                name: "alert_rule_states");

            migrationBuilder.DropTable(
                name: "alert_event_transitions");

            migrationBuilder.DropTable(
                name: "alert_evaluation_work_items");

            migrationBuilder.DropTable(
                name: "alert_events");

            migrationBuilder.DropTable(
                name: "alert_device_coordination");

            migrationBuilder.DropTable(
                name: "alert_rule_revisions");

            migrationBuilder.DropTable(
                name: "alert_rules");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_telemetry_ingestion_records_WorkspaceId_DeviceId_Id",
                table: "telemetry_ingestion_records");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_telemetry_ingestion_records_WorkspaceId_Id",
                table: "telemetry_ingestion_records");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_metric_definitions_WorkspaceId_Id",
                table: "metric_definitions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_devices_WorkspaceId_Identifier",
                table: "devices");
            migrationBuilder.Sql("DROP FUNCTION fluxo_alert_append_only(); DROP FUNCTION fluxo_alert_event_guard();");
        }
    }
}
