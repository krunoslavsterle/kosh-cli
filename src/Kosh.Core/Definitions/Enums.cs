namespace Kosh.Core.Definitions;

public enum ExecutionMode
{
    BlockingUntilExit,
    BlockingUntilReady,
    NonBlocking
}

public enum ConfigType
{
    InitConfig,
    ExampleConfig,
    RealConfig
}

public enum ConfigLogType
{
    None,
    Error,
    All
}