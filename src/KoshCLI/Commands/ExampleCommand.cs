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
        var yaml = File.ReadAllText(Constants.ExampleConfigFile);

        KoshConsole.Info($"\n\n{yaml}");
        return 0;
    }
}
