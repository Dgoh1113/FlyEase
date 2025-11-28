using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace FlyEase.Services
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderPassword { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
    }

    public interface IEmailService
    {
        Task<bool> SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string newPassword);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task<bool> SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string newPassword)
        {
            try
            {
                var subject = "Your New Password - FlyEase";
                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 10px 10px; }}
        .password-box {{ background: #fff; border: 2px dashed #667eea; padding: 15px; text-align: center; margin: 20px 0; font-size: 18px; font-weight: bold; color: #667eea; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #666; }}
        .btn-login {{ background: #667eea; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>FlyEase</h1>
            <h2>Password Reset</h2>
        </div>
        <div class='content'>
            <p>Dear {recipientName},</p>
            <p>Your password has been reset successfully. Here is your new temporary password:</p>
            
            <div class='password-box'>
                {newPassword}
            </div>
            
            <p><strong>Important Security Instructions:</strong></p>
            <ol>
                <li>Login immediately using this temporary password</li>
                <li>Change your password after logging in for security</li>
                <li>Do not share this password with anyone</li>
                <li>If you didn't request this reset, please contact us immediately</li>
            </ol>
            
            <p>
                <a href='[YOUR_APP_URL]/Auth/Login' class='btn-login'>
                    Login to Your Account
                </a>
            </p>
        </div>
        <div class='footer'>
            <p>This is an automated message. Please do not reply to this email.</p>
            <p>&copy; 2024 FlyEase. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

                return await SendEmailAsync(recipientEmail, recipientName, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email to {Email}", recipientEmail);
                return false;
            }
        }

        private async Task<bool> SendEmailAsync(string recipientEmail, string recipientName, string subject, string body)
        {
            try
            {
                using (var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword);
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(new MailAddress(recipientEmail, recipientName));

                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation("Email sent successfully to {Email}", recipientEmail);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", recipientEmail);
                return false;
            }
        }
    }
}