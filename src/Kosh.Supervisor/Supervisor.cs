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

    private readonly Dictionary<string, ServiceId> _serviceNameToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ServiceId> _groupNameToId = new(StringComparer.OrdinalIgnoreCase);


    private readonly Subject<ServiceRuntime> _serviceEvents = new();
    private readonly Subject<GroupRuntime> _groupEvents = new();
    private readonly Subject<ServiceLogEvent> _logEvents = new();

    public IObservable<ServiceRuntime> ServiceEvents => _serviceEvents;
    public IObservable<GroupRuntime> GroupEvents => _groupEvents;
    public IObservable<ServiceLogEvent> LogEvents => _logEvents;

    public Supervisor(ConfigDefinition config, IRunnerFactory runnerFactory)
    {
        _config = config;
        _runnerFactory = runnerFactory;

        // Build runtime state
        foreach (var group in config.Groups)
        {
            var serviceRuntimes = new List<ServiceRuntime>();
            foreach (var service in group.Services)
            {
                var sRuntime = new ServiceRuntime(service);
                _services[service.Id] = sRuntime;
                _serviceNameToId[service.Name] = service.Id;
                serviceRuntimes.Add(new ServiceRuntime(service));
            }

            var groupRuntime = new GroupRuntime(group, serviceRuntimes);
            _groups[group.Id] = groupRuntime;
        }
    }

    // ------------------------------------------------------------
    // Start ALL groups in YAML order
    // ------------------------------------------------------------
    public async Task<Result> StartAllAsync(CancellationToken ct)
    {
        foreach (var group in _config.Groups)
        {
            var result = await StartGroupAsync(group.Id, ct);
            if (result.IsFailed)
                return result;
        }

        return Result.Ok();
    }

    // ------------------------------------------------------------
    // Start a single group (blocking)
    // ------------------------------------------------------------
    public async Task<Result> StartGroupAsync(GroupId groupId, CancellationToken ct)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return Result.Fail($"Group '{groupId}' not found.");

        group.Status = GroupStatus.Running;
        _groupEvents.OnNext(group);

        foreach (var service in group.Services)
        {
            var result = await StartServiceAsync(service.Definition.Id, ct);
            if (result.IsFailed)
            {
                group.Status = GroupStatus.Failed;
                _groupEvents.OnNext(group);
                return result;
            }
        }

        group.Status = GroupStatus.Completed;
        _groupEvents.OnNext(group);

        return Result.Ok();
    }

    // ------------------------------------------------------------
    // Start a single service (blocking)
    // ------------------------------------------------------------
    public async Task<Result> StartServiceAsync(ServiceId serviceId, CancellationToken ct)
    {
        if (!_services.TryGetValue(serviceId, out var runtime))
            return Result.Fail($"Service '{serviceId}' not found.");

        if (runtime.Status == ServiceStatus.Running)
            return Result.Ok(); // already running

        runtime.Status = ServiceStatus.Starting;
        _serviceEvents.OnNext(runtime);

        var runnerResult = _runnerFactory.Create(runtime.Definition.RunnerType);
        if (runnerResult.IsFailed)
            return runnerResult.ToResult();

        var process = await runnerResult.Value.StartAsync(runtime.Definition, ct);

        // TODO: ???
        if (process == null)
        {
            runtime.Status = ServiceStatus.Failed;
            _serviceEvents.OnNext(runtime);
            return Result.Fail($"Failed to start service '{runtime.Definition.Name}'.");
        }

        runtime.SetProcess(process);
        runtime.Status = ServiceStatus.Running;
        _serviceEvents.OnNext(runtime);

        // Subscribe to logs
        process.Logs.Subscribe(log =>
        {
            _logEvents.OnNext(new ServiceLogEvent(runtime.Definition.Id, runtime.Definition.Name, log.Type,
                log.Line));
        });

        // Wait for exit asynchronously
        _ = Task.Run(async () =>
        {
            var exitCode = await process.WaitForExitAsync(ct);

            runtime.Status = exitCode == 0 ? ServiceStatus.Stopped : ServiceStatus.Failed;
            _serviceEvents.OnNext(runtime);
        }, ct);

        return Result.Ok();
    }
}