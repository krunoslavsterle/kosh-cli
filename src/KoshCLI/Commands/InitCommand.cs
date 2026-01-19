using KoshCLI.Terminal;
using Spectre.Console.Cli;

namespace KoshCLI.Commands;

public class InitCommand : Command<InitCommand.InitSettings>
{
    public class InitSettings : CommandSettings { }

    public override int Execute(
        CommandContext context,
        InitSettings settings,
        CancellationToken cancellationToken
    )
    {
        var cwd = Directory.GetCurrentDirectory();
        var path = Path.Combine(cwd, Constants.ConfigFile);

        if (File.Exists(path))
        {
            KoshConsole.Info($"{Constants.ConfigFile} already exists.");
            Environment.Exit(0);
        }

        var exeDir = AppContext.BaseDirectory;
        var yaml = File.ReadAllText(Path.Combine(exeDir, Constants.InitConfigFile));

        File.WriteAllText(path, yaml);

        KoshConsole.Success($"{Constants.ConfigFile} has been created in the current directory");
        return 0;
    }
}
