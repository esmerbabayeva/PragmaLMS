using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Pragma.Application.Contracts;

namespace Pragma.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly byte[] _pepperBytes;

    public OtpService(IConfiguration configuration)
    {
        var pepper = configuration["Otp:Pepper"] ?? "PragmaLMS_Secure_Otp_Pepper_Key_2026";
        _pepperBytes = Encoding.UTF8.GetBytes(pepper);
    }

    public string GenerateCode()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
    }

    public string HashCode(string code)
    {
        using var hmac = new HMACSHA256(_pepperBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(code.Trim()));
        return Convert.ToHexString(hash);
    }

    public bool VerifyCode(string inputCode, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(inputCode) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var computedHash = HashCode(inputCode);
        var computedBytes = Encoding.UTF8.GetBytes(computedHash);
        var storedBytes = Encoding.UTF8.GetBytes(storedHash);

        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }
}
