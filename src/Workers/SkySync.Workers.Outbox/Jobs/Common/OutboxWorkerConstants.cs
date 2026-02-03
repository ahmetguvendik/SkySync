namespace SkySync.Workers.Outbox.Jobs.Common;

/// <summary>
/// Outbox worker'lar için ortak sabitler
/// </summary>
internal static class OutboxWorkerConstants
{
    /// <summary>Her döngüde işlenecek maksimum mesaj sayısı</summary>
    public const int BatchSize = 20;

    /// <summary>Her döngü arası bekleme süresi (saniye)</summary>
    public const int DelaySeconds = 2;

    /// <summary>Maksimum deneme sayısı</summary>
    public const int MaxRetryCount = 5;

    /// <summary>Event'lerin namespace'i</summary>
    public const string EventNamespace = "SkySync.Shared.Events";
}
