using Kosh.Cli.Rendering;
using Kosh.Config;
using Kosh.Core.Definitions;
using Spectre.Console.Cli;

namespace Kosh.Cli.Commands.Example;

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
        var yaml = ConfigProcessor.ReadConfig(exeDir, ConfigType.ExampleConfig);

        KoshConsole.Info($"\n\n{yaml}");
        return 0;
    }
}