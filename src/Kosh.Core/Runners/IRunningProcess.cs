using Kosh.Core.Events;
using Kosh.Core.ValueObjects;

namespace Kosh.Core.Runners;

public interface IRunningProcess
{
    ServiceId ServiceId { get; }

    IObservable<ProcessLog> Logs { get; }

    Task<int> WaitForExitAsync(CancellationToken ct);

    Task StopAsync(CancellationToken ct);
}