using FluentValidation;
using Pragma.Application.DTOs.Auth;

namespace Pragma.Application.Validators.Auth;

/// <summary>
/// Giriş sorğusu üçün FluentValidation qaydaları.
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email mütləq daxil edilməlidir.")
            .EmailAddress().WithMessage("Düzgün email formatı daxil edin.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifrə mütləq daxil edilməlidir.");
    }
}
