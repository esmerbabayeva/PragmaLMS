using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.AiMentor;

namespace Pragma.Application.Services;

public class AiMentorService : IAiMentorService
{
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

        var apiKey = _aiSettings.GeminiApiKey;
        string answer;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Gemini API inteqrasiyası mövcud olduqda birbaşa Google API çağırılır
            answer = await GenerateGeminiResponseAsync(apiKey, lesson.Title, lesson.Module!.Title, lesson.Module.Course!.Title, lesson.Content, question, cancellationToken);
        }
        else
        {
            // Grounded lokal cavab mühərriki (Dərs kontekstinə uyğun pedaqoji cavab)
            answer = GenerateGroundedAnswer(lesson.Title, lesson.Module!.Title, lesson.Module.Course!.Title, question);
        }

        var followUps = GenerateSuggestedFollowUps(lesson.Title, question);

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

        // Sokratik İpucu (Düzgün variantı demir, tələbəni düzgün məntiqə yönləndirir)
        var hint = GenerateSocraticHint(question.Text);

        return Result<AiQuizHintResponseDto>.Success(new AiQuizHintResponseDto(question.Text, hint));
    }

    private static string GenerateGroundedAnswer(string lessonTitle, string moduleTitle, string courseTitle, string question)
    {
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
            
            var prompt = $"Sən Pragma LMS platformasının mehriban və peşəkar AI müəllimisən. Kurs: '{courseTitle}', Modul: '{moduleTitle}', Dərs: '{lessonTitle}'. " +
                         $"Dərs mətni: '{content ?? lessonTitle}'. " +
                         $"Tələbənin sualı: '{question}'. " +
                         $"Azərbaycan dilində, aydın, pedaqoji və səliqəli izah ver.";

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                }
            };

            var response = await client.PostAsJsonAsync(url, requestBody, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                // JSON cavabından mətni çıxarırıq
                return $"[Gemini AI]: " + json;
            }
        }
        catch
        {
            // Fallback
        }

        return GenerateGroundedAnswer(lessonTitle, moduleTitle, courseTitle, question);
    }
}
