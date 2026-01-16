using FluentValidation;

namespace KoshCLI.Config;

internal class KoshConfigValidator : AbstractValidator<KoshConfig>
{
    public KoshConfigValidator()
    {
        RuleFor(x => x.ProjectName).NotEmpty().WithMessage("Project Name must be defined");
        RuleFor(x => x.Proxy)
            .Must(v => v is null or "caddy")
            .WithMessage("Only caddy Proxy is supported at the moment");
    }
}
