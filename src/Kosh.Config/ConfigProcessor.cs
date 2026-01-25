using FluentResults;
using Kosh.Config.Internal;
using Kosh.Config.Parsing;
using Kosh.Core.Definitions;

namespace Kosh.Config;

public static class ConfigProcessor
{
    public static Result<ConfigDefinition> Process(string? configPath)
    {
        // 1) Load YAML structure
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
        
        return Result.Ok(new ConfigDefinition(yamlRoot.ProjectName!, groupsResult.Value));
    }
}
