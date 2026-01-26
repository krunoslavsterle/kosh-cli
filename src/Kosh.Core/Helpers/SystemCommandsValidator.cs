using Kosh.Core.Definitions;
using Kosh.Core.Runners;

namespace Kosh.Core.Helpers;

public class SystemCommandsValidationResult
{
    public bool DockerValid { get; set; } = true;
    public bool DockerComposeValid { get; set; } = true;
    public bool ProxyValid { get; set; } = true;

    public bool IsValid => DockerValid && DockerComposeValid && ProxyValid;
}

public static class SystemCommandsValidator
{
    public static SystemCommandsValidationResult ValidateConfig(ConfigDefinition config)
    {
        var result = new SystemCommandsValidationResult();
        var runnerTypes = config.Groups
            .SelectMany(g => g.Services.Select(s => s.RunnerDefinition.Type))
            .Distinct()
            .ToList();

        if (runnerTypes.Any(type => type == RunnerType.DockerCompose))
        {
            result.DockerValid = SystemHelper.CheckSystemCommandExistence("docker");
            result.DockerComposeValid = SystemHelper.CheckSystemCommandExistence("docker", "compose version");
        }

        if (runnerTypes.Any(type => type == RunnerType.Caddy))
        {
            result.ProxyValid = SystemHelper.CheckSystemCommandExistence("caddy", "version");
        }

        return result;
    }
}