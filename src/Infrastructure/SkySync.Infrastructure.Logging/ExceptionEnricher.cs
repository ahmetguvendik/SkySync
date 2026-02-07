using Serilog.Core;
using Serilog.Events;

namespace SkySync.Infrastructure.Logging;

/// <summary>
/// Exception loglarını zenginleştirir: ExceptionType, InnerExceptionCount, StackTraceHash.
/// Seq'de hata analizi için kullanılır.
/// </summary>
internal sealed class ExceptionEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Exception == null) return;

        var ex = logEvent.Exception;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ExceptionType", ex.GetType().FullName ?? ex.GetType().Name));

        var innerCount = 0;
        var inner = ex.InnerException;
        while (inner != null)
        {
            innerCount++;
            inner = inner.InnerException;
        }
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("InnerExceptionCount", innerCount));

        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            var hash = ComputeSimpleHash(ex.StackTrace);
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("StackTraceHash", hash));
        }
    }

    private static string ComputeSimpleHash(string s)
    {
        var hash = 0;
        foreach (var c in s)
            hash = (hash * 31 + c) & 0x7FFFFFFF;
        return hash.ToString("X8");
    }
}
