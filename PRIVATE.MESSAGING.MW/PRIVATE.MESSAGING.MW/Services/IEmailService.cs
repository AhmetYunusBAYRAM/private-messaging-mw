namespace PRIVATE.MESSAGING.MW.Services;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string otp);
}
