using Kosh.Core.Runtime;
using Kosh.Core.Supervisor;
using Kosh.Core.ValueObjects;

namespace Kosh.Api.Endpoints;

public static class StopServiceEndpoint
{
    public static IEndpointRouteBuilder MapStopServiceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/services/{id}/stop", async
                (string id, ISupervisor supervisor, CancellationToken ct) =>
            {
                var serviceId = new ServiceId(id);
                var service = supervisor.Services.Values.FirstOrDefault(s => s.Definition.Id == serviceId);

                if (service is null)
                {
                    return Results.NotFound(new { Message = $"Service with ID {id} not found." });
                }

                if (service.Status != ServiceStatus.Running)
                {
                    return Results.BadRequest(new { Message = $"Service with ID {id} is not running." });
                }

                await supervisor.StopServiceAsync(serviceId, ct);
                return Results.NoContent();
            }
        );

        return app;
    }
}