using System.Diagnostics;
using FluentResults;
using KoshCLI.Commands;
using KoshCLI.Config;
using KoshCLI.Helpers;
using KoshCLI.Terminal;

namespace KoshCLI.Services.Node;

internal class NodeServiceRunner : IServiceRunner
{
    private readonly ServiceConfig _serviceConfig;
    private readonly string _workingDirectory;
    private Process? _process;

    public NodeServiceRunner(ServiceConfig config, string rootDirectory)
    {
        _serviceConfig = config;
        _workingDirectory = Path.GetFullPath(Path.Combine(rootDirectory, _serviceConfig.Path!));
    }

    public bool ShouldStopOnExit { get; private set; }

    public Result Setup()
    {
        return Result.Ok();
    }

    public void Start(CancellationToken cancellationToken)
    {
        var args = BuildArguments(_serviceConfig);

        KoshConsole.Info($"Starting node service [bold][[{_serviceConfig.Name}]][/] ...");

        var psi = new ProcessStartInfo
        {
            FileName = "npm",
            Arguments = $"run {args}",
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.LoadEnvs(_serviceConfig, _workingDirectory);

        _process = new Process { StartInfo = psi };
        _process.SetupConsoleLogs(_serviceConfig, errorLogsByDefault: true);

        try
        {
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            KoshConsole.Success($"Service [bold][[{_serviceConfig.Name}]][/] started.");
        }
        catch (Exception ex)
        {
            KoshConsole.Error(
                $"Failed to start dotnet service {_serviceConfig.Name}: {ex.Message}"
            );
        }
    }

    private static string BuildArguments(ServiceConfig service)
    {
        if (string.IsNullOrEmpty(service.Args))
            return "dev";

        return service.Args;
    }

    public void Dispose()
    {
        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                KoshConsole.Error(
                    $"Failed to stop node process (PID: {_process.Id}): {ex.Message}"
                );
            }
        }

        _process?.Dispose();
        _process = null;
    }
}