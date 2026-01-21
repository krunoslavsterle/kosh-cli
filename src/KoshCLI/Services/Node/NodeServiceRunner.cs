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
        ShouldStopOnExit = true;
    }

    public bool ShouldStopOnExit { get; private set; }

    public Result Setup()
    {
        return Result.Ok();
    }

    public void Start(CancellationToken cancellationToken)
    {
        var args = BuildArguments(_serviceConfig);
        var localEnv = EnvHelpers.LoadEnvFile(_workingDirectory);

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

        foreach (var env in _serviceConfig.Env)
            psi.Environment[env.Key] = env.Value;

        foreach (var env in localEnv)
            psi.Environment[env.Key] = env.Value;

        if (_serviceConfig.InheritRootEnv)
        {
            foreach (var env in StartCommand.GlobalEnv)
                psi.Environment[env.Key] = env.Value;
        }

        _process = new Process { StartInfo = psi };

        foreach (var kv in _serviceConfig.Env)
            _process.StartInfo.Environment[kv.Key] = kv.Value;

        if (_serviceConfig.Logs)
        {
            _process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    KoshConsole.WriteServiceLog(_serviceConfig.Name!, e.Data);
            };
        }

        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                KoshConsole.WriteServiceErrorLog(_serviceConfig.Name!, e.Data);
        };

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
