using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using PRIVATE.MESSAGING.MW.Models.Other;

namespace PRIVATE.MESSAGING.MW.Services;

public class EmailService : IEmailService
{
    private readonly SmtpSettings _smtpSettings;

    public EmailService(IOptions<SmtpSettings> smtpSettings)
    {
        _smtpSettings = smtpSettings.Value;
    }

    public async Task SendOtpEmailAsync(string toEmail, string otp)
    {
        var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
        {
            Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password),
            EnableSsl = _smtpSettings.EnableSsl
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_smtpSettings.Username, "Stealth Chat"),
            Subject = "Giriş Şifreniz (OTP) - Stealth Chat",
            Body = $"<h1>Stealth Chat</h1><p>Tek kullanımlık giriş şifreniz: <strong style='font-size:24px; color:#007bff;'>{otp}</strong></p><p>Bu kod 5 dakika boyunca geçerlidir.</p>",
            IsBodyHtml = true,
        };

        mailMessage.To.Add(toEmail);

        await client.SendMailAsync(mailMessage);
    }
}
