using FluentResults;

namespace Kosh.Core.Runners;

public interface IRunnerFactory
{
    Result<IRunner> Create(RunnerType type);
}
