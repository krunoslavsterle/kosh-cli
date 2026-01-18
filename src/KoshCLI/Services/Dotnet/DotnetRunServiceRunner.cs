using KoshCLI.Config;
using KoshCLI.Terminal;

namespace KoshCLI.Services.Dotnet;

public class DotnetRunServiceRunner : DotnetServiceRunnerBase, IServiceRunner
{
    public DotnetRunServiceRunner(ServiceConfig serviceConfig, string rootDirectory)
        : base(serviceConfig, rootDirectory)
    {
        ShouldStopOnExit = false;
    }

    public bool ShouldStopOnExit { get; private set; }

    public void Start(CancellationToken ct)
    {
        Start(withBuild: true, waitForExit: true);
    }

    public void Dispose() { }
}
