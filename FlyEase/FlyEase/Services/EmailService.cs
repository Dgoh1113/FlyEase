using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using FlyEase.Data;

namespace FlyEase.Services
{
    public class EmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        // ==========================================
        // 1. SEND OTP EMAIL
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
        // 2. BOOKING CONFIRMATION
        // ==========================================
        public async Task SendBookingConfirmation(string toEmail, string userName, int bookingId, string packageName, decimal amountPaid, string status)
        {
            string subject = $"Booking Confirmed! #{bookingId} - FlyEase Travel ✈️";
            string statusColor = status == "Confirmed" || status == "Completed" ? "#198754" : "#ffc107";

            string body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                    <h2 style='color: #0d6efd; text-align: center;'>Payment Successful!</h2>
                    <p>Hi <strong>{userName}</strong>,</p>
                    <p>Thank you for booking with FlyEase. Your payment has been received and your booking is confirmed.</p>
                    
                    <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <table style='width: 100%;'>
                            <tr><td style='padding: 5px; font-weight: bold;'>Booking ID:</td><td style='padding: 5px;'>#{bookingId}</td></tr>
                            <tr><td style='padding: 5px; font-weight: bold;'>Package:</td><td style='padding: 5px;'>{packageName}</td></tr>
                            <tr><td style='padding: 5px; font-weight: bold;'>Amount Paid:</td><td style='padding: 5px;'>RM {amountPaid:N2}</td></tr>
                            <tr><td style='padding: 5px; font-weight: bold;'>Status:</td><td style='padding: 5px; color: {statusColor}; font-weight: bold;'>{status}</td></tr>
                        </table>
                    </div>
                    <p>You can view your full itinerary in your <a href='https://localhost:7068/Auth/Profile'>Profile Dashboard</a>.</p>
                    <hr>
                    <small style='color: #6c757d;'>FlyEase Travel & Tours</small>
                </div>";

            await SendEmailAsync(toEmail, subject, body);
        }

        // ==========================================
        // 3. REVIEW INVITATION
        // ==========================================
        public async Task SendReviewInvitation(string userEmail, string userName, int bookingId, string packageName, string packageImageUrl)
        {
            string reviewLink = $"https://localhost:7068/Feedback/Create?bookingId={bookingId}";
            string displayImage = string.IsNullOrEmpty(packageImageUrl) ? "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800" : (packageImageUrl.StartsWith("/") ? "https://localhost:7068" + packageImageUrl : packageImageUrl);

            string body = await GetTemplateHtml("Invitation.html");
            if (!string.IsNullOrEmpty(body))
            {
                body = body.Replace("{{UserName}}", userName).Replace("{{PackageName}}", packageName).Replace("{{Image}}", displayImage).Replace("{{Link}}", reviewLink).Replace("{{Year}}", DateTime.Now.Year.ToString());
            }
            else
            {
                body = $@"<h3>Hi {userName},</h3><p>We hope you enjoyed <strong>{packageName}</strong>!</p><p><a href='{reviewLink}'>Rate your experience</a></p>";
            }
            await SendEmailAsync(userEmail, $"How was your trip to {packageName}? ✈️", body);
        }

        // ==========================================
        // 4. REVIEW CONFIRMATION (NO VOUCHER)
        // ==========================================
        public async Task SendReviewConfirmation(string userEmail, string userName, string packageName, int rating, string comment, string emotion)
        {
            string stars = new string('★', rating) + new string('☆', 5 - rating);
            string emotionDisplay = emotion switch { "Sad" => "Disappointed 😞", "Neutral" => "It was okay 😐", "Happy" => "Happy 🙂", "Excited" => "Excited 🤩", "Loved" => "Loved it! 😍", _ => emotion };

            string subject;
            string customMessage;
            string couponHtml = ""; // REMOVED COUPON

            if (rating <= 2)
            {
                subject = "We're sorry to hear that... 😔";
                customMessage = "We are truly sorry that your experience didn't meet your expectations. We appreciate your honesty and will work to improve.";
            }
            else if (rating == 3)
            {
                subject = "Thanks for your feedback! 😐";
                customMessage = "Thank you for your feedback. We are always looking for ways to make our trips better.";
            }
            else
            {
                subject = "We're glad you enjoyed it! 🤩";
                customMessage = "Thank you for the great review! We are thrilled you had a good time with us.";
            }

            string body = await GetTemplateHtml("Confirmation.html");
            if (!string.IsNullOrEmpty(body))
            {
                body = body.Replace("{{UserName}}", userName).Replace("{{PackageName}}", packageName).Replace("{{Stars}}", stars)
                           .Replace("{{Emotion}}", emotionDisplay).Replace("{{Comment}}", comment).Replace("{{Message}}", customMessage)
                           .Replace("{{Coupon}}", couponHtml).Replace("{{Year}}", DateTime.Now.Year.ToString());
            }
            else
            {
                body = $@"<h3>Hi {userName},</h3><p>{customMessage}</p><p><strong>Your Rating:</strong> {stars}</p>";
            }
            await SendEmailAsync(userEmail, subject, body);
        }

        // ==========================================
        // 5. ADMIN RESPONSE EMAIL (NEW)
        // ==========================================
        public async Task SendAdminResponseEmail(string toEmail, string userName, string packageName, string originalComment, string adminReply)
        {
            string subject = $"Response to your review of {packageName} - FlyEase";
            string body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                    <h2 style='color: #0d6efd;'>FlyEase Response</h2>
                    <p>Hi <strong>{userName}</strong>,</p>
                    <p>Thank you for your review of <strong>{packageName}</strong>. Our team has responded:</p>
                    <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #ccc;'>
                        <p style='margin:0; font-size:12px; color:#666;'>You wrote:</p>
                        <p style='font-style: italic; margin-top:5px;'>""{originalComment}""</p>
                    </div>
                    <div style='background-color: #e7f1ff; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #0d6efd;'>
                        <p style='margin:0; font-size:12px; color:#0d6efd; font-weight:bold;'>Admin Reply:</p>
                        <p style='margin-top:5px;'>""{adminReply}""</p>
                    </div>
                    <p>We appreciate your feedback!</p>
                    <hr>
                    <small style='color: #6c757d;'>FlyEase Travel & Tours</small>
                </div>";
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendRefundNotification(string userEmail, string userName, string packageName, decimal refundAmount)
        {
            string subject = $"Important: Booking Cancellation & Refund - {packageName}";
            string body = $@"<div style='font-family: Arial;'><h2>Booking Cancelled</h2><p>Refund Processed: RM {refundAmount:N2}</p></div>";
            await SendEmailAsync(userEmail, subject, body);
        }

        private async Task<string> GetTemplateHtml(string fileName)
        {
            try { var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "templates", fileName); if (File.Exists(path)) return await File.ReadAllTextAsync(path); } catch { }
            return string.Empty;
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                using (var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort))
                {
                    client.EnableSsl = true; client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword);
                    var mailMessage = new MailMessage { From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName), Subject = subject, Body = htmlBody, IsBodyHtml = true };
                    mailMessage.To.Add(toEmail); await client.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex) { Console.WriteLine("Email Error: " + ex.Message); }
        }
    }
}