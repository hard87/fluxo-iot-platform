namespace Fluxo.Application.Common.Exceptions;

public sealed class TelemetryQueryValidationException : ValidationException
{
    public string ErrorCode { get; }
    public TelemetryQueryValidationException(string errorCode, string message) : base(message) => ErrorCode = errorCode;
}
