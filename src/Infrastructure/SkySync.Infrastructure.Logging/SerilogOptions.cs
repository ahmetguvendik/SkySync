namespace SkySync.Infrastructure.Logging;

/// <summary>
/// Serilog + Seq konfigürasyon seçenekleri.
/// appsettings.json "Serilog" ve "Seq" bölümlerinden okunur.
/// </summary>
public sealed class SerilogOptions
{
    public const string SectionName = "Serilog";
    public const string SeqSectionName = "Seq";

    /// <summary>
    /// Servis adı (Flight, Reservation, Gateway vb.) - Her log satırına eklenir.
    /// </summary>
    public string ServiceName { get; set; } = "SkySync";

    /// <summary>
    /// Seq sunucu URL (örn: http://localhost:5341)
    /// </summary>
    public string SeqServerUrl { get; set; } = "http://localhost:5341";

    /// <summary>
    /// Seq API Key (Production'da authentication için)
    /// </summary>
    public string? SeqApiKey { get; set; }

    /// <summary>
    /// Console sink etkin mi?
    /// </summary>
    public bool EnableConsole { get; set; } = true;

    /// <summary>
    /// Seq sink etkin mi?
    /// </summary>
    public bool EnableSeq { get; set; } = true;
}
