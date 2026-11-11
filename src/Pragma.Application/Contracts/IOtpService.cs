namespace Pragma.Application.Contracts;

public interface IOtpService
{
    string GenerateCode();
    string HashCode(string code);
    bool VerifyCode(string inputCode, string storedHash);
}
