using System.Text.Json;
using Kosh.Core.Logs;

namespace Kosh.Api.Endpoints;

public static class GetLogsStreamEndpoint
{
    public static IEndpointRouteBuilder MapGetLogsStreamEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/logs/stream",
            async (HttpContext context, LogRingBuffer buffer) =>
            {
                context.Response.Headers.Append("Content-Type", "text/event-stream");
                context.Response.Headers.Append("Cache-Control", "no-cache");
                context.Response.Headers.Append("Connection", "keep-alive");

                var reader = buffer.Subscribe();

                await foreach (var log in reader.ReadAllAsync(context.RequestAborted))
                {
                    var json = JsonSerializer.Serialize(log);
                    await context.Response.WriteAsync($"data: {json}\n\n");
                    await context.Response.Body.FlushAsync();
                }
            }
        );

        return app;
    }
}
