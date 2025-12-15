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
        public string AdminEmail { get; set; } = string.Empty;
    }

    public interface IEmailService
    {
        Task<bool> SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string newPassword);
        Task<bool> SendPasswordResetLinkAsync(string recipientEmail, string recipientName, string resetLink);
        Task<bool> SendContactFormEmailAsync(ContactFormData contactData);
        Task<bool> SendBookingConfirmationEmailAsync(BookingConfirmationData bookingData);
    }

    public class ContactFormData
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
    }

    public class BookingConfirmationData
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public DateTime TravelDate { get; set; }
        public decimal Amount { get; set; }
        public int NumberOfPeople { get; set; }
    }

    public class PasswordResetData
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ResetCode { get; set; } = string.Empty;
        public DateTime ExpiryTime { get; set; }
    }

    public class ForgetEmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<ForgetEmailService> _logger;

        public ForgetEmailService(IOptions<EmailSettings> emailSettings, ILogger<ForgetEmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        // 1. Password Reset Link
        public async Task<bool> SendPasswordResetLinkAsync(string recipientEmail, string recipientName, string resetLink)
        {
            try
            {
                var subject = "Reset Your Password - FlyEase Travel";
                var body = $@"
                <!DOCTYPE html><html><head><style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                    .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
                    .btn-reset {{ background: #667eea; color: white; padding: 14px 28px; text-decoration: none; border-radius: 8px; display: inline-block; font-weight: 600; margin: 20px 0; }}
                </style></head><body>
                    <div class='container'>
                        <div class='header'><h1>✈️ FlyEase Travel</h1><p>Password Reset Request</p></div>
                        <div class='content'>
                            <p>Hello <strong>{recipientName}</strong>,</p>
                            <p>Click below to reset your password:</p>
                            <div style='text-align: center;'><a href='{resetLink}' class='btn-reset'>Reset My Password</a></div>
                            <p><small>Link expires in 30 seconds.</small></p>
                        </div>
                    </div>
                </body></html>";

                return await SendEmailAsync(recipientEmail, recipientName, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset link to {Email}", recipientEmail);
                return false;
            }
        }

        // 2. Password Reset (Temp Password)
        public async Task<bool> SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string newPassword)
        {
            try
            {
                var subject = "Your New Password - FlyEase";
                var body = $"Your new temporary password is: <b>{newPassword}</b><br>Please login and change it immediately.";
                return await SendEmailAsync(recipientEmail, recipientName, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email to {Email}", recipientEmail);
                return false;
            }
        }

        // 3. CONTACT FORM (UPDATED FOR REPLY-TO)
        public async Task<bool> SendContactFormEmailAsync(ContactFormData contactData)
        {
            try
            {
                // --- Email to Admin ---
                var adminSubject = $"New Contact Form: {contactData.Subject}";
                var adminBody = $@"
                <!DOCTYPE html><html><head><style>
                    body {{ font-family: Arial, sans-serif; }}
                    .container {{ padding: 20px; border: 1px solid #ddd; border-radius: 5px; }}
                    .label {{ font-weight: bold; color: #555; }}
                </style></head><body>
                    <div class='container'>
                        <h2>📨 New Message from Website</h2>
                        <p><span class='label'>Name:</span> {contactData.Name}</p>
                        <p><span class='label'>Email:</span> {contactData.Email}</p>
                        <p><span class='label'>Phone:</span> {contactData.Phone ?? "N/A"}</p>
                        <p><span class='label'>Subject:</span> {contactData.Subject}</p>
                        <hr>
                        <p><strong>Message:</strong></p>
                        <p>{contactData.Message.Replace("\n", "<br>")}</p>
                        <br>
                        <p style='color: #888;'><em>Tip: Just click 'Reply' to respond to {contactData.Name}.</em></p>
                    </div>
                </body></html>";

                // SEND TO ADMIN with REPLY-TO set to CUSTOMER'S EMAIL
                var adminSuccess = await SendEmailAsync(
                    _emailSettings.AdminEmail,
                    "FlyEase Admin",
                    adminSubject,
                    adminBody,
                    contactData.Email // <--- AUTO-DETECT REPLAY TO THIS EMAIL
                );

                // --- Auto-reply to Customer ---
                var customerSubject = "We received your message - FlyEase Travel";
                var customerBody = $"Hi {contactData.Name},<br><br>Thanks for reaching out! We have received your message regarding '{contactData.Subject}' and will get back to you within 24 hours.<br><br>Best regards,<br>FlyEase Team";

                var customerSuccess = await SendEmailAsync(
                    contactData.Email,
                    contactData.Name,
                    customerSubject,
                    customerBody
                );

                return adminSuccess && customerSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing contact form.");
                return false;
            }
        }

        // 4. Booking Confirmation
        public async Task<bool> SendBookingConfirmationEmailAsync(BookingConfirmationData bookingData)
        {
            try
            {
                var subject = $"Booking Confirmed #{bookingData.BookingId}";
                var body = $"Dear {bookingData.CustomerName},<br><br>Your booking for {bookingData.PackageName} is confirmed!<br>Amount: RM {bookingData.Amount:N2}";
                return await SendEmailAsync(bookingData.CustomerEmail, bookingData.CustomerName, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending booking confirmation.");
                return false;
            }
        }

        // ==========================================
        // PRIVATE HELPER (UPDATED)
        // ==========================================
        private async Task<bool> SendEmailAsync(string recipientEmail, string recipientName, string subject, string body, string? replyToEmail = null)
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

                    // *** THIS MAKES "AUTO DETECT" WORK ***
                    if (!string.IsNullOrEmpty(replyToEmail))
                    {
                        mailMessage.ReplyToList.Add(new MailAddress(replyToEmail));
                    }

                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation("Email sent to {Email} (Reply-To: {ReplyTo})", recipientEmail, replyToEmail ?? "Default");
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