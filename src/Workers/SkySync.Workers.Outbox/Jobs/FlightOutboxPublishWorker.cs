using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkySync.Services.Flight.Application.Interfaces;
using SkySync.Services.Flight.Application.UnitOfWorks;
using SkySync.Shared.Events;
using SkySync.Shared.OutboxTable;

namespace SkySync.Workers.Outbox.Jobs;

/// <summary>
/// Outbox Pattern Worker - Veritabanındaki işlenmemiş mesajları RabbitMQ'ya yayınlar
/// Senior Level Best Practices:
/// - Polymorphic Publish (Runtime Type Resolution)
/// - Retry Mechanism with Max Retry Count
/// - Parallel Processing for Performance
/// - Idempotency Support
/// </summary>
public class FlightOutboxPublishWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FlightOutboxPublishWorker> _logger;
    
    // Configuration Constants
    private const int BatchSize = 20; // Her döngüde işlenecek maksimum mesaj sayısı
    private const int DelaySeconds = 2; // Her döngü arası bekleme süresi (saniye)
    private const int MaxRetryCount = 5; // Maksimum deneme sayısı
    private const string EventNamespace = "SkySync.Shared.Events"; // Event'lerin namespace'i

    public FlightOutboxPublishWorker(
        IServiceProvider serviceProvider,
        ILogger<FlightOutboxPublishWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Flight Outbox Worker başlatıldı. Her {DelaySeconds} saniyede bir kontrol edilecek. MaxRetry: {MaxRetryCount}",
            DelaySeconds, MaxRetryCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                // 1. Henüz işlenmemiş mesajları çek (İlk N tanesini al)
                var unprocessedMessages = await outboxRepository.GetUnprocessedMessagesAsync(stoppingToken);
                var messagesToProcess = unprocessedMessages.Take(BatchSize).ToList();

                if (!messagesToProcess.Any())
                {
                    await Task.Delay(TimeSpan.FromSeconds(DelaySeconds), stoppingToken);
                    continue;
                }

                // 2. IPublishEndpoint'i scope'dan al (Scoped service olduğu için)
                var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                // 3. PARALLEL PROCESSING: Tüm mesajları paralel olarak işle
                var publishTasks = messagesToProcess.Select(message => 
                    ProcessMessageAsync(message, outboxRepository, publishEndpoint, stoppingToken)
                ).ToArray();

                var results = await Task.WhenAll(publishTasks);

                // 4. Sonuçları topla
                var processedCount = results.Count(r => r.IsSuccess);
                var failedCount = results.Count(r => !r.IsSuccess && r.ShouldRetry);
                var permanentlyFailedCount = results.Count(r => !r.IsSuccess && !r.ShouldRetry);

                // 5. Değişiklikleri kaydet
                if (processedCount > 0 || failedCount > 0 || permanentlyFailedCount > 0)
                {
                    await unitOfWork.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation(
                        "Outbox işlemi tamamlandı. Başarılı: {ProcessedCount}, Retry Gereken: {FailedCount}, Kalıcı Başarısız: {PermanentlyFailedCount}, Toplam: {TotalCount}",
                        processedCount, failedCount, permanentlyFailedCount, messagesToProcess.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox mesajları yayınlanırken bir hata oluştu.");
            }

            // 6. Belirli bir süre bekle
            await Task.Delay(TimeSpan.FromSeconds(DelaySeconds), stoppingToken);
        }
    }

    /// <summary>
    /// Tek bir mesajı işler ve sonucunu döner (Parallel processing için)
    /// </summary>
    private async Task<MessageProcessResult> ProcessMessageAsync(
        OutboxMessage message,
        IOutboxRepository outboxRepository,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            // Retry count kontrolü
            if (message.RetryCount >= MaxRetryCount)
            {
                message.IsFailed = true;
                message.Error = $"Maksimum deneme sayısına ({MaxRetryCount}) ulaşıldı. Mesaj kalıcı olarak başarısız işaretlendi.";
                await outboxRepository.UpdateAsync(message, cancellationToken);
                
                _logger.LogWarning(
                    "Mesaj maksimum deneme sayısına ulaştı. MessageId: {MessageId}, Type: {Type}, RetryCount: {RetryCount}",
                    message.Id, message.Type, message.RetryCount);
                
                return new MessageProcessResult { IsSuccess = false, ShouldRetry = false };
            }

            // POLYMORPHIC PUBLISH: Runtime'da tipi bul ve yayınla
            var published = await PublishMessagePolymorphicallyAsync(message, publishEndpoint, cancellationToken);

            if (published)
            {
                // Başarılı: İşaretle ve retry count'u sıfırla
                message.ProcessedOn = DateTime.UtcNow;
                message.Error = null;
                message.RetryCount = 0;
                await outboxRepository.UpdateAsync(message, cancellationToken);
                
                return new MessageProcessResult { IsSuccess = true, ShouldRetry = false };
            }
            else
            {
                // Başarısız: Retry count'u artır
                message.RetryCount++;
                message.Error = $"Deserialization veya publish işlemi başarısız oldu. Retry: {message.RetryCount}/{MaxRetryCount}";
                await outboxRepository.UpdateAsync(message, cancellationToken);
                
                _logger.LogWarning(
                    "Mesaj yayınlanamadı, retry yapılacak. MessageId: {MessageId}, Type: {Type}, RetryCount: {RetryCount}",
                    message.Id, message.Type, message.RetryCount);
                
                return new MessageProcessResult 
                { 
                    IsSuccess = false, 
                    ShouldRetry = message.RetryCount < MaxRetryCount 
                };
            }
        }
        catch (Exception ex)
        {
            // Hata durumunda retry count'u artır
            message.RetryCount++;
            message.Error = $"Hata: {ex.Message} (Retry: {message.RetryCount}/{MaxRetryCount})";
            
            if (message.RetryCount >= MaxRetryCount)
            {
                message.IsFailed = true;
            }
            
            await outboxRepository.UpdateAsync(message, cancellationToken);
            
            _logger.LogError(ex, 
                "Mesaj işlenirken hata oluştu. MessageId: {MessageId}, Type: {Type}, RetryCount: {RetryCount}",
                message.Id, message.Type, message.RetryCount);
            
            return new MessageProcessResult 
            { 
                IsSuccess = false, 
                ShouldRetry = message.RetryCount < MaxRetryCount 
            };
        }
    }

    /// <summary>
    /// POLYMORPHIC PUBLISH: Runtime'da event tipini bulup yayınlar (Open/Closed Principle)
    /// Bu yaklaşım sayesinde yeni event tipleri eklerken bu kodu değiştirmene gerek yok.
    /// </summary>
    private async Task<bool> PublishMessagePolymorphicallyAsync(OutboxMessage message, IPublishEndpoint publishEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Event tipinin tam namespace'ini oluştur
            var fullTypeName = $"{EventNamespace}.{message.Type}";
            
            // 2. Runtime'da tipi bul - Tüm yüklü assembly'lerde ara
            var eventType = Type.GetType(fullTypeName);
            
            // Eğer bulunamazsa, tüm yüklü assembly'lerde ara
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
                _logger.LogWarning(
                    "Event tipi bulunamadı. Type: {Type}, FullName: {FullTypeName}, MessageId: {MessageId}",
                    message.Type, fullTypeName, message.Id);
                return false;
            }

            // 3. JSON'ı deserialize et (generic olmayan yöntem)
            var eventInstance = JsonSerializer.Deserialize(message.Content, eventType);
            if (eventInstance == null)
            {
                _logger.LogWarning(
                    "Event deserialize edilemedi. Type: {Type}, MessageId: {MessageId}",
                    message.Type, message.Id);
                return false;
            }

            // 4. MassTransit'in object overload'unu kullanarak yayınla
            // ✅ SABİT MessageId: OutboxMessage.Id'yi kullan (Idempotency için KRİTİK!)
            // Bu şekilde aynı OutboxMessage tekrar publish edilse bile AYNI MessageId olur
            await publishEndpoint.Publish(eventInstance, eventType, ctx =>
            {
                ctx.MessageId = message.Id; // ← SENİN SİSTEMİNDEKİ GİBİ!
            }, cancellationToken);
            
            _logger.LogInformation(
                "✅ Event yayınlandı. Type: {Type}, MessageId: {MessageId} (OutboxMessage.Id)",
                message.Type, message.Id);
            
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, 
                "JSON deserialization hatası. MessageId: {MessageId}, Type: {Type}",
                message.Id, message.Type);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Mesaj yayınlanırken hata oluştu. MessageId: {MessageId}, Type: {Type}",
                message.Id, message.Type);
            return false;
        }
    }

    /// <summary>
    /// Mesaj işleme sonucu (Parallel processing için)
    /// </summary>
    private class MessageProcessResult
    {
        public bool IsSuccess { get; set; }
        public bool ShouldRetry { get; set; }
    }
}
