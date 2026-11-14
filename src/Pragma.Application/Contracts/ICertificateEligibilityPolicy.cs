using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Contracts;

public record CertificateEligibilityEvaluation(
    CertificateEligibilityResult Result,
    decimal OfficialGrade,
    int TotalAbsences,
    int MaxAbsenceLimit,
    bool IsProvisional,
    bool IsCohortFinished,
    string Reason
);

/// <summary>
/// Sertifikat hüququ qərarı üçün tək siyasət mərkəzi (Single Point of Truth).
/// Controller, UI, Excel və servislər yalnız bu siyasəti çağırır.
/// </summary>
public interface ICertificateEligibilityPolicy
{
    CertificateEligibilityEvaluation Evaluate(
        CohortEnrollment enrollment,
        decimal officialGrade,
        int totalAbsences,
        int maxAbsenceLimit,
        bool isCohortFinished);
}
