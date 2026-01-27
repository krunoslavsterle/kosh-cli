using FluentResults;
using Kosh.Config.Internal;
using Kosh.Config.Parsing;
using Kosh.Core.Constants;
using Kosh.Core.Definitions;
using Kosh.Core.Helpers;

namespace Kosh.Config;

public static class ConfigProcessor
{
    public static Result<ConfigDefinition> Process(string? configPath)
    {
        var osPlatformResult = SystemHelper.GetOsPlatform();
        if (osPlatformResult.IsFailed)
            return osPlatformResult.ToResult<ConfigDefinition>();

        var yamlResult = ConfigLoader.Load(configPath);
        if (yamlResult.IsFailed)
            return yamlResult.ToResult<ConfigDefinition>();

        var yamlRoot = yamlResult.Value;

        // TODO: SHOULD WE DO THIS HERE OR IN YAML VALIDATOR?
        // var continuity = GroupBuilder.ValidateGroupContinuity(root.Services);
        // if (continuity.IsFailed)
        //     return continuity.ToResult<ConfigDefinition>();

        var groupsResult = GroupBuilder.BuildGroups(yamlRoot.Services, yamlRoot.Root!);
        if (groupsResult.IsFailed)
            return groupsResult.ToResult<ConfigDefinition>();

        var hostsResult = HostsBuilder.BuildHosts(yamlRoot.Hosts);
        if (hostsResult.IsFailed)
            return hostsResult.ToResult<ConfigDefinition>();

        return Result.Ok(new ConfigDefinition(yamlRoot.ProjectName!, yamlRoot.Root, osPlatformResult.Value,
            hostsResult.Value, groupsResult.Value));
    }

    public static Result<string> ReadConfig(string? configPath, ConfigType configType)
    {
        return ConfigLoader.Read(configPath, configType);
    }

    public static Result CreateConfig(string configPath)
    {
        var path = Path.Combine(configPath, ConfigConstants.ConfigFile);

        if (File.Exists(path))
            return Result.Fail($"{ConfigConstants.ConfigFile} already exists.");

        var exeDir = AppContext.BaseDirectory;
        var yaml = File.ReadAllText(Path.Combine(exeDir, ConfigConstants.InitConfigFile));

        File.WriteAllText(path, yaml);
        return Result.Ok();
    }
}