namespace SkySync.Shared.Options;

/// <summary>
/// Payment ayarlarını (ör. timeout) merkezileştirir.
/// </summary>
public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    public int TimeoutMinutes { get; set; } = 5;
}
