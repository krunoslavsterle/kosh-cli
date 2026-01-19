using KoshCLI.Terminal;
using Spectre.Console.Cli;

namespace KoshCLI.Commands;

public class ExampleCommand : Command<ExampleCommand.ExampleSettings>
{
    public class ExampleSettings : CommandSettings { }

    public override int Execute(
        CommandContext context,
        ExampleSettings settings,
        CancellationToken cancellationToken
    )
    {
        var configPath = Path.GetFullPath(Constants.ExampleConfigFile);
        var yaml = File.ReadAllText(configPath);

        KoshConsole.Info($"\n\n{yaml}");
        return 0;
    }
}
