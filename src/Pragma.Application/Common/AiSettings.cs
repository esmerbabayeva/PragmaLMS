namespace Pragma.Application.Common;

/// <summary>
/// Süni intellekt və xarici AI provayderləri üçün tənzimləmələr (POCO).
/// Clean Architecture: Application qatı IConfiguration-dan asılı olmur.
/// </summary>
public class AiSettings
{
    public string? GeminiApiKey { get; set; }
}
