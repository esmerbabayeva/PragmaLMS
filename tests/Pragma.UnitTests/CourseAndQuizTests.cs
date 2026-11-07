using Pragma.Application.DTOs.Courses;
using Pragma.Application.DTOs.Quizzes;
using Pragma.Application.Validators.Courses;
using Pragma.Application.Validators.Quizzes;
using Pragma.Domain.Enums;

namespace Pragma.UnitTests;

public class CourseAndQuizTests
{
    [Fact]
    public void CreateCourseValidator_WhenTitleIsEmpty_ReturnsValidationError()
    {
        // Arrange
        var validator = new CreateCourseValidator();
        var dto = new CreateCourseDto(
            Title: "",
            ShortDescription: "Qısa təsvir",
            Description: "Tam təsvir",
            ThumbnailUrl: null,
            Price: 50m,
            IsFree: false,
            Level: CourseLevel.Beginner,
            CategoryId: Guid.NewGuid()
        );

        // Act
        var result = validator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCourseDto.Title));
    }

    [Fact]
    public void CreateCourseValidator_WhenPriceIsNegative_ReturnsValidationError()
    {
        // Arrange
        var validator = new CreateCourseValidator();
        var dto = new CreateCourseDto(
            Title: "C# Masterclass",
            ShortDescription: "Qısa təsvir",
            Description: "Tam təsvir",
            ThumbnailUrl: null,
            Price: -20m, // Mənfi qiymət
            IsFree: false,
            Level: CourseLevel.Beginner,
            CategoryId: Guid.NewGuid()
        );

        // Act
        var result = validator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCourseDto.Price));
    }

    [Fact]
    public void CreateQuizValidator_WhenQuestionHasNoCorrectOption_ReturnsValidationError()
    {
        // Arrange
        var validator = new CreateQuizValidator();
        var dto = new CreateQuizDto(
            Title: "C# Təməlləri Testi",
            Description: "Test",
            PassingScorePercentage: 70,
            TimeLimitMinutes: 15,
            MaxAttempts: 3,
            Questions: new List<CreateQuestionDto>
            {
                new(
                    Text: "C# hansı növ dildir?",
                    Points: 10,
                    OrderIndex: 1,
                    Explanation: "İzah",
                    Options: new List<CreateAnswerOptionDto>
                    {
                        new("Funksional", false),
                        new("Yalnız skript", false) // Heç biri düzgün deyil!
                    }
                )
            }
        );

        // Act
        var result = validator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("1 düzgün cavab variantı"));
    }

    [Fact]
    public void CreateQuizValidator_WhenValid_ReturnsSuccess()
    {
        // Arrange
        var validator = new CreateQuizValidator();
        var dto = new CreateQuizDto(
            Title: "C# Təməlləri Testi",
            Description: "Test",
            PassingScorePercentage: 70,
            TimeLimitMinutes: 15,
            MaxAttempts: 3,
            Questions: new List<CreateQuestionDto>
            {
                new(
                    Text: "C# tip təhlükəsizdirmi?",
                    Points: 10,
                    OrderIndex: 1,
                    Explanation: "Bəli, güclü tipləşdirilmiş dildir.",
                    Options: new List<CreateAnswerOptionDto>
                    {
                        new("Bəli", true),
                        new("Xeyr", false)
                    }
                )
            }
        );

        // Act
        var result = validator.Validate(dto);

        // Assert
        Assert.True(result.IsValid);
    }
}
