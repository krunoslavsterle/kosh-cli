using System.Diagnostics;
using System.Reactive.Subjects;
using Kosh.Core.Events;
using Kosh.Core.Runners;
using Kosh.Core.ValueObjects;

namespace Kosh.Runners;

public sealed class RunningProcess : IRunningProcess, IDisposable
{
    private readonly Subject<ProcessLog> _logs = new();
    private readonly Subject<ProcessMetrics> _metrics = new();
    private readonly CancellationTokenSource _metricsCts = new();
    private bool _disposed;

    public ServiceId ServiceId { get; }
    public IObservable<ProcessLog> Logs => _logs;
    public IObservable<ProcessMetrics> Metrics => _metrics;
    public TaskCompletionSource<int> Ready { get; } = new();

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

        _process.Exited += (_, _) =>
        {
            _logs.OnCompleted();
        };

        StartMetricsLoop();
    }

    private async void StartMetricsLoop()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            var lastTime = DateTime.UtcNow;
            var lastTotalProcessorTime = _process.TotalProcessorTime;

            while (await timer.WaitForNextTickAsync(_metricsCts.Token))
            {
                if (_process.HasExited) break;
                
                _process.Refresh();
                
                var currentTime = DateTime.UtcNow;
                var currentTotalProcessorTime = _process.TotalProcessorTime;

                var cpuUsedMs = (currentTotalProcessorTime - lastTotalProcessorTime).TotalMilliseconds;
                var totalMsPassed = (currentTime - lastTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                var cpuPercent = cpuUsageTotal * 100;

                lastTime = currentTime;
                lastTotalProcessorTime = currentTotalProcessorTime;

                _metrics.OnNext(new ProcessMetrics(cpuPercent, _process.WorkingSet64));
            }
        }
        catch
        {
            // Ignore errors (e.g., process exited)
        }
        finally
        {
            _metrics.OnCompleted();
        }
    }

    public async Task<int> WaitForExitAsync(CancellationToken ct)
    {
        try
        {
            await _process.WaitForExitAsync(ct);
            return _process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            return -1;
        }
        finally
        {
            _logs.OnCompleted();
        }
    }

    public Task<int> SetRuntimeReady(CancellationToken ct)
    {
        Ready.TrySetResult(1);
        return Task.FromResult(1);
    }

    public Task StopAsync(CancellationToken ct)
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort
        }
        finally
        {
            _logs.OnCompleted();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _metricsCts.Cancel();
        _metricsCts.Dispose();
        _metrics.Dispose();

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort
        }

        _process.Dispose();
        _logs.Dispose();
    }
}