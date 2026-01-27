using System.ComponentModel;
using Kosh.Cli.Rendering;
using Kosh.Config;
using Kosh.Core.Constants;
using Spectre.Console.Cli;

namespace Kosh.Cli.Commands.Init;

public class InitCommand : Command<InitCommand.InitSettings>
{
    public class InitSettings : CommandSettings
    {
        [CommandOption("-c|--config <PATH>")]
        [Description($"Optional path to a custom {ConfigConstants.ConfigFile}")]
        public string? ConfigPath { get; set; }
    }

    public override int Execute(
        CommandContext context,
        InitSettings settings,
        CancellationToken cancellationToken
    )
    {
        var cwd = Directory.GetCurrentDirectory();
        var createResult = ConfigProcessor.CreateConfig(settings.ConfigPath ?? cwd);

        if (createResult.IsFailed)
        {
            KoshConsole.Info(createResult.Errors.First().Message);
            return 0;
        }

        KoshConsole.Success($"{ConfigConstants.ConfigFile} has been created in the current directory");
        return 0;
    }
}