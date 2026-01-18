using System.Security.Cryptography;
using KoshCLI.Config;
using KoshCLI.Terminal;

namespace KoshCLI.Services.Dotnet;

internal sealed class DotnetWatchServiceRunner : DotnetServiceRunnerBase, IServiceRunner
{
    private string? _lastDllHash;
    private bool _isRestarting;
    private Timer? _watchTimer;

    public DotnetWatchServiceRunner(ServiceConfig serviceConfig)
        : base(serviceConfig)
    {
        ShouldStopOnExit = true;
    }

    public bool ShouldStopOnExit { get; private set; }

    public void Start(CancellationToken ct)
    {
        _lastDllHash = ComputeDllHashSafe(MainDllPath);

        Start();

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

            Stop();

            // Short delay for the OS to free up the port
            Thread.Sleep(300);
            Start(false);
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
            var currentHash = ComputeDllHashSafe(MainDllPath);
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
        Stop();
    }
}
