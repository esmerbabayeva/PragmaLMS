using Microsoft.EntityFrameworkCore;
using Pragma.Application.Contracts;
using Pragma.Infrastructure.Persistence;

namespace Pragma.Infrastructure.Services;

public class UserLookupService : IUserLookupService
{
    private readonly AppDbContext _context;

    public UserLookupService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserBasicInfoDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserBasicInfoDto(u.Id, u.FullName, u.Email ?? string.Empty))
            .FirstOrDefaultAsync(ct);

        return user;
    }

    public async Task<IReadOnlyDictionary<Guid, UserBasicInfoDto>> GetUsersByIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var idList = userIds.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, UserBasicInfoDto>();

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => idList.Contains(u.Id))
            .Select(u => new UserBasicInfoDto(u.Id, u.FullName, u.Email ?? string.Empty))
            .ToDictionaryAsync(u => u.Id, u => u, ct);

        return users;
    }

    public async Task<IReadOnlySet<Guid>> GetUserIdsInRoleAsync(string roleName, CancellationToken ct = default)
    {
        var normalizedRole = roleName.ToUpperInvariant();
        var role = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.NormalizedName == normalizedRole, ct);

        if (role == null)
            return new HashSet<Guid>();

        var userIds = await _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == role.Id)
            .Select(ur => ur.UserId)
            .ToListAsync(ct);

        return userIds.ToHashSet();
    }
}
