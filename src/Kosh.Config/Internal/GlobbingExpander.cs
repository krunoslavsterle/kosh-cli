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

    public static Result<List<ServiceDefinition>> Expand(YamlService yamlService, string rootDir,
        IReadOnlyDictionary<string, string> globalEnvironment)
    {
        var results = new List<ServiceDefinition>();
        var matcher = new Matcher();

        if (yamlService.Path!.EndsWith(".csproj"))
            matcher.AddInclude(yamlService.Path);
        else
            matcher.AddInclude($"{yamlService.Path}/*.csproj");

        var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(rootDir)));
        var projects = result.Files.Select(f => f.Path).ToList();

        if (projects.Count == 0)
            return Result.Fail($"No projects found matching the glob pattern '{yamlService.Path}'.");

        foreach (var projectPath in projects)
        {
            var fullPath = Path.Combine(rootDir, projectPath);
            var serviceDefinitionResult =
                ServiceBuilder.CreateAbsolute(yamlService, Path.GetDirectoryName(fullPath)!, globalEnvironment);

            if (serviceDefinitionResult.IsFailed)
                return serviceDefinitionResult.ToResult();

            results.Add(serviceDefinitionResult.Value);
        }

        return results;
    }
}