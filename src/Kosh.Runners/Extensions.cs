using System.Diagnostics;
using Kosh.Runners.Helpers;

namespace Kosh.Runners;

public static class Extensions
{
    public static void LoadEnvs(this ProcessStartInfo self, IReadOnlyDictionary<string, string> environment, string serviceDirectory)
    {
        foreach (var env in environment)
            self.Environment[env.Key] = env.Value;

        var localEnv = EnvHelper.LoadEnvFile(serviceDirectory);

        foreach (var env in localEnv)
        {
            if (self.Environment.TryGetValue(env.Key, out _))
                continue;

            self.Environment[env.Key] = env.Value;
        }

        // TODO: IMPLEMENT GLOBAL ENV
        // if (serviceConfig.InheritEnv)
        // {
        //     foreach (var env in StartCommand.GlobalEnv)
        //     {
        //         if (self.Environment.TryGetValue(env.Key, out _))
        //             continue;
        //
        //         self.Environment[env.Key] = env.Value;
        //     }
        // }
    }
}