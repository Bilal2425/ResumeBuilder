using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using BaseApi.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaseApi.Service
{
    public class EmailService
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<SmtpSettings> settings, ILogger<EmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<bool> SendResetTokenEmailAsync(string toEmail, string token)
        {
            string subject = "Password Reset Request - ResumeCraft";
            string body = $@"
<h3>ResumeCraft Password Reset</h3>
<p>You requested a password reset for your ResumeCraft account.</p>
<p>Please use the following reset token to set a new password:</p>
<p style=""font-size: 16px; font-weight: bold; background-color: #f1f5f9; padding: 10px 15px; border-radius: 4px; display: inline-block; font-family: monospace; letter-spacing: 1px;"">
    {token}
</p>
<p>If you did not request this reset, please ignore this email.</p>
<hr/>
<p style=""font-size: 11px; color: #64748b;"">This is an automated message. Please do not reply directly to this email.</p>";

            // Fallback to Console logging if SMTP is not configured
            if (string.IsNullOrEmpty(_settings.Server) || string.IsNullOrEmpty(_settings.Username) || string.IsNullOrEmpty(_settings.Password))
            {
                _logger.LogInformation("====================================================");
                _logger.LogInformation($"DEV MODE: Email to {toEmail} would have been sent.");
                _logger.LogInformation($"Token: {token}");
                _logger.LogInformation("Configure SmtpSettings in appsettings.json to send actual emails.");
                _logger.LogInformation("====================================================");
                return false;
            }

            try
            {
                using (var client = new SmtpClient(_settings.Server, _settings.Port))
                {
                    client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
                    client.EnableSsl = _settings.EnableSsl;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(toEmail);

                    _logger.LogInformation($"Sending reset email to {toEmail} via {_settings.Server}...");
                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation("Reset email sent successfully.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {toEmail} via SMTP.");
                throw;
            }
        }
    }
}
