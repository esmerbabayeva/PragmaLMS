using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.AiMentor;

namespace Pragma.Application.Services;

public class AiMentorService : IAiMentorService
{
    public const string OutOfScopeMessage =
        "Hörmətli istifadəçi, mən yalnız Pragma LMS tədris platforması, dərslər, kurrikulum, dərs cədvəli, qayıblar (qb), qiymətləndirmə və akademik göstəricilər üzrə suallara cavab vermək üçün ixtisaslaşmışam. Tədrisdən kənar və ya ümumi mövzular (hava proqnozu, əyləncə, xəbərlər və s.) üzrə məlumat təqdim edə bilmirəm. Zəhmət olmasa tədris və ya platforma ilə bağlı sualınızı ünvanlayın.";

    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICourseAccessService _accessService;
    private readonly AiSettings _aiSettings;

    public AiMentorService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        ICourseAccessService accessService,
        AiSettings aiSettings)
    {
        _context = context;
        _currentUserService = currentUserService;
        _accessService = accessService;
        _aiSettings = aiSettings;
    }

    public async Task<Result<AiAskResponseDto>> AskLessonQuestionAsync(
        Guid lessonId,
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            return Result<AiAskResponseDto>.Failure(Error.Validation("Ai.QuestionEmpty", "Sual mətni daxil edilməlidir."));

        var studentId = _currentUserService.UserId;
        if (!studentId.HasValue)
            return Result<AiAskResponseDto>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var canAccess = await _accessService.CanAccessLessonAsync(lessonId, studentId, cancellationToken);
        if (!canAccess)
            return Result<AiAskResponseDto>.Failure(Error.Forbidden("Bu dərsə giriş icazəniz yoxdur."));

        var lesson = await _context.Lessons
            .AsNoTracking()
            .Include(l => l.Module)
                .ThenInclude(m => m!.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId && !l.IsDeleted, cancellationToken);

        if (lesson == null)
            return Result<AiAskResponseDto>.Failure(Error.NotFound("Dərs tapılmadı."));

        // DƏMİR QAYDA: Sayt xarici / tədrisdən kənar sualların filtrasiyası (Guardrail)
        if (IsOutOfScope(question))
        {
            return Result<AiAskResponseDto>.Success(new AiAskResponseDto(
                OutOfScopeMessage,
                new List<string>
                {
                    $"{lesson.Title} dərsinin əsas məğzi nədir?",
                    "Bu mövzu üzrə praktiki tapşırıq nümunəsi verə bilərsən?",
                    "Kvizə hazırlaşmaq üçün nəyə diqqət etməliyəm?"
                }));
        }

        var apiKey = _aiSettings.GeminiApiKey;
        string answer;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            answer = await GenerateGeminiResponseAsync(apiKey, lesson.Title, lesson.Module!.Title, lesson.Module.Course!.Title, lesson.Content, question, cancellationToken);
        }
        else
        {
            answer = GenerateGroundedAnswer(lesson.Title, lesson.Module!.Title, lesson.Module.Course!.Title, question);
        }

        var followUps = GenerateSuggestedFollowUps(lesson.Title, question);
        return Result<AiAskResponseDto>.Success(new AiAskResponseDto(answer, followUps));
    }

    public async Task<Result<AiAskResponseDto>> AskGlobalQuestionAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            return Result<AiAskResponseDto>.Failure(Error.Validation("Ai.QuestionEmpty", "Sual mətni daxil edilməlidir."));

        // DƏMİR QAYDA: Sayt xarici / tədrisdən kənar sualların filtrasiyası (Guardrail)
        if (IsOutOfScope(question))
        {
            return Result<AiAskResponseDto>.Success(new AiAskResponseDto(
                OutOfScopeMessage,
                new List<string>
                {
                    "QB və qayıb limitləri necə hesablanır?",
                    "Yekun balın hesablanma qaydası necədir?",
                    "Hansı hallarda diplom verilir?",
                    "Dərs saatlarıma haradan baxa bilərəm?"
                }));
        }

        var qNorm = question.Trim().ToLowerInvariant();
        bool isCapabilityQuery = qNorm.Contains("komek") || qNorm.Contains("kömək") || qNorm.Contains("bacar") ||
                                 qNorm.Contains("imkan") || qNorm.Contains("ne ede bilirsen") || qNorm.Contains("nə edə bilərsən") ||
                                 qNorm.Contains("ne edirsen") || qNorm.Contains("nə edirsən") || qNorm.Contains("kimsen") || qNorm.Contains("kimsən");
        bool isPureGreeting = (qNorm.Contains("salam") || qNorm.Contains("hello") || qNorm.Contains("hi") ||
                               qNorm.Contains("sabah") || qNorm.Contains("axşam") || qNorm.Contains("axs") ||
                               qNorm.Contains("hər vaxt") || qNorm.Contains("her vaxt")) &&
                              !qNorm.Contains("qb") && !qNorm.Contains("bal") && !qNorm.Contains("sertifikat") && !qNorm.Contains("cədvəl") && !qNorm.Contains("kurs");

        var apiKey = _aiSettings.GeminiApiKey;
        string answer;

        if (isCapabilityQuery || isPureGreeting)
        {
            // Salam və ya imkanlar/köməklik sorğularında dərhal zəngin və bənd-bənd dəqiq bələdçi qaytarılır
            answer = GenerateGlobalAnswer(question);
        }
        else if (!string.IsNullOrWhiteSpace(apiKey))
        {
            answer = await GenerateGeminiGlobalResponseAsync(apiKey, question, cancellationToken);
        }
        else
        {
            answer = GenerateGlobalAnswer(question);
        }

        var followUps = GenerateGlobalFollowUps(question);
        return Result<AiAskResponseDto>.Success(new AiAskResponseDto(answer, followUps));
    }

    public async Task<Result<AiQuizHintResponseDto>> GetQuizQuestionHintAsync(
        Guid quizId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        var studentId = _currentUserService.UserId;
        if (!studentId.HasValue)
            return Result<AiQuizHintResponseDto>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var question = await _context.Questions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == questionId && q.QuizId == quizId && !q.IsDeleted, cancellationToken);

        if (question == null)
            return Result<AiQuizHintResponseDto>.Failure(Error.NotFound("Sual tapılmadı."));

        var hint = GenerateSocraticHint(question.Text);
        return Result<AiQuizHintResponseDto>.Success(new AiQuizHintResponseDto(question.Text, hint));
    }

    /// <summary>
    /// Sualın Pragma LMS platformasından və ya tədrisdən kənar olub-olmadığını yoxlayır.
    /// </summary>
    public static bool IsOutOfScope(string rawQuestion)
    {
        if (string.IsNullOrWhiteSpace(rawQuestion)) return true;
        var q = rawQuestion.Trim().ToLowerInvariant();

        // 1. Tədris və platforma ilə heç bir əlaqəsi olmayan qadağan edilmiş açar sözlər (tam söz və ya kök)
        string[] offTopicKeywords =
        [
            "hava", "havalar", "yağış", "yagis", "qar", "istilik", "dərəcə", "derece", "külək", "kulek", "weather", "temperature", "forecast",
            "futbol", "messi", "ronaldo", "çempionat", "cempionat",
            "kino", "film", "serial", "aktyor", "müğənni", "mugenny", "mahnı", "mahni", "musiqi",
            "yemək", "yemek", "resept", "pizza", "restoran", "otel", "səyahət", "seyahat",
            "dollar", "avro", "məzənnə", "mezenne", "valyuta", "bitcoin", "kripto",
            "bürclər", "burcler", "fal", "baxıcı",
            "prezident", "siyasət", "siyaset", "müharibə", "muharibe"
        ];

        // Açar sözləri cümlədəki sözlərə görə analiz edirik
        var words = q.Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '-', '(', ')', '[', ']', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        // Hər hansı qadağan söz tam söz və ya sözün başlanğıcı kimi keçirmi?
        foreach (var word in words)
        {
            if (offTopicKeywords.Any(kw => word == kw || (kw.Length >= 4 && word.StartsWith(kw))))
            {
                // İstisna: Əgər daxilində proqramlaşdırma konteksti varsa (məsələn: "hava proqramı c# kodu")
                if (words.Any(w => w is "c#" or ".net" or "kod" or "api" or "lms" or "dərs" or "ders"))
                {
                    continue;
                }
                return true;
            }
        }

        // Çoxsözlü spesifik ifadələr
        if (q.Contains("hava ") || q.Contains(" hava") || q.Contains("ulduz falı") || q.Contains("oyun hesabı") || q.Contains("futbol matçı"))
        {
            if (!q.Contains("c#") && !q.Contains(".net") && !q.Contains("kod") && !q.Contains("api") && !q.Contains("lms") && !q.Contains("dərs") && !q.Contains("ders"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Platforma üzrə daxili zəngin bilik bazasından cavab formalaşdırır (Dərs cədvəli, QB, bal, sertifikat).
    /// </summary>
    private static string GenerateGlobalAnswer(string question)
    {
        var q = question.ToLowerInvariant();

        // 1. Köməkliklər, İmkanlar və AI Səlahiyyətləri (Spesifik bənd-bənd izah)
        if (q.Contains("komek") || q.Contains("kömək") || q.Contains("bacar") || q.Contains("imkan") ||
            q.Contains("nə edə bilərsən") || q.Contains("ne ede bilirsen") || q.Contains("nə edirsən") || q.Contains("ne edirsen") ||
            q.Contains("kimsen") || q.Contains("kimsən") || q.Contains("öhdəlik") || q.Contains("ohdelik") || q.Contains("səlahiyyət") || q.Contains("selahiyyet"))
        {
            return "💡 **Pragma LMS AI Mentor olaraq sizə aşağıdakı konkret istiqamətlərdə köməklik göstərə bilərəm:**\n\n" +
                   "1. 📋 **Davamiyyət və Qayıb (QB) İdarəsi:**\n" +
                   "   • Maksimum qayıb limitiniz (6 qayıb) və cari qayıb vəziyyətiniz.\n" +
                   "   • 3 gecikmənin 1 qayıb (`floor(Late / 3)`) hesablanması və 4-cü qayıbda avtomatik xəbərdarlıq mexanizmi.\n\n" +
                   "2. 🎯 **Yekun Bal və Qiymətləndirmə:**\n" +
                   "   • Kvizlər (30%), Mini Layihələr (30%) və Final Layihə (40%) üzrə bal bölgüsü.\n" +
                   "   • Keçid həddi (70.0 bal), `AwayFromZero` yuvarlaqlaşdırması və müvəqqəti (`IsProvisional`) balın izahı.\n\n" +
                   "3. 🎓 **Diplom və Sertifikat Şərtləri:**\n" +
                   "   • Məzun Diplomu (bal ≥ 70.0 və qayıb ≤ 6) və İştirak Sertifikatı (< 70.0 bal) tələbləri.\n" +
                   "   • QR kod və unikal şifrə ilə `/verify/{kod}` səhifəsində rəsmi sənəd doğrulanması.\n\n" +
                   "4. ⏰ **Dərs Cədvəli və Tədris Saatları:**\n" +
                   "   • Qrupunuzun dərs günləri, başlama/bitmə vaxtları (Bakı vaxtı ilə) və keçilmiş/qalan saatların bölgüsü.\n\n" +
                   "5. 📚 **Kurrikulum və Texnologiyalar:**\n" +
                   "   • C# 14, .NET 10 Onion Architecture, Alpine.js, Tailwind CSS və AI inteqrasiyası üzrə suallarınızın cavablandırılması.\n\n" +
                   "Aşağıdakı düymələrdən birini seçə və ya maraqlandığınız sualı birbaşa ünvanlaya bilərsiniz!";
        }

        // 2. Salamlaşma (Giriş / Xoş gəlmisiniz)
        if (q.Contains("salam") || q.Contains("sabah") || q.Contains("axşam") || q.Contains("axs") || q.Contains("hər vaxt") || q.Contains("her vaxt") || q.Contains("hello") || q.Contains("hi") || q.Contains("merhaba") || q.Contains("necəsən") || q.Contains("necesen"))
        {
            return "Salam! Gününüz uğurlu olsun. Pragma LMS üzrə rəsmi AI Akademik Mentorunuzam.\n\n" +
                   "Sizə bu gün tədrisinizlə bağlı necə kömək edə bilərəm?\n" +
                   "• Qayıb və ya davamiyyət limitlərinizi öyrənmək istəyirsiniz?\n" +
                   "• Yekun bal və kviz nəticələrinizin hesablanması ilə maraqlanırsınız?\n" +
                   "• Dərs cədvəli və ya sertifikat şərtləri barədə sualınız var?\n\n" +
                   "Zəhmət olmasa maraqlandığınız mövzunu qeyd edin və ya aşağıdakı sürətli düymələrdən birini seçin!";
        }

        // 3. QB / Qayıb / Davamiyyət
        if (q.Contains("qb") || q.Contains("qayib") || q.Contains("qayıb") || q.Contains("davamiyy") || q.Contains("gecikm") || q.Contains("limit") || q.Contains("absent"))
        {
            return "📋 **Pragma LMS Davamiyyət və Qayıb (QB) Qaydaları:**\n\n" +
                   "• **Maksimum Qayıb Limiti:** 6 qayıbdır (`MaxAbsenceLimit = 6`).\n" +
                   "• **Gecikmə Nisbəti (Late Ratio):** Hər 3 dərsə gecikmə 1 qayıb hesab olunur (`LateToAbsenceRatio = 3`). Düstur: `Qayıb = Absent + floor(Late / 3)`.\n" +
                   "• **Xəbərdarlıq:** Qayıb sayı limitin 2 vahid altına çatdıqda (4-cü qayıbda) tələbəyə sistem tərəfindən avtomatik xəbərdarlıq bildirişi göndərilir.\n" +
                   "• **Kursdan Kənarlaşdırma (Dropped):** Qayıb sayı 6-nı keçdikdə tələbənin statusu avtomatik `Dropped` olur və həmin kurs üzrə sertifikat hüququ ləğv edilir.\n" +
                   "• **Üzrlü Qayıblar (Excused):** Sənədlə əsaslandırılmış icazəli dərslər qayıb sayılmır.";
        }

        // 4. Bal / Qiymətləndirmə / İmtahan / Yekun Bal
        if (q.Contains("bal") || q.Contains("qiymət") || q.Contains("qiymet") || q.Contains("yekun") || q.Contains("grade") || q.Contains("hesabla") || q.Contains("imtahan") || q.Contains("mini layih") || q.Contains("final"))
        {
            return "🎯 **Pragma LMS Yekun Balının Hesablanma Qaydası:**\n\n" +
                   "Akademik yekun bal aşağıdakı 3 komponent əsasında hesablanır:\n" +
                   "1. **Kviz (Quiz) Ortalaması (30%):** Tələbənin kursdakı hər kviz üzrə ən yüksək nəticəsi götürülür və ortalaması çıxarılır.\n" +
                   "2. **Mini Layihə Ortalaması (30%):** Müəllim tərəfindən hər mini layihə üzrə verilən 0–100 balların ortalaması.\n" +
                   "3. **Final Layihə (40%):** Kursun sonunda təqdim edilən yekun layihənin balı (0–100).\n\n" +
                   "• **Yekun Bal Düsturu:** `Yekun = (Kviz Ort. × 0.3) + (Mini Layihə Ort. × 0.3) + (Final × 0.4)`.\n" +
                   "• **Keçid Həddi:** 70.0 baldır. Bal 1 onluq rəqəmlə yuvarlaqlaşdırılır (`AwayFromZero`).";
        }

        // 5. Sertifikatlar və Diplom
        if (q.Contains("sertifikat") || q.Contains("diplom") || q.Contains("certificate") || q.Contains("məzun") || q.Contains("mezun") || q.Contains("tesdiq") || q.Contains("verify"))
        {
            return "🎓 **Pragma LMS Rəsmi Sertifikat Qərar Qaydaları:**\n\n" +
                   "Kurs yekunlaşdıqda `ICertificateEligibilityPolicy` tərəfindən iki növ sertifikat verilir:\n" +
                   "1. **Məzun Diplomu (`CertificateType.Diploma`):** Rəsmi yekun balı **≥ 70.0** və qayıbları limit daxilində (≤ 6) olan tələbələrə təqdim olunur.\n" +
                   "2. **Dərsdə İştirak Sertifikatı (`CertificateType.Attendance`):** Yekun balı **< 70.0** olan, lakin qayıb limitini aşmayan tələbələrə verilir.\n" +
                   "• **Qayıb Limiti Aşılarsa:** Tələbə `Dropped` olur və heç bir sertifikat verilmir.\n" +
                   "• **Doğrulama:** Hər bir sənəd unikal şifrə və QR kodla təmin olunur, `/verify/{kod}` ünvanında dərhal təsdiqlənir.";
        }

        // 6. Dərs Cədvəli və Dərs Saatları
        if (q.Contains("cədvəl") || q.Contains("cedvel") || q.Contains("saat") || q.Contains("dərs saat") || q.Contains("ders saat") || q.Contains("schedule") || q.Contains("vaxt"))
        {
            return "⏰ **Dərs Saatları və Cədvəl İdarəsi:**\n\n" +
                   "• Dərs saatları ümumi deyil, **hər tədris qrupu üzrə fərdi olaraq** hesablanır.\n" +
                   "• Qrup kartında həm **'Keçilmiş Saat'**, həm də plana uyğun **'Qalan Saat'** və tərəqqi faizi real vaxt rejimində göstərilir.\n" +
                   "• Bütün dərslər və cədvəllər rəsmi Bakı vaxtı (`Asia/Baku` - UTC+4) ilə tənzimlənir.";
        }

        // 7. Kurslar və Kurrikulum
        if (q.Contains("kurs") || q.Contains("c#") || q.Contains(".net") || q.Contains("frontend") || q.Contains("backend") || q.Contains("ai") || q.Contains("gemini") || q.Contains("alpine") || q.Contains("tailwind"))
        {
            return "📚 **Pragma LMS-də Tədris Olunan Əsas Kurslar:**\n\n" +
                   "1. **C# 14 və .NET 10 ilə Müasir Onion Architecture:** Clean Architecture, DDD, CQRS, SQL Server, QuestPDF və ClosedXML.\n" +
                   "2. **Müasir Frontend: Alpine.js, Tailwind CSS & Reaktiv UI:** Komponent arxitekturası, SPA naviqasiyası, Dark/Light rejim və Fetch API.\n" +
                   "3. **Generative AI & LLM İnteqrasiyası: Google Gemini API:** RAG arxitekturası, dialoq mühərrikləri, Sokratik tədris metodları.\n\n" +
                   "Hər bir kurs video dərslər, ağıllı saniyəlik qeydlər, tək/çox cavablı kvizlər və rəsmi diplomla təmin olunur.";
        }

        // 8. Digər platforma funksiyaları (Excel, Profil, Rol)
        if (q.Contains("excel") || q.Contains("jurnal") || q.Contains("rol") || q.Contains("müəllim") || q.Contains("instructor") || q.Contains("profil"))
        {
            return "⚙️ **Pragma LMS Platforma İmkanları:**\n\n" +
                   "• **Müəllim Kabineti:** Qruplara baxış, davamiyyət, mini-layihə və final ballarının daxil edilməsi və jurnalın **Excel (.xlsx)** formatında ixracı.\n" +
                   "• **Instructor Kabineti:** Kursların yaradılması, kurrikulum modulları və dərc/qaralama idarəetməsi.\n" +
                   "• **Tələbə Portalı:** Dərslər, təkrar video izləmə (saxlanılan saniyədən davam etmə), kvizlər və diplom yüklənməsi.";
        }

        // Fallback: Sual kateqoriyalara tam uyğun gəlmədikdə istifadəçiyə dəqiq yönləndirmə təklif edirik
        return "Sualınızı daha dəqiq cavablandıra bilməyim üçün mövzunu dəqiqləşdirə bilərsinizmi?\n\n" +
               "Mən yalnız Pragma LMS tədrisi üzrə ixtisaslaşmışam. Məsələn, bunları soruşa bilərsiniz:\n" +
               "• *'Maksimum qayıb limitim nə qədərdir?'*\n" +
               "• *'Yekun balım necə hesablanır?'*\n" +
               "• *'Diplom almaq üçün minimum bal neçədir?'*\n" +
               "• *'Dərs cədvəlim haradadır?'*\n" +
               "• Və ya bütün imkanlarımı görmək üçün *'Mənə hansı köməklikləri göstərə bilirsən?'* yazın.";
    }

    private static string GenerateGroundedAnswer(string lessonTitle, string moduleTitle, string courseTitle, string question)
    {
        var q = question.ToLowerInvariant();

        // Köməkliklər
        if (q.Contains("komek") || q.Contains("kömək") || q.Contains("bacar") || q.Contains("imkan") || q.Contains("nə edə bilərsən") || q.Contains("ne ede bilirsen"))
        {
            return $"'{courseTitle}' kursunun '{lessonTitle}' dərsi daxilində sizə aşağıdakı konkret istiqamətlərdə kömək edə bilərəm:\n\n" +
                   "1. 💻 **Kod və Arxitektura İzahı:** Dərsdəki konsepsiyaların, sintaksis və arxitektura prinsiplərinin detallı izahı.\n" +
                   "2. ❓ **Sual-Cavab və Xətalar:** Kod nümunəsini tətbiq edərkən qarşılaşdığınız çətinliklərin və xətaların analizi.\n" +
                   "3. 🎯 **Kviz İpucları:** Bu dərsin sonunda təqdim olunacaq test sualları üçün Sokratik ipucları və tövsiyələr.\n" +
                   "4. 🚀 **Praktiki Tətbiq:** Öyrəndiyiniz metodun real istehsal layihələrində necə istifadə olunması.\n\n" +
                   "Bu dərs üzrə anlamaqda çətinlik çəkdiyiniz hissəni birbaşa ünvanlaya bilərsiniz!";
        }

        // Salamlaşma
        if (q.Contains("salam") || q.Contains("necəsən") || q.Contains("necesen"))
        {
            return $"Salam! '{courseTitle}' kursunun '{lessonTitle}' dərsinə xoş gəlmisiniz. Mövzu üzrə dərsin video izahı, kod strukturu və ya kvizlə bağlı suallarınızı ünvanlaya bilərsiniz!";
        }

        return $"Salam! Mən sizin Pragma AI Mentorunuzam. '{courseTitle}' kursunun '{moduleTitle}' modulundakı '{lessonTitle}' dərsi üzrə sualınızı təhlil etdim:\n\n" +
               $"Sual: \"{question}\"\n\n" +
               $"İzah: Bu mövzuda əsas diqqət yetirməli olduğunuz prinsip proqram təminatının dayanıqlığı və təmiz arxitektura prinsipləridir. " +
               $"Dərsdə qeyd edildiyi kimi, hər bir komponent (məsələn, Entity, Servis və ya Controller) öz məsuliyyət dairəsində (Single Responsibility) işləməlidir. " +
               $"Daha yaxşı mənimsəmək üçün dərsin kod nümunəsini tətbiq edib 'Unit Tests' layihəsində yoxlamağınız tövsiyə olunur.";
    }

    private static List<string> GenerateSuggestedFollowUps(string lessonTitle, string question)
    {
        return new List<string>
        {
            $"{lessonTitle} mövzusunda ən çox edilən səhvlər nələrdir?",
            "Bunu real layihədə necə tətbiq edə bilərəm?",
            "Bu mövzu üzrə praktiki tapşırıq nümunəsi verə bilərsən?"
        };
    }

    private static List<string> GenerateGlobalFollowUps(string question)
    {
        var q = question.ToLowerInvariant();
        if (q.Contains("komek") || q.Contains("kömək") || q.Contains("bacar") || q.Contains("imkan") || q.Contains("salam"))
        {
            return new List<string>
            {
                "QB limitim nə qədərdir?",
                "Yekun balım necə hesablanır?",
                "Diplom almaq üçün neçə bal lazımdır?",
                "Dərs saatlarıma haradan baxa bilərəm?"
            };
        }

        if (q.Contains("qb") || q.Contains("qayib") || q.Contains("davamiyy"))
        {
            return new List<string>
            {
                "Gecikmələr qayıba necə çevrilir?",
                "4-cü qayıbda nə baş verir?",
                "Yekun balım davamiyyətə görə kəsilə bilərmi?"
            };
        }

        if (q.Contains("bal") || q.Contains("qiymet") || q.Contains("yekun"))
        {
            return new List<string>
            {
                "Quizlərin çəkisi neçə faizdir?",
                "Keçid balı neçədir?",
                "Mini layihə balımı necə öyrənə bilərəm?"
            };
        }

        return new List<string>
        {
            "QB limitim nə qədərdir?",
            "Yekun bal necə hesablanır?",
            "Diplom almaq üçün minimum bal neçədir?",
            "C# və .NET 10 kursunun məzmunu nədir?"
        };
    }

    private static string GenerateSocraticHint(string questionText)
    {
        return $"İpucu (Sokratik metod): Bu sualda cavabı tapmaq üçün arxitekturanın əsas 'Asılılıqların İdarəedilməsi' (Dependency Inversion) qaydasını xatırlayın. " +
               $"Əsas biznes məntiqinin digər xarici infrastrukturlardan asılı olub-olmadığını düşünün. " +
               $"Variantları təhlil edərkən ən müstəqil və dəyişməz olan qatın xüsusiyyətlərinə fokuslanın.";
    }

    private static async Task<string> GenerateGeminiResponseAsync(
        string apiKey, string lessonTitle, string moduleTitle, string courseTitle, string? content, string question, CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

            var systemPrompt =
                "Sən Pragma LMS platformasının rəsmi AI Akademik Köməkçisisən. " +
                $"Cari dərs: '{lessonTitle}', Modul: '{moduleTitle}', Kurs: '{courseTitle}'. " +
                $"Məzmun: '{content ?? lessonTitle}'. " +
                "DƏMİR QAYDA: Sənin səlahiyyətin YALNIZ Pragma LMS platforması, bu dərs, kurrikulum, dərs cədvəli, qayıblar (qb), qiymətləndirmə və proqramlaşdırma tədrisi ilə məhduddur. " +
                "Əgər sual hava, idman, yemək, kino, siyasət və ya tədrisdən kənar mövzulardadırsa, qətiyyən ümumi cavab vermə! Yalnız bu rəsmi cümlə ilə cavab ver: " +
                OutOfScopeMessage;

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"{systemPrompt}\n\nTələbənin sualı: {question}" }
                        }
                    }
                }
            };

            var response = await client.PostAsJsonAsync(url, requestBody, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (!string.IsNullOrWhiteSpace(text))
                    return text.Trim();
            }
        }
        catch
        {
            // Fallback
        }

        return GenerateGroundedAnswer(lessonTitle, moduleTitle, courseTitle, question);
    }

    private static async Task<string> GenerateGeminiGlobalResponseAsync(
        string apiKey, string question, CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

            var systemPrompt =
                "Sən Pragma LMS platformasının rəsmi AI Akademik Köməkçisisən. " +
                "Pragma LMS qaydaları: " +
                "1. Davamiyyət/QB: Max qayıb 6, hər 3 gecikmə 1 qayıb, 4-cü qayıbda xəbərdarlıq, limit aşılarsa Dropped. " +
                "2. Yekun bal: Quizlər 30%, Mini layihələr 30%, Final layihə 40%. Keçid balı 70.0. AwayFromZero yuvarlaqlaşdırması. " +
                "3. Sertifikat: Bal >= 70 və qayıb <= 6 olduqda Diplom, bal < 70 və qayıb <= 6 olduqda İştirak sertifikatı. Limit aşılarsa sertifikat verilmir. Doğrulama: /verify/{code}. " +
                "4. Dərs saatları hər qrup üzrə fərdidir. " +
                "XÜSUSİ TƏLİMATLAR: " +
                "- İstifadəçi yalnız salam verdikdə, qısa və nəzakətli salamla, nə ilə kömək edə biləcəyini soruş. Şablon ümumi mətni təkrar-təkrar yazma. " +
                "- İstifadəçi 'nə kömək edə bilərsən?', 'hansı köməklikləri göstərə bilirsən?' və ya imkanların barədə soruşduqda, ümumi sözlər demə, Davamiyyət/QB, Yekun Bal, Diplom/Sertifikat, Dərs cədvəli və Kurrikulum üzrə bənd-bənd və aydın köməklikləri təqdim et. " +
                "DƏMİR QAYDA: Sənin səlahiyyətin YALNIZ Pragma LMS platforması, dərslər, kurrikulum, dərs cədvəli, qayıblar (qb), qiymətləndirmə və tədris mövzuları ilə məhduddur. " +
                "Əgər sual hava, idman, yemək, kino, siyasət və ya tədrisdən kənar mövzulardadırsa, qətiyyən ümumi cavab vermə! Yalnız bu rəsmi cümlə ilə cavab ver: " +
                OutOfScopeMessage;

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"{systemPrompt}\n\nİstifadəçinin sualı: {question}" }
                        }
                    }
                }
            };

            var response = await client.PostAsJsonAsync(url, requestBody, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (!string.IsNullOrWhiteSpace(text))
                    return text.Trim();
            }
        }
        catch
        {
            // Fallback
        }

        return GenerateGlobalAnswer(question);
    }
}
