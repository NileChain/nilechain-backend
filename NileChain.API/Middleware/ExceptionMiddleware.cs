using FluentValidation;
using NileChain.Application.Common;
using System.Text.Json;

namespace NileChain.API.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ILogger<ExceptionMiddleware> logger)
        {
            try
            {
                await _next(context);
            }
            catch (ValidationException ex)
            {
                var correlationId = ResolveCorrelationId(context);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    code = "Validation.Error",
                    message = "Validation failed.",
                    errors = ex.Errors.Select(x => x.ErrorMessage),
                    correlationId
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response));
            }
            catch (Exception ex)
            {
                var correlationId = ResolveCorrelationId(context);
                logger.LogError(
                    ex,
                    "Unhandled exception. CorrelationId={CorrelationId} Path={Path}",
                    correlationId,
                    context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    code = ClientErrorSanitizer.GenericServerCode,
                    message = ClientErrorSanitizer.GenericServerMessage,
                    correlationId
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response));
            }
        }

        internal static string ResolveCorrelationId(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var header)
                && !string.IsNullOrWhiteSpace(header))
            {
                return header.ToString();
            }

            return context.TraceIdentifier;
        }
    }
}
