using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Notes;

namespace Pragma.API.Controllers;

/// <summary>
/// Dərslərdə konkret video saniyəsinə bağlanan ağıllı zaman qeydləri.
/// </summary>
[Authorize]
[ApiController]
[Route("api")]
public class NotesController : BaseApiController
{
    private readonly ILessonNoteService _noteService;

    public NotesController(ILessonNoteService noteService)
    {
        _noteService = noteService;
    }

    /// <summary>
    /// Videonun cari saniyəsinə bağlı ağıllı zaman qeydi əlavə edir.
    /// </summary>
    [HttpPost("lessons/{id:guid}/notes")]
    public async Task<IActionResult> CreateNote(
        Guid id,
        [FromBody] CreateLessonNoteRequestDto dto,
        CancellationToken ct)
    {
        var result = await _noteService.CreateNoteAsync(id, dto, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Cari dərsin bütün zaman qeydlərini xronoloji ardıcıllıqla qaytarır.
    /// </summary>
    [HttpGet("lessons/{id:guid}/notes")]
    public async Task<IActionResult> GetNotes(Guid id, CancellationToken ct)
    {
        var result = await _noteService.GetNotesForLessonAsync(id, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Şəxsi zaman qeydini silir.
    /// </summary>
    [HttpDelete("notes/{id:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id, CancellationToken ct)
    {
        var result = await _noteService.DeleteNoteAsync(id, ct);
        return HandleResult(result);
    }
}
