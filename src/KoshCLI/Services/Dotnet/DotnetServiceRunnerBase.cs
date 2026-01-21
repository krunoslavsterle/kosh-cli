using System.Diagnostics;
using FluentResults;
using KoshCLI.Config;
using KoshCLI.Helpers;
using KoshCLI.Terminal;

namespace KoshCLI.Services.Dotnet;

internal record DotnetProjectConfiguration(
    string ProjectDirectory,
    string CsprojPath,
    string TargetedFramework,
    string OutputDirectory
);

internal abstract class DotnetServiceRunnerBase
{
    protected readonly ServiceConfig ServiceConfig;
    private static string? _dotnetRoot;

    protected DotnetServiceRunnerBase(ServiceConfig serviceConfig)
    {
        ServiceConfig = serviceConfig;

        if (!OperatingSystem.IsWindows() && _dotnetRoot == null)
        {
            _dotnetRoot = GetDotnetRoot();
        }
    }

    public bool ShouldStopOnExit { get; protected set; }

    protected Process Run(DotnetProjectConfiguration projectConfiguration, bool withBuild = true)
    {
        var args = $"run --project {projectConfiguration.CsprojPath}";

        if (!withBuild)
            args = $"{args} --no-build";

        if (ServiceConfig.Args is not null)
            args = $"{args} {ServiceConfig.Args}";

        var process = StartDotnetProcess(projectConfiguration.ProjectDirectory, args);

        KoshConsole.WriteServiceLog(
            ServiceConfig.Name!,
            $"dotnet run started (PID: {process!.Id})"
        );

        return process;
    }

    protected Process Watch(DotnetProjectConfiguration projectConfiguration)
    {
        var args = $"watch --project \"{projectConfiguration.CsprojPath}\"";

        if (ServiceConfig.Args is not null)
            args = $"{args} {ServiceConfig.Args}";

        var process = StartDotnetProcess(projectConfiguration.ProjectDirectory, args);

        KoshConsole.WriteServiceLog(
            ServiceConfig.Name!,
            $"dotnet watch started (PID: {process!.Id})"
        );

        return process;
    }

    protected void Stop(Process process)
    {
        if (process is { HasExited: false })
            try
            {
                KoshConsole.WriteServiceLog(
                    ServiceConfig.Name!,
                    $"Stopping dotnet run (PID: {process.Id})..."
                );

                process.Kill(true);
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                KoshConsole.WriteServiceErrorLog(
                    ServiceConfig.Name!,
                    $"Failed to stop process: {ex.Message}"
                );
            }

        process.Dispose();
    }

    protected Result<DotnetProjectConfiguration> CreateProjectConfiguration(string path)
    {
        var csprojPathResult = DotnetHelpers.ResolveCsprojPath(path);
        if (csprojPathResult.IsFailed)
            return csprojPathResult.ToResult();

        var csprojPath = csprojPathResult.Value;
        var projectDirectory = Path.GetDirectoryName(csprojPath)!;

        var targetedFrameworkResult = DotnetHelpers.DetectTargetFramework(csprojPath);
        if (targetedFrameworkResult.IsFailed)
            return targetedFrameworkResult.ToResult();

        var targetedFramework = targetedFrameworkResult.Value;

        var outputDirectory = DotnetHelpers.ResolveOutputDirectory(
            projectDirectory,
            targetedFramework
        );

        return new DotnetProjectConfiguration(
            projectDirectory,
            csprojPath,
            targetedFramework,
            outputDirectory
        );
    }

    private Process StartDotnetProcess(string projectDirectory, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!OperatingSystem.IsWindows())
        {
            psi.Environment["DOTNET_ROOT"] = GetDotnetRoot();
            psi.Environment["PATH"] =
                psi.Environment["DOTNET_ROOT"] + ":" + Environment.GetEnvironmentVariable("PATH");
        }

        foreach (var env in ServiceConfig.Env)
        {
            psi.Environment[env.Key] = env.Value;
        }

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        if (ServiceConfig.ShouldLog)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    KoshConsole.WriteServiceLog(ServiceConfig.Name!, e.Data);
            };
        }

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                KoshConsole.WriteServiceErrorLog(ServiceConfig.Name!, e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private static string GetDotnetRoot()
    {
        if (_dotnetRoot != null)
            return _dotnetRoot;

        var dotnetPath = Process
            .Start(
                new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "dotnet",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                }
            )!
            .StandardOutput.ReadToEnd()
            .Trim();

        _dotnetRoot = Path.GetDirectoryName(Path.GetDirectoryName(dotnetPath))!;

        return _dotnetRoot;
    }
}
