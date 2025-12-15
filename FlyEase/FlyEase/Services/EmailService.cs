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

        // Inside FlyEase/Services/EmailService.cs

        public async Task SendReviewConfirmation(string userEmail, string userName, string packageName, int rating, string comment, string emotion)
        {
            // 1. Generate Stars String (e.g., ★★★☆☆)
            string stars = "";
            for (int i = 0; i < 5; i++) stars += (i < rating) ? "★" : "☆";

            // 2. Determine Emotion Display
            string emotionDisplay = emotion switch
            {
                "Sad" => "Disappointed 😞",
                "Neutral" => "It was okay 😐",
                "Happy" => "Happy 🙂",
                "Excited" => "Excited 🤩",
                "Loved" => "Loved it! 😍",
                _ => emotion
            };

            // 3. CONDITIONAL LOGIC: Subject, Message, and Coupon
            string subject;
            string customMessage;
            string couponHtml = ""; // Default is empty (no coupon)

            if (rating <= 2)
            {
                // === BAD RATING (1-2 Stars) ===
                subject = "We're sorry to hear that... 😔";
                customMessage = "We are truly sorry that your experience didn't meet your expectations. We take your feedback seriously and will do our best to improve. As a token of our sincere apology, please accept this discount for your next trip.";

                // Inject a Coupon Block
                couponHtml = @"
            <div style='background-color: #fff3cd; border: 2px dashed #ffc107; border-radius: 10px; padding: 20px; text-align: center; margin: 20px 0;'>
                <p style='color: #856404; margin: 0 0 10px 0; font-weight: bold;'>A small gift for you</p>
                <h2 style='color: #d39e00; margin: 0; font-size: 24px; letter-spacing: 2px;'>SORRY50</h2>
                <p style='font-size: 12px; color: #856404; margin: 5px 0 0 0;'>Use this code to get RM50 off your next booking.</p>
            </div>";
            }
            else if (rating == 3)
            {
                // === NEUTRAL RATING (3 Stars) ===
                subject = "Thanks for your feedback! 😐";
                customMessage = "Thank you for your feedback. We are always looking to improve our services. If there is anything specific we can do better next time, please let us know by replying to this email!";
            }
            else
            {
                // === GOOD RATING (4-5 Stars) ===
                subject = "We're glad you enjoyed it! 🤩";
                customMessage = "Thank you for the great review! We are thrilled you had a good time. Is there anything else we can do to make your next experience even better? Let us know!";
            }

            // 4. Load and Replace Template
            string body = await GetTemplateHtml("Confirmation.html");

            if (!string.IsNullOrEmpty(body))
            {
                body = body.Replace("{{UserName}}", userName)
                           .Replace("{{PackageName}}", packageName)
                           .Replace("{{Stars}}", stars)
                           .Replace("{{Emotion}}", emotionDisplay)
                           .Replace("{{Comment}}", comment)
                           // Inject the new dynamic parts:
                           .Replace("{{Message}}", customMessage)
                           .Replace("{{Coupon}}", couponHtml)
                           .Replace("{{Year}}", DateTime.Now.Year.ToString());
            }
            else
            {
                // Fallback if template is missing
                body = $"{customMessage} <br/><br/> You rated: {stars}";
            }

            await SendEmailAsync(userEmail, subject, body);
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