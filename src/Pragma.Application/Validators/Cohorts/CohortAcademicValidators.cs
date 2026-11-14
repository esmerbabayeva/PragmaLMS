using FluentValidation;
using Pragma.Application.Contracts;
using Pragma.Domain.Enums;

namespace Pragma.Application.Validators.Cohorts;

/// <summary>
/// Bulk davamiyyət daxiletməsi üçün FluentValidation qaydaları.
/// </summary>
public class BulkAttendanceValidator : AbstractValidator<BulkAttendanceDto>
{
    public BulkAttendanceValidator()
    {
        RuleFor(x => x.Entries)
            .NotEmpty().WithMessage("Davamiyyət siyahısı boş ola bilməz.");

        When(x => x.Entries != null && x.Entries.Count > 0, () =>
        {
            RuleFor(x => x.Entries)
                .Must(entries => entries.Select(e => e.StudentId).Distinct().Count() == entries.Count)
                .WithMessage("Eyni tələbə üçün birdən çox davamiyyət qeydi göndərilə bilməz.");

            RuleForEach(x => x.Entries).ChildRules(entry =>
            {
                entry.RuleFor(e => e.StudentId)
                    .NotEmpty().WithMessage("Tələbə ID-si boş ola bilməz.");

                entry.RuleFor(e => e.Status)
                    .IsInEnum().WithMessage("Davamiyyət statusu keçərli enum dəyəri olmalıdır.");
            });
        });
    }
}

/// <summary>
/// Bulk qiymət daxiletməsi üçün FluentValidation qaydaları.
/// </summary>
public class BulkSubmitGradesValidator : AbstractValidator<BulkSubmitGradesDto>
{
    public BulkSubmitGradesValidator()
    {
        RuleFor(x => x.Grades)
            .NotEmpty().WithMessage("Qiymət siyahısı boş ola bilməz.");

        When(x => x.Grades != null && x.Grades.Count > 0, () =>
        {
            RuleFor(x => x.Grades)
                .Must(grades => grades.Select(g => g.StudentId).Distinct().Count() == grades.Count)
                .WithMessage("Eyni tələbə üçün birdən çox qiymət qeydi göndərilə bilməz.");

            RuleForEach(x => x.Grades).ChildRules(entry =>
            {
                entry.RuleFor(g => g.StudentId)
                    .NotEmpty().WithMessage("Tələbə ID-si boş ola bilməz.");

                entry.When(g => g.MiniProjectScore.HasValue, () =>
                {
                    entry.RuleFor(g => g.MiniProjectScore!.Value)
                        .InclusiveBetween(0m, 100m)
                        .WithMessage("Mini layihə balı 0 ilə 100 arasında olmalıdır.");
                });

                entry.When(g => g.FinalScore.HasValue, () =>
                {
                    entry.RuleFor(g => g.FinalScore!.Value)
                        .InclusiveBetween(0m, 100m)
                        .WithMessage("Final balı 0 ilə 100 arasında olmalıdır.");
                });
            });
        });
    }
}

/// <summary>
/// Fərdi komponent qiyməti (MiniProject, Final) daxiletməsi üçün FluentValidation qaydaları.
/// </summary>
public class SubmitGradeValidator : AbstractValidator<SubmitGradeDto>
{
    public SubmitGradeValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Tələbə ID-si mütləq daxil edilməlidir.");

        RuleFor(x => x.ComponentType)
            .IsInEnum().WithMessage("Komponent növü keçərli olmalıdır.")
            .Must(c => c != GradeComponentType.Quiz)
            .WithMessage("Kviz balı əl ilə daxil edilmir, avtomatik hesablanır.");

        RuleFor(x => x.Score)
            .InclusiveBetween(0m, 100m)
            .WithMessage("Bal 0 ilə 100 arasında olmalıdır.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Qeyd 1000 simvoldan çox ola bilməz.");
    }
}
