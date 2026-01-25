using FluentResults;
using Kosh.Config.Models;
using Kosh.Core.Definitions;
using Kosh.Core.ValueObjects;

namespace Kosh.Config.Internal;

internal static class GroupBuilder
{
    // TODO: IMPLEMENT WITH #10 - Service Group
    // public static Result ValidateGroupContinuity(IReadOnlyList<YamlService> yaml)
    // {
    //     var lastIndex = new Dictionary<string, int>();
    //     for (int i = 0; i < yaml.Count; i++)
    //     {
    //         var g = yaml[i].Group;
    //         if (g is null) continue;
    //         if (!lastIndex.TryGetValue(g, out var prev))
    //         {
    //             lastIndex[g] = i;
    //             continue;
    //         }
    //
    //         if (i > prev + 1) return Result.Fail($"Services belonging to group '{g}' must be consecutive.");
    //         lastIndex[g] = i;
    //     }
    //
    //     return Result.Ok();
    // }

    public static Result<List<GroupDefinition>> BuildGroups(IReadOnlyList<YamlService> yamlServices, string rootPath)
    {
        var groups = new List<GroupDefinition>();
        
        foreach (var yamlService in yamlServices)
        {
            if (GlobbingExpander.IsGlob(yamlService.Path!))
            {
                var globedServicesResult = GlobbingExpander.Expand(yamlService, rootPath);
                if (globedServicesResult.IsFailed)
                    return globedServicesResult.ToResult();
                
                groups.Add(Create(globedServicesResult.Value, yamlService.Name!));
                continue;
            }
            
            // TODO: UPDATE THIS WITH GROUP IMPLEMENTATION.
            var groupName = yamlService.Name!;
            var serviceDefinitionResult = ServiceBuilder.Create(yamlService, rootPath);
            if (serviceDefinitionResult.IsFailed)
                return serviceDefinitionResult.ToResult();
            
            groups.Add(Create(serviceDefinitionResult.Value, groupName));
        }
        
        return groups;
    }

    private static GroupDefinition Create(List<ServiceDefinition> groupedServices, string  groupName)
    {
        return new GroupDefinition(GroupId.New(), groupName, groupedServices);
    }
    
    private static GroupDefinition Create(ServiceDefinition service, string  groupName)
    {
        return new GroupDefinition(GroupId.New(), groupName, new List<ServiceDefinition>{service});
    }
}