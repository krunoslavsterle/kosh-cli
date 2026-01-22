using System.Diagnostics;
using System.Security.Cryptography;
using FluentResults;
using KoshCLI.Config;
using KoshCLI.Helpers;
using KoshCLI.Terminal;

namespace KoshCLI.Services.Dotnet;

internal class DotnetWatchAltServiceRunner: DotnetServiceRunnerBase, IServiceRunner
{
    private readonly string _rootDirectory;

    private DotnetProjectConfiguration? _projectConfiguration;
    private Process? _process;
    
    private string? _lastDllHash;
    private string? _mainDllPath;
    private bool _isRestarting;
    private Timer? _watchTimer;

    public DotnetWatchAltServiceRunner(ServiceConfig serviceConfig, string rootDirectory)
        : base(serviceConfig)
    {
        _rootDirectory = rootDirectory;
    }
    
    public Result Setup()
    {
        var workingDirectory = Path.GetFullPath(Path.Combine(_rootDirectory, ServiceConfig.Path!));
        var configurationResult = CreateProjectConfiguration(workingDirectory);
        if (configurationResult.IsFailed)
            return configurationResult.ToResult();

        _projectConfiguration = configurationResult.Value;
        _mainDllPath = DotnetHelpers.ResolveMainDllPath(_projectConfiguration.OutputDirectory, _projectConfiguration.CsprojPath);
        
        return Result.Ok();
    }


    public void Start(CancellationToken ct)
    {
        _lastDllHash = ComputeDllHashSafe(_mainDllPath!);

        _process = Run(_projectConfiguration!);

        _watchTimer = new Timer(
            _ => OnWatchTick(),
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1)
        );
    }

    private void RestartProcess()
    {
        if (_isRestarting)
            return;

        _isRestarting = true;

        try
        {
            KoshConsole.WriteServiceLog(
                ServiceConfig.Name!,
                "Change detected, restarting service..."
            );

            Stop(_process!);
            _process = null;

            // Short delay for the OS to free up the port
            Thread.Sleep(300);
            _process = Run(_projectConfiguration!, false);
        }
        finally
        {
            _isRestarting = false;
        }
    }

    private void OnWatchTick()
    {
        try
        {
            var currentHash = ComputeDllHashSafe(_mainDllPath);
            if (currentHash is null)
                return;

            if (_lastDllHash is null)
            {
                _lastDllHash = currentHash;
                return;
            }

            if (!string.Equals(_lastDllHash, currentHash, StringComparison.Ordinal))
            {
                _lastDllHash = currentHash;
                RestartProcess();
            }
        }
        catch (Exception ex)
        {
            KoshConsole.WriteServiceErrorLog(ServiceConfig.Name!, $"Watcher error: {ex.Message}");
        }
    }

    private static string? ComputeDllHashSafe(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        Stop(_process!);
        _process = null;
        _projectConfiguration = null;
        _lastDllHash = null;
        _watchTimer!.Dispose();
    }
}