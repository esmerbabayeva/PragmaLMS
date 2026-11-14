namespace Pragma.Application.Common;

/// <summary>
/// Sistem üzrə Policy-Based avtorizasiya qaydaları.
/// Bütün hüquqlar rola deyil, konkret əməliyyat siyasətlərinə (policies) bağlanır.
/// </summary>
public static class AuthorizationPolicies
{
    // ==========================================
    // 1. ADMİN SİYASƏTLƏRİ (YALNIZ ADMİN)
    // ==========================================
    /// <summary>Yalnız sistem SuperAdmin-i üçün ümumi siyasət.</summary>
    public const string RequireAdmin = "RequireAdmin";

    /// <summary>İstifadəçi yaratmaq, redaktə etmək, silmək, bloklamaq/blokdan çıxarmaq, parolu sıfırlamaq (Yalnız Admin).</summary>
    public const string CanManageUsers = "CanManageUsers";

    /// <summary>İstifadəçilərə Admin, Instructor, Teacher, Student rollarını təyin etmək və ya almaq (Yalnız Admin).</summary>
    public const string CanManageRoles = "CanManageRoles";

    /// <summary>Müstəqil ASP.NET Core MVC Admin Portalı (port 5089) və sistem admin konsoluna çıxış (Yalnız Admin).</summary>
    public const string CanAccessAdminPanel = "CanAccessAdminPanel";

    /// <summary>İstənilən tələbənin yekun/imtahan balını düzəltmək və audit log-a yazmaq (Yalnız Admin).</summary>
    public const string CanAdjustGrades = "CanAdjustGrades";

    /// <summary>Ümumi sistem statistikalarına, hesabatlara baxmaq və ClosedXML Excel ixracı (Yalnız Admin).</summary>
    public const string CanViewSystemStatistics = "CanViewSystemStatistics";

    /// <summary>Sistemdəki kritik əməliyyatların audit qeydlərinə baxmaq (Yalnız Admin).</summary>
    public const string CanViewAuditLogs = "CanViewAuditLogs";

    // ==========================================
    // 2. KURS, QRUP VƏ MƏZMUN SİYASƏTLƏRİ (ADMİN & INSTRUCTOR)
    // ==========================================
    /// <summary>Kurs yaratmaq, redaktə etmək, arxivləşdirmək, publish/unpublish (Admin hər kursu, Instructor yalnız öz kursunu).</summary>
    public const string CanManageCourses = "CanManageCourses";

    /// <summary>Kursa və ya qrupa tələbə qeydiyyatı (Enrollment) idarəetməsi (Admin hər kursu, Instructor yalnız öz kursunu).</summary>
    public const string CanManageEnrollments = "CanManageEnrollments";

    /// <summary>Qrup (Cohort) yaratmaq, silmək, qrupa tələbə və Teacher təyin etmək (Admin və Instructor).</summary>
    public const string CanManageCohorts = "CanManageCohorts";

    /// <summary>Kviz yaratmaq, sualları/variantları/vaxtı redaktə etmək və silmək (Admin, Instructor və Teacher).</summary>
    public const string CanManageQuiz = "CanManageQuiz";

    /// <summary>Qrupların siyahısına, dərs saatlarına və tələbə siyahılarına baxmaq (Admin, Instructor və Teacher).</summary>
    public const string CanViewGroups = "CanViewGroups";

    /// <summary>Tələbələrə qiymət qoymaq (quiz, mini layihə, final, davamiyyət) və şərh yazmaq (Admin, Instructor və Teacher).</summary>
    public const string CanGradeStudents = "CanGradeStudents";

    /// <summary>Nəticələrə, qiymət cədvəlinə (Gradebook) baxmaq və Excel ixrac etmək (Admin, Instructor və Teacher).</summary>
    public const string CanViewResults = "CanViewResults";

    /// <summary>Kursun məzmununa (dərs, video, material) baxmaq (Admin, Instructor, Teacher və qeydiyyatlı tələbə).</summary>
    public const string CanViewAssignedCourseContent = "CanViewAssignedCourseContent";

    // ==========================================
    // 3. TƏLƏBƏ (STUDENT) SİYASƏTLƏRİ (YALNIZ STUDENT)
    // ==========================================
    /// <summary>Yalnız tələbə rolu tələb edən ümumi siyasət (Yalnız Student).</summary>
    public const string RequireStudent = "RequireStudent";

    /// <summary>Yalnız qeydiyyatda olduğu öz kurslarının siyahısına və dərslərinə baxmaq (Yalnız Student).</summary>
    public const string CanViewOwnCourses = "CanViewOwnCourses";

    /// <summary>Dərsi izləmək, video izlənmə saniyəsini yeniləmək və dərsi tamamlamaq/XP qazanmaq (Yalnız Student).</summary>
    public const string CanTrackProgress = "CanTrackProgress";

    /// <summary>Qeydiyyatda olduğu kurs üzrə açıq kvizdə imtahan vermək (Yalnız Student).</summary>
    public const string CanTakeQuiz = "CanTakeQuiz";

    /// <summary>Yalnız öz ballarına, quiz nəticələrinə və tərəqqisinə baxmaq (Yalnız Student).</summary>
    public const string CanViewOwnGrades = "CanViewOwnGrades";

    /// <summary>Öz profilini və parolunu idarə etmək (Bütün autentifikasiyadan keçmiş istifadəçilər).</summary>
    public const string CanManageProfile = "CanManageProfile";
}
