using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pragma.Infrastructure;
using Pragma.Infrastructure.Identity;
using Xunit;

namespace Pragma.UnitTests;

public class JwtKeyValidatorTests
{
    private const string ValidKey = "ThisIsAStrongProductionSecretKeyWithAtLeast32Characters!";
    private const string ShortKey = "TooShortKey123";
    private const string PlaceholderKey = "prefix_YourSecretKeyHere_suffix_1234567890";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateAndGetKey_InProduction_WhenKeyIsNullOrWhitespace_ThrowsInvalidOperationException(string? key)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtKeyValidator.ValidateAndGetKey(key, "Production"));

        Assert.Contains("JWT SecretKey", ex.Message);
    }

    [Fact]
    public void ValidateAndGetKey_InProduction_WhenKeyIsShorterThan32Chars_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtKeyValidator.ValidateAndGetKey(ShortKey, "Production"));

        Assert.Contains("32 simvol", ex.Message);
        Assert.DoesNotContain(ShortKey, ex.Message);
    }

    [Fact]
    public void ValidateAndGetKey_InProduction_WhenKeyEqualsDefaultFallback_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtKeyValidator.ValidateAndGetKey(JwtKeyValidator.DefaultFallbackKey, "Production"));

        Assert.Contains("default inkişaf (fallback)", ex.Message);
        Assert.DoesNotContain(JwtKeyValidator.DefaultFallbackKey, ex.Message);
    }

    [Fact]
    public void ValidateAndGetKey_InProduction_WhenKeyContainsPlaceholder_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtKeyValidator.ValidateAndGetKey(PlaceholderKey, "Production"));

        Assert.Contains("yer tutucu (placeholder)", ex.Message);
        Assert.DoesNotContain(PlaceholderKey, ex.Message);
    }

    [Fact]
    public void ValidateAndGetKey_InProduction_WhenKeyIsValid_ReturnsKey()
    {
        var result = JwtKeyValidator.ValidateAndGetKey(ValidKey, "Production");

        Assert.Equal(ValidKey, result);
    }

    [Fact]
    public void ValidateAndGetKey_InStaging_WhenKeyIsInvalid_AlsoThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JwtKeyValidator.ValidateAndGetKey(ShortKey, "Staging"));
    }

    [Fact]
    public void ValidateAndGetKey_InDevelopment_WhenKeyIsNullOrWhitespace_ReturnsDefaultFallbackKey()
    {
        var result = JwtKeyValidator.ValidateAndGetKey(null, "Development");

        Assert.Equal(JwtKeyValidator.DefaultFallbackKey, result);
    }

    [Fact]
    public void ValidateAndGetKey_InDevelopment_WhenKeyIsProvided_ReturnsProvidedKeyEvenIfShort()
    {
        var result = JwtKeyValidator.ValidateAndGetKey(ShortKey, "Development");

        Assert.Equal(ShortKey, result);
    }

    [Fact]
    public void ValidateAndGetKey_InDevelopment_WhenKeyIsDefaultFallback_ReturnsDefaultFallback()
    {
        var result = JwtKeyValidator.ValidateAndGetKey(JwtKeyValidator.DefaultFallbackKey, "Development");

        Assert.Equal(JwtKeyValidator.DefaultFallbackKey, result);
    }

    [Fact]
    public void ValidateAndGetKey_InTesting_WhenKeyIsNullOrWhitespace_ReturnsDefaultFallbackKey()
    {
        var result = JwtKeyValidator.ValidateAndGetKey("", "Testing");

        Assert.Equal(JwtKeyValidator.DefaultFallbackKey, result);
    }

    [Fact]
    public void ValidateAndGetKey_InTesting_WhenKeyContainsPlaceholder_DoesNotThrowAndReturnsKey()
    {
        var result = JwtKeyValidator.ValidateAndGetKey(PlaceholderKey, "Testing");

        Assert.Equal(PlaceholderKey, result);
    }

    [Fact]
    public void ValidateAndGetKey_WhenEnvironmentIsNull_BehavesLikeProductionAndThrowsOnInvalidKey()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtKeyValidator.ValidateAndGetKey(ShortKey, null));

        Assert.Contains("32 simvol", ex.Message);
    }

    [Fact]
    public void AddInfrastructureServices_WhenEnvironmentIsNull_BehavesLikeProductionAndThrowsOnInvalidKey()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build(); // No key, empty

        Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructureServices(config, environment: null));
    }
}
