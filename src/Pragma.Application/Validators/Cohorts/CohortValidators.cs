using FluentValidation;
using Pragma.Application.DTOs.Cohorts;

namespace Pragma.Application.Validators.Cohorts;

public class CreateCohortValidator : AbstractValidator<CreateCohortDto>
{
    public CreateCohortValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Kohort adı mütləq daxil edilməlidir.")
            .MaximumLength(150).WithMessage("Kohort adı 150 simvoldan çox ola bilməz.");

        RuleFor(x => x.CourseId)
            .NotEmpty().WithMessage("Kurs mütləq seçilməlidir.");

        RuleFor(x => x.TeacherId)
            .NotEmpty().WithMessage("Müəllim mütləq təyin edilməlidir.");

        RuleFor(x => x.TotalHours)
            .GreaterThan(0).WithMessage("Ümumi dərs saatı 0-dan böyük olmalıdır.");

        RuleFor(x => x.MaxAbsenceLimit)
            .GreaterThan(0).WithMessage("Maksimum qayıb limiti ən azı 1 olmalıdır.");

        RuleFor(x => x.LateToAbsenceRatio)
            .GreaterThan(0).WithMessage("Gecikmə nisbəti ən azı 1 olmalıdır.");

        RuleFor(x => x)
            .Must(x => (x.QuizWeight + x.MiniProjectWeight + x.FinalWeight) == 100m)
            .WithMessage("Çəkilərin cəmi (Quiz + Mini Layihə + Final) dəqiq 100% olmalıdır.");
    }
}

public class UpdateCohortValidator : AbstractValidator<UpdateCohortDto>
{
    public UpdateCohortValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Kohort adı mütləq daxil edilməlidir.")
            .MaximumLength(150).WithMessage("Kohort adı 150 simvoldan çox ola bilməz.");

        RuleFor(x => x.TeacherId)
            .NotEmpty().WithMessage("Müəllim mütləq təyin edilməlidir.");

        RuleFor(x => x.TotalHours)
            .GreaterThan(0).WithMessage("Ümumi dərs saatı 0-dan böyük olmalıdır.");

        RuleFor(x => x.MaxAbsenceLimit)
            .GreaterThan(0).WithMessage("Maksimum qayıb limiti ən azı 1 olmalıdır.");

        RuleFor(x => x.LateToAbsenceRatio)
            .GreaterThan(0).WithMessage("Gecikmə nisbəti ən azı 1 olmalıdır.");

        RuleFor(x => x)
            .Must(x => (x.QuizWeight + x.MiniProjectWeight + x.FinalWeight) == 100m)
            .WithMessage("Çəkilərin cəmi (Quiz + Mini Layihə + Final) dəqiq 100% olmalıdır.");
    }
}

public class CreateCohortScheduleValidator : AbstractValidator<CreateCohortScheduleDto>
{
    public CreateCohortScheduleValidator()
    {
        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime)
            .WithMessage("Dərsin bitmə vaxtı başlama vaxtından sonra olmalıdır.");
    }
}
