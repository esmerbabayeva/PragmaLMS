using FluentValidation;
using Pragma.Application.DTOs.Lessons;
using Pragma.Domain.Enums;

namespace Pragma.Application.Validators.Lessons;

public class CreateLessonValidator : AbstractValidator<CreateLessonDto>
{
    public CreateLessonValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Dərsin adı mütləq daxil edilməlidir.")
            .MaximumLength(200).WithMessage("Dərsin adı 200 simvoldan çox ola bilməz.");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Sıra nömrəsi mənfi ola bilməz.");

        When(x => x.Type == LessonType.Video, () =>
        {
            RuleFor(x => x.VideoUrl)
                .NotEmpty().WithMessage("Video dərsi üçün Video URL mütləq daxil edilməlidir.");
        });

        When(x => x.Type == LessonType.Markdown, () =>
        {
            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("Mətn dərsi üçün məzmun (Content) mütləq daxil edilməlidir.");
        });
    }
}
