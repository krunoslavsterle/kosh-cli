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
        var exeDir = AppContext.BaseDirectory;
        var yaml = File.ReadAllText(Path.Combine(exeDir, Constants.ExampleConfigFile));

        KoshConsole.Info($"\n\n{yaml}");
        return 0;
    }
}
