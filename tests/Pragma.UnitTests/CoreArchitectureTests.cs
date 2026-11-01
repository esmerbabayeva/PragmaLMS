using Pragma.Application.Common;
using Pragma.Application.DTOs.Auth;
using Pragma.Application.Validators.Auth;
using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.UnitTests;

public class TestEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}

public class CoreArchitectureTests
{
    [Fact]
    public void BaseEntity_WhenCreated_HasValidDefaults()
    {
        // Arrange & Act
        var entity = new TestEntity { Name = "Test" };

        // Assert
        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.False(entity.IsDeleted);
        Assert.Null(entity.UpdatedAtUtc);
        Assert.True(entity.CreatedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void BaseEntity_WhenMarkedDeleted_IsDeletedIsTrue()
    {
        // Arrange
        var entity = new TestEntity { Name = "Test" };

        // Act
        entity.MarkDeleted();

        // Assert
        Assert.True(entity.IsDeleted);
        Assert.NotNull(entity.UpdatedAtUtc);
    }

    [Fact]
    public void Result_WhenSuccess_HasValueAndNoError()
    {
        // Act
        var result = Result<string>.Success("Uğurlu!");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("Uğurlu!", result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Result_WhenFailure_ThrowsWhenAccessingValue()
    {
        // Arrange
        var error = Error.NotFound("Course.NotFound", "Kurs tapılmadı.");

        // Act
        var result = Result<string>.Failure(error);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void RegisterValidator_WhenEmailIsInvalid_ReturnsError()
    {
        // Arrange
        var validator = new RegisterRequestValidator();
        var dto = new RegisterRequestDto(
            FirstName: "Ali",
            LastName: "Aliyev",
            Email: "invalid-email-format",
            Password: "Password123!",
            ConfirmPassword: "Password123!",
            Role: UserRoles.Student
        );

        // Act
        var result = validator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequestDto.Email));
    }
}
