using FluentValidation;
using Pragma.Application.DTOs.Notes;

namespace Pragma.Application.Validators.Notes;

public class CreateLessonNoteRequestValidator : AbstractValidator<CreateLessonNoteRequestDto>
{
    public CreateLessonNoteRequestValidator()
    {
        RuleFor(x => x.PositionSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Zaman mövqeyi mənfi ola bilməz.");

        RuleFor(x => x.NoteText)
            .NotEmpty().WithMessage("Qeyd mətni boş ola bilməz.")
            .MaximumLength(2000).WithMessage("Qeyd maksimum 2000 simvol ola bilər.");
    }
}
