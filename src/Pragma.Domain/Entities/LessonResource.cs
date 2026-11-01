using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Dərsə əlavə olunmuş endirilə bilən əlavə material (PDF, ZIP, kod faylı və s.).
/// </summary>
public class LessonResource : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; } = 0;
    public string? FileExtension { get; set; }

    public Guid LessonId { get; set; }
    public Lesson? Lesson { get; set; }
}
