using KoshCLI.Config;

namespace KoshCLI.System;

internal class SystemCommandsValidationResult
{
    public bool DockerValid { get; set; } = true;
    public bool DockerComposeValid { get; set; } = true;
    public bool ProxyValid { get; set; } = true;

    public bool IsValid => DockerValid && DockerComposeValid && ProxyValid;
}

internal static class SystemCommandsValidator
{
    public static SystemCommandsValidationResult ValidateConfig(KoshConfig config)
    {
        var result = new SystemCommandsValidationResult();

        if (config.Docker is not null)
        {
            result.DockerValid = SystemCommandChecker.Exists("docker");
            result.DockerComposeValid = SystemCommandChecker.Exists("docker", "compose version");
        }

        if (config.Proxy is not null)
        {
            result.ProxyValid =
                config.Proxy == "caddy" && SystemCommandChecker.Exists("caddy", "version");
        }

        return result;
    }
}
