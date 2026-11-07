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
}
