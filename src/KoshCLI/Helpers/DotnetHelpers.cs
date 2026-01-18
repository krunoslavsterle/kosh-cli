using System.Xml.Linq;
using FluentResults;

namespace KoshCLI.Helpers;

public static class DotnetHelpers
{
    public static string ResolveMainDllPath(string outputDirectory, string projectPath)
    {
        var assemblyName =
            TryReadAssemblyNameFromCsproj(projectPath)
            ?? Path.GetFileNameWithoutExtension(projectPath);

        return Path.Combine(outputDirectory, $"{assemblyName}.dll");

        // TODO: CHECK IF WE NEED TO WAIT FOR THE DLL.
        // if (File.Exists(projectDll))
        //     return projectDll;
        //
        // return Result.Fail($"Project [{projectDll}] file not detected in [{outputDirectory}]");

        // KoshConsole.WriteServiceLog(
        //     projectName,
        //     $"Waiting for {projectDll} to appear in {outputDirectory}..."
        // );
        //
        // var timeout = TimeSpan.FromSeconds(30);
        // var start = DateTime.UtcNow;
        //
        // // Wait for the DLL.
        // while (DateTime.UtcNow - start < timeout && !cancellationToken.IsCancellationRequested)
        // {
        //     if (File.Exists(projectDll))
        //     {
        //         KoshConsole.WriteServiceLog(
        //             projectName,
        //             $"{projectDll} resolved (late): {projectDll}"
        //         );
        //
        //         return projectDll;
        //     }
        //     await Task.Delay(500, cancellationToken);
        // }
        //
        // return Result.Fail($"Project [{projectDll}] file not detected in [{outputDirectory}]");
    }

    public static Result<string> ResolveCsprojPath(string projectPath)
    {
        if (File.Exists(projectPath) && projectPath.EndsWith(".csproj"))
            return projectPath;

        if (!Directory.Exists((projectPath)))
            return Result.Fail($"Project directory [{projectPath}] doesn't exist");

        var csprojs = Directory.GetFiles(projectPath, "*.csproj", SearchOption.TopDirectoryOnly);

        if (csprojs.Length == 0)
            return Result.Fail($"No .csproj found in directory: {projectPath}");

        if (csprojs.Length > 1)
            return Result.Fail($"Multiple .csproj files found in directory: {projectPath}");

        return csprojs[0];
    }

    public static string ResolveOutputDirectory(string projectDirectory, string targetedFramework)
    {
        // TODO: IMPLEMENT OVERRIDE IN .koshconfig
        return Path.GetFullPath(Path.Combine(projectDirectory, "bin", "Debug", targetedFramework));
    }

    public static Result<string> DetectTargetFramework(string csprojPath)
    {
        if (!File.Exists(csprojPath))
            return Result.Fail($"The .scproj file doesn't exist on path: {csprojPath}");

        var doc = XDocument.Load(csprojPath);
        var ns = doc.Root!.Name.Namespace;

        // Check <TargetFramework> tag.
        var tf = doc.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(tf))
            return tf;

        // Check <TargetFrameworks> tag.
        var tfs = doc.Descendants(ns + "TargetFrameworks").FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(tfs))
            return Result.Fail("Not possible to determine Target Framework for this project");

        var frameworks = tfs.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .ToList();

        var firstFramework = frameworks.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f));
        if (firstFramework is null)
            return Result.Fail("Not possible to determine Target Framework for this project");

        return firstFramework;
    }

    private static string? TryReadAssemblyNameFromCsproj(string csprojPath)
    {
        if (!File.Exists(csprojPath))
            return null;

        try
        {
            var doc = XDocument.Load(csprojPath);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            var name = doc.Descendants(ns + "AssemblyName").FirstOrDefault()?.Value;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }
}
