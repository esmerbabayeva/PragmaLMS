namespace Pragma.Application.Contracts;

public record UserBasicInfoDto(
    Guid Id,
    string FullName,
    string Email
);

public interface IUserLookupService
{
    Task<UserBasicInfoDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, UserBasicInfoDto>> GetUsersByIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);
    Task<IReadOnlySet<Guid>> GetUserIdsInRoleAsync(string roleName, CancellationToken ct = default);
}
