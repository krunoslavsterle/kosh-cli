using FluentResults;

namespace KoshCLI.Services;

internal interface IServiceRunner : IDisposable
{
    public Result Setup();
    public void Start(CancellationToken cancellationToken);
}
