using FluentValidation;
using Pragma.Application.DTOs.Quizzes;

namespace Pragma.Application.Validators.Quizzes;

public class CreateQuizValidator : AbstractValidator<CreateQuizDto>
{
    public CreateQuizValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Quiz başlığı mütləq daxil edilməlidir.")
            .MaximumLength(200).WithMessage("Quiz başlığı 200 simvoldan çox ola bilməz.");

        RuleFor(x => x.PassingScorePercentage)
            .InclusiveBetween(1, 100).WithMessage("Keçid balı 1% ilə 100% arasında olmalıdır.");

        RuleFor(x => x.MaxAttempts)
            .GreaterThan(0).WithMessage("Maksimum cəhd sayı ən azı 1 olmalıdır.");

        RuleFor(x => x.Questions)
            .NotEmpty().WithMessage("Quiz üçün ən azı 1 sual daxil edilməlidir.");

        RuleForEach(x => x.Questions).ChildRules(q =>
        {
            q.RuleFor(x => x.Text)
                .NotEmpty().WithMessage("Sualın mətni boş ola bilməz.");

            q.RuleFor(x => x.Points)
                .GreaterThan(0).WithMessage("Sualın balı 0-dan böyük olmalıdır.");

            q.RuleFor(x => x.Options)
                .Must(opts => opts != null && opts.Count >= 2)
                .WithMessage("Hər sual üçün ən azı 2 cavab variantı olmalıdır.");

            q.RuleFor(x => x.Options)
                .Must(opts => opts != null && opts.Count(o => o.IsCorrect) >= 1)
                .WithMessage("Hər sualda ən azı 1 düzgün cavab variantı seçilməlidir.");
        });
    }
}

public class CreateQuizTemplateValidator : AbstractValidator<CreateQuizTemplateDto>
{
    public CreateQuizTemplateValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Şablon başlığı mütləq daxil edilməlidir.")
            .MaximumLength(250).WithMessage("Şablon başlığı 250 simvoldan çox ola bilməz.");

        RuleFor(x => x.DefaultPassingScorePercentage)
            .InclusiveBetween(1, 100).WithMessage("Keçid balı 1% ilə 100% arasında olmalıdır.");

        RuleFor(x => x.DefaultMaxAttempts)
            .GreaterThan(0).WithMessage("Maksimum cəhd sayı ən azı 1 olmalıdır.");

        RuleFor(x => x.Questions)
            .NotEmpty().WithMessage("Şablon üçün ən azı 1 sual daxil edilməlidir.");

        RuleForEach(x => x.Questions).ChildRules(q =>
        {
            q.RuleFor(x => x.Text)
                .NotEmpty().WithMessage("Sualın mətni boş ola bilməz.");

            q.RuleFor(x => x.Points)
                .GreaterThan(0).WithMessage("Sualın balı 0-dan böyük olmalıdır.");

            q.RuleFor(x => x.Options)
                .Must(opts => opts != null && opts.Count >= 2)
                .WithMessage("Hər sual üçün ən azı 2 cavab variantı olmalıdır.");

            q.RuleFor(x => x.Options)
                .Must(opts => opts != null && opts.Count(o => o.IsCorrect) >= 1)
                .WithMessage("Hər sualda ən azı 1 düzgün cavab variantı seçilməlidir.");
        });
    }
}

public class CreateQuizAssignmentValidator : AbstractValidator<CreateQuizAssignmentDto>
{
    public CreateQuizAssignmentValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("Quiz şablonu (TemplateId) mütləq seçilməlidir.");

        RuleFor(x => x)
            .Must(x => x.CohortId.HasValue || x.CourseId.HasValue || (x.StudentIds != null && x.StudentIds.Count > 0))
            .WithMessage("Hədəf qrup (CohortId), kurs (CourseId) və ya konkret tələbələr (StudentIds) seçilməlidir.");

        When(x => x.PassingScorePercentage.HasValue, () =>
        {
            RuleFor(x => x.PassingScorePercentage!.Value)
                .InclusiveBetween(1, 100).WithMessage("Keçid balı 1% ilə 100% arasında olmalıdır.");
        });

        When(x => x.MaxAttempts.HasValue, () =>
        {
            RuleFor(x => x.MaxAttempts!.Value)
                .GreaterThan(0).WithMessage("Maksimum cəhd sayı ən azı 1 olmalıdır.");
        });

        When(x => x.StartDateUtc.HasValue && x.EndDateUtc.HasValue, () =>
        {
            RuleFor(x => x)
                .Must(x => x.EndDateUtc > x.StartDateUtc)
                .WithMessage("İmtahanın bitmə tarixi (Deadline) başlama tarixindən sonra olmalıdır.");
        });
    }
}
