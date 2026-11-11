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
