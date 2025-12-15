using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace FlyEase.Services
{
    public class EmailService
    {
        private readonly string _gmail = "leelokhom22@gmail.com";
        private readonly string _appPassword = "rmqu agai fqvs gayf";

        // 1. INVITATION EMAIL (Used by AdminDashboard)
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

            string body = await GetTemplateHtml("Confirmation.html");

            if (!string.IsNullOrEmpty(body))
            {
                body = body.Replace("{{UserName}}", userName)
                           .Replace("{{PackageName}}", packageName)
                           .Replace("{{Stars}}", stars)
                           .Replace("{{Emotion}}", emotionDisplay)
                           .Replace("{{Comment}}", comment)
                           .Replace("{{Year}}", DateTime.Now.Year.ToString());
            }
            else
            {
                body = $"Thank you {userName} for reviewing {packageName}!";
            }

            await SendEmailAsync(userEmail, "Thanks for your feedback! ⭐", body);
        }

        // ==========================================
        // NEW: REFUND NOTIFICATION
        // ==========================================
        public async Task SendRefundNotification(string userEmail, string userName, string packageName, decimal refundAmount)
        {
            string subject = $"Important: Booking Cancellation & Refund - {packageName}";

            // Inline HTML for refund notification (Simpler than loading a template file for now)
            string body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                    <h2 style='color: #dc3545;'>Booking Cancelled</h2>
                    <p>Dear <strong>{userName}</strong>,</p>
                    <p>We regret to inform you that the package <strong>{packageName}</strong> has been discontinued and cancelled by our administration.</p>
                    
                    <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <h3 style='margin-top:0; color: #198754;'>Refund Processed</h3>
                        <p>A refund of <strong>RM {refundAmount:N2}</strong> has been initiated to your original payment method.</p>
                        <small>Please allow 5-10 business days for the amount to reflect in your account.</small>
                    </div>

                    <p>We apologize for any inconvenience caused. Please browse our website for other exciting packages!</p>
                    <br>
                    <p>Sincerely,<br><strong>FlyEase Support Team</strong></p>
                </div>";

            await SendEmailAsync(userEmail, subject, body);
        }

        // --- HELPER: Read Template from wwwroot/templates ---
        private async Task<string> GetTemplateHtml(string fileName)
        {
            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "templates", fileName);
                if (File.Exists(path))
                {
                    return await File.ReadAllTextAsync(path);
                }
            }
            catch { }
            return string.Empty;
        }

        // --- HELPER: Send Email ---
        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(_gmail, _appPassword),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_gmail, "FlyEase Support"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(toEmail);
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Email Error: " + ex.Message);
            }
        }
    }
}