using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Pragma.Application.Common;
using Pragma.Application.DTOs.Auth;
using Pragma.Application.DTOs.Categories;
using Pragma.Application.DTOs.Courses;
using Pragma.Application.DTOs.Gamification;
using Pragma.Application.DTOs.Gradebook;

namespace Pragma.Admin.Services;

public class PragmaApiClient : IPragmaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PragmaApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
    }

    private void AttachBearerToken()
    {
        var token = _httpContextAccessor.HttpContext?.User?.FindFirst("AccessToken")?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<AuthResponseDto?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { email, password });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("auth/login", content, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<AuthResponseDto>(json, JsonOptions);
    }

    public async Task<PagedResult<CourseSummaryDto>?> GetCoursesAsync(string? search = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        AttachBearerToken();
        var url = $"courses?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";

        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<PagedResult<CourseSummaryDto>>(json, JsonOptions);
    }

    public async Task<CourseDetailDto?> GetCourseByIdAsync(Guid id, CancellationToken ct = default)
    {
        AttachBearerToken();
        var response = await _httpClient.GetAsync($"courses/{id}", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<CourseDetailDto>(json, JsonOptions);
    }

    public async Task<bool> PublishCourseAsync(Guid id, CancellationToken ct = default)
    {
        AttachBearerToken();
        var response = await _httpClient.PutAsync($"courses/{id}/publish", null, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnpublishCourseAsync(Guid id, CancellationToken ct = default)
    {
        AttachBearerToken();
        var response = await _httpClient.PutAsync($"courses/{id}/unpublish", null, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<CategoryDto>?> GetCategoriesAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("categories", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<IReadOnlyList<CategoryDto>>(json, JsonOptions);
    }

    public async Task<bool> CreateCategoryAsync(CreateCategoryDto dto, CancellationToken ct = default)
    {
        AttachBearerToken();
        var payload = JsonSerializer.Serialize(dto);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("categories", content, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<CourseGradebookDto?> GetGradebookAsync(Guid courseId, CancellationToken ct = default)
    {
        AttachBearerToken();
        var response = await _httpClient.GetAsync($"courses/{courseId}/gradebook", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<CourseGradebookDto>(json, JsonOptions);
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> DownloadGradebookExcelAsync(Guid courseId, CancellationToken ct = default)
    {
        AttachBearerToken();
        var response = await _httpClient.GetAsync($"courses/{courseId}/gradebook/export-excel", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar 
            ?? response.Content.Headers.ContentDisposition?.FileName 
            ?? $"Gradebook_{courseId}.xlsx";

        fileName = fileName.Trim('"');
        return (bytes, contentType, fileName);
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>?> GetLeaderboardAsync(int top = 10, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"gamification/leaderboard?top={top}", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<IReadOnlyList<LeaderboardEntryDto>>(json, JsonOptions);
    }
}
