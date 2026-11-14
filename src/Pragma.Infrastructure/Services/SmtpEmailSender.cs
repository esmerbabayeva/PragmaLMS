using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using Pragma.Application.Contracts;

namespace Pragma.Infrastructure.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly IHostEnvironment _env;

    public SmtpEmailSender(
        IConfiguration configuration,
        ILogger<SmtpEmailSender> logger,
        IHostEnvironment env)
    {
        _configuration = configuration;
        _logger = logger;
        _env = env;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Smtp:Host"];
        var user = _configuration["Smtp:User"];
        var pass = _configuration["Smtp:AppPassword"] ?? _configuration["Smtp:Password"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            if (_env.IsDevelopment())
            {
                _logger.LogInformation("================ [DEVELOPMENT EMAIL DISPATCH] ================");
                _logger.LogInformation("To: {To}", to);
                _logger.LogInformation("Subject: {Subject}", subject);
                _logger.LogInformation("Body: {HtmlBody}", htmlBody);
                _logger.LogInformation("==============================================================");
                return;
            }

            throw new InvalidOperationException("SMTP credentials are not configured in production.");
        }

        var port = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 587;
        var fromName = _configuration["Smtp:FromName"] ?? "Pragma LMS";
        var fromAddress = _configuration["Smtp:FromAddress"] ?? user;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(user, pass, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Email sent successfully to {To}", to);
    }
}
