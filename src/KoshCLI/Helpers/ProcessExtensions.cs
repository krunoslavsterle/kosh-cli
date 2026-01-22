using System.Diagnostics;
using KoshCLI.Commands;
using KoshCLI.Config;
using KoshCLI.Terminal;

namespace KoshCLI.Helpers;

public static class ProcessExtensions
{
    public static void LoadEnvs(this ProcessStartInfo self, ServiceConfig serviceConfig, string serviceDirectory)
    {
        foreach (var env in serviceConfig.Env)
            self.Environment[env.Key] = env.Value;

        var localEnv = EnvHelpers.LoadEnvFile(serviceDirectory);

        foreach (var env in localEnv)
        {
            if (self.Environment.TryGetValue(env.Key, out _))
                continue;

            self.Environment[env.Key] = env.Value;
        }

        if (serviceConfig.InheritEnv)
        {
            foreach (var env in StartCommand.GlobalEnv)
            {
                if (self.Environment.TryGetValue(env.Key, out _))
                    continue;

                self.Environment[env.Key] = env.Value;
            }
        }
    }

    public static void SetupConsoleLogs(this Process self, ServiceConfig serviceConfig, bool errorLogsByDefault = true)
    {
        if (!serviceConfig.Logs)
        {
            if (errorLogsByDefault)
            {
                self.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        KoshConsole.WriteServiceErrorLog(serviceConfig.Name!, e.Data);
                };
            }
            
            return;
        }

        self.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                KoshConsole.WriteServiceLog(serviceConfig.Name!, e.Data);
        };
        
        self.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                KoshConsole.WriteServiceErrorLog(serviceConfig.Name!, e.Data);
        };
    }
}