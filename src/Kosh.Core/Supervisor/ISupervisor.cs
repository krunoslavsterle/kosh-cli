using FluentResults;
using Kosh.Core.Events;
using Kosh.Core.Runtime;
using Kosh.Core.ValueObjects;

namespace Kosh.Core.Supervisor;

public interface ISupervisor
{
    Task<Result> StartAllAsync(CancellationToken ct);
    Task<Result> StartGroupAsync(GroupId groupId, CancellationToken ct);
    Task<Result> StartServiceAsync(ServiceId serviceId, CancellationToken ct);
    Task<Result> StartServiceByNameAsync(string name, CancellationToken ct);
    Task<Result> StopServiceAsync(ServiceId serviceId, CancellationToken ct);

    IReadOnlyDictionary<ServiceId, ServiceRuntime> Services { get; }
    IReadOnlyDictionary<GroupId, GroupRuntime> Groups { get; }

    IObservable<ServiceRuntime> ServiceEvents { get; }
    IObservable<GroupRuntime> GroupEvents { get; }
    IObservable<ServiceLogEvent> ServiceLogs { get; }
}