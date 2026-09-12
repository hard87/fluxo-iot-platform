using Fluxo.Domain.Alerts;
using Fluxo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxo.Infrastructure.Alerts;

internal static class AlertModelConfiguration
{
    public static void Configure(ModelBuilder m)
    {
        m.Entity<Device>().HasAlternateKey(x => new { x.WorkspaceId, x.Identifier });
        m.Entity<MetricDefinition>().HasAlternateKey(x => new { x.WorkspaceId, x.Id });
        m.Entity<TelemetryIngestionRecord>().HasAlternateKey(x => new { x.WorkspaceId, x.Id });
        m.Entity<TelemetryIngestionRecord>().HasAlternateKey(x => new { x.WorkspaceId, x.DeviceId, x.Id });

        var rule = Scoped<AlertRule>(m, "alert_rules");
        rule.HasOne<Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        rule.HasOne<AlertRuleRevision>().WithMany()
            .HasForeignKey(x => new { x.WorkspaceId, x.Id, x.CurrentRevisionId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.RuleId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var revision = Scoped<AlertRuleRevision>(m, "alert_rule_revisions");
        revision.HasAlternateKey(x => new { x.WorkspaceId, x.RuleId, x.Id });
        revision.HasIndex(x => new { x.WorkspaceId, x.RuleId, x.Version }).IsUnique();
        revision.HasOne<AlertRule>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.RuleId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        revision.HasOne<MetricDefinition>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.MetricDefinitionId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        revision.HasOne<Device>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.DeviceIdentifier })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.Identifier }).OnDelete(DeleteBehavior.Restrict);
        revision.HasOne<PlatformUser>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        revision.ToTable(t => t.HasCheckConstraint("CK_alert_revision_parameters",
            "\"DurationSeconds\" >= 0 AND \"CooldownSeconds\" >= 0 AND \"ExpectedIntervalSeconds\" > 0 AND \"Hysteresis\" >= 0 AND \"Version\" > 0"));

        var device = m.Entity<AlertDeviceCoordination>();
        device.ToTable("alert_device_coordination");
        device.HasKey(x => new { x.WorkspaceId, x.DeviceIdentifier });
        device.Property(x => x.DeviceIdentifier).HasMaxLength(120);
        var work = Scoped<AlertEvaluationWorkItem>(m, "alert_evaluation_work_items");
        work.HasAlternateKey(x => new { x.WorkspaceId, x.DeviceIdentifier, x.QueueOrder, x.Id });
        work.HasIndex(x => new { x.WorkspaceId, x.IngestionRecordId }).IsUnique();
        work.HasIndex(x => new { x.WorkspaceId, x.DeviceIdentifier, x.QueueOrder }).IsUnique();
        work.HasOne<TelemetryIngestionRecord>().WithMany()
            .HasForeignKey(x => new { x.WorkspaceId, x.DeviceIdentifier, x.IngestionRecordId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.DeviceId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        work.HasOne<AlertDeviceCoordination>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.DeviceIdentifier })
            .OnDelete(DeleteBehavior.Restrict);

        var attempt = Scoped<AlertEvaluationAttempt>(m, "alert_evaluation_attempts");
        attempt.HasIndex(x => new { x.WorkItemId, x.RuleId, x.RevisionId }).IsUnique();
        attempt.HasIndex(x => new { x.WorkspaceId, x.DeviceIdentifier, x.RuleId, x.QueueOrder });
        attempt.HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.LeaseUntilUtc });
        attempt.HasOne<AlertEvaluationWorkItem>().WithMany()
            .HasForeignKey(x => new { x.WorkspaceId, x.DeviceIdentifier, x.QueueOrder, x.WorkItemId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.DeviceIdentifier, x.QueueOrder, x.Id }).OnDelete(DeleteBehavior.Restrict);
        attempt.HasOne<AlertRuleRevision>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.RuleId, x.RevisionId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.RuleId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        attempt.ToTable(t => t.HasCheckConstraint("CK_alert_attempt_status",
            "\"Status\" IN ('Pending','Claimed','Completed','Failed','DeadLetter','Skipped')"));

        var state = m.Entity<AlertRuleState>();
        state.ToTable("alert_rule_states");
        state.HasKey(x => new { x.WorkspaceId, x.RuleId, x.DeviceIdentifier });
        state.HasOne<AlertRuleRevision>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.RuleId, x.RevisionId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.RuleId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        state.HasOne<Device>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.DeviceIdentifier })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.Identifier }).OnDelete(DeleteBehavior.Restrict);
        state.HasOne<AlertEvent>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.RuleId, x.DeviceIdentifier, x.ActiveEventId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.RuleId, x.DeviceIdentifier, x.Id }).OnDelete(DeleteBehavior.Restrict);

        var occurrence = Scoped<AlertEvent>(m, "alert_events");
        occurrence.HasAlternateKey(x => new { x.WorkspaceId, x.RuleId, x.DeviceIdentifier, x.Id });
        occurrence.HasIndex(x => new { x.WorkspaceId, x.RuleId, x.DeviceIdentifier }).IsUnique().HasFilter("\"Status\" = 'Firing'");
        occurrence.HasOne<AlertRuleRevision>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.RuleId, x.RevisionId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.RuleId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        occurrence.HasOne<Device>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.DeviceIdentifier })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.Identifier }).OnDelete(DeleteBehavior.Restrict);
        occurrence.ToTable(t => t.HasCheckConstraint("CK_alert_event_status", "\"Status\" IN ('Firing','Resolved','Closed')"));

        var transition = Scoped<AlertEventTransition>(m, "alert_event_transitions");
        transition.HasIndex(x => new { x.EventId, x.Ordinal }).IsUnique();
        transition.HasOne<AlertEvent>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.EventId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        transition.HasOne<TelemetryIngestionRecord>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.IngestionRecordId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        transition.ToTable(t => t.HasCheckConstraint("CK_alert_transition_kind", "\"Kind\" IN ('Firing','Resolved','Closed')"));
        var ack = Scoped<AlertAcknowledgement>(m, "alert_acknowledgements");
        ack.HasIndex(x => new { x.EventId, x.AuthorId }).IsUnique();
        ack.HasOne<AlertEvent>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.EventId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        ack.HasOne<PlatformUser>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        var intent = Scoped<AlertDeliveryIntent>(m, "alert_delivery_intents");
        intent.HasIndex(x => x.TransitionId).IsUnique();
        intent.HasOne<AlertEventTransition>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.TransitionId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private static EntityTypeBuilder<T> Scoped<T>(ModelBuilder m, string table) where T : class
    {
        var b = m.Entity<T>();
        b.ToTable(table); b.HasKey("Id"); b.HasAlternateKey("WorkspaceId", "Id");
        return b;
    }
}
