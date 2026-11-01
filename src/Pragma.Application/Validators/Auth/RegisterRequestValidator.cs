using FluentValidation;
using Pragma.Application.DTOs.Auth;
using Pragma.Domain.Enums;

namespace Pragma.Application.Validators.Auth;

/// <summary>
/// Qeydiyyat sorğusu üçün FluentValidation qaydaları.
/// </summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad mütləq daxil edilməlidir.")
            .MaximumLength(50).WithMessage("Ad 50 simvoldan çox ola bilməz.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad mütləq daxil edilməlidir.")
            .MaximumLength(50).WithMessage("Soyad 50 simvoldan çox ola bilməz.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email ünvanı mütləq daxil edilməlidir.")
            .EmailAddress().WithMessage("Düzgün email formatı daxil edin.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifrə mütləq daxil edilməlidir.")
            .MinimumLength(6).WithMessage("Şifrə ən azı 6 simvoldan ibarət olmalıdır.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Təkrar şifrə əsas şifrə ilə eyni olmalıdır.");

        RuleFor(x => x.Role)
            .Must(role => role == UserRoles.Student || role == UserRoles.Instructor)
            .WithMessage("Rol yalnız 'Student' və ya 'Instructor' ola bilər.");
    }
}
