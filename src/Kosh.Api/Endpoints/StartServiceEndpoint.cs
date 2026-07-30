using Kosh.Core.Runtime;
using Kosh.Core.Supervisor;
using Kosh.Core.ValueObjects;

namespace Kosh.Api.Endpoints;

public static class StartServiceEndpoint
{
    public static IEndpointRouteBuilder MapStartServiceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/services/{id}/start", async
                (string id, ISupervisor supervisor, CancellationToken ct) =>
            {
                var serviceId = new ServiceId(id);
                var service = supervisor.Services.Values.FirstOrDefault(s => s.Definition.Id == serviceId);

                if (service is null)
                {
                    return Results.NotFound(new { Message = $"Service with ID {id} not found." });
                }

                if (service.Status != ServiceStatus.Stopped && service.Status != ServiceStatus.NotStarted)
                {
                    return Results.BadRequest(new { Message = $"Service with ID {id} is not stopped." });
                }

                await supervisor.StartServiceAsync(serviceId, ct);
                return Results.NoContent();
            }
        );

        return app;
    }
}