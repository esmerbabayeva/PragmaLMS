using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Certificates;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class CertificateService : ICertificateService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserLookupService _userLookup;
    private readonly ICertificateGenerator _certificateGenerator;
    private readonly ICertificateEligibilityPolicy _eligibilityPolicy;
    private readonly IAuditLogService _auditLog;

    public CertificateService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IUserLookupService userLookup,
        ICertificateGenerator certificateGenerator,
        ICertificateEligibilityPolicy eligibilityPolicy,
        IAuditLogService auditLog)
    {
        _context = context;
        _currentUserService = currentUserService;
        _userLookup = userLookup;
        _certificateGenerator = certificateGenerator;
        _eligibilityPolicy = eligibilityPolicy;
        _auditLog = auditLog;
    }

    public async Task<Result<CertificateDto>> GenerateCertificateAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var studentId = _currentUserService.UserId;
        if (!studentId.HasValue)
            return Result<CertificateDto>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

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

        var enrollment = await _context.Enrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId.Value && !e.IsDeleted, cancellationToken);

        if (enrollment == null)
            return Result<CertificateDto>.Failure(Error.Forbidden("Bu kursa qeydiyyatınız yoxdur."));

        if (!enrollment.IsCompleted && enrollment.ProgressPercentage < 99.0)
            return Result<CertificateDto>.Failure(Error.Validation("Certificate.NotEligible", "Sertifikat əldə etmək üçün bütün dərsləri 100% tamamlamalısınız."));

        CohortEnrollment? cohortEnr = null;
        if (course.DeliveryMode == DeliveryMode.Cohort)
        {
            cohortEnr = await _context.CohortEnrollments
                .Include(ce => ce.Cohort)
                .AsNoTracking()
                .FirstOrDefaultAsync(ce => ce.StudentId == studentId.Value && ce.Cohort != null && ce.Cohort.CourseId == courseId && !ce.IsDeleted, cancellationToken);

            if (cohortEnr != null)
            {
                var isDeadlinePassed = cohortEnr.Cohort == null
                                    || cohortEnr.Cohort.Status == CohortStatus.Finished
                                    || !cohortEnr.Cohort.EndDate.HasValue
                                    || cohortEnr.Cohort.EndDate.Value <= DateTime.UtcNow;

                var hasFinalGrade = await _context.CohortGradeEntries
                    .AnyAsync(g => g.CohortId == cohortEnr.CohortId && g.StudentId == studentId.Value && g.ComponentType == GradeComponentType.Final && !g.IsDeleted, cancellationToken);

                if (cohortEnr.FinalNotSubmitted || (isDeadlinePassed && !hasFinalGrade))
                {
                    return Result<CertificateDto>.Failure(Error.Conflict("Certificate.FinalNotSubmitted", "Final layihə təhvil verilmədiyi üçün sertifikat verilə bilməz."));
                }
            }
        }

        var studentInfo = await _userLookup.GetUserByIdAsync(studentId.Value, cancellationToken);
        var fullName = studentInfo?.FullName ?? "Məzun";

        var slugPart = course.Slug.Length >= 4 ? course.Slug[..4].ToUpperInvariant() : "LMS";
        var randomPart = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var certCode = $"PRAGMA-{DateTime.UtcNow.Year}-{slugPart}-{randomPart}";
        var verifySecret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..8];

        var cert = new Certificate
        {
            StudentId = studentId.Value,
            CourseId = courseId,
            CohortId = cohortEnr?.CohortId,
            Type = CertificateType.Diploma,
            CertificateCode = certCode,
            VerificationCode = verifySecret,
            StudentFullName = fullName,
            CourseTitle = course.Title,
            GradePercentage = (course.DeliveryMode == DeliveryMode.Cohort && cohortEnr?.OfficialFinalScore.HasValue == true)
                ? (double)cohortEnr.OfficialFinalScore.Value
                : enrollment.ProgressPercentage,
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
            var genResult = await GenerateCertificateAsync(courseId, cancellationToken);
            if (genResult.IsFailure)
                return Result<CertificateFileResult>.Failure(genResult.Error);

            cert = await _context.Certificates
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == genResult.Value.Id, cancellationToken);
        }

        if (cert == null)
            return Result<CertificateFileResult>.Failure(Error.NotFound("Sertifikat tapılmadı."));

        if (cert.IsRevoked)
        {
            return Result<CertificateFileResult>.Failure(Error.Forbidden("Certificate.Revoked", "Bu sertifikat ləğv edilmişdir və yüklənə bilməz."));
        }

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
            $"https://pragma.edu.az/verify/{cert.VerificationCode}"
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
        {
            return Result<CertificateVerificationDto>.Success(new CertificateVerificationDto(
                IsValid: false,
                IsRevoked: false,
                RevokedReason: null,
                RevokedAtUtc: null,
                Type: null,
                CertificateCode: null,
                VerificationCode: null,
                StudentFullName: null,
                CourseTitle: null,
                GradePercentage: null,
                IssuedAtUtc: null,
                Message: "Doğrulama kodu daxil edilməyib."
            ));
        }

        var queryCode = certificateCode.Trim();

        var cert = await _context.Certificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                (c.CertificateCode == queryCode || c.VerificationCode == queryCode) && !c.IsDeleted,
                cancellationToken);

        if (cert == null)
        {
            return Result<CertificateVerificationDto>.Success(new CertificateVerificationDto(
                IsValid: false,
                IsRevoked: false,
                RevokedReason: null,
                RevokedAtUtc: null,
                Type: null,
                CertificateCode: queryCode,
                VerificationCode: null,
                StudentFullName: null,
                CourseTitle: null,
                GradePercentage: null,
                IssuedAtUtc: null,
                Message: "Bu koda uyğun rəsmi Pragma sertifikatı tapılmadı."
            ));
        }

        if (cert.IsRevoked)
        {
            return Result<CertificateVerificationDto>.Success(new CertificateVerificationDto(
                IsValid: false,
                IsRevoked: true,
                RevokedReason: cert.RevokedReason ?? "Sertifikat rəhbərlik tərəfindən ləğv edilmişdir.",
                RevokedAtUtc: cert.RevokedAtUtc,
                Type: cert.Type,
                CertificateCode: cert.CertificateCode,
                VerificationCode: cert.VerificationCode,
                StudentFullName: cert.StudentFullName,
                CourseTitle: cert.CourseTitle,
                GradePercentage: cert.GradePercentage,
                IssuedAtUtc: cert.IssuedAtUtc,
                Message: $"DİQQƏT: Bu sertifikat ləğv edilmişdir ({cert.RevokedReason}) və hüquqi qüvvəyə malik deyil."
            ));
        }

        var typeTitle = cert.Type == CertificateType.Diploma ? "Diplom" : "Dərsdə İştirak Sertifikatı";

        return Result<CertificateVerificationDto>.Success(new CertificateVerificationDto(
            IsValid: true,
            IsRevoked: false,
            RevokedReason: null,
            RevokedAtUtc: null,
            Type: cert.Type,
            CertificateCode: cert.CertificateCode,
            VerificationCode: cert.VerificationCode,
            StudentFullName: cert.StudentFullName,
            CourseTitle: cert.CourseTitle,
            GradePercentage: cert.GradePercentage,
            IssuedAtUtc: cert.IssuedAtUtc,
            Message: $"Rəsmi Pragma LMS {typeTitle} təsdiq edildi. Məzun: {cert.StudentFullName}, Kurs: {cert.CourseTitle}."
        ));
    }

    public async Task<Result<IReadOnlyCollection<CohortStudentEligibilityDto>>> GetCohortCertificateEligibilityAsync(
        Guid cohortId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .Include(c => c.AttendanceRecords)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, cancellationToken);

        if (cohort == null)
        {
            return Result<IReadOnlyCollection<CohortStudentEligibilityDto>>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        // Səlahiyyət yoxlanışı
        if (!isAdmin && cohort.TeacherId != currentUserId && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<IReadOnlyCollection<CohortStudentEligibilityDto>>.Failure(Error.Forbidden("Cohort.Forbidden", "Sertifikat uyğunluq cədvəlinə baxmaq səlahiyyətiniz yoxdur."));
        }

        var enrollments = cohort.Enrollments.Where(e => !e.IsDeleted).ToList();
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var userDict = await _userLookup.GetUsersByIdsAsync(studentIds, cancellationToken);

        var existingCerts = await _context.Certificates
            .AsNoTracking()
            .Where(c => c.CohortId == cohortId && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        var certDict = existingCerts.ToDictionary(c => c.StudentId);

        var list = new List<CohortStudentEligibilityDto>();
        var isCohortFinished = cohort.Status == CohortStatus.Finished;

        foreach (var enr in enrollments)
        {
            var user = userDict.TryGetValue(enr.StudentId, out var u) ? u : null;
            var records = cohort.AttendanceRecords.Where(r => r.StudentId == enr.StudentId && !r.IsDeleted).ToList();

            var absents = records.Count(r => r.Status == AttendanceStatus.Absent);
            var lates = records.Count(r => r.Status == AttendanceStatus.Late);
            var effectiveAbsences = absents + (lates / cohort.LateToAbsenceRatio);

            var score = enr.OfficialFinalScore ?? 0m;
            var eval = _eligibilityPolicy.Evaluate(enr, score, effectiveAbsences, cohort.MaxAbsenceLimit, isCohortFinished);

            certDict.TryGetValue(enr.StudentId, out var cert);

            list.Add(new CohortStudentEligibilityDto(
                StudentId: enr.StudentId,
                FullName: user?.FullName ?? "Tələbə",
                Email: user?.Email ?? string.Empty,
                OfficialGrade: eval.OfficialGrade,
                TotalAbsences: effectiveAbsences,
                MaxAbsenceLimit: cohort.MaxAbsenceLimit,
                Result: eval.Result,
                Reason: eval.Reason,
                HasCertificate: cert != null,
                CertificateCode: cert?.CertificateCode
            ));
        }

        return Result<IReadOnlyCollection<CohortStudentEligibilityDto>>.Success(list);
    }

    public async Task<Result<int>> IssueCohortCertificatesAsync(
        Guid cohortId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .Include(c => c.AttendanceRecords)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, cancellationToken);

        if (cohort == null)
        {
            return Result<int>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        if (!isAdmin && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<int>.Failure(Error.Forbidden("Cohort.Forbidden", "Sertifikatları təqdim etmək səlahiyyətiniz yoxdur."));
        }

        var enrollments = cohort.Enrollments.Where(e => !e.IsDeleted).ToList();
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var userDict = await _userLookup.GetUsersByIdsAsync(studentIds, cancellationToken);

        var existingCerts = await _context.Certificates
            .Where(c => c.CohortId == cohortId && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        var certDict = existingCerts.ToDictionary(c => c.StudentId);
        int issuedCount = 0;
        var isFinished = cohort.Status == CohortStatus.Finished;

        foreach (var enr in enrollments)
        {
            if (certDict.ContainsKey(enr.StudentId)) continue; // Artıq sertifikatı var

            var records = cohort.AttendanceRecords.Where(r => r.StudentId == enr.StudentId && !r.IsDeleted).ToList();
            var absents = records.Count(r => r.Status == AttendanceStatus.Absent);
            var lates = records.Count(r => r.Status == AttendanceStatus.Late);
            var effectiveAbsences = absents + (lates / cohort.LateToAbsenceRatio);

            var score = enr.OfficialFinalScore ?? 0m;
            var eval = _eligibilityPolicy.Evaluate(enr, score, effectiveAbsences, cohort.MaxAbsenceLimit, isFinished);

            if (eval.Result == CertificateEligibilityResult.Diploma || eval.Result == CertificateEligibilityResult.Attendance)
            {
                var certType = eval.Result == CertificateEligibilityResult.Diploma ? CertificateType.Diploma : CertificateType.Attendance;
                var studentName = userDict.TryGetValue(enr.StudentId, out var s) ? s.FullName : "Məzun";
                var randomCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                var verifySecret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..8];
                var prefix = certType == CertificateType.Diploma ? "PRAGMA-DIP" : "PRAGMA-ATT";
                var certCode = $"{prefix}-{DateTime.UtcNow.Year}-{randomCode}";

                var certificate = new Certificate
                {
                    StudentId = enr.StudentId,
                    CourseId = cohort.CourseId,
                    CohortId = cohortId,
                    Type = certType,
                    CertificateCode = certCode,
                    VerificationCode = verifySecret,
                    StudentFullName = studentName,
                    CourseTitle = cohort.Course?.Title ?? cohort.Name,
                    GradePercentage = (double)eval.OfficialGrade,
                    IssuedAtUtc = DateTime.UtcNow
                };

                _context.Certificates.Add(certificate);
                issuedCount++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(issuedCount);
    }

    public async Task<Result<bool>> RevokeCertificateAsync(
        string code,
        string reason,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<bool>.Failure(Error.Validation("Certificate.RevokeReasonRequired", "Sertifikatın ləğv edilmə səbəbi qeyd olunmalıdır."));
        }

        var queryCode = code.Trim();
        var cert = await _context.Certificates
            .FirstOrDefaultAsync(c =>
                (c.CertificateCode == queryCode || c.VerificationCode == queryCode) && !c.IsDeleted,
                cancellationToken);

        if (cert == null)
        {
            return Result<bool>.Failure(Error.NotFound("Certificate.NotFound", "Sertifikat tapılmadı."));
        }

        cert.IsRevoked = true;
        cert.RevokedReason = reason.Trim();
        cert.RevokedAtUtc = DateTime.UtcNow;

        await _auditLog.LogAsync(
            userId: adminUserId,
            userEmail: "Admin",
            action: "CertificateRevoked",
            entityName: nameof(Certificate),
            entityId: cert.Id.ToString(),
            details: $"Sertifikat ({cert.CertificateCode}) ləğv edildi. Səbəb: {reason.Trim()}",
            cancellationToken: cancellationToken
        );

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    private static CertificateDto MapToDto(Certificate cert, string instructorName)
    {
        return new CertificateDto(
            cert.Id,
            cert.CourseId,
            cert.CohortId,
            cert.Type,
            cert.CertificateCode,
            cert.VerificationCode,
            cert.StudentFullName,
            cert.CourseTitle,
            instructorName,
            cert.GradePercentage,
            cert.IssuedAtUtc,
            $"https://pragma.edu.az/verify/{cert.VerificationCode}",
            cert.IsRevoked,
            cert.RevokedReason
        );
    }
}
