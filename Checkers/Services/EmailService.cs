using Checkers.Settings;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Checkers.Services
{
    public interface IEmailService
    {
        Task SendActivationEmailAsync(string toEmail, string activationLink);
    }
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly EmailSettings _emailSettings;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IOptions<EmailSettings> emailSettings)
        {
            _configuration = configuration;
            _logger = logger;
            _emailSettings = emailSettings.Value;
        }

        public async Task SendActivationEmailAsync(string toEmail, string activationLink)
        {
            try
            {
                var smtpHost = _emailSettings.SmtpHost;
                var smtpPort = int.Parse(_emailSettings.SmtpPort ?? "587");
                var smtpUsername = _emailSettings.SmtpUsername;
                var smtpPassword = _emailSettings.SmtpPassword;
                var fromEmail = _emailSettings.FromEmail;
                var fromName = _emailSettings.FromName;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = "Activate Your Checkers Account",
                    Body = GetEmailBody(activationLink),
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = true
                };

                await smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("Activation email sent successfully to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send activation email to {Email}", toEmail);
                throw new Exception($"Failed to send activation email: {ex.Message}");
            }
        }
        private string GetEmailBody(string activationLink)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Activate Your Account</title>
</head>
<body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #f4f4f4; padding: 20px;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 40px 20px; text-align: center;'>
                            <h1 style='color: #ffffff; margin: 0; font-size: 28px;'>Welcome to Checkers!</h1>
                        </td>
                    </tr>
                    
                    <!-- Body -->
                    <tr>
                        <td style='padding: 40px 30px;'>
                            <h2 style='color: #333333; margin: 0 0 20px 0; font-size: 24px;'>Activate Your Account</h2>
                            <p style='color: #666666; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;'>
                                Thank you for registering with Checkers! We're excited to have you on board.
                            </p>
                            <p style='color: #666666; font-size: 16px; line-height: 1.6; margin: 0 0 30px 0;'>
                                To complete your registration and activate your account, please click the button below:
                            </p>
                            
                            <!-- Button -->
                            <table width='100%' cellpadding='0' cellspacing='0'>
                                <tr>
                                    <td align='center' style='padding: 20px 0;'>
                                        <a href='{activationLink}' 
                                           style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                                                  color: #ffffff; 
                                                  text-decoration: none; 
                                                  padding: 15px 40px; 
                                                  border-radius: 5px; 
                                                  font-size: 16px; 
                                                  font-weight: bold;
                                                  display: inline-block;'>
                                            Activate My Account
                                        </a>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Alternative Link -->
                            <p style='color: #666666; font-size: 14px; line-height: 1.6; margin: 30px 0 0 0;'>
                                Or copy and paste this link into your browser:
                            </p>
                            <p style='background-color: #f8f9fa; 
                                      padding: 15px; 
                                      border-radius: 5px; 
                                      word-break: break-all; 
                                      color: #667eea; 
                                      font-size: 14px;
                                      margin: 10px 0 0 0;'>
                                {activationLink}
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #f8f9fa; padding: 30px; text-align: center; border-top: 1px solid #e9ecef;'>
                            <p style='color: #999999; font-size: 14px; margin: 0 0 10px 0;'>
                                If you didn't create an account with Checkers, you can safely ignore this email.
                            </p>
                            <p style='color: #999999; font-size: 12px; margin: 0;'>
                                © 2025 Checkers. All rights reserved.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
            ";
        }
    }
}
