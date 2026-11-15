using Pragma.Domain.Enums;

namespace Pragma.Application.Contracts;

public record CertificateRenderModel(
    string CertificateCode,
    string StudentFullName,
    string CourseTitle,
    string InstructorName,
    DateTime IssuedAtUtc,
    string VerificationUrl,
    CertificateType Type = CertificateType.Diploma,
    double GradePercentage = 100.0
);

public interface ICertificateGenerator
{
    byte[] GeneratePdf(CertificateRenderModel model);
}
