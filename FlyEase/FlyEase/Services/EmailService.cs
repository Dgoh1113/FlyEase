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
            // Prepare Data
            string reviewLink = $"https://localhost:7068/Feedback/Create?bookingId={bookingId}";

            // Image Logic (Use public Unsplash image if local to avoid broken images in Gmail)
            string displayImage = string.IsNullOrEmpty(packageImageUrl)
                ? "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?q=80&w=600&auto=format&fit=crop"
                : (packageImageUrl.StartsWith("/") ? "https://localhost:7068" + packageImageUrl : packageImageUrl);

            // Read HTML Template
            string body = await GetTemplateHtml("Invitation.html");

            // Replace Placeholders
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

        // ... inside class EmailService ...

        // 2. CONFIRMATION EMAIL (Update this method)
        public async Task SendReviewConfirmation(string userEmail, string userName, string packageName, int rating, string comment, string emotion)
        {
            // 1. Generate Stars
            string stars = "";
            for (int i = 0; i < 5; i++) stars += (i < rating) ? "★" : "☆";

            // 2. Generate Emotion Text with Emoji
            string emotionDisplay = emotion switch
            {
                "Sad" => "Disappointed 😞",
                "Neutral" => "It was okay 😐",
                "Happy" => "Happy 🙂",
                "Excited" => "Excited 🤩",
                "Loved" => "Loved it! 😍",
                _ => emotion // Fallback
            };

            // 3. Read Template
            string body = await GetTemplateHtml("Confirmation.html");

            // 4. Replace Placeholders
            if (!string.IsNullOrEmpty(body))
            {
                body = body.Replace("{{UserName}}", userName)
                           .Replace("{{PackageName}}", packageName)
                           .Replace("{{Stars}}", stars)
                           .Replace("{{Emotion}}", emotionDisplay) // <--- NEW
                           .Replace("{{Comment}}", comment)
                           .Replace("{{Year}}", DateTime.Now.Year.ToString());
            }
            else
            {
                body = $"Thank you {userName} for reviewing {packageName}!";
            }

            await SendEmailAsync(userEmail, "Thanks for your feedback! ⭐", body);
        }

        // --- HELPER: Read Template from wwwroot/templates ---
        private async Task<string> GetTemplateHtml(string fileName)
        {
            try
            {
                // Finds the path without needing 'IWebHostEnvironment'
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "templates", fileName);
                if (File.Exists(path))
                {
                    return await File.ReadAllTextAsync(path);
                }
            }
            catch
            {
                // Ignore file read errors
            }
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