using Kosh.Core.Definitions;
using Kosh.Core.Runtime;
using Kosh.Core.Supervisor;
using Kosh.Core.ValueObjects;

namespace Kosh.Api.Endpoints;

public record StatusResponse(SystemStatusDto SystemStatus, IEnumerable<GroupStatusDto> Groups);

public record SystemStatusDto(
    decimal CpuPercentage,
    decimal MemoryUsage,
    decimal TotalMemory,
    long UptimeSeconds,
    int ServicesStarting,
    int ServicesStopped,
    int ServicesRunning,
    int ServicesFailed
);

public record GroupStatusDto(
    GroupId Id,
    string Name,
    ExecutionMode ExecutionMode,
    string Status,
    bool IsVirtualGroup,
    IReadOnlyList<ServiceStatusDto> Services
);

public record ServiceStatusDto(ServiceId Id, string Name, string Status, DateTime? StartedAt);

public static class GetStatusEndpoint
{
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/status",
            (ISupervisor supervisor) =>
            {
                // var system = await SystemInfoProvider.GetSystemInfoAsync();
                // var services = await ServiceStatusProvider.GetAllStatusesAsync();

                var random = new Random();

                var groups = supervisor
                    .Groups.Values.Select(g => new GroupStatusDto(
                        g.Definition.Id,
                        g.Definition.Name,
                        g.Definition.ExecutionMode,
                        g.Status.ToString(),
                        g.Definition.IsVirtualGroup,
                        g.Services.Select(s => new ServiceStatusDto(
                                s.Definition.Id,
                                s.Definition.Name,
                                s.Status.ToString(),
                                s.StartedAt
                            ))
                            .ToList()
                    ))
                    .ToList();

                var system = new SystemStatusDto(
                    random.Next(5, 30),
                    random.Next(2048, 4092),
                    8192,
                    3600,
                    supervisor.Groups.Values.Sum(g =>
                        g.Services.Count(s => s.Status == ServiceStatus.Starting)
                    ),
                    supervisor.Groups.Values.Sum(g =>
                        g.Services.Count(s => s.Status == ServiceStatus.Stopped)
                    ),
                    supervisor.Groups.Values.Sum(g =>
                        g.Services.Count(s => s.Status == ServiceStatus.Running)
                    ),
                    supervisor.Groups.Values.Sum(g =>
                        g.Services.Count(s => s.Status == ServiceStatus.Failed)
                    )
                );

                return Results.Ok(new StatusResponse(system, groups));
            }
        );

        return app;
    }
}
