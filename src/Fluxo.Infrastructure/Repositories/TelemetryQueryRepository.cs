using System.Data;
using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fluxo.Infrastructure.Repositories;

public sealed class TelemetryQueryRepository(FluxoDbContext db) : ITelemetryQueryRepository
{
    private static readonly IReadOnlyDictionary<string, string> Intervals = new Dictionary<string, string>
    { ["1m"]="1 minute", ["5m"]="5 minutes", ["15m"]="15 minutes", ["1h"]="1 hour", ["6h"]="6 hours", ["1d"]="1 day" };

    public async Task<IReadOnlyList<MetricDefinition>> ListMetricDefinitionsAsync(Guid workspaceId, CancellationToken ct) =>
        await db.MetricDefinitions.AsNoTracking().Where(x => x.WorkspaceId == workspaceId && x.IsQueryable)
            .OrderBy(x => x.DisplayName).ThenBy(x => x.MetricKey).ToListAsync(ct);

    public async Task<IReadOnlyList<MetricDefinition>> ResolveMetricDefinitionsAsync(Guid workspaceId,
        IReadOnlyCollection<string> metricKeys, CancellationToken ct) =>
        await db.MetricDefinitions.AsNoTracking().Where(x => x.WorkspaceId == workspaceId && metricKeys.Contains(x.MetricKey))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TelemetryPointResponse>> QuerySeriesAsync(Guid workspaceId, Device device,
        MetricDefinition metric, DateTime fromUtc, DateTime toUtc, string aggregation, string? bucket,
        int rawLimit, CancellationToken ct)
    {
        if (aggregation == "raw")
            return await db.TelemetryPoints.AsNoTracking()
                .Where(x => x.WorkspaceId == workspaceId && x.DeviceId == device.Identifier &&
                    x.MetricDefinitionId == metric.Id && x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
                .OrderBy(x => x.OccurredAtUtc).Take(rawLimit)
                .Select(x => new TelemetryPointResponse(x.OccurredAtUtc, x.NumericValue, x.BooleanValue, x.TextValue, null))
                .ToListAsync(ct);

        var interval = Intervals[bucket!];
        var valueExpression = aggregation switch
        {
            "avg" => "avg(\"NumericValue\")", "min" => "min(\"NumericValue\")",
            "max" => "max(\"NumericValue\")", "sum" => "sum(\"NumericValue\")", _ => "NULL"
        };
        var sql = aggregation == "last"
            ? $"""
              SELECT bucket, "NumericValue", "BooleanValue", "TextValue", sample_count FROM (
                SELECT DISTINCT ON (bucket) bucket, "NumericValue", "BooleanValue", "TextValue", sample_count
                FROM (SELECT date_bin(INTERVAL '{interval}', "OccurredAtUtc", TIMESTAMPTZ '2001-01-01 00:00:00+00') bucket,
                     "OccurredAtUtc", "NumericValue", "BooleanValue", "TextValue",
                     count(*) OVER (PARTITION BY date_bin(INTERVAL '{interval}', "OccurredAtUtc", TIMESTAMPTZ '2001-01-01 00:00:00+00'))::int sample_count
                     FROM telemetry_points WHERE "WorkspaceId"=@w AND "DeviceId"=@d AND "MetricDefinitionId"=@m
                     AND "OccurredAtUtc">=@f AND "OccurredAtUtc"<@t) p ORDER BY bucket, "OccurredAtUtc" DESC) q ORDER BY bucket
              """
            : $"""
              SELECT date_bin(INTERVAL '{interval}', "OccurredAtUtc", TIMESTAMPTZ '2001-01-01 00:00:00+00') bucket,
                     {valueExpression} numeric_value, NULL::boolean boolean_value, NULL::text text_value, count(*)::int sample_count
              FROM telemetry_points WHERE "WorkspaceId"=@w AND "DeviceId"=@d AND "MetricDefinitionId"=@m
                AND "OccurredAtUtc">=@f AND "OccurredAtUtc"<@t GROUP BY bucket ORDER BY bucket
              """;
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql; command.CommandTimeout = 8;
            command.Parameters.Add(new NpgsqlParameter("w", workspaceId));
            command.Parameters.Add(new NpgsqlParameter("d", device.Identifier));
            command.Parameters.Add(new NpgsqlParameter("m", metric.Id));
            command.Parameters.Add(new NpgsqlParameter("f", fromUtc));
            command.Parameters.Add(new NpgsqlParameter("t", toUtc));
            if (command.Connection!.State != ConnectionState.Open) await command.Connection.OpenAsync(ct);
            var result = new List<TelemetryPointResponse>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result.Add(new(reader.GetDateTime(0), reader.IsDBNull(1) ? null : reader.GetDouble(1),
                    reader.IsDBNull(2) ? null : reader.GetBoolean(2), reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetInt32(4)));
            return result;
        }
        catch (NpgsqlException ex) when (!ct.IsCancellationRequested && (ex.IsTransient || ex.InnerException is TimeoutException))
        { throw new QueryTimeoutException("Telemetry query exceeded the 8 second timeout.", ex); }
    }
}
