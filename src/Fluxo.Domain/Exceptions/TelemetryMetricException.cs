namespace Fluxo.Domain.Exceptions;
public sealed class MetricTypeMismatchException(string key) : Exception($"MetricTypeMismatch: metric '{key}' has a different established type.");
public sealed class MetricCardinalityGuardException(string key) : Exception($"MetricCardinalityGuardTriggered: discovery limit exceeded by '{key}'.");
public sealed class MetricWorkspaceLimitException(string key, int limit) : Exception($"MetricWorkspaceLimitTriggered: workspace limit {limit} exceeded by '{key}'.");
