namespace Pragma.Domain.Enums;

/// <summary>
/// Sistemdə mövcud olan əsas istifadəçi rolları.
/// </summary>
public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Instructor = "Instructor";
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    public static readonly IReadOnlyList<string> All = [Admin, Moderator, Instructor, Teacher, Student];
}
