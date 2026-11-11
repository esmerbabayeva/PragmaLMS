using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Pragma.Application.DTOs.Auth;
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
}
