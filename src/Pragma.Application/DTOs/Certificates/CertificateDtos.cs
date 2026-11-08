namespace Pragma.Application.DTOs.Certificates;

public record CertificateDto(
    Guid Id,
    Guid CourseId,
    string CertificateCode,
    string StudentFullName,
    string CourseTitle,
    string InstructorName,
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
