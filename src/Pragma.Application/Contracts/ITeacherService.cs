using Pragma.Application.Common;
using Pragma.Application.DTOs.Teacher;

namespace Pragma.Application.Contracts;

public interface ITeacherService
{
    /// <summary>
    /// Müəllimə təyin olunmuş qrupların və kursların siyahısını qaytarır (dərs saatları ilə).
    /// </summary>
    Task<Result<IReadOnlyCollection<AssignedGroupSummaryDto>>> GetAssignedGroupsAsync(Guid teacherId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Qrupdakı tələbələrin siyahısını, cari ballarını və davamiyyət göstəricilərini qaytarır.
    /// </summary>
    Task<Result<IReadOnlyCollection<GroupStudentDto>>> GetGroupStudentsAsync(Guid groupId, Guid teacherId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Tələbəyə bal (mini layihə, final) qoyur və ya davamiyyətini qeyd edir.
    /// </summary>
    Task<Result<bool>> AssignGradeAsync(AssignGradeDto dto, Guid teacherId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Müəllimin təyin olunmuş kursu üzrə kvizlərin siyahısını qaytarır.
    /// </summary>
    Task<Result<IReadOnlyCollection<TeacherQuizSummaryDto>>> GetGroupQuizzesAsync(Guid groupId, Guid teacherId, bool isAdmin, CancellationToken ct = default);
}
