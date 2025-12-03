using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace FlyEase.Services
{
    public class EmailService
    {
        // 1. HARDCODED CREDENTIALS (For testing only)
        // In a real app, these should go in appsettings.json
        private readonly string _gmail = "leelokhom22@gmail.com";
        private readonly string _appPassword = "rmqu agai fqvs gayf"; // Get this from Google Account > Security > App Passwords

        public async Task SendReviewInvitation(string userEmail, string userName, int bookingId, string packageName)
        {
            try
            {
                // 2. SETUP CLIENT
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(_gmail, _appPassword),
                    EnableSsl = true,
                };

                // 3. CREATE LINK
                // IMPORTANT: Replace '7123' with your actual running port number (check your browser URL)
                string reviewLink = $"https://localhost:7123/Feedback/Create?bookingId={bookingId}";

                // 4. CRAFT MESSAGE
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_gmail, "FlyEase Team"),
                    Subject = $"Enjoyed your trip to {packageName}? Rate us!",
                    Body = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 5px;'>
                            <h2 style='color: #0066a1;'>Welcome back, {userName}!</h2>
                            <p>We hope you had a wonderful time in <strong>{packageName}</strong>.</p>
                            <p>Now that your trip is completed, we would love to hear your thoughts.</p>
                            <br>
                            <a href='{reviewLink}' style='background-color: #0066a1; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                ★ Rate Your Trip
                            </a>
                            <br><br>
                            <p style='font-size: 12px; color: #888;'>If the button doesn't work, click here: <a href='{reviewLink}'>{reviewLink}</a></p>
                        </div>",
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(userEmail);

                // 5. SEND
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (System.Exception ex)
            {
                // For now, we just ignore email errors so the app doesn't crash
                System.Console.WriteLine("Email error: " + ex.Message);
            }
        }
        // [file]: Services/EmailService.cs

        public async Task SendReviewConfirmation(string userEmail, string userName, string packageName, int rating, string comment)
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
                    From = new MailAddress(_gmail, "FlyEase Team"),
                    Subject = "Thanks for your feedback!",
                    Body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 5px;'>
                    <h2 style='color: #28a745;'>Thank You, {userName}!</h2>
                    <p>We received your review for <strong>{packageName}</strong>.</p>
                    <hr>
                    <p><strong>Your Rating:</strong> {rating} / 5 Stars</p>
                    <p><strong>Your Comment:</strong></p>
                    <blockquote style='background: #f9f9f9; padding: 10px; border-left: 4px solid #ccc;'>
                        {comment}
                    </blockquote>
                    <br>
                    <p>We appreciate your support!</p>
                    <p>Safe travels,<br>FlyEase Team</p>
                </div>",
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(userEmail);
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine("Confirmation Email Failed: " + ex.Message);
            }
        }
    }
}