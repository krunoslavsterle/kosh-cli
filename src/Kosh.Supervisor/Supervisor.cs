using System.Reactive.Subjects;
using FluentResults;
using Kosh.Core.Definitions;
using Kosh.Core.Events;
using Kosh.Core.Runners;
using Kosh.Core.Runtime;
using Kosh.Core.Supervisor;
using Kosh.Core.ValueObjects;

namespace Kosh.Supervisor;

public sealed class Supervisor : ISupervisor
{
    private readonly ConfigDefinition _config;
    private readonly IRunnerFactory _runnerFactory;

    private readonly Dictionary<ServiceId, ServiceRuntime> _services = new();
    private readonly Dictionary<GroupId, GroupRuntime> _groups = new();

    // TODO: THIS WILL BE NEEDED LATER.
    // private readonly Dictionary<string, ServiceId> _serviceNameToId = new(StringComparer.OrdinalIgnoreCase);
    // private readonly Dictionary<string, ServiceId> _groupNameToId = new(StringComparer.OrdinalIgnoreCase);

    private readonly Subject<ServiceRuntime> _serviceEvents = new();
    private readonly Subject<GroupRuntime> _groupEvents = new();

    private readonly Subject<ServiceLogEvent> _serviceLogs = new();
    private readonly Subject<GroupLogEvent> _groupLogs = new();

    public IObservable<ServiceRuntime> ServiceEvents => _serviceEvents;
    public IObservable<GroupRuntime> GroupEvents => _groupEvents;
    public IObservable<ServiceLogEvent> ServiceLogs => _serviceLogs;
    public IObservable<GroupLogEvent> GroupLogs => _groupLogs;

    public Supervisor(ConfigDefinition config, IRunnerFactory runnerFactory)
    {
        _config = config;
        _runnerFactory = runnerFactory;

        // Build runtime state
        foreach (var group in config.ServiceGroups)
        {
            var serviceRuntimes = new List<ServiceRuntime>();
            foreach (var service in group.Services)
            {
                var sRuntime = new ServiceRuntime(service);
                _services[service.Id] = sRuntime;
                // _serviceNameToId[service.Name] = service.Id;
                serviceRuntimes.Add(sRuntime);
            }

            var groupRuntime = new GroupRuntime(group, serviceRuntimes);
            _groups[group.Id] = groupRuntime;
        }
    }

    // Start all Groups.
    public async Task<Result> StartAllAsync(CancellationToken ct)
    {
        foreach (var group in _config.ServiceGroups)
        {
            var result = await StartGroupAsync(group.Id, ct);
            if (result.IsFailed)
                return result;
        }

        return Result.Ok();
    }

    public async Task<Result> StopAllAsync(CancellationToken ct)
    {
        foreach (var group in _groups.Values)
        {
            group.Status = GroupStatus.Completed;
            _groupEvents.OnNext(group);
        }

        foreach (var runtime in _services.Values)
        {
            if (runtime.Process != null)
            {
                runtime.Status = ServiceStatus.Stopped;
                _serviceEvents.OnNext(runtime);
                await runtime.Process.StopAsync(ct);
            }
        }

        return Result.Ok();
    }

    public async Task<Result> StopServiceAsync(ServiceId serviceId, CancellationToken ct)
    {
        if (!_services.TryGetValue(serviceId, out var runtime))
            return Result.Fail($"Service '{serviceId}' not found.");

        if (runtime.Process != null && runtime.Status is ServiceStatus.Running or ServiceStatus.Ready or ServiceStatus.Starting)
        {
            runtime.Status = ServiceStatus.Stopped;
            _serviceEvents.OnNext(runtime);
            await runtime.Process.StopAsync(ct);
        }

        return Result.Ok();
    }

    // Start a single Group and handles ExecutionMode.
    public async Task<Result> StartGroupAsync(GroupId groupId, CancellationToken ct)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return Result.Fail($"Group '{groupId}' not found.");

        group.Status = GroupStatus.Running;
        _groupEvents.OnNext(group);

        var tasks = new List<Task>();
        var isBlocking = false;

        foreach (var service in group.Services)
        {
            if (service.Definition.ManualStart)
            {
                _serviceEvents.OnNext(_services[service.Definition.Id]);
                continue;
            }

            var result = await StartServiceAsync(service.Definition.Id, ct);
            if (result.IsFailed)
            {
                group.Status = GroupStatus.Failed;
                _groupEvents.OnNext(group);
                return result;
            }

            isBlocking |= service.Definition.RunnerDefinition.DefaultExecutionMode != ExecutionMode.NonBlocking;

            if (service.Definition.RunnerDefinition.DefaultExecutionMode == ExecutionMode.BlockingUntilExit)
                tasks.Add(_services[service.Definition.Id].Completion.Task);

            if (service.Definition.RunnerDefinition.DefaultExecutionMode == ExecutionMode.BlockingUntilReady)
                tasks.Add(_services[service.Definition.Id].Process!.Ready.Task);
        }

        if (isBlocking)
        {
            if (!group.Definition.IsVirtualGroup)
                _groupLogs.OnNext(new GroupLogEvent(group.Definition.Id, group.Definition.Name, LogType.Info,
                    "Waiting Group to finish"));

            await Task.WhenAll(tasks);

            group.Status = GroupStatus.Completed;
            _groupEvents.OnNext(group);
        }
        else
        {
            group.Status = GroupStatus.Running;
            _groupEvents.OnNext(group);
        }

        return Result.Ok();
    }


    // Start a single Service in BLOCKING mode.
    public async Task<Result> StartServiceAsync(ServiceId serviceId, CancellationToken ct, string? argsOverride = null)
    {
        if (!_services.TryGetValue(serviceId, out var runtime))
            return Result.Fail($"Service '{serviceId}' not found.");

        if (runtime.Status is ServiceStatus.Running or ServiceStatus.Ready or ServiceStatus.Starting)
            return Result.Ok();

        runtime.ResetCompletion();
        runtime.Status = ServiceStatus.Starting;
        _serviceEvents.OnNext(runtime);

        var runnerResult = _runnerFactory.Create(runtime.Definition.RunnerDefinition.Type);
        if (runnerResult.IsFailed)
            return runnerResult.ToResult();

        var definitionToRun = runtime.Definition;
        if (!string.IsNullOrWhiteSpace(argsOverride))
        {
            var combinedArgs = string.IsNullOrWhiteSpace(definitionToRun.Args)
                ? argsOverride
                : $"{definitionToRun.Args} {argsOverride}";
            definitionToRun = definitionToRun with { Args = combinedArgs };
        }

        var processResult = await runnerResult.Value.StartAsync(definitionToRun, ct);
        if (processResult.IsFailed)
        {
            runtime.Status = ServiceStatus.Failed;
            _serviceEvents.OnNext(runtime);
            return Result.Fail($"Failed to start service '{runtime.Definition.Name}'.");
        }

        var process = processResult.Value;

        runtime.SetProcess(process);
        runtime.Status = ServiceStatus.Running;
        
        process.Metrics.Subscribe(metrics => 
        {
            runtime.Metrics = metrics;
        });

        _ = process.Ready.Task.ContinueWith((Task _) =>
        {
            runtime.Status = ServiceStatus.Ready;
            _serviceEvents.OnNext(runtime);
        }, ct);

        _serviceEvents.OnNext(runtime);

        // Subscribe to Service logs.
        process.Logs.Subscribe(log =>
        {
            if (runtime.Definition.ConfigLogType == ConfigLogType.None)
                return;

            if (runtime.Definition.ConfigLogType == ConfigLogType.Error && log.Type != LogType.Error)
                return;

            _serviceLogs.OnNext(new ServiceLogEvent(runtime.Definition.Id, runtime.Definition.Name, log.Type,
                log.Line));
        });

        // Wait for exit asynchronously
        _ = Task.Run(async () =>
        {
            var exitCode = await process.WaitForExitAsync(ct);

            runtime.Status = exitCode == 0 ? ServiceStatus.Stopped : ServiceStatus.Failed;
            runtime.Completion.TrySetResult(exitCode);

            _serviceEvents.OnNext(runtime);
        }, ct);

        return Result.Ok();
    }
}