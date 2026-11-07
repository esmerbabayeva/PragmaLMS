namespace Pragma.Application.Contracts;

public record CertificateRenderModel(
    string CertificateCode,
    string StudentFullName,
    string CourseTitle,
    string InstructorName,
    DateTime IssuedAtUtc,
    string VerificationUrl
);

public interface ICertificateGenerator
{
    byte[] GeneratePdf(CertificateRenderModel model);
}
