using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LMS.Services;

public sealed class EmailDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class GmailEmailSender(Microsoft.Extensions.Options.IOptions<EmailSettings> options)
{
    private readonly EmailSettings settings = options.Value;

    public void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(settings.Host) ||
            settings.Port is < 1 or > 65535 ||
            settings.TimeoutSeconds is < 1 or > 120 ||
            string.IsNullOrWhiteSpace(settings.Username) ||
            string.IsNullOrWhiteSpace(settings.AppPassword))
        {
            throw new EmailDeliveryException("SMTP configuration is incomplete. Check Email__Host, Email__Port, Email__Username, Email__AppPassword and Email__TimeoutSeconds.");
        }
    }

    public async Task SendPasswordResetCodeAsync(string recipient, string code, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        var deliveryToken = timeout.Token;
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
                TextBody = $"Mã xác nhận đặt lại mật khẩu LearnSpace: {code}. Mã có hiệu lực trong 10 phút và chỉ sử dụng một lần.",
                HtmlBody = $"""
                    <div style="font-family:Arial,sans-serif;max-width:560px;margin:auto;color:#253858;line-height:1.6">
                      <h2>Đặt lại mật khẩu LearnSpace</h2>
                      <p>Mã xác nhận của bạn:</p>
                      <div style="font-size:32px;font-weight:700;letter-spacing:8px;padding:16px;background:#f2f5fc;border-radius:10px;text-align:center">{escapedCode}</div>
                      <p>Mã có hiệu lực trong 10 phút và chỉ sử dụng được một lần. Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.</p>
                    </div>
                    """
            }.ToMessageBody();

            using var client = new SmtpClient { Timeout = settings.TimeoutSeconds * 1000 };
            var security = settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(settings.Host.Trim(), settings.Port, security, deliveryToken);
            // Google displays App Passwords in groups separated by spaces.
            var password = settings.Host.Trim().Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase)
                ? settings.AppPassword.Replace(" ", "").Trim()
                : settings.AppPassword;
            await client.AuthenticateAsync(settings.Username.Trim(), password, deliveryToken);
            await client.SendAsync(message, deliveryToken);
            // SMTP has accepted DATA at this point. A QUIT failure must not invalidate the delivered OTP.
            try { await client.DisconnectAsync(true, deliveryToken); }
            catch (Exception exception) when (exception is IOException or SmtpProtocolException or SmtpCommandException or MailKit.ServiceNotConnectedException or OperationCanceledException) { }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmailDeliveryException("SMTP delivery timed out.", exception);
        }
        catch (Exception exception) when (
            exception is MailKit.Security.AuthenticationException or
            MailKit.Security.SslHandshakeException or
            SmtpCommandException or
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
