using KoshCLI;
using KoshCLI.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("kosh");

    config
        .AddCommand<InitCommand>("init")
        .WithDescription($"Creates {Constants.ConfigFile} file in the current directory");

    config
        .AddCommand<StartCommand>("start")
        .WithDescription($"Starts all services defined in {Constants.ConfigFile}");

    config
        .AddCommand<ExampleCommand>("example")
        .WithDescription("Shows the example of the koshconfig.yaml");

    config.AddCommand<VersionCommand>("version").WithDescription("Shows current kosh version");
});

return app.Run(args);
