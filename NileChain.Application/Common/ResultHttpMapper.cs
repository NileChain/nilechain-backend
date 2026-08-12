using System.Net;

namespace NileChain.Application.Common;

/// <summary>
/// Maps <see cref="Error"/> codes to HTTP status and a unified API error envelope.
/// </summary>
public static class ResultHttpMapper
{
    public sealed record ApiErrorBody(string Code, string Message, IEnumerable<string>? Errors = null);

    public static HttpStatusCode MapStatus(Error? error)
    {
        if (error is null)
            return HttpStatusCode.BadRequest;

        var code = error.Code ?? string.Empty;

        if (Contains(code, "NotFound"))
            return HttpStatusCode.NotFound;

        if (Contains(code, "Unauthorized") || Contains(code, "Forbidden"))
            return HttpStatusCode.Forbidden;

        if (Contains(code, "AlreadyExists")
            || Contains(code, "Conflict")
            || Contains(code, "Concurrency")
            || Contains(code, "Frozen")
            || Contains(code, "Blocked")
            || Contains(code, "ActiveExists")
            || Contains(code, "WeighbridgeRequired")
            || Contains(code, "AcceptedExceedsWeighed")
            || Contains(code, "EligibilityChanged")
            || string.Equals(code, "Factory.IdempotencyConflict", StringComparison.OrdinalIgnoreCase))
        {
            return HttpStatusCode.Conflict;
        }

        if (Contains(code, "Validation") || Contains(code, "Invalid"))
            return HttpStatusCode.BadRequest;

        return HttpStatusCode.BadRequest;
    }

    public static object ToErrorBody(Error error, IEnumerable<string>? errors = null) =>
        new
        {
            code = error.Code,
            message = error.Description,
            errors
        };

    public static ApiErrorBody ToTypedBody(Error error, IEnumerable<string>? errors = null) =>
        new(error.Code, error.Description, errors);

    private static bool Contains(string code, string token) =>
        code.Contains(token, StringComparison.OrdinalIgnoreCase);
}
