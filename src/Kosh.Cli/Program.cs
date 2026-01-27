using Kosh.Cli.Commands.Example;
using Kosh.Cli.Commands.Init;
using Kosh.Cli.Commands.Start;
using Kosh.Cli.Commands.Version;
using Kosh.Core.Constants;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("kosh");
    
    config
        .AddCommand<InitCommand>("init")
        .WithDescription($"Creates {ConfigConstants.ConfigFile} file in the current directory");
    
    config
        .AddCommand<StartCommand>("start")
        .WithDescription($"Starts all services defined in {ConfigConstants.ConfigFile}");
    
    config
        .AddCommand<ExampleCommand>("example")
        .WithDescription("Shows the example of the koshconfig.yaml");
    
    config.AddCommand<VersionCommand>("version").WithDescription("Shows current kosh version");
});

return app.Run(args);