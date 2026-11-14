using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Auth;
using Pragma.Domain.Enums;
using Xunit;

namespace Pragma.IntegrationTests;

public class CohortValidationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CohortValidationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> LoginAsAdminAsync()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto("admin@pragma.edu.az", "PragmaAdmin2026!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResult = await loginResponse.Content.ReadFromJsonAsync<TokenResultDto>();
        tokenResult.Should().NotBeNull();
        return tokenResult!.AccessToken;
    }

    #region 1. BulkAttendanceDto Validation Tests

    [Fact]
    public async Task BulkAttendance_WhenEntriesIsEmpty_Returns400BadRequest()
    {
        var adminToken = await LoginAsAdminAsync();
        var cohortId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/cohorts/{cohortId}/attendance")
        {
            Content = JsonContent.Create(new BulkAttendanceDto(
                Date: DateOnly.FromDateTime(DateTime.UtcNow),
                Entries: new List<AttendanceEntryDto>() // Boş siyahı
            ))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Validation Error");
        body.Should().Contain("Davamiyyət siyahısı boş ola bilməz");
    }

    [Fact]
    public async Task BulkAttendance_WhenDuplicateStudentId_Returns400BadRequest()
    {
        var adminToken = await LoginAsAdminAsync();
        var cohortId = Guid.NewGuid();
        var duplicateStudentId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/cohorts/{cohortId}/attendance")
        {
            Content = JsonContent.Create(new BulkAttendanceDto(
                Date: DateOnly.FromDateTime(DateTime.UtcNow),
                Entries: new List<AttendanceEntryDto>
                {
                    new(duplicateStudentId, AttendanceStatus.Present),
                    new(duplicateStudentId, AttendanceStatus.Absent) // Təkrarlanan StudentId
                }
            ))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Validation Error");
        body.Should().Contain("Eyni tələbə üçün birdən çox davamiyyət qeydi göndərilə bilməz");
    }

    #endregion

    #region 2. BulkSubmitGradesDto Validation Tests

    [Fact]
    public async Task BulkSubmitGrades_WhenGradesIsEmpty_Returns400BadRequest()
    {
        var adminToken = await LoginAsAdminAsync();
        var cohortId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/cohorts/{cohortId}/grades/bulk")
        {
            Content = JsonContent.Create(new BulkSubmitGradesDto(
                Grades: new List<BulkGradeStudentItemDto>() // Boş siyahı
            ))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Validation Error");
        body.Should().Contain("Qiymət siyahısı boş ola bilməz");
    }

    [Fact]
    public async Task BulkSubmitGrades_WhenDuplicateStudentId_Returns400BadRequest()
    {
        var adminToken = await LoginAsAdminAsync();
        var cohortId = Guid.NewGuid();
        var duplicateStudentId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/cohorts/{cohortId}/grades/bulk")
        {
            Content = JsonContent.Create(new BulkSubmitGradesDto(
                Grades: new List<BulkGradeStudentItemDto>
                {
                    new(duplicateStudentId, 80m, 85m),
                    new(duplicateStudentId, 90m, 95m) // Təkrarlanan StudentId
                }
            ))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Validation Error");
        body.Should().Contain("Eyni tələbə üçün birdən çox qiymət qeydi göndərilə bilməz");
    }

    [Fact]
    public async Task BulkSubmitGrades_WhenScoreIs150_Returns400BadRequest()
    {
        var adminToken = await LoginAsAdminAsync();
        var cohortId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/cohorts/{cohortId}/grades/bulk")
        {
            Content = JsonContent.Create(new BulkSubmitGradesDto(
                Grades: new List<BulkGradeStudentItemDto>
                {
                    new(Guid.NewGuid(), 150m, 80m) // 150 bal (limitdən artıq)
                }
            ))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Validation Error");
        body.Should().Contain("Mini layihə balı 0 ilə 100 arasında olmalıdır");
    }

    #endregion

    #region 3. SubmitGradeDto (SubmitComponentGrade) Validation Tests

    [Fact]
    public async Task SubmitComponentGrade_WhenScoreIs150_Returns400BadRequest()
    {
        var adminToken = await LoginAsAdminAsync();
        var cohortId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/cohorts/{cohortId}/grades")
        {
            Content = JsonContent.Create(new SubmitGradeDto(
                StudentId: Guid.NewGuid(),
                ComponentType: GradeComponentType.Final,
                Score: 150m // 150 bal (limitdən artıq)
            ))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Validation Error");
        body.Should().Contain("Bal 0 ilə 100 arasında olmalıdır");
    }

    #endregion
}
