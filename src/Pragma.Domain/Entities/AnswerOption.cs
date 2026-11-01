using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Suala aid variant. 
/// TƏHLÜKƏSİZLİK: IsCorrect sahəsi imtahan zamanı heç vaxt frontenda göndərilmir.
/// </summary>
public class AnswerOption : BaseEntity
{
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; } = false;

    // Sual əlaqəsi
    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }
}
