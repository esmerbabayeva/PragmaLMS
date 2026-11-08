using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Certificates;
using Pragma.Domain.Entities;

namespace Pragma.Application.Services;

public class CertificateService : ICertificateService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserLookupService _userLookup;
    private readonly ICertificateGenerator _certificateGenerator;

    public CertificateService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IUserLookupService userLookup,
        ICertificateGenerator certificateGenerator)
    {
        _context = context;
        _currentUserService = currentUserService;
        _userLookup = userLookup;
        _certificateGenerator = certificateGenerator;
    }

    public async Task<Result<CertificateDto>> GenerateCertificateAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var studentId = _currentUserService.UserId;
        if (!studentId.HasValue)
            return Result<CertificateDto>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var enrollment = await _context.Enrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId.Value && !e.IsDeleted, cancellationToken);

        if (enrollment == null)
            return Result<CertificateDto>.Failure(Error.Forbidden("Bu kursa qeydiyyatınız yoxdur."));

        if (!enrollment.IsCompleted && enrollment.ProgressPercentage < 100.0)
            return Result<CertificateDto>.Failure(Error.Validation("Certificate.NotEligible", "Sertifikat əldə etmək üçün bütün dərsləri 100% tamamlamalısınız."));

        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, cancellationToken);

        if (course == null)
            return Result<CertificateDto>.Failure(Error.NotFound("Kurs tapılmadı."));

        var instructorInfo = await _userLookup.GetUserByIdAsync(course.InstructorId, cancellationToken);
        var instructorName = instructorInfo?.FullName ?? "Rəşad Əliyev";

        var existingCert = await _context.Certificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CourseId == courseId && c.StudentId == studentId.Value && !c.IsDeleted, cancellationToken);

        if (existingCert != null)
        {
            return Result<CertificateDto>.Success(MapToDto(existingCert, instructorName));
        }

        var studentInfo = await _userLookup.GetUserByIdAsync(studentId.Value, cancellationToken);
        var fullName = studentInfo?.FullName ?? "Məzun";

        var slugPart = course.Slug.Length >= 4 ? course.Slug[..4].ToUpperInvariant() : "LMS";
        var randomPart = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var certCode = $"PRAGMA-{DateTime.UtcNow.Year}-{slugPart}-{randomPart}";

        var cert = new Certificate
        {
            StudentId = studentId.Value,
            CourseId = courseId,
            CertificateCode = certCode,
            StudentFullName = fullName,
            CourseTitle = course.Title,
            GradePercentage = enrollment.ProgressPercentage,
            IssuedAtUtc = DateTime.UtcNow
        };

        _context.Certificates.Add(cert);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<CertificateDto>.Success(MapToDto(cert, instructorName));
    }

    public async Task<Result<CertificateFileResult>> DownloadCertificatePdfAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var studentId = _currentUserService.UserId;
        if (!studentId.HasValue)
            return Result<CertificateFileResult>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var cert = await _context.Certificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CourseId == courseId && c.StudentId == studentId.Value && !c.IsDeleted, cancellationToken);

        if (cert == null)
        {
            // Əgər sertifikat bazada hələ yaranmayıbsa amma bitirilibsə, dərhal generasiya edirik
            var genResult = await GenerateCertificateAsync(courseId, cancellationToken);
            if (genResult.IsFailure)
                return Result<CertificateFileResult>.Failure(genResult.Error);

            cert = await _context.Certificates
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == genResult.Value.Id, cancellationToken);
        }

        if (cert == null)
            return Result<CertificateFileResult>.Failure(Error.NotFound("Sertifikat tapılmadı."));

        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

        var instructorInfo = course != null ? await _userLookup.GetUserByIdAsync(course.InstructorId, cancellationToken) : null;
        var instructorName = instructorInfo?.FullName ?? "Pragma Tədris Rəhbərliyi";

        var renderModel = new CertificateRenderModel(
            cert.CertificateCode,
            cert.StudentFullName,
            cert.CourseTitle,
            instructorName,
            cert.IssuedAtUtc,
            $"https://pragma.edu.az/verify/{cert.CertificateCode}"
        );

        var pdfBytes = _certificateGenerator.GeneratePdf(renderModel);
        var fileName = $"Pragma_Certificate_{cert.CertificateCode}.pdf";

        return Result<CertificateFileResult>.Success(new CertificateFileResult(
            pdfBytes,
            "application/pdf",
            fileName
        ));
    }

    public async Task<Result<CertificateVerificationDto>> VerifyCertificateAsync(string certificateCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(certificateCode))
            return Result<CertificateVerificationDto>.Success(new CertificateVerificationDto(
                false, null, null, null, null, "Sertifikat kodu daxil edilməyib."));

        var cert = await _context.Certificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CertificateCode == certificateCode.Trim().ToUpperInvariant() && !c.IsDeleted, cancellationToken);

        if (cert == null)
        {
            return Result<CertificateVerificationDto>.Success(new CertificateVerificationDto(
                false, certificateCode, null, null, null, "Bu nömrəyə uyğun rəsmi Pragma sertifikatı tapılmadı."));
        }

        return Result<CertificateVerificationDto>.Success(new CertificateVerificationDto(
            true,
            cert.CertificateCode,
            cert.StudentFullName,
            cert.CourseTitle,
            cert.IssuedAtUtc,
            "Rəsmi Pragma LMS sertifikatı təsdiq edildi."));
    }

    private static CertificateDto MapToDto(Certificate cert, string instructorName)
    {
        return new CertificateDto(
            cert.Id,
            cert.CourseId,
            cert.CertificateCode,
            cert.StudentFullName,
            cert.CourseTitle,
            instructorName,
            cert.GradePercentage,
            cert.IssuedAtUtc,
            $"https://pragma.edu.az/verify/{cert.CertificateCode}"
        );
    }
}
