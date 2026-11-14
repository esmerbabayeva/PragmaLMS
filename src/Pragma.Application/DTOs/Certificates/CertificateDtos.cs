using Pragma.Domain.Enums;

namespace Pragma.Application.DTOs.Certificates;

public record CertificateDto(
    Guid Id,
    Guid CourseId,
    Guid? CohortId,
    CertificateType Type,
    string CertificateCode,
    string VerificationCode,
    string StudentFullName,
    string CourseTitle,
    string InstructorName,
    double GradePercentage,
    DateTime IssuedAtUtc,
    string VerificationUrl,
    bool IsRevoked = false,
    string? RevokedReason = null
);

public record CertificateVerificationDto(
    bool IsValid,
    bool IsRevoked,
    string? RevokedReason,
    DateTime? RevokedAtUtc,
    CertificateType? Type,
    string? CertificateCode,
    string? VerificationCode,
    string? StudentFullName,
    string? CourseTitle,
    double? GradePercentage,
    DateTime? IssuedAtUtc,
    string Message
);

public record CohortStudentEligibilityDto(
    Guid StudentId,
    string FullName,
    string Email,
    decimal OfficialGrade,
    int TotalAbsences,
    int MaxAbsenceLimit,
    CertificateEligibilityResult Result,
    string Reason,
    bool HasCertificate,
    string? CertificateCode
);

public record RevokeCertificateDto(
    string Reason
);
