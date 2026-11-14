using Pragma.Application.Common;
using Pragma.Application.DTOs.Certificates;

namespace Pragma.Application.Contracts;

public record CertificateFileResult(
    byte[] Content,
    string ContentType,
    string FileName
);

public interface ICertificateService
{
    Task<Result<CertificateDto>> GenerateCertificateAsync(Guid courseId, CancellationToken cancellationToken = default);
    Task<Result<CertificateFileResult>> DownloadCertificatePdfAsync(Guid courseId, CancellationToken cancellationToken = default);
    Task<Result<CertificateVerificationDto>> VerifyCertificateAsync(string certificateCode, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<CohortStudentEligibilityDto>>> GetCohortCertificateEligibilityAsync(Guid cohortId, Guid currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result<int>> IssueCohortCertificatesAsync(Guid cohortId, Guid currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result<bool>> RevokeCertificateAsync(string code, string reason, Guid adminUserId, CancellationToken cancellationToken = default);
}
