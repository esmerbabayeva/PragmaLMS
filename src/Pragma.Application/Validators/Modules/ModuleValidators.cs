using FluentValidation;
using Pragma.Application.DTOs.Modules;

namespace Pragma.Application.Validators.Modules;

public class CreateModuleValidator : AbstractValidator<CreateModuleDto>
{
    public CreateModuleValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Modulun adı mütləq daxil edilməlidir.")
            .MaximumLength(200).WithMessage("Modulun adı 200 simvoldan çox ola bilməz.");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Sıra nömrəsi mənfi ola bilməz.");
    }
}
