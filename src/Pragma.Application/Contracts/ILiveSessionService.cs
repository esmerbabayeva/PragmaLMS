using Pragma.Application.Common;
using Pragma.Application.DTOs.LiveSession;

namespace Pragma.Application.Contracts;

public interface ILiveSessionService
{
    Task<Result<LiveSessionDto>> GetLessonLiveSessionAsync(Guid lessonId, CancellationToken cancellationToken = default);
}
