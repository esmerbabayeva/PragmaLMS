using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Şablondan seçilərək tələbələrə və ya qruplara təyin edilmiş aktiv imtahan vahidi.
/// Data Integrity: QuestionsSnapshotJson vasitəsilə sualların və balların həmin andakı dondurulmuş surəti saxlanılır.
/// Əsas şablon dəyişdirilsə belə təyin edilmiş quiz dəyişməz qalır.
/// </summary>
public class QuizAssignment : BaseEntity
{
    public Guid TemplateId { get; set; }
    public QuizTemplate? Template { get; set; }

    public Guid TeacherId { get; set; }

    /// <summary>
    /// Hədəf kurs (əgər kurs səviyyəsində təyindirsə).
    /// </summary>
    public Guid? CourseId { get; set; }
    public Course? Course { get; set; }

    /// <summary>
    /// Hədəf kohort / qrup ID-si (əgər qrup səviyyəsində təyindirsə).
    /// </summary>
    public Guid? CohortId { get; set; }
    public Cohort? Cohort { get; set; }

    /// <summary>
    /// Konkret seçilmiş tələbələrin ID siyahısı (JSON formatında; boşdursa bütün qrupa aiddir).
    /// </summary>
    public string? StudentIdsJson { get; set; }

    /// <summary>
    /// Əgər kurrikuluma aid konkret dərsə bağlanırsa (istəyə bağlı).
    /// </summary>
    public Guid? LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    /// <summary>
    /// İmtahanın başlığı (varsayılan: şablonun başlığı).
    /// </summary>
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Təyin edilmiş fərdi vaxt limiti (dəqiqə). Şablondakı dəyərdən fərqli ola bilər.
    /// </summary>
    public int? CustomTimeLimitMinutes { get; set; }

    /// <summary>
    /// Keçid balı faizlə (məsələn: 70%).
    /// </summary>
    public int PassingScorePercentage { get; set; } = 70;

    /// <summary>
    /// Hər suala düşən fərdi bal (boşdursa sualın öz balı istifadə olunur).
    /// </summary>
    public int? PointsPerQuestion { get; set; }

    /// <summary>
    /// Maksimum imtahan cəhdi sayı (varsayılan: 3).
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// İmtahanın başlama tarixi (UTC). Boşdursa dərhal aktivdir.
    /// </summary>
    public DateTime? StartDateUtc { get; set; }

    /// <summary>
    /// İmtahanın bitmə tarixi / Deadline (UTC). Bu tarixdən sonra imtahan vermək bağlanır.
    /// </summary>
    public DateTime? EndDateUtc { get; set; }

    /// <summary>
    /// İmtahanın aktivlik statusu.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Təyinat anında şablondan çıxarılmış DONDURULMUŞ SUAL VƏ CAVAB SNAPSHOT-u (JSON).
    /// Gələcəkdə şablon dəyişsə belə, tələbənin gördüyü imtahan məhz bu snapshot əsasında işləyir.
    /// </summary>
    public string QuestionsSnapshotJson { get; set; } = "[]";

    /// <summary>
    /// Əgər mövcud Quiz cədvəli ilə əlaqələndirilibsə (inteqrasiya üçün).
    /// </summary>
    public Guid? QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    /// <summary>
    /// Bu təyinat üzrə tələbələrin verdiyi imtahan cəhdləri.
    /// </summary>
    public ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
}
