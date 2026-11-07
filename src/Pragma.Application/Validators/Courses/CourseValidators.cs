using FluentValidation;
using Pragma.Application.DTOs.Courses;

namespace Pragma.Application.Validators.Courses;

public class CreateCourseValidator : AbstractValidator<CreateCourseDto>
{
    public CreateCourseValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Kursun adı mütləq daxil edilməlidir.")
            .MaximumLength(200).WithMessage("Kursun adı 200 simvoldan çox ola bilməz.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Kateqoriya mütləq seçilməlidir.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Qiymət mənfi ola bilməz.");
    }
}

public class UpdateCourseValidator : AbstractValidator<UpdateCourseDto>
{
    public UpdateCourseValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Kursun adı mütləq daxil edilməlidir.")
            .MaximumLength(200).WithMessage("Kursun adı 200 simvoldan çox ola bilməz.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Kateqoriya mütləq seçilməlidir.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Qiymət mənfi ola bilməz.");
    }
}
