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
                serviceRuntimes.Add(new ServiceRuntime(service));
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
            var result = await StartServiceAsync(service.Definition.Id, ct);
            if (result.IsFailed)
            {
                group.Status = GroupStatus.Failed;
                _groupEvents.OnNext(group);
                return result;
            }

            isBlocking = service.Definition.RunnerDefinition.DefaultExecutionMode != ExecutionMode.NonBlocking;

            if (service.Definition.RunnerDefinition.DefaultExecutionMode == ExecutionMode.BlockingUntilExit)
                tasks.Add(_services[service.Definition.Id].Completion.Task);

            if (service.Definition.RunnerDefinition.DefaultExecutionMode == ExecutionMode.BlockingUntilReady)
                tasks.Add(_services[service.Definition.Id].Process!.Ready.Task);
        }

        if (isBlocking)
        {
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
    public async Task<Result> StartServiceAsync(ServiceId serviceId, CancellationToken ct)
    {
        if (!_services.TryGetValue(serviceId, out var runtime))
            return Result.Fail($"Service '{serviceId}' not found.");

        if (runtime.Status == ServiceStatus.Running)
            return Result.Ok();

        runtime.Status = ServiceStatus.Starting;
        _serviceEvents.OnNext(runtime);

        var runnerResult = _runnerFactory.Create(runtime.Definition.RunnerDefinition.Type);
        if (runnerResult.IsFailed)
            return runnerResult.ToResult();

        var processResult = await runnerResult.Value.StartAsync(runtime.Definition, ct);
        if (processResult.IsFailed)
        {
            runtime.Status = ServiceStatus.Failed;
            _serviceEvents.OnNext(runtime);
            return Result.Fail($"Failed to start service '{runtime.Definition.Name}'.");
        }

        var process = processResult.Value;

        runtime.SetProcess(process);
        runtime.Status = ServiceStatus.Running;

        _ = process.Ready.Task.ContinueWith((Task _) =>
        {
            runtime.Status = ServiceStatus.Ready;
            _serviceEvents.OnNext(runtime);
        }, ct);

        _serviceEvents.OnNext(runtime);

        // Subscribe to Service logs.
        process.Logs.Subscribe(log =>
        {
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