namespace Anota.Api;

/// <summary>
/// Raised when the anota API returns a non-2xx response. Carries the HTTP status
/// code and the server's message (the problem-details <c>detail</c>, falling back
/// to <c>title</c>, then the raw response body).
/// </summary>
public sealed class AnotaApiError : Exception
{
    /// <summary>The HTTP status code of the failed response.</summary>
    public int Status { get; }

    /// <summary>Create an error carrying the HTTP status code and the server's message.</summary>
    public AnotaApiError(int status, string message) : base(message)
    {
        Status = status;
    }
}
