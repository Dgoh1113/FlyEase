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
                string reviewLink = $"https://localhost:7068/Feedback/Create?bookingId={bookingId}";

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

                string emailSubject = "";
                string emailBody = "";

                // ==========================================================================================
                // SCENARIO A: 1 or 2 Stars (Apology + Ask Why + Discount)
                // ==========================================================================================
                if (rating <= 2)
                {
                    emailSubject = $"We are truly sorry about your trip to {packageName}";
                    emailBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #f5c6cb; background-color: #fff; border-radius: 8px;'>
                    <h2 style='color: #dc3545; margin-top: 0;'>We missed the mark, {userName}.</h2>
                    <p>We noticed you rated <strong>{packageName}</strong> with only {rating} star(s).</p>
                    
                    <div style='background: #f8d7da; padding: 15px; border-left: 5px solid #dc3545; margin: 15px 0; color: #721c24;'>
                        <em>""{comment}""</em>
                    </div>

                    <p><strong>We want to understand exactly what went wrong.</strong></p>
                    <p>Could you please reply to this email and tell us more? Your detailed feedback will help us prevent this from happening to anyone else.</p>
                    
                    <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>

                    <p>As a token of our apology, we would like to offer you a discount on your next attempt with us:</p>
                    <div style='background: #eee; padding: 15px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 3px; border-radius: 5px;'>
                        SORRY20
                    </div>
                    <p style='text-align: center; font-size: 12px; color: #666;'>Use this code at checkout for 20% off.</p>

                    <br>
                    <p>Sincerely,<br>The FlyEase Quality Team</p>
                </div>";
                }
                // ==========================================================================================
                // SCENARIO B: 3 Stars (Neutral - Ask for Improvements)
                // ==========================================================================================
                else if (rating == 3)
                {
                    emailSubject = "How can we turn this into 5 stars?";
                    emailBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ffeeba; background-color: #fff; border-radius: 8px;'>
                    <h2 style='color: #856404; margin-top: 0;'>Hi {userName}, thanks for the feedback.</h2>
                    <p>You rated <strong>{packageName}</strong> as 'Average' (3 Stars).</p>
                    
                    <div style='background: #fff3cd; padding: 15px; border-left: 5px solid #ffc107; margin: 15px 0; color: #856404;'>
                        <em>""{comment}""</em>
                    </div>

                    <p>At FlyEase, we aim for 'Excellent', not just 'Average'.</p>
                    <p><strong>What is the ONE thing we could have done better?</strong></p>
                    <p>Please reply to this email with your suggestions. We read every single reply!</p>

                    <br>
                    <p>Warm regards,<br>FlyEase Team</p>
                </div>";
                }
                // ==========================================================================================
                // SCENARIO C: 4 or 5 Stars (Gratitude + Continuous Improvement)
                // ==========================================================================================
                else
                {
                    emailSubject = "You made our day! (And a quick question)";
                    emailBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #c3e6cb; background-color: #fff; border-radius: 8px;'>
                    <h2 style='color: #155724; margin-top: 0;'>Thank You, {userName}!</h2>
                    <p>We are thrilled that you enjoyed your trip to <strong>{packageName}</strong>!</p>
                    
                    <div style='background: #d4edda; padding: 15px; border-left: 5px solid #28a745; margin: 15px 0; color: #155724;'>
                        <em>""{comment}""</em>
                    </div>

                    <p>We are happy that you are happy! However, we are always looking to improve.</p>
                    <p><strong>Is there anything—even small—that we could do to make your next experience even better?</strong></p>
                    <p>Feel free to reply and let us know. We'd love to hear your thoughts.</p>

                    <br>
                    <p>Happy Travels,<br>FlyEase Team</p>
                </div>";
                }

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_gmail, "FlyEase Team"),
                    Subject = emailSubject,
                    Body = emailBody,
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(userEmail);
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Email Error: " + ex.Message);
            }
        }
    }
}