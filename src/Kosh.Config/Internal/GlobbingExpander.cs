using FluentResults;
using Kosh.Config.Models;
using Kosh.Core.Definitions;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Kosh.Config.Internal;

internal static class GlobbingExpander
{
    public static bool IsGlob(string path)
    {
        return path.Contains('*') || path.Contains('?');
    }

    public static Result<List<ServiceDefinition>> Expand(YamlService yamlService, string rootDir)
    {
        var results = new List<ServiceDefinition>();

        var matcher = new Matcher();
        var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(rootDir)));
        var projects = result.Files.Select(f => f.Path).ToList();

        foreach (var projectPath in projects)
        {
            var fullPath = Path.Combine(rootDir, projectPath);
            var serviceDefinitionResult = ServiceBuilder.CreateAbsolute(yamlService, fullPath);
            if (serviceDefinitionResult.IsFailed)
                return serviceDefinitionResult.ToResult();

            results.Add(serviceDefinitionResult.Value);
        }

        return results;
    }
}