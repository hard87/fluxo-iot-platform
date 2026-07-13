using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Fluxo.Infrastructure.Repositories;
public sealed class NpgsqlBinaryCopyTelemetryPointWriter(FluxoDbContext context) : ITelemetryPointWriter
{
    public async Task WriteAsync(IReadOnlyCollection<TelemetryPoint> points, CancellationToken cancellationToken = default)
    {
        if (points.Count == 0) return;
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var writer = await connection.BeginBinaryImportAsync("""
            COPY telemetry_points ("Id","OccurredAtUtc","TenantId","WorkspaceId","DeviceId","MetricDefinitionId",
              "NumericValue","BooleanValue","TextValue","IngestionRecordId") FROM STDIN (FORMAT BINARY)
            """, cancellationToken);
        foreach (var point in points)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(point.Id, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(point.OccurredAtUtc, NpgsqlDbType.TimestampTz, cancellationToken);
            await writer.WriteAsync(point.TenantId, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(point.WorkspaceId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(point.DeviceId, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(point.MetricDefinitionId, NpgsqlDbType.Uuid, cancellationToken);
            if (point.NumericValue is null) await writer.WriteNullAsync(cancellationToken); else await writer.WriteAsync(point.NumericValue.Value, NpgsqlDbType.Double, cancellationToken);
            if (point.BooleanValue is null) await writer.WriteNullAsync(cancellationToken); else await writer.WriteAsync(point.BooleanValue.Value, NpgsqlDbType.Boolean, cancellationToken);
            if (point.TextValue is null) await writer.WriteNullAsync(cancellationToken); else await writer.WriteAsync(point.TextValue, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(point.IngestionRecordId, NpgsqlDbType.Uuid, cancellationToken);
        }
        await writer.CompleteAsync(cancellationToken);
    }
}
