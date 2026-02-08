using System.Text.Json.Serialization;

namespace SkySync.Services.Payment.Application.Features.Commands.Payment.Responses;

public class ProcessPaymentCommandResponse
{
    [JsonPropertyName("success")]
    public bool IsSuccess { get; set; }
    public string? TransactionId { get; set; }
    public string Message { get; set; } = string.Empty;
    /// <summary>
    /// Hata kodu: PAYMENT_EXPIRED, DUPLICATE_PAYMENT, PAYMENT_FAILED, vb.
    /// </summary>
    public string? Code { get; set; }
    /// <summary>
    /// HTTP durum kodu. Controller için - JSON'a serilemez.
    /// </summary>
    [JsonIgnore]
    public int StatusCode { get; set; }
}
