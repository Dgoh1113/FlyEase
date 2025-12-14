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
        public string AdminEmail { get; set; } = string.Empty; // Add for contact form
    }

    public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string newPassword);
    Task<bool> SendPasswordResetLinkAsync(string recipientEmail, string recipientName, string resetLink); // Add this
    Task<bool> SendContactFormEmailAsync(ContactFormData contactData);
    Task<bool> SendBookingConfirmationEmailAsync(BookingConfirmationData bookingData);
}

    // Add data models for contact form
    public class ContactFormData
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
    }

    // Optional: For booking confirmations
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
        public async Task<bool> SendPasswordResetLinkAsync(string recipientEmail, string recipientName, string resetLink)
        {
            try
            {
                var subject = "Reset Your Password - FlyEase Travel";
                var expiryTime = DateTime.Now.AddSeconds(30);

                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
        .btn-reset {{ background: #667eea; color: white; padding: 14px 28px; text-decoration: none; border-radius: 8px; display: inline-block; font-weight: 600; margin: 20px 0; }}
        .warning {{ background: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 8px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✈️ FlyEase Travel</h1>
            <p>Password Reset Request</p>
        </div>
        
        <div class='content'>
            <p>Hello <strong>{recipientName}</strong>,</p>
            <p>We received a request to reset your password for your FlyEase Travel account.</p>
            
            <div style='text-align: center;'>
                <a href='{resetLink}' class='btn-reset'>
                    Reset My Password
                </a>
            </div>
            
            <p style='text-align: center;'>
                <small>Or copy and paste this link:<br>
                <code style='word-break: break-all; color: #667eea;'>{resetLink}</code></small>
            </p>
            
            <div class='warning'>
                <strong>⚠️ Important:</strong> This link will expire in <strong>30 seconds</strong>. 
                If you didn't request this reset, please ignore this email.
            </div>
        </div>
        
        <div class='footer'>
            <p>This is an automated message. Please do not reply.</p>
            <p>&copy; {DateTime.Now.Year} FlyEase Travel</p>
        </div>
    </div>
</body>
</html>";

                return await SendEmailAsync(recipientEmail, recipientName, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset link to {Email}", recipientEmail);
                return false;
            }
        }
        // Existing Password Reset Method
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

        // NEW: Contact Form Email Method
        public async Task<bool> SendContactFormEmailAsync(ContactFormData contactData)
        {
            try
            {
                // Email to Admin (Notification)
                var adminSubject = $"New Contact Form: {contactData.Subject}";
                var adminBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #0d6efd 0%, #198754 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f8f9fa; padding: 20px; border-radius: 0 0 10px 10px; }}
        .info-box {{ background: white; border-left: 4px solid #0d6efd; padding: 15px; margin: 15px 0; }}
        .label {{ font-weight: bold; color: #495057; }}
        .value {{ color: #212529; }}
        .message-box {{ background: white; border: 1px solid #dee2e6; padding: 15px; margin: 15px 0; border-radius: 5px; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #6c757d; }}
        .urgent {{ background: #fff3cd; border: 1px solid #ffc107; padding: 10px; margin: 10px 0; border-radius: 5px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📨 New Contact Message</h1>
            <p>FlyEase Travel Website</p>
        </div>
        
        <div class='content'>
            <div class='info-box'>
                <div><span class='label'>From:</span> <span class='value'>{contactData.Name}</span></div>
                <div><span class='label'>Email:</span> <span class='value'>{contactData.Email}</span></div>
                <div><span class='label'>Phone:</span> <span class='value'>{contactData.Phone ?? "Not provided"}</span></div>
                <div><span class='label'>Subject:</span> <span class='value'>{contactData.Subject}</span></div>
                <div><span class='label'>Received:</span> <span class='value'>{contactData.SubmittedAt:dd MMM yyyy HH:mm}</span></div>
            </div>
            
            <div class='message-box'>
                <h3>Message:</h3>
                <p>{contactData.Message.Replace("\n", "<br>")}</p>
            </div>
            
            <div class='urgent'>
                <strong>⚠ Action Required:</strong> Please respond within 24 hours.
            </div>
            
            <p>
                <a href='mailto:{contactData.Email}?subject=Re: {contactData.Subject}' style='background: #0d6efd; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                    Reply to {contactData.Name}
                </a>
            </p>
        </div>
        
        <div class='footer'>
            <p>This message was sent from the contact form on FlyEase Travel website.</p>
            <p>&copy; {DateTime.Now.Year} FlyEase Travel. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

                // Send to admin
                var adminSuccess = await SendEmailAsync(
                    _emailSettings.AdminEmail,
                    "FlyEase Admin",
                    adminSubject,
                    adminBody
                );

                // Auto-reply to customer
                var customerSubject = "Thank you for contacting FlyEase Travel";
                var customerBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.8; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #0d6efd 0%, #198754 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #ffffff; padding: 30px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .highlight {{ background: #e7f1ff; padding: 15px; border-left: 4px solid #0d6efd; margin: 20px 0; }}
        .contact-info {{ background: #f8f9fa; padding: 20px; border-radius: 8px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; color: #6c757d; font-size: 14px; }}
        .icon {{ color: #0d6efd; margin-right: 10px; }}
        .btn-primary {{ background: #0d6efd; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>✈️ FlyEase Travel</h1>
            <p style='margin: 10px 0 0; opacity: 0.9;'>Your Gateway to Amazing Adventures</p>
        </div>
        
        <div class='content'>
            <h2>Hello {contactData.Name},</h2>
            
            <p>Thank you for reaching out to FlyEase Travel! We've received your message and our team will get back to you as soon as possible.</p>
            
            <div class='highlight'>
                <h3 style='margin-top: 0;'>📋 Message Summary</h3>
                <p><strong>Subject:</strong> {contactData.Subject}</p>
                <p><strong>Received:</strong> {contactData.SubmittedAt:dddd, dd MMMM yyyy 'at' HH:mm}</p>
                <p><strong>Reference:</strong> CF-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}</p>
            </div>
            
            <h3>⏱️ What happens next?</h3>
            <ol>
                <li>Our customer service team will review your inquiry</li>
                <li>You'll receive a personal response within <strong>24 hours</strong></li>
                <li>We'll work with you to find the perfect travel solution</li>
            </ol>
            
            <div class='contact-info'>
                <h3 style='margin-top: 0;'>📞 Need immediate assistance?</h3>
                <p><i class='fas fa-phone icon'></i> <strong>Phone:</strong> +60 3-1234 5678 (Mon-Fri, 9AM-6PM)</p>
                <p><i class='fas fa-envelope icon'></i> <strong>Email:</strong> support@flyease.com</p>
                <p><i class='fas fa-map-marker-alt icon'></i> <strong>Address:</strong> Level 23, Menara, KLCC, 50088 Kuala Lumpur</p>
            </div>
            
            <p style='text-align: center;'>
                <a href='[YOUR_APP_URL]' class='btn-primary'>Visit Our Website</a>
            </p>
        </div>
        
        <div class='footer'>
            <p>This is an automated confirmation. Please do not reply to this email.</p>
            <p>© {DateTime.Now.Year} FlyEase Travel Sdn Bhd. All rights reserved.</p>
            <p style='font-size: 12px; color: #adb5bd;'>
                If you didn't submit this contact form, please ignore this email.
            </p>
        </div>
    </div>
</body>
</html>";

                // Send auto-reply to customer
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
                _logger.LogError(ex, "Error sending contact form email from {Name} ({Email})",
                    contactData.Name, contactData.Email);
                return false;
            }
        }

        // Optional: Booking Confirmation Email
        public async Task<bool> SendBookingConfirmationEmailAsync(BookingConfirmationData bookingData)
        {
            try
            {
                var subject = $"Booking Confirmation #{bookingData.BookingId} - FlyEase Travel";
                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #198754 0%, #0d6efd 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #ffffff; padding: 20px; border-radius: 0 0 10px 10px; }}
        .booking-details {{ background: #f8f9fa; padding: 15px; border-radius: 5px; margin: 15px 0; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎉 Booking Confirmed!</h1>
            <p>FlyEase Travel - Your adventure awaits!</p>
        </div>
        <div class='content'>
            <p>Dear {bookingData.CustomerName},</p>
            <p>Your booking has been confirmed successfully. Here are your booking details:</p>
            
            <div class='booking-details'>
                <p><strong>Booking ID:</strong> #{bookingData.BookingId}</p>
                <p><strong>Package:</strong> {bookingData.PackageName}</p>
                <p><strong>Travel Date:</strong> {bookingData.TravelDate:dd MMMM yyyy}</p>
                <p><strong>Travelers:</strong> {bookingData.NumberOfPeople} person(s)</p>
                <p><strong>Total Amount:</strong> RM {bookingData.Amount:N2}</p>
            </div>
            
            <p>You can view your booking details by logging into your account.</p>
        </div>
        <div class='footer'>
            <p>Thank you for choosing FlyEase Travel!</p>
            <p>&copy; {DateTime.Now.Year} FlyEase Travel. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

                return await SendEmailAsync(
                    bookingData.CustomerEmail,
                    bookingData.CustomerName,
                    subject,
                    body
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending booking confirmation to {Email}", bookingData.CustomerEmail);
                return false;
            }
        }

        // Private method to send email (used by all methods)
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