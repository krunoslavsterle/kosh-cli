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

    IObservable<ServiceRuntime> ServiceEvents { get; }
    IObservable<GroupRuntime> GroupEvents { get; }
    IObservable<ServiceLogEvent> ServiceLogs { get; }
}
