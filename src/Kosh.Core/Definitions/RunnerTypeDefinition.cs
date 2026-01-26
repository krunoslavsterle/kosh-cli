using Kosh.Core.Runners;

namespace Kosh.Core.Definitions;

public sealed record RunnerTypeDefinition(RunnerType Type, ExecutionMode DefaultExecutionMode);