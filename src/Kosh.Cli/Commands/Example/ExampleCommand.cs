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
        var yamlResult = ConfigProcessor.ReadConfig(exeDir, ConfigType.ExampleConfig);

        if (yamlResult.IsFailed)
        {
            KoshConsole.Error(yamlResult.Errors[0].Message);
            return 1;
        }

        KoshConsole.Info($"\n\n{yamlResult.Value}");
        return 0;
    }
}