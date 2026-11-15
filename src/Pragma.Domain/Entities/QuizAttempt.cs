using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

/// <summary>
/// Tələbənin bir quiz üzrə imtahan cəhdi və nəticəsi.
/// </summary>
public class QuizAttempt : BaseEntity
{
    public Guid StudentId { get; set; }

    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    /// <summary>
    /// Əgər imtahan QuizAssignment vasitəsilə verilibsə.
    /// </summary>
    public Guid? QuizAssignmentId { get; set; }
    public QuizAssignment? QuizAssignment { get; set; }

    public int AttemptNumber { get; set; } = 1;
    public int TotalQuestions { get; set; } = 0;
    public int CorrectAnswers { get; set; } = 0;
    public int EarnedPoints { get; set; } = 0;
    public int TotalPoints { get; set; } = 0;

    public double ScorePercentage { get; set; } = 0.0;
    public bool IsPassed { get; set; } = false;
    public QuizAttemptStatus Status { get; set; } = QuizAttemptStatus.InProgress;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    // Tələbənin verdiyi cavablar
    public ICollection<QuizAttemptAnswer> Answers { get; set; } = new List<QuizAttemptAnswer>();
}
