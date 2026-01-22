using KoshCLI.Config;
using KoshCLI.Terminal;

namespace KoshCLI.Services;

internal static class ServiceExecutionManager
{
    private static readonly List<IServiceRunner> RunningServices = [];

    public static void StartAll(List<ServiceConfig> services, string rootDirectory)
    {
        if (services.Count == 0)
        {
            KoshConsole.Info($"No services defined in {Constants.ConfigFile}.");
            return;
        }

        foreach (var service in services)
        {
            if (service.Type is null || service.Name is null)
            {
                KoshConsole.Error(
                    "Invalid service entry in config (missing name or type). Skipping."
                );
                continue;
            }

            var serviceRunnerResult = ServiceRunnerFactory.Create(service, rootDirectory);
            if (serviceRunnerResult.IsFailed)
            {
                KoshConsole.Error(serviceRunnerResult.Errors.First().Message);
                StopAll();
                return;
            }

            var serviceRunner = serviceRunnerResult.Value;

            var setupResult = serviceRunner.Setup();
            if (setupResult.IsFailed)
            {
                KoshConsole.Error(setupResult.Errors.First().Message);
                StopAll();
                return;
            }

            serviceRunner.Start(CancellationToken.None); // TODO: IMPLEMENT CANCELLATION TOKEN.
            RunningServices.Add(serviceRunner);
        }
    }

    public static void StopAll()
    {
        foreach (var service in RunningServices)
        {
            service.Dispose();
        }
    }
}