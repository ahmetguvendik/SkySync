namespace SkySync.Workers.Outbox.Jobs.Common;

/// <summary>
/// Mesaj işleme sonucu (Parallel processing için)
/// </summary>
internal sealed class MessageProcessResult
{
    public bool IsSuccess { get; set; }
    public bool ShouldRetry { get; set; }
}
