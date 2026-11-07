using FluentValidation;
using Pragma.Application.DTOs.Progress;

namespace Pragma.Application.Validators.Progress;

public class UpdateVideoPositionRequestValidator : AbstractValidator<UpdateVideoPositionRequestDto>
{
    public UpdateVideoPositionRequestValidator()
    {
        RuleFor(x => x.CurrentPositionSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Video mövqeyi mənfi ola bilməz.");

        RuleFor(x => x.DurationSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Video müddəti mənfi ola bilməz.");
    }
}
