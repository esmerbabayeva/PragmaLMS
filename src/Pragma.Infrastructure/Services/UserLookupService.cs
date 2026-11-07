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
}
