using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Quiz daxilindəki fərdi sual vahidi.
/// </summary>
public class Question : BaseEntity
{
    public string Text { get; set; } = string.Empty;
    public int Points { get; set; } = 10;
    public int OrderIndex { get; set; } = 0;
    
    /// <summary>
    /// Sualın düzgün izahı (imtahandan sonra tələbəyə göstərilə bilər).
    /// </summary>
    public string? Explanation { get; set; }

    // Quiz əlaqəsi
    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    // Cavab variantları
    public ICollection<AnswerOption> Options { get; set; } = new List<AnswerOption>();
}
