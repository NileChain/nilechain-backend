using Microsoft.AspNetCore.Http;

namespace NileChain.API.Extensions;

public static class HttpContextClientInfo
{
    public static string? ClientIp(this HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();

    public static string? ClientUserAgent(this HttpContext context)
    {
        var ua = context.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(ua) ? null : ua;
    }
}
