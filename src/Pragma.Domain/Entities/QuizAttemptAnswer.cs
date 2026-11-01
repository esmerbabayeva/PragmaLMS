using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// İmtahan zamanı tələbənin konkret suala verdiyi cavab seçimi.
/// </summary>
public class QuizAttemptAnswer : BaseEntity
{
    public Guid QuizAttemptId { get; set; }
    public QuizAttempt? QuizAttempt { get; set; }

    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }

    public Guid? SelectedOptionId { get; set; }
    public AnswerOption? SelectedOption { get; set; }

    public bool IsCorrect { get; set; } = false;
}
