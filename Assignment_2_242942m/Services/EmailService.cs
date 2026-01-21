using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Assignment_2_242942m.Services
{
    public interface IEmailService
    {
        Task SendResetLinkAsync(string toEmail, string resetLink);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _cfg;
        public EmailService(IConfiguration cfg) => _cfg = cfg;

        public async Task SendResetLinkAsync(string toEmail, string resetLink)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Bookworms Online", _cfg["Smtp:Username"]));
            message.To.Add(new MailboxAddress(toEmail, toEmail));
            message.Subject = "Reset your password";
            message.Body = new TextPart("plain")
            {
                Text = $"Click the link to reset your password (valid 15 min): {resetLink}"
            };

            using var client = new SmtpClient();

            client.CheckCertificateRevocation = false;
            await client.ConnectAsync(_cfg["Smtp:Host"],
                                      int.Parse(_cfg["Smtp:Port"]),
                                      MailKit.Security.SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(_cfg["Smtp:Username"], _cfg["Smtp:Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
