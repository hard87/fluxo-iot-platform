namespace Fluxo.Application.Common.Exceptions;

public sealed class QueryTimeoutException : Exception
{
    public QueryTimeoutException(string message, Exception? innerException = null) : base(message, innerException) { }
}
