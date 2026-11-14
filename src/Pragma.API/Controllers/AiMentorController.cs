using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.AiMentor;

namespace Pragma.API.Controllers;

/// <summary>
/// Google Gemini AI əsaslı dərs mentoru, ümumi platforma köməkçisi və Sokratik kviz ipucları.
/// </summary>
[Authorize]
[ApiController]
[Route("api")]
public class AiMentorController : BaseApiController
{
    private readonly IAiMentorService _aiMentorService;

    public AiMentorController(IAiMentorService aiMentorService)
    {
        _aiMentorService = aiMentorService;
    }

    /// <summary>
    /// Ekranın sol alt küncündəki qlobal AI köməkçi: platforma, dərs cədvəli, qayıblar (qb), ballar və qaydalar haqqında sualları cavablandırır.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("ai/ask")]
    public async Task<ActionResult<AiAskResponseDto>> AskGlobalQuestion(
        [FromBody] AiAskRequestDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _aiMentorService.AskGlobalQuestionAsync(dto.Question, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Dərs mövzusuna uyğun AI mentordan sual soruşur və izah alır.
    /// </summary>
    [HttpPost("lessons/{id:guid}/ai-ask")]
    public async Task<ActionResult<AiAskResponseDto>> AskLessonQuestion(
        Guid id,
        [FromBody] AiAskRequestDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _aiMentorService.AskLessonQuestionAsync(id, dto.Question, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Kviz sualına dair cavabı bildirmədən yönləndirici Sokratik ipucu verir.
    /// </summary>
    [HttpGet("quizzes/{quizId:guid}/questions/{questionId:guid}/hint")]
    public async Task<ActionResult<AiQuizHintResponseDto>> GetQuizQuestionHint(
        Guid quizId,
        Guid questionId,
        CancellationToken cancellationToken)
    {
        var result = await _aiMentorService.GetQuizQuestionHintAsync(quizId, questionId, cancellationToken);
        return HandleResult(result);
    }
}
