using Pragma.Application.Contracts;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class CertificateEligibilityPolicy : ICertificateEligibilityPolicy
{
    public CertificateEligibilityEvaluation Evaluate(
        CohortEnrollment enrollment,
        decimal officialGrade,
        int totalAbsences,
        int maxAbsenceLimit,
        bool isCohortFinished)
    {
        // 1. Rəsmi yuvarlaqlaşdırma qaydası (1 onluq, MidpointRounding.AwayFromZero)
        var roundedGrade = Math.Round(officialGrade, 1, MidpointRounding.AwayFromZero);

        // 2. Kurs bitməyibsə və ya bal hələ müvəqqətidirsə -> Pending
        if (!isCohortFinished || enrollment.IsProvisional)
        {
            return new CertificateEligibilityEvaluation(
                Result: CertificateEligibilityResult.Pending,
                OfficialGrade: roundedGrade,
                TotalAbsences: totalAbsences,
                MaxAbsenceLimit: maxAbsenceLimit,
                IsProvisional: true,
                IsCohortFinished: isCohortFinished,
                Reason: "Kohort hələ tamamlanmayıb və ya yekun bal hələ müvəqqətidir (IsProvisional=true)."
            );
        }

        // 3. Final layihə təhvil verilməyibsə -> Denied (balından asılı olmayaraq sertifikat verilmir)
        if (enrollment.FinalNotSubmitted)
        {
            return new CertificateEligibilityEvaluation(
                Result: CertificateEligibilityResult.Denied,
                OfficialGrade: roundedGrade,
                TotalAbsences: totalAbsences,
                MaxAbsenceLimit: maxAbsenceLimit,
                IsProvisional: false,
                IsCohortFinished: isCohortFinished,
                Reason: "Final layihə təhvil verilmədiyi üçün sertifikat verilmir."
            );
        }

        // 4. Qayıb sayı limiti aşıbsa və ya status Dropped-dirsə -> Denied
        if (totalAbsences > maxAbsenceLimit || enrollment.Status == CohortEnrollmentStatus.Dropped)
        {
            return new CertificateEligibilityEvaluation(
                Result: CertificateEligibilityResult.Denied,
                OfficialGrade: roundedGrade,
                TotalAbsences: totalAbsences,
                MaxAbsenceLimit: maxAbsenceLimit,
                IsProvisional: false,
                IsCohortFinished: isCohortFinished,
                Reason: $"Qayıb sayı ({totalAbsences}) limiti ({maxAbsenceLimit}) aşdığı və ya tələbə qrupdan xaric edildiyi (Dropped) üçün sertifikat verilmir."
            );
        }

        // 4. Bal >= 70.0 -> Fəxri Diplom (Diploma)
        if (roundedGrade >= 70.0m)
        {
            return new CertificateEligibilityEvaluation(
                Result: CertificateEligibilityResult.Diploma,
                OfficialGrade: roundedGrade,
                TotalAbsences: totalAbsences,
                MaxAbsenceLimit: maxAbsenceLimit,
                IsProvisional: false,
                IsCohortFinished: isCohortFinished,
                Reason: $"Tələbə yekun {roundedGrade:F1} bal və {totalAbsences} qayıb ilə Fəxri Diplom (Diploma) hüququ qazanmışdır."
            );
        }

        // 5. Bal < 70.0 və Qayıb <= Limit -> Dinləyici İştirak Sertifikatı (Attendance)
        return new CertificateEligibilityEvaluation(
            Result: CertificateEligibilityResult.Attendance,
            OfficialGrade: roundedGrade,
            TotalAbsences: totalAbsences,
            MaxAbsenceLimit: maxAbsenceLimit,
            IsProvisional: false,
            IsCohortFinished: isCohortFinished,
            Reason: $"Tələbə yekun {roundedGrade:F1} bal və {totalAbsences} qayıb ilə Dərsdə İştirak Sertifikatı (Attendance) hüququ qazanmışdır."
        );
    }
}
