using Serilog.Context;

namespace PinballPVP.Api.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string Header = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext httpContext)
    {
        var correlationId = httpContext.Request.Headers[Header].FirstOrDefault()
            ?? httpContext.TraceIdentifier;

        httpContext.Response.Headers[Header] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(httpContext);
        }
    }
}
