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

public class SetCohortFinalDeadlineValidator : AbstractValidator<SetCohortFinalDeadlineDto>
{
    public SetCohortFinalDeadlineValidator()
    {
        RuleFor(x => x.Date)
            .Must(d => string.IsNullOrWhiteSpace(d) || System.DateOnly.TryParseExact(d.Trim(), "yyyy-MM-dd", out _))
            .WithMessage("Tarix 'yyyy-MM-dd' formatında olmalıdır.");

        RuleFor(x => x.Time)
            .Must(t => string.IsNullOrWhiteSpace(t) || System.TimeOnly.TryParseExact(t.Trim(), "HH:mm", out _) || System.TimeOnly.TryParseExact(t.Trim(), "HH:mm:ss", out _))
            .WithMessage("Saat 'HH:mm' formatında olmalıdır.");
    }
}

public class SubmitFinalProjectValidator : AbstractValidator<SubmitFinalProjectDto>
{
    public SubmitFinalProjectValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Final layihə linki mütləq daxil edilməlidir.")
            .MaximumLength(500).WithMessage("Link 500 simvoldan çox ola bilməz.")
            .Must(u => !string.IsNullOrWhiteSpace(u) && !u.Contains(" "))
            .WithMessage("Linkdə boşluq ola bilməz.")
            .Must(u =>
            {
                if (string.IsNullOrWhiteSpace(u)) return false;
                if (!System.Uri.TryCreate(u.Trim(), System.UriKind.Absolute, out var uri)) return false;
                var scheme = uri.Scheme.ToLowerInvariant();
                if (scheme != "http" && scheme != "https") return false;
                if (scheme == "javascript" || scheme == "data" || scheme == "vbscript") return false;
                return true;
            })
            .WithMessage("Link mütləq etibarlı http və ya https formatında olmalıdır.");
    }
}
