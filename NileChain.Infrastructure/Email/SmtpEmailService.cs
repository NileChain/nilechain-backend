using NileChain.Application.Dtos.Email;
using NileChain.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace NileChain.Infrastructure.Email
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailOptions _options;

        public SmtpEmailService(IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendAsync(EmailMessage message)
        {
            var email = new MimeMessage();

            email.From.Add(
                new MailboxAddress(
                    _options.SenderName,
                    _options.SenderEmail));

            email.To.Add(
                MailboxAddress.Parse(message.To));

            email.Subject = message.Subject;

            email.Body = new TextPart(
                message.IsHtml ? "html" : "plain")
            {
                Text = message.Body
            };

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseSsl
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                _options.Username,
                _options.Password);

            await smtp.SendAsync(email);

            await smtp.DisconnectAsync(true);
        }
        public async Task SendConfirmEmailAsync(string to, string confirmationLink)
        {
            var path =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Email",
                    "Templates",
                    "ConfirmEmail.html");

            var html =
                await File.ReadAllTextAsync(path);

            html =
                TemplateRenderer.Render(
                    html,
                    new Dictionary<string, string>
                    {
                {
                    "ConfirmationLink",
                    confirmationLink
                }
                    });

            await SendAsync(
                new EmailMessage
                {
                    To = to,
                    Subject = "Confirm your email",
                    Body = html,
                    IsHtml = true
                });
        }
    }
}
