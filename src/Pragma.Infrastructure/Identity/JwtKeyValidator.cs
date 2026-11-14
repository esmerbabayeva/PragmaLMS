namespace Pragma.Infrastructure.Identity;

/// <summary>
/// Mühitə uyğun olaraq JWT SecretKey konfiqurasiyasının təhlükəsizlik qaydalarını yoxlayan statik validator.
/// </summary>
public static class JwtKeyValidator
{
    public const string DefaultFallbackKey = "PragmaSuperSecretKeyForDevelopmentOnly1234567890!@#$";

    /// <summary>
    /// JWT SecretKey-i mühitə görə yoxlayır və qaytarır.
    /// Development və Testing mühitlərində açar boş olduqda default fallback açarı qaytarır.
    /// Digər mühitlərdə (məs. Production, Staging):
    /// - Açar boş və ya whitespace olduqda,
    /// - 32 simvoldan qısa olduqda,
    /// - Default fallback açara bərabər olduqda,
    /// - və ya "YourSecretKeyHere" ehtiva etdikdə,
    /// InvalidOperationException atılır (mesajda açarın özü heç vaxt göstərilmir).
    /// </summary>
    public static string ValidateAndGetKey(string? secretKey, string? environmentName)
    {
        var isDevOrTest = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

        if (isDevOrTest)
        {
            return string.IsNullOrWhiteSpace(secretKey) ? DefaultFallbackKey : secretKey;
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("JWT SecretKey konfiqurasiyası təyin edilməyib və ya boşdur. Production mühitində etibarlı açar tələb olunur.");
        }

        if (secretKey.Length < 32)
        {
            throw new InvalidOperationException("JWT SecretKey ən azı 32 simvol uzunluğunda olmalıdır.");
        }

        if (string.Equals(secretKey, DefaultFallbackKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Production mühitində default inkişaf (fallback) JWT açarından istifadə edilə bilməz.");
        }

        if (secretKey.Contains("YourSecretKeyHere", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Production mühitində nümunə/yer tutucu (placeholder) JWT açarından istifadə edilə bilməz.");
        }

        return secretKey;
    }
}
