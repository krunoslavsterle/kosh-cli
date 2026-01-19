using System.Diagnostics;
using FluentResults;
using KoshCLI.Config;
using KoshCLI.Terminal;

namespace KoshCLI.Services.Proxy;

internal class CaddyServiceRunner : IServiceRunner
{
    private readonly ServiceConfig _serviceConfig;
    private readonly string _workingDirectory;
    private Process? _process;

    public CaddyServiceRunner(ServiceConfig config, string rootDirectory)
    {
        _serviceConfig = config;
        _workingDirectory = Path.GetFullPath(Path.Combine(rootDirectory, _serviceConfig.Path!));
        ShouldStopOnExit = true;
    }

    public bool ShouldStopOnExit { get; }

    public Result Setup()
    {
        return Result.Ok();
    }

    public void Start(CancellationToken cancellationToken)
    {
        var args = BuildArguments(_serviceConfig);

        KoshConsole.Info($"Starting caddy service [bold][[{_serviceConfig.Name}]][/] ...");

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "caddy",
                Arguments = args,
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        foreach (var kv in _serviceConfig.Env)
            _process.StartInfo.Environment[kv.Key] = kv.Value;

        if (!_serviceConfig.Logs.HasValue || _serviceConfig.Logs.Value)
        {
            _process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    KoshConsole.WriteServiceLog(_serviceConfig.Name!, e.Data);
            };

            _process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    KoshConsole.WriteServiceErrorLog(_serviceConfig.Name!, e.Data);
            };
        }

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

    public void Dispose()
    {
        if (_process is { HasExited: false })
            try
            {
                _process.Kill(true);
                _process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                KoshConsole.Error(
                    $"Failed to stop node process (PID: {_process.Id}): {ex.Message}"
                );
            }

        _process?.Dispose();
        _process = null;
    }

    private static string BuildArguments(ServiceConfig service)
    {
        const string baseArgs = "run";
        return $"{baseArgs} {service.Args}";
    }
}