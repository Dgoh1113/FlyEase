using System.Net;
using System.Net.Mail;

namespace FlyEase.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void SendEmail(string toEmail, string subject, string messageBody)
        {
            var host = _configuration["Mailtrap:Host"];
            var port = int.Parse(_configuration["Mailtrap:Port"]);
            var username = _configuration["Mailtrap:Username"];
            var password = _configuration["Mailtrap:Password"];

            var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress("no-reply@flyease.com", "FlyEase Support"),
                Subject = subject,
                Body = messageBody,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            client.Send(mailMessage);
        }
    }
}