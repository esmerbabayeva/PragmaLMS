using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Auth;
using Pragma.Application.DTOs.Cohorts;
using Pragma.Application.DTOs.Courses;
using Pragma.Application.DTOs.Teacher;
using Pragma.Domain.Enums;
using Xunit;

namespace Pragma.IntegrationTests;

public class TeacherAuthorizationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TeacherAuthorizationIntegrationTests(CustomWebApplicationFactory factory)
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
    public async Task Teacher_AttemptingToCreateCourse_ShouldReturn403Forbidden()
    {
        // 1. Müəllim kimi daxil ol
        var teacherToken = await LoginAsync("teacher@pragma.edu.az", "Password123!");

        // 2. Kurs yaratmağa cəhd et
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/courses")
        {
            Content = JsonContent.Create(new CreateCourseDto(
                Title: "İcazəsiz Kurs",
                ShortDescription: "Təsvir",
                Description: "Detallı",
                ThumbnailUrl: null,
                Price: 0m,
                IsFree: true,
                Level: CourseLevel.Beginner,
                CategoryId: Guid.NewGuid()
            ))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);

        var response = await _client.SendAsync(request);

        // 3. 403 Forbidden olmalıdır (Müəllim kurs yarada bilməz)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_AttemptingToPublishOrDeleteCourse_ShouldReturn403Forbidden()
    {
        var teacherToken = await LoginAsync("teacher@pragma.edu.az", "Password123!");
        var randomCourseId = Guid.NewGuid();

        // Publish cəhdi
        var pubRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/courses/{randomCourseId}/publish");
        pubRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        var pubResponse = await _client.SendAsync(pubRequest);
        pubResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Delete cəhdi
        var delRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/courses/{randomCourseId}");
        delRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        var delResponse = await _client.SendAsync(delRequest);
        delResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_GetAssignedGroups_ShouldReturn200OkWithGroups()
    {
        var teacherToken = await LoginAsync("teacher@pragma.edu.az", "Password123!");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/teacher/groups");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var groups = await response.Content.ReadFromJsonAsync<List<AssignedGroupSummaryDto>>();
        groups.Should().NotBeNull();
        groups.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Student_AttemptingToAccessTeacherGroups_ShouldReturn403Forbidden()
    {
        var studentToken = await LoginAsync("student@pragma.edu.az", "Password123!");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/teacher/groups");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Student_AttemptingToPostAttendance_ShouldReturn403Forbidden()
    {
        var studentToken = await LoginAsync("student@pragma.edu.az", "Password123!");
        var cohortId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/cohorts/{cohortId}/attendance")
        {
            Content = JsonContent.Create(new BulkAttendanceDto(
                Date: DateOnly.FromDateTime(DateTime.UtcNow),
                Entries: new List<AttendanceEntryDto>()
            ))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Student_AttemptingToPostComponentGrade_ShouldReturn403Forbidden()
    {
        var studentToken = await LoginAsync("student@pragma.edu.az", "Password123!");
        var cohortId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/cohorts/{cohortId}/grades")
        {
            Content = JsonContent.Create(new SubmitGradeDto(
                StudentId: Guid.NewGuid(),
                ComponentType: GradeComponentType.Final,
                Score: 90m
            ))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Student_AttemptingToPostBulkGrades_ShouldReturn403Forbidden()
    {
        var studentToken = await LoginAsync("student@pragma.edu.az", "Password123!");
        var cohortId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/cohorts/{cohortId}/grades/bulk")
        {
            Content = JsonContent.Create(new BulkSubmitGradesDto(
                Grades: new List<BulkGradeStudentItemDto>()
            ))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Student_AttemptingToAccessAnotherStudentGrade_ShouldReturn403Forbidden()
    {
        // 1. Leyla kimi daxil ol və öz UserId-sini götür
        var leylaToken = await LoginAsync("leyla@pragma.edu.az", "Password123!");
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", leylaToken);
        var meResponse = await _client.SendAsync(meRequest);
        var leylaUser = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
        leylaUser.Should().NotBeNull();

        // 2. Əsmər (student@pragma.edu.az) kimi daxil ol və kohortları oxu
        var esmerToken = await LoginAsync("student@pragma.edu.az", "Password123!");
        var cohortsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/cohorts");
        cohortsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", esmerToken);
        var cohortsResponse = await _client.SendAsync(cohortsRequest);
        cohortsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cohorts = await cohortsResponse.Content.ReadFromJsonAsync<PagedResult<CohortSummaryDto>>();
        cohorts.Should().NotBeNull();
        cohorts!.Items.Should().NotBeEmpty();

        var cohortId = cohorts.Items.First().Id;

        // 3. Əsmər Leylanın qiymətini çəkməyə cəhd edir (IDOR cəhdi)
        var idorRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/cohorts/{cohortId}/grades/students/{leylaUser!.Id}");
        idorRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", esmerToken);

        var response = await _client.SendAsync(idorRequest);

        // 4. Əsmər həmin kohortun üzvü olsa belə, müəllim və ya həmin tələbə olmadığı üçün 403 Forbidden almalıdır
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_PostingInvalidCohortWeights_TriggersValidationFilterAndReturns400BadRequest()
    {
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");

        // Invalid cohort: weights sum is 20 + 20 + 20 = 60 != 100
        var invalidDto = new CreateCohortDto(
            Name: "Test Invalid Cohort",
            CourseId: Guid.NewGuid(),
            TeacherId: Guid.NewGuid(),
            StartDate: DateTime.UtcNow.AddDays(1),
            TotalHours: 40,
            QuizWeight: 20m,
            MiniProjectWeight: 20m,
            FinalWeight: 20m
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/cohorts")
        {
            Content = JsonContent.Create(invalidDto)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Validation Error");
    }
}
