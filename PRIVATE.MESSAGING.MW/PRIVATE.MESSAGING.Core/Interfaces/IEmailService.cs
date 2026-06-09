namespace PRIVATE.MESSAGING.Core.Interfaces;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string otp);
}
