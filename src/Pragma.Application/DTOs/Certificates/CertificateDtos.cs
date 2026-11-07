namespace Pragma.Application.DTOs.Certificates;

public record CertificateDto(
    Guid Id,
    Guid CourseId,
    string CertificateCode,
    string StudentFullName,
    string CourseTitle,
    double GradePercentage,
    DateTime IssuedAtUtc,
    string VerificationUrl
);

public record CertificateVerificationDto(
    bool IsValid,
    string? CertificateCode,
    string? StudentFullName,
    string? CourseTitle,
    DateTime? IssuedAtUtc,
    string Message
);
