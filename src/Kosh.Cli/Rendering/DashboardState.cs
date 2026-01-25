using System.Collections.Concurrent;
using Kosh.Core.Runtime;
using Kosh.Core.ValueObjects;

namespace Kosh.Cli.Rendering;

public sealed class DashboardState
{
    public ConcurrentDictionary<ServiceId, ServiceStatus> Statuses { get; } = new();
    public ConcurrentQueue<string> Logs { get; } = new();

    public ServiceId? FocusedService { get; set; }
}
