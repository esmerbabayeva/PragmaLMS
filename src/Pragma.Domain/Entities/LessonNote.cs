using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Dərs izlənərkən tələbənin konkret saniyəyə (timestamp) bağladığı ağıllı qeyd.
/// (20% Fərdi Yaradıcılıq Funksiyası: Smart Timestamp Bookmarks).
/// </summary>
public class LessonNote : BaseEntity
{
    public Guid StudentId { get; set; }
    
    public Guid LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    /// <summary>
    /// Videonun konkret saniyəsi (məsələn: 254s -> 04:14).
    /// </summary>
    public int PositionSeconds { get; set; }

    /// <summary>
    /// Tələbənin yazdığı şəxsi konspekt / qeyd mətni.
    /// </summary>
    public string NoteText { get; set; } = string.Empty;
}
