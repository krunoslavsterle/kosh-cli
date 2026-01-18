using FluentResults;

namespace KoshCLI.Services;

internal interface IServiceRunner : IDisposable
{
    public bool ShouldStopOnExit { get; }

    public Result Setup();
    public void Start(CancellationToken cancellationToken);
}
