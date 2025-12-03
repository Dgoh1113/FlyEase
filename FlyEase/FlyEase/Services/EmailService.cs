using System;
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

        // FIXED: Added bookingId here so the link generation works
        public async Task SendReviewInvitation(string userEmail, string userName, string packageName, int bookingId)
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
                // IMPORTANT: Replace '7123' with your actual running port number
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
            catch (Exception ex)
            {
                // For now, we just ignore email errors so the app doesn't crash
                Console.WriteLine("Email error: " + ex.Message);
            }
        }

        // === NEW UPDATED METHOD APPLIED BELOW ===
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

                // === LOGIC: Determine Email Content based on Rating ===

                // SCENARIO A: 1 or 2 Stars (Apology + Discount)
                if (rating <= 2)
                {
                    emailSubject = "We're Sorry your trip wasn't perfect.";
                    emailBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #f5c6cb; background-color: #fff; border-radius: 5px;'>
                            <h2 style='color: #dc3545;'>We Owe You an Apology, {userName}.</h2>
                            <p>We noticed you rated your trip to <strong>{packageName}</strong> with only {rating} stars.</p>
                            <p>We read your feedback:</p>
                            <blockquote style='background: #fbeaea; padding: 10px; border-left: 4px solid #dc3545; font-style: italic;'>
                                ""{comment}""
                            </blockquote>
                            <p>We are truly sorry that we did not meet your expectations. We take this seriously and will investigate the issue.</p>
                            <p>To make it up to you, please accept this <strong>20% Discount Code</strong> for your next booking:</p>
                            <div style='background: #eee; padding: 15px; text-align: center; font-size: 20px; font-weight: bold; letter-spacing: 2px; margin: 20px 0;'>
                                SORRY20
                            </div>
                            <p>We hope you give us another chance to provide the experience you deserve.</p>
                            <br>
                            <p>Sincerely,<br>FlyEase Customer Care Team</p>
                        </div>";
                }
                // SCENARIO B: 3 Stars (Ask for Improvements)
                else if (rating == 3)
                {
                    emailSubject = "How can we make it better?";
                    emailBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ffeeba; background-color: #fff; border-radius: 5px;'>
                            <h2 style='color: #ffc107;'>Hi {userName}, thank you for your feedback.</h2>
                            <p>You rated your trip to <strong>{packageName}</strong> as 3 stars (Average).</p>
                            <p>We strive for excellence, and 'average' isn't good enough for us. We want to know exactly what would have turned that 3 stars into 5 stars.</p>
                            <blockquote style='background: #fff3cd; padding: 10px; border-left: 4px solid #ffc107; font-style: italic;'>
                                ""{comment}""
                            </blockquote>
                            <p>If you have more specific suggestions on improvements, please reply to this email directly. Our management team reads every reply.</p>
                            <br>
                            <p>Warm regards,<br>FlyEase Quality Assurance</p>
                        </div>";
                }
                // SCENARIO C: 4 or 5 Stars (Gratitude)
                else
                {
                    emailSubject = "Thank You for choosing FlyEase!";
                    emailBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #c3e6cb; background-color: #fff; border-radius: 5px;'>
                            <h2 style='color: #28a745;'>You're Awesome, {userName}!</h2>
                            <p>Thank you so much for the {rating}-star rating for <strong>{packageName}</strong>.</p>
                            <p>We are thrilled that you had a great time! Your kind words mean the world to us.</p>
                            <blockquote style='background: #d4edda; padding: 10px; border-left: 4px solid #28a745; font-style: italic;'>
                                ""{comment}""
                            </blockquote>
                            <p>We can't wait to help you plan your next adventure. See you soon!</p>
                            <br>
                            <p>Happy Travels,<br>The FlyEase Team</p>
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