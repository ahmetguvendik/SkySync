using System.Text.Json;
using MassTransit;
using MassTransit.Logging;
using Microsoft.Extensions.Logging;
using SkySync.Shared.OutboxTable;

namespace SkySync.Workers.Outbox.Jobs.Common;

/// <summary>
/// Outbox mesajlarını polymorphic olarak RabbitMQ'ya yayınlamak için ortak yardımcı.
/// Runtime'da event tipini bulup yayınlar (Open/Closed Principle).
/// </summary>
internal static class OutboxPublishHelper
{
    /// <summary>
    /// Runtime'da event tipini bulup yayınlar. Yeni event tipleri eklerken bu kodu değiştirmene gerek yok.
    /// </summary>
    public static async Task<bool> PublishMessagePolymorphicallyAsync(
        OutboxMessage message,
        IPublishEndpoint publishEndpoint,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var fullTypeName = $"{OutboxWorkerConstants.EventNamespace}.{message.Type}";
            var eventType = Type.GetType(fullTypeName);

            if (eventType == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    eventType = assembly.GetType(fullTypeName);
                    if (eventType != null)
                        break;
                }
            }

            if (eventType == null)
            {
                logger.LogWarning(
                    "Event tipi bulunamadı. Type: {Type}, FullName: {FullTypeName}, MessageId: {MessageId}",
                    message.Type, fullTypeName, message.Id);
                return false;
            }

            var eventInstance = JsonSerializer.Deserialize(message.Content, eventType);
            if (eventInstance == null)
            {
                logger.LogWarning(
                    "Event deserialize edilemedi. Type: {Type}, MessageId: {MessageId}",
                    message.Type, message.Id);
                return false;
            }

            await publishEndpoint.Publish(eventInstance, eventType, ctx =>
            {
                ctx.MessageId = message.Id; // Idempotency için sabit MessageId
                // MassTransit MT-Activity-Id - Consumer aynı trace'e devam eder (DiagnosticHeaders.ActivityId)
                if (!string.IsNullOrEmpty(message.Traceparent))
                    ctx.Headers.Set(DiagnosticHeaders.ActivityId, message.Traceparent);
            }, cancellationToken);

            logger.LogInformation(
                "Event published. Type: {Type}, MessageId: {MessageId}",
                message.Type, message.Id);

            return true;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "JSON deserialization hatası. MessageId: {MessageId}, Type: {Type}",
                message.Id, message.Type);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Mesaj yayınlanırken hata oluştu. MessageId: {MessageId}, Type: {Type}",
                message.Id, message.Type);
            return false;
        }
    }
}
