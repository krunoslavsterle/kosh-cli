using FluentValidation;

namespace KoshCLI.Config;

internal class KoshConfigValidator : AbstractValidator<KoshConfig>
{
    public KoshConfigValidator()
    {
        RuleFor(x => x.ProjectName).NotEmpty().WithMessage("Project Name must be defined");

        RuleFor(x => x.Root)
            .Must(r => string.IsNullOrEmpty(r) || Directory.Exists(r))
            .WithMessage("Root path doesn't exist");

        RuleForEach(x => x.Services).SetValidator(x => new ServiceValidator(x.Root!));
    }
}

internal class ServiceValidator : AbstractValidator<ServiceConfig>
{
    public ServiceValidator(string root)
    {
        RuleFor(srv => srv.Name).NotEmpty().WithMessage("Service Name must be defined");

        RuleFor(srv => srv.Type)
            .NotEmpty()
            .WithMessage(srv => $"[{srv.Name}] service type [{srv.Type}] is not defined");

        RuleFor(srv => srv.Type)
            .Must(t => Constants.ServiceTypes.Contains(t))
            .WithMessage(srv => $"[{srv.Name}] service type [{srv.Type}] is not supported");

        RuleFor(srv => srv.Path)
            .Must(p => p != null)
            .WithMessage(srv => $"[{srv.Name}] service path must be defined");
        
        RuleFor(srv => srv.Path)
            .Must(p => p != null && (p.Contains('*') || p.Contains('?') || Directory.Exists(Path.GetFullPath(Path.Combine(root, p!)))))
            .WithName(srv => srv.Name)
            .WithMessage(srv => $"[{srv.Name}] service path doesn't exist");
        
        When(
            service => service.Type == "dotnet-watch",
            () =>
            {
                RuleFor(service => service).SetValidator(new DotnetWatchServiceValidator());
            }
        );

        // TODO: VALIDATE ALL SERVICES.
    }
}

internal class DotnetWatchServiceValidator : AbstractValidator<ServiceConfig>
{
    public DotnetWatchServiceValidator()
    {
        // TODO: VALIDATE
    }
}
