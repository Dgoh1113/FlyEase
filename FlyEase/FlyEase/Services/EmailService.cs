using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace FlyEase.Services
{
    public class EmailService
    {
        private readonly EmailSettings _emailSettings;

        // Inject the same settings used by ForgetEmailService
        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public EmailService()
        {
        }

        // ==========================================
        // 1. SEND OTP EMAIL (New)
        // ==========================================
        public async Task SendOtpEmail(string toEmail, string otp)
        {
            string subject = "Your FlyEase Verification Code";
            string body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; text-align: center; border: 1px solid #ddd; border-radius: 10px; max-width: 500px; margin: auto;'>
                    <h2 style='color: #0d6efd;'>FlyEase Verification</h2>
                    <p>Your verification code is:</p>
                    <h1 style='letter-spacing: 5px; background: #f8f9fa; padding: 10px; border-radius: 5px; display: inline-block; margin: 10px 0;'>{otp}</h1>
                    <p>This code is valid for 5 minutes.</p>
                    <small>If you did not request this code, please ignore this email.</small>
                </div>";

            await SendEmailAsync(toEmail, subject, body);
        }

        // ==========================================
        // 2. INVITATION EMAIL
        // ==========================================
        public async Task SendReviewInvitation(string userEmail, string userName, int bookingId, string packageName, string packageImageUrl)
        {
            string reviewLink = $"https://localhost:7068/Feedback/Create?bookingId={bookingId}";
            string displayImage = string.IsNullOrEmpty(packageImageUrl)
                ? "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?q=80&w=600&auto=format&fit=crop"
                : (packageImageUrl.StartsWith("/") ? "https://localhost:7068" + packageImageUrl : packageImageUrl);

            string body = await GetTemplateHtml("Invitation.html");

            if (!string.IsNullOrEmpty(body))
            {
                body = body.Replace("{{UserName}}", userName)
                           .Replace("{{PackageName}}", packageName)
                           .Replace("{{Image}}", displayImage)
                           .Replace("{{Link}}", reviewLink)
                           .Replace("{{Year}}", DateTime.Now.Year.ToString());
            }
            else
            {
                body = $"Hi {userName}, please rate your trip to {packageName}: {reviewLink}";
            }

            await SendEmailAsync(userEmail, $"How was your trip to {packageName}? ✈️", body);
        }

        // ==========================================
        // 3. CONFIRMATION EMAIL
        // ==========================================
        public async Task SendReviewConfirmation(string userEmail, string userName, string packageName, int rating, string comment, string emotion)
        {
            string stars = "";
            for (int i = 0; i < 5; i++) stars += (i < rating) ? "★" : "☆";

            string emotionDisplay = emotion switch
            {
                "Sad" => "Disappointed 😞",
                "Neutral" => "It was okay 😐",
                "Happy" => "Happy 🙂",
                "Excited" => "Excited 🤩",
                "Loved" => "Loved it! 😍",
                _ => emotion
            };

            string subject;
            string customMessage;
            string couponHtml = "";

            if (rating <= 2)
            {
                subject = "We're sorry to hear that... 😔";
                customMessage = "We are truly sorry that your experience didn't meet your expectations.";
                couponHtml = @"<div style='background-color: #fff3cd; border: 2px dashed #ffc107; border-radius: 10px; padding: 20px; margin: 20px 0;'>
                    <h2 style='color: #d39e00; margin: 0;'>SORRY50</h2>
                    <p>Use this code to get RM50 off your next booking.</p>
                </div>";
            }
            else if (rating == 3)
            {
                subject = "Thanks for your feedback! 😐";
                customMessage = "Thank you for your feedback. We are always looking to improve.";
            }
            else
            {
                subject = "We're glad you enjoyed it! 🤩";
                customMessage = "Thank you for the great review! We are thrilled you had a good time.";
            }

            string body = await GetTemplateHtml("Confirmation.html");

            if (!string.IsNullOrEmpty(body))
            {
                body = body.Replace("{{UserName}}", userName)
                           .Replace("{{PackageName}}", packageName)
                           .Replace("{{Stars}}", stars)
                           .Replace("{{Emotion}}", emotionDisplay)
                           .Replace("{{Comment}}", comment)
                           .Replace("{{Message}}", customMessage)
                           .Replace("{{Coupon}}", couponHtml)
                           .Replace("{{Year}}", DateTime.Now.Year.ToString());
            }
            else
            {
                body = $"{customMessage} <br/><br/> You rated: {stars}";
            }

            await SendEmailAsync(userEmail, subject, body);
        }

        // ==========================================
        // 4. REFUND NOTIFICATION
        // ==========================================
        public async Task SendRefundNotification(string userEmail, string userName, string packageName, decimal refundAmount)
        {
            string subject = $"Important: Booking Cancellation & Refund - {packageName}";
            string body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                    <h2 style='color: #dc3545;'>Booking Cancelled</h2>
                    <p>Dear <strong>{userName}</strong>,</p>
                    <p>We regret to inform you that <strong>{packageName}</strong> has been cancelled.</p>
                    <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px;'>
                        <h3 style='margin-top:0; color: #198754;'>Refund: RM {refundAmount:N2}</h3>
                    </div>
                    <p>Sincerely,<br><strong>FlyEase Support Team</strong></p>
                </div>";

            await SendEmailAsync(userEmail, subject, body);
        }

        // ==========================================
        // HELPERS
        // ==========================================
        private async Task<string> GetTemplateHtml(string fileName)
        {
            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "templates", fileName);
                if (File.Exists(path)) return await File.ReadAllTextAsync(path);
            }
            catch { }
            return string.Empty;
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                // USE SETTINGS FROM APPSETTINGS.JSON (Like Forgot Password)
                using (var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                        Subject = subject,
                        Body = htmlBody,
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(toEmail);
                    await client.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex)
            {
                // Log error locally if needed
                Console.WriteLine("Email Error: " + ex.Message);
            }
        }
    }
}