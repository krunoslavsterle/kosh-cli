using System.Diagnostics;
using Kosh.Core.Definitions;
using Kosh.Core.Helpers;

namespace Kosh.Runners;

public static class Extensions
{
    public static void LoadEnvs(this ProcessStartInfo self, ServiceDefinition service)
    {
        foreach (var env in service.Environment)
            self.Environment[env.Key] = env.Value;

        var localEnv = EnvHelper.LoadEnvFile(service.WorkingDirectory);
        foreach (var env in localEnv)
        {
            if (self.Environment.TryGetValue(env.Key, out _))
                continue;

            self.Environment[env.Key] = env.Value;
        }

        if (service.InheritEnv)
        {
            foreach (var env in service.GlobalEnvironment)
            {
                if (self.Environment.TryGetValue(env.Key, out _))
                    continue;

                self.Environment[env.Key] = env.Value;
            }
        }
    }

    public static IEnumerable<string> ToSplitArgs(this string? self)
    {
        if (string.IsNullOrWhiteSpace(self))
            return [];

        return self.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}