using System.Diagnostics;
using FluentResults;
using KoshCLI.Config;
using KoshCLI.Helpers;
using KoshCLI.Terminal;

namespace KoshCLI.Services.Dotnet;

public abstract class DotnetServiceRunnerBase
{
    protected readonly ServiceConfig ServiceConfig;
    private readonly string _workingDirectory;
    protected string MainDllPath = null!;

    private string _csprojPath = null!;
    private string _projectDirectory = null!;
    private string _targetedFramework = null!;
    private string _outputDirectory = null!;

    private Process? _process;

    protected DotnetServiceRunnerBase(ServiceConfig serviceConfig, string rootDirectory)
    {
        ServiceConfig = serviceConfig;
        _workingDirectory = Path.GetFullPath(Path.Combine(rootDirectory, ServiceConfig.Path!));
    }

    public Result Setup()
    {
        var csprojPathResult = DotnetHelpers.ResolveCsprojPath(_workingDirectory);
        if (csprojPathResult.IsFailed)
            return csprojPathResult.ToResult();

        _csprojPath = csprojPathResult.Value;
        _projectDirectory = Path.GetDirectoryName(_csprojPath)!;

        var targetedFrameworkResult = DotnetHelpers.DetectTargetFramework(_csprojPath);
        if (targetedFrameworkResult.IsFailed)
            return targetedFrameworkResult.ToResult();

        _targetedFramework = targetedFrameworkResult.Value;

        _outputDirectory = DotnetHelpers.ResolveOutputDirectory(
            _projectDirectory,
            _targetedFramework
        );

        MainDllPath = DotnetHelpers.ResolveMainDllPath(_outputDirectory, _csprojPath);

        return Result.Ok();
    }

    protected void Start(bool withBuild = true, bool waitForExit = false)
    {
        var args = $"run --project \"{_csprojPath}\"";

        if (!withBuild)
            args = $"{args} --no-build";

        if (ServiceConfig.Args is not null)
            args = $"{args} {ServiceConfig.Args}";

        StartDotnetProcess(args);

        KoshConsole.WriteServiceLog(
            ServiceConfig.Name!,
            $"dotnet run started (PID: {_process!.Id})"
        );

        if (waitForExit)
            _process.WaitForExit();
    }

    protected void Stop()
    {
        if (_process is { HasExited: false })
        {
            try
            {
                KoshConsole.WriteServiceLog(
                    ServiceConfig.Name!,
                    $"Stopping dotnet run (PID: {_process.Id})..."
                );

                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                KoshConsole.WriteServiceErrorLog(
                    ServiceConfig.Name!,
                    $"Failed to stop process: {ex.Message}"
                );
            }
        }

        _process?.Dispose();
        _process = null;
    }

    private void StartDotnetProcess(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = args,
            WorkingDirectory = _projectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                KoshConsole.WriteServiceLog(ServiceConfig.Name!, e.Data);
        };

        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                KoshConsole.WriteServiceErrorLog(ServiceConfig.Name!, e.Data);
        };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }
}
