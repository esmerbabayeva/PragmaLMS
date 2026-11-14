namespace Pragma.Application.Common;

public record PagedQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Search = null,
    string? SortBy = null,
    bool SortDescending = false
)
{
    public int SafePageNumber => PageNumber < 1 ? 1 : PageNumber;
    public int SafePageSize => PageSize switch
    {
        < 1 => 10,
        > 100 => 100,
        _ => PageSize
    };
}
