using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

/// <summary>
/// Kursu və ya Kohortu bitirən tələbəyə təqdim olunan rəsmi Diplom və ya İştirak sertifikatı.
/// Unikal yoxlama kodu və QR doğrulama ilə təmin edilir.
/// </summary>
public class Certificate : BaseEntity
{
    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }
    public Course? Course { get; set; }

    /// <summary>
    /// Əgər sertifikat Cohort əsaslı kurs üçün verilibsə CohortId.
    /// </summary>
    public Guid? CohortId { get; set; }
    public Cohort? Cohort { get; set; }

    /// <summary>
    /// Sertifikat növü: Fəxri Diplom (>=70) və ya Dinləyici İştirak Sertifikatı (<70).
    /// </summary>
    public CertificateType Type { get; set; } = CertificateType.Diploma;

    /// <summary>
    /// Unikal sertifikat nömrəsi (məs: PRAGMA-2026-A8K9F).
    /// </summary>
    public string CertificateCode { get; set; } = string.Empty;

    /// <summary>
    /// Təsadüfi, təxmin edilə bilməyən kriptoqrafik doğrulama kodu.
    /// </summary>
    public string VerificationCode { get; set; } = string.Empty;

    public string StudentFullName { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public double GradePercentage { get; set; } = 100.0;

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Sertifikatın ləğv edilmə vəziyyəti (Admin tərəfindən).
    /// </summary>
    public bool IsRevoked { get; set; } = false;
    public string? RevokedReason { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
