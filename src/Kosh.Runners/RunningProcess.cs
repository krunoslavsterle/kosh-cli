using System.Diagnostics;
using System.Reactive.Subjects;
using Kosh.Core.Events;
using Kosh.Core.Runners;
using Kosh.Core.ValueObjects;

namespace Kosh.Runners;

public sealed class RunningProcess : IRunningProcess
{
    private readonly Subject<ProcessLog> _logs = new();

    public ServiceId ServiceId { get; }
    public IObservable<ProcessLog> Logs => _logs;

    private readonly Process _process;

    public RunningProcess(ServiceId id, Process process)
    {
        ServiceId = id;
        _process = process;

        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                _logs.OnNext(new ProcessLog(LogType.Info, e.Data));
        };

        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                _logs.OnNext(new ProcessLog(LogType.Error, e.Data));
        };
    }

    public Task<int> WaitForExitAsync(CancellationToken ct)
    {
        return Task.Run(() =>
        {
            _process.WaitForExit();
            return _process.ExitCode;
        }, ct);
    }

    public Task StopAsync(CancellationToken ct)
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(true);
        }
        catch
        {
            // TODO: HANDLE THIS.
        }

        return Task.CompletedTask;
    }
}