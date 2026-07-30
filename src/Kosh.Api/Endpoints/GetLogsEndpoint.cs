using Kosh.Core.Logs;

namespace Kosh.Api.Endpoints;

public static class GetLogsEndpoint
{
    public static IEndpointRouteBuilder MapGetLogsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/logs",
            (LogRingBuffer buffer) =>
            {
                var logs = buffer.GetRange(0, 10000);
                return Results.Ok(logs);
            }
        );

        return app;
    }
}