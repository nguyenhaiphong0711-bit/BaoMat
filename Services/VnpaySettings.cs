namespace LMS.Services;

public sealed class VnpaySettings
{
    public string TmnCode { get; set; } = "";
    public string HashSecret { get; set; } = "";
    public string PaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string BankCode { get; set; } = "MB";
    public string ReturnUrl { get; set; } = "";
    public string IpnUrl { get; set; } = "";
}

public sealed record VnpayCallbackResult(
    bool IsValidSignature,
    bool IsMerchantValid,
    string TransactionReference,
    long AmountVnd,
    string ResponseCode,
    string TransactionStatus,
    string TransactionNumber);

public sealed class VnpayConfigurationException(string message) : Exception(message);
