using Kosh.Core.Events;
using Kosh.Core.ValueObjects;

namespace Kosh.Core.Runners;

public interface IRunningProcess
{
    ServiceId ServiceId { get; }

    IObservable<ProcessLog> Logs { get; }
    TaskCompletionSource<int> Ready { get; }

    Task<int> WaitForExitAsync(CancellationToken ct);
    Task<int> SetRuntimeReady(CancellationToken ct);

    Task StopAsync(CancellationToken ct);
}