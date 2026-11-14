using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.DTOs.Auth;
using Pragma.Application.DTOs.Categories;
using Pragma.Application.DTOs.Courses;
using Pragma.Domain.Enums;
using Xunit;

namespace Pragma.IntegrationTests;

public class CategoryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CategoryIntegrationTests(CustomWebApplicationFactory factory)
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
    public async Task GetCategories_ReturnsOk_AndValidStructure()
    {
        var response = await _client.GetAsync("/api/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
        categories.Should().NotBeNull();
    }

    [Fact]
    public async Task PostCategory_AsAdmin_Returns201Created_AndGetByIdReturnsTranslations()
    {
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createDto = new CreateCategoryDto(
            Name: "Data Science",
            Description: "Data Science Təsvir",
            IconName: "data-icon",
            SortOrder: 5,
            Translations: new List<CategoryTranslationDto>
            {
                new("az", "Məlumat Elmi", "Data Science AZ"),
                new("en", "Data Science", "Data Science EN"),
                new("ru", "Наука о данных", "Data Science RU")
            }
        );

        var postResponse = await _client.PostAsJsonAsync("/api/categories", createDto);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdId = await postResponse.Content.ReadFromJsonAsync<Guid>();
        createdId.Should().NotBeEmpty();

        // GET by ID with default / az
        var getResponseAz = await _client.GetAsync($"/api/categories/{createdId}?lang=az");
        getResponseAz.StatusCode.Should().Be(HttpStatusCode.OK);
        var categoryAz = await getResponseAz.Content.ReadFromJsonAsync<CategoryDto>();
        categoryAz.Should().NotBeNull();
        categoryAz!.Name.Should().Be("Məlumat Elmi");
        categoryAz.Translations.Should().HaveCount(3);

        // GET by ID with ru
        var getResponseRu = await _client.GetAsync($"/api/categories/{createdId}?lang=ru");
        getResponseRu.StatusCode.Should().Be(HttpStatusCode.OK);
        var categoryRu = await getResponseRu.Content.ReadFromJsonAsync<CategoryDto>();
        categoryRu.Should().NotBeNull();
        categoryRu!.Name.Should().Be("Наука о данных");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetCategoryById_WhenNotFound_Returns404ProblemDetails()
    {
        var nonExistentId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/categories/{nonExistentId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PostCategory_AsStudent_Returns403Forbidden()
    {
        var studentToken = await LoginAsync("student@pragma.edu.az", "Password123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var createDto = new CreateCategoryDto("İcazəsiz", null, null, 1);
        var response = await _client.PostAsJsonAsync("/api/categories", createDto);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task PutCategory_AsAdmin_WhenValid_Returns200Ok_AndUpdatesCategory()
    {
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 1. Kateqoriya yarat
        var createDto = new CreateCategoryDto("Original Cat", "Original Desc", "icon1", 1);
        var postResponse = await _client.PostAsJsonAsync("/api/categories", createDto);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var categoryId = await postResponse.Content.ReadFromJsonAsync<Guid>();

        // 2. PUT ilə yenilə
        var updateDto = new UpdateCategoryDto(
            Name: "Yenilənmiş Cat",
            Description: "Yenilənmiş Desc",
            IconName: "icon2",
            SortOrder: 2,
            Translations: new List<CategoryTranslationDto>
            {
                new("az", "Yenilənmiş Cat AZ", "AZ Desc"),
                new("en", "Updated Cat EN", "EN Desc")
            }
        );

        var putResponse = await _client.PutAsJsonAsync($"/api/categories/{categoryId}", updateDto);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. GET ilə yoxla
        var getResponse = await _client.GetAsync($"/api/categories/{categoryId}?lang=en");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var category = await getResponse.Content.ReadFromJsonAsync<CategoryDto>();
        category.Should().NotBeNull();
        category!.Name.Should().Be("Updated Cat EN");
        category.IconName.Should().Be("icon2");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task PutCategory_AsAdmin_WhenNotFound_Returns404ProblemDetails()
    {
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var nonExistentId = Guid.NewGuid();
        var updateDto = new UpdateCategoryDto("Test Name", null, null, 1);

        var response = await _client.PutAsJsonAsync($"/api/categories/{nonExistentId}", updateDto);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Be("Category.NotFound");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task DeleteCategory_AsAdmin_WhenNoLinkedCourses_ReturnsSuccess_AndCategoryIsNoLongerReturned()
    {
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 1. Kursu olmayan kateqoriya yarat
        var createDto = new CreateCategoryDto("Silinəcək Cat", null, null, 1);
        var postResponse = await _client.PostAsJsonAsync("/api/categories", createDto);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var categoryId = await postResponse.Content.ReadFromJsonAsync<Guid>();

        // 2. DELETE et
        var deleteResponse = await _client.DeleteAsync($"/api/categories/{categoryId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. GET 404 qaytarmalıdır
        var getResponse = await _client.GetAsync($"/api/categories/{categoryId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task DeleteCategory_AsAdmin_WhenHasLinkedCourses_Returns409ConflictProblemDetails()
    {
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 1. Kateqoriya yarat
        var createDto = new CreateCategoryDto("Kurslu Kateqoriya", null, null, 1);
        var postResponse = await _client.PostAsJsonAsync("/api/categories", createDto);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var categoryId = await postResponse.Content.ReadFromJsonAsync<Guid>();

        // 2. Bu kateqoriyaya bağlı kurs yarat
        var createCourseDto = new CreateCourseDto(
            Title: "Kateqoriyaya Bağlı Kurs",
            ShortDescription: "Təsvir",
            Description: "Ətraflı",
            ThumbnailUrl: null,
            Price: 50m,
            IsFree: false,
            Level: CourseLevel.Beginner,
            CategoryId: categoryId
        );
        var courseResponse = await _client.PostAsJsonAsync("/api/courses", createCourseDto);
        courseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. DELETE cəhd etdikdə 409 Conflict ProblemDetails qayıtmalıdır
        var deleteResponse = await _client.DeleteAsync($"/api/categories/{categoryId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problemDetails = await deleteResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Be("Category.HasLinkedCourses");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task PutCategory_And_DeleteCategory_AsTeacherOrStudent_Returns403Forbidden()
    {
        // 1. Admin kimi kateqoriya yarat
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createDto = new CreateCategoryDto("Yetki Test Cat", null, null, 1);
        var postResponse = await _client.PostAsJsonAsync("/api/categories", createDto);
        var categoryId = await postResponse.Content.ReadFromJsonAsync<Guid>();

        // 2. Müəllim kimi PUT və DELETE etməyə cəhd et -> 403
        var teacherToken = await LoginAsync("teacher@pragma.edu.az", "Password123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);

        var updateDto = new UpdateCategoryDto("Müəllim Dəyişir", null, null, 1);
        var teacherPutResponse = await _client.PutAsJsonAsync($"/api/categories/{categoryId}", updateDto);
        teacherPutResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var teacherDeleteResponse = await _client.DeleteAsync($"/api/categories/{categoryId}");
        teacherDeleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Tələbə kimi PUT və DELETE etməyə cəhd et -> 403
        var studentToken = await LoginAsync("student@pragma.edu.az", "Password123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var studentPutResponse = await _client.PutAsJsonAsync($"/api/categories/{categoryId}", updateDto);
        studentPutResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var studentDeleteResponse = await _client.DeleteAsync($"/api/categories/{categoryId}");
        studentDeleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task PostCategory_And_PutCategory_WithInvalidDto_Returns400BadRequestProblemDetails()
    {
        var adminToken = await LoginAsync("admin@pragma.edu.az", "PragmaAdmin2026!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 1. Ad çox qısa (< 2 simvol)
        var invalidShortNameDto = new CreateCategoryDto("A", null, null, 1);
        var shortNameResponse = await _client.PostAsJsonAsync("/api/categories", invalidShortNameDto);
        shortNameResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 2. Dəstəklənməyən dil kodu ("fr")
        var unsupportedLangDto = new CreateCategoryDto("Fransız Dili", null, null, 1, new List<CategoryTranslationDto>
        {
            new("fr", "Langue", "Desc")
        });
        var unsupportedResponse = await _client.PostAsJsonAsync("/api/categories", unsupportedLangDto);
        unsupportedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 3. Təkrarlanan dil kodu ("en", "en")
        var duplicateLangDto = new CreateCategoryDto("Duplicate Lang", null, null, 1, new List<CategoryTranslationDto>
        {
            new("en", "English 1", "Desc"),
            new("en", "English 2", "Desc")
        });
        var duplicateResponse = await _client.PostAsJsonAsync("/api/categories", duplicateLangDto);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 4. PUT üçün boş ad
        var invalidPutDto = new UpdateCategoryDto("", null, null, 1);
        var putResponse = await _client.PutAsJsonAsync($"/api/categories/{Guid.NewGuid()}", invalidPutDto);
        putResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
