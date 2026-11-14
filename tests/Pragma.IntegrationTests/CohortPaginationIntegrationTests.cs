using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.DTOs.Auth;
using Pragma.Application.DTOs.Cohorts;
using Xunit;

namespace Pragma.IntegrationTests;

public class CohortPaginationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CohortPaginationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResult = await loginResponse.Content.ReadFromJsonAsync<TokenResultDto>();
        tokenResult.Should().NotBeNull();
        return tokenResult!.AccessToken;
    }

    [Fact]
    public async Task GetCohorts_DefaultPagination_ReturnsPagedResultObject()
    {
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/cohorts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<CohortSummaryDto>>();
        pagedResult.Should().NotBeNull();
        pagedResult!.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        pagedResult.PageNumber.Should().Be(1);
        pagedResult.PageSize.Should().Be(50);
        pagedResult.Items.Should().NotBeNull();
        pagedResult.Items.Count.Should().BeLessThanOrEqualTo(50);
        pagedResult.Items.Count.Should().Be(Math.Min(pagedResult.TotalCount, 50));
    }

    [Fact]
    public async Task GetCohorts_WithPageSize2_ReturnsPagesWithDisjointCohorts()
    {
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");

        // Page 1
        var req1 = new HttpRequestMessage(HttpMethod.Get, "/api/cohorts?pageNumber=1&pageSize=2");
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var res1 = await _client.SendAsync(req1);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedResult1 = await res1.Content.ReadFromJsonAsync<PagedResult<CohortSummaryDto>>();
        pagedResult1.Should().NotBeNull();
        pagedResult1!.PageNumber.Should().Be(1);
        pagedResult1.PageSize.Should().Be(2);

        if (pagedResult1.Items.Count == 2)
        {
            // Page 2
            var req2 = new HttpRequestMessage(HttpMethod.Get, "/api/cohorts?pageNumber=2&pageSize=2");
            req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var res2 = await _client.SendAsync(req2);
            res2.StatusCode.Should().Be(HttpStatusCode.OK);
            var pagedResult2 = await res2.Content.ReadFromJsonAsync<PagedResult<CohortSummaryDto>>();
            pagedResult2.Should().NotBeNull();
            pagedResult2!.PageNumber.Should().Be(2);
            pagedResult2.PageSize.Should().Be(2);

            // Səhifələrin ID-ləri kəsişməməlidir (disjoint)
            var p1Ids = pagedResult1.Items.Select(x => x.Id).ToHashSet();
            var p2Ids = pagedResult2.Items.Select(x => x.Id).ToHashSet();
            p1Ids.Overlaps(p2Ids).Should().BeFalse();
        }
    }

    [Fact]
    public async Task GetCohorts_WithPageSizeGreaterThan100_ClampsTo100()
    {
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/cohorts?pageSize=500");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<CohortSummaryDto>>();
        pagedResult.Should().NotBeNull();
        pagedResult!.PageSize.Should().Be(100);
        pagedResult.Items.Count.Should().BeLessThanOrEqualTo(100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetCohorts_WithInvalidPageNumber_Returns400ProblemDetails(int invalidPage)
    {
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/cohorts?pageNumber={invalidPage}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Detail.Should().Contain("pageNumber");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetCohorts_WithInvalidPageSize_Returns400ProblemDetails(int invalidPageSize)
    {
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/cohorts?pageSize={invalidPageSize}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Detail.Should().Contain("pageSize");
    }

    [Fact]
    public async Task GetCohorts_RoleVisibility_PreservesVisibilityRules()
    {
        // 1. Admin bütün kohortları görür
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");
        var adminReq = new HttpRequestMessage(HttpMethod.Get, "/api/cohorts");
        adminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var adminRes = await _client.SendAsync(adminReq);
        adminRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminPaged = await adminRes.Content.ReadFromJsonAsync<PagedResult<CohortSummaryDto>>();
        adminPaged.Should().NotBeNull();
        var adminTotal = adminPaged!.TotalCount;

        // 2. Müəllim yalnız təhkim olunduğu kohortları görür
        var teacherToken = await LoginAsync("teacher@pragma.edu.az", "Password123!");
        var teacherReq = new HttpRequestMessage(HttpMethod.Get, "/api/cohorts");
        teacherReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        var teacherRes = await _client.SendAsync(teacherReq);
        teacherRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var teacherPaged = await teacherRes.Content.ReadFromJsonAsync<PagedResult<CohortSummaryDto>>();
        teacherPaged.Should().NotBeNull();
        teacherPaged!.TotalCount.Should().BeLessThanOrEqualTo(adminTotal);

        // 3. Tələbə yalnız yazıldığı kohortları görür
        var studentToken = await LoginAsync("student@pragma.edu.az", "Password123!");
        var studentReq = new HttpRequestMessage(HttpMethod.Get, "/api/cohorts");
        studentReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var studentRes = await _client.SendAsync(studentReq);
        studentRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var studentPaged = await studentRes.Content.ReadFromJsonAsync<PagedResult<CohortSummaryDto>>();
        studentPaged.Should().NotBeNull();
        studentPaged!.TotalCount.Should().BeLessThanOrEqualTo(adminTotal);
    }
}
