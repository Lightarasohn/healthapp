using healthapp.Interfaces;
using System.Net;
using System.Net.Mail;

namespace healthapp.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var emailUser = _config["EMAIL_USER"];
            var emailPass = _config["EMAIL_PASS"];
            var host = "smtp.gmail.com"; // veya env'den alınabilir
            var port = 587;

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            client.Credentials = new NetworkCredential(emailUser, emailPass);

            var mailMessage = new MailMessage
            {
                From = new MailAddress(emailUser!, "HealthApp"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            
            mailMessage.To.Add(to);

            await client.SendMailAsync(mailMessage);
        }
    }
}