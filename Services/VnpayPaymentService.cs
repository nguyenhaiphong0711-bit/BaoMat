using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using LMS.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LMS.Services;

public sealed class VnpayPaymentService(
    IOptions<VnpaySettings> options,
    ILogger<VnpayPaymentService> logger)
{
    private readonly VnpaySettings settings = options.Value;

    public string CreatePaymentUrl(
        TuitionInvoice invoice,
        string clientIpAddress,
        string returnUrl,
        string? ipnUrl,
        DateTime nowUtc,
        bool requireHttps)
    {
        EnsureConfigured();
        ValidateUrl(settings.PaymentUrl, nameof(settings.PaymentUrl), requireHttps);
        ValidateUrl(returnUrl, nameof(returnUrl), requireHttps);
        if (string.IsNullOrWhiteSpace(ipnUrl))
            throw new VnpayConfigurationException("VNPAY IpnUrl must be configured.");
        ValidateUrl(ipnUrl, nameof(settings.IpnUrl), requireHttps);
        if (invoice.AmountVnd <= 0 || invoice.AmountVnd > long.MaxValue / 100)
            throw new ArgumentOutOfRangeException(nameof(invoice), "Invoice amount must be a positive VND amount.");

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, GetVietnamTimeZone());
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = settings.TmnCode,
            ["vnp_Amount"] = checked(invoice.AmountVnd * 100).ToString(CultureInfo.InvariantCulture),
            ["vnp_CreateDate"] = localNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = NormalizeIpAddress(clientIpAddress),
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = $"Thanh toan hoc phi {invoice.Id}",
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_TxnRef"] = invoice.VnpayTransactionReference,
            ["vnp_ExpireDate"] = localNow.AddMinutes(15).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
        };
        if (!string.IsNullOrWhiteSpace(settings.BankCode))
            parameters["vnp_BankCode"] = settings.BankCode.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(ipnUrl))
            parameters["vnp_IpnUrl"] = ipnUrl;

        var query = BuildQuery(parameters);
        var signature = ComputeSignature(query);
        return $"{settings.PaymentUrl}?{query}&vnp_SecureHashType=HmacSHA512&vnp_SecureHash={signature}";
    }

    public VnpayCallbackResult ReadCallback(IQueryCollection query)
    {
        var callbackFields = query
            .Where(pair => pair.Key.StartsWith("vnp_", StringComparison.Ordinal) &&
                pair.Key is not "vnp_SecureHash" and not "vnp_SecureHashType")
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        if (callbackFields.Any(pair => pair.Value.Count != 1) || query["vnp_SecureHash"].Count != 1)
            return InvalidCallback();

        var parameters = callbackFields.ToDictionary(
            pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);

        var expectedSignature = ComputeSignature(BuildQuery(new SortedDictionary<string, string>(parameters, StringComparer.Ordinal)));
        var suppliedSignature = query["vnp_SecureHash"].ToString();
        var isValid = IsValidSignature(expectedSignature, suppliedSignature);
        if (!isValid)
            logger.LogWarning("Received VNPAY callback with invalid signature.");

        var hasAmount = long.TryParse(parameters.GetValueOrDefault("vnp_Amount"), NumberStyles.None,
            CultureInfo.InvariantCulture, out var amountInMinorUnits);
        var amountVnd = hasAmount && amountInMinorUnits > 0 && amountInMinorUnits % 100 == 0
            ? amountInMinorUnits / 100
            : -1;

        return new VnpayCallbackResult(
            isValid,
            string.Equals(parameters.GetValueOrDefault("vnp_TmnCode"), settings.TmnCode, StringComparison.Ordinal),
            parameters.GetValueOrDefault("vnp_TxnRef") ?? "",
            amountVnd,
            parameters.GetValueOrDefault("vnp_ResponseCode") ?? "",
            parameters.GetValueOrDefault("vnp_TransactionStatus") ?? "",
            parameters.GetValueOrDefault("vnp_TransactionNo") ?? "");
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(settings.TmnCode) ||
            string.IsNullOrWhiteSpace(settings.HashSecret) ||
            string.IsNullOrWhiteSpace(settings.PaymentUrl))
        {
            throw new VnpayConfigurationException(
                "VNPAY is not configured. Set Vnpay__TmnCode and Vnpay__HashSecret.");
        }
    }

    private static void ValidateUrl(string value, string settingName, bool requireHttps)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && (requireHttps || uri.Scheme != Uri.UriSchemeHttp)))
        {
            throw new VnpayConfigurationException(
                $"{settingName} must be an absolute {(requireHttps ? "HTTPS" : "HTTP or HTTPS")} URL.");
        }
    }

    private static VnpayCallbackResult InvalidCallback() =>
        new(false, false, "", -1, "", "", "");

    private string ComputeSignature(string query)
    {
        EnsureConfigured();
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(settings.HashSecret));
        return Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(query)));
    }

    private static bool IsValidSignature(string expected, string supplied)
    {
        if (supplied.Length != expected.Length)
            return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected),
                Convert.FromHexString(supplied));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string BuildQuery(IEnumerable<KeyValuePair<string, string>> parameters) =>
        string.Join("&", parameters.Select(pair =>
            $"{HttpUtility.UrlEncode(pair.Key, Encoding.UTF8)}={HttpUtility.UrlEncode(pair.Value, Encoding.UTF8)}"));

    private static string NormalizeIpAddress(string ipAddress)
    {
        var normalized = ipAddress == "::1" ? "127.0.0.1" : ipAddress;
        return normalized.Length <= 45 ? normalized : normalized[..45];
    }

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
