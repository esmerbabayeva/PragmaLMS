using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Kursu 100% uğurla bitirən tələbəyə təqdim olunan rəsmi sertifikat.
/// Unikal kod və QR doğrulama ilə təmin edilir.
/// </summary>
public class Certificate : BaseEntity
{
    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }
    public Course? Course { get; set; }

    /// <summary>
    /// Unikal sertifikat nömrəsi (məs: PRAGMA-2026-A8K9F).
    /// </summary>
    public string CertificateCode { get; set; } = string.Empty;

    public string StudentFullName { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public double GradePercentage { get; set; } = 100.0;

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
}
