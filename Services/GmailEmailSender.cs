using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LMS.Services;

public sealed class EmailDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class GmailEmailSender(Microsoft.Extensions.Options.IOptions<EmailSettings> options)
{
    private readonly EmailSettings settings = options.Value;

    public async Task SendPasswordResetCodeAsync(string recipient, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Host) ||
            settings.Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(settings.Username) ||
            string.IsNullOrWhiteSpace(settings.AppPassword))
        {
            throw new EmailDeliveryException("Gmail SMTP configuration is incomplete.");
        }

        try
        {
            var message = new MimeMessage();
            var senderEmail = string.IsNullOrWhiteSpace(settings.SenderEmail)
                ? settings.Username
                : settings.SenderEmail;
            message.From.Add(new MailboxAddress(settings.SenderName, senderEmail));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = "Mã xác nhận đặt lại mật khẩu LearnSpace";
            var escapedCode = System.Net.WebUtility.HtmlEncode(code);
            message.Body = new BodyBuilder
            {
                HtmlBody = $"""
                    <div style="font-family:Arial,sans-serif;max-width:560px;margin:auto;color:#253858;line-height:1.6">
                      <h2>Đặt lại mật khẩu LearnSpace</h2>
                      <p>Mã xác nhận của bạn:</p>
                      <div style="font-size:32px;font-weight:700;letter-spacing:8px;padding:16px;background:#f2f5fc;border-radius:10px;text-align:center">{escapedCode}</div>
                      <p>Mã có hiệu lực trong 10 phút và chỉ sử dụng được một lần. Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.</p>
                    </div>
                    """
            }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(settings.Username, settings.AppPassword, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception exception) when (
            exception is SmtpCommandException or
            SmtpProtocolException or
            MailKit.ServiceNotAuthenticatedException or
            MailKit.ServiceNotConnectedException or
            System.Security.Authentication.AuthenticationException or
            IOException or
            System.Net.Sockets.SocketException or
            FormatException or
            ArgumentException or
            TimeoutException)
        {
            throw new EmailDeliveryException("Gmail SMTP could not deliver the password reset message.", exception);
        }
    }
}
