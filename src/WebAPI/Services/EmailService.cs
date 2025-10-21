using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SuperPanel.WebAPI.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = false);
        Task SendEmailAsync(string[] to, string subject, string body, bool isHtml = false);
    }

    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;

        public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false)
        {
            await SendEmailAsync(new[] { to }, subject, body, isHtml);
        }

        public async Task SendEmailAsync(string[] to, string subject, string body, bool isHtml = false)
        {
            try
            {
                var smtpSettings = _configuration.GetSection("Smtp");
                var smtpServer = smtpSettings["Server"];
                var smtpPort = int.Parse(smtpSettings["Port"] ?? "587");
                var smtpUsername = smtpSettings["Username"];
                var smtpPassword = smtpSettings["Password"];
                var smtpEnableSsl = bool.Parse(smtpSettings["EnableSsl"] ?? "true");
                var fromEmail = smtpSettings["FromEmail"] ?? "noreply@superpanel.com";
                var fromName = smtpSettings["FromName"] ?? "SuperPanel Alert System";

                if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    _logger.LogWarning("SMTP settings not configured. Skipping email send to {Recipients}", string.Join(", ", to));
                    return;
                }

                using var smtpClient = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = smtpEnableSsl,
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml,
                };

                foreach (var recipient in to)
                {
                    if (!string.IsNullOrWhiteSpace(recipient))
                    {
                        mailMessage.To.Add(recipient.Trim());
                    }
                }

                await smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("Email sent successfully to {Count} recipients: {Subject}", to.Length, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Recipients}: {Subject}", string.Join(", ", to), subject);
                throw;
            }
        }
    }
}