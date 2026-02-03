using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkySync.Services.Flight.Application.Interfaces;
using SkySync.Services.Flight.Application.UnitOfWorks;
using SkySync.Shared.OutboxTable;
using SkySync.Workers.Outbox.Jobs.Common;

namespace SkySync.Workers.Outbox.Jobs.Flight;

/// <summary>
/// Flight Service Outbox Pattern Worker - Veritabanındaki işlenmemiş mesajları RabbitMQ'ya yayınlar.
/// Senior Level: Polymorphic Publish, Retry, Parallel Processing, Idempotency.
/// </summary>
public class FlightOutboxPublishWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FlightOutboxPublishWorker> _logger;

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
            OutboxWorkerConstants.DelaySeconds, OutboxWorkerConstants.MaxRetryCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var unprocessedMessages = await outboxRepository.GetUnprocessedMessagesAsync(stoppingToken);
                var messagesToProcess = unprocessedMessages.Take(OutboxWorkerConstants.BatchSize).ToList();

                if (messagesToProcess.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(OutboxWorkerConstants.DelaySeconds), stoppingToken);
                    continue;
                }

                var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                var publishTasks = messagesToProcess.Select(message =>
                    ProcessMessageAsync(message, outboxRepository, publishEndpoint, stoppingToken)
                ).ToArray();

                var results = await Task.WhenAll(publishTasks);

                var processedCount = results.Count(r => r.IsSuccess);
                var failedCount = results.Count(r => !r.IsSuccess && r.ShouldRetry);
                var permanentlyFailedCount = results.Count(r => !r.IsSuccess && !r.ShouldRetry);

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

            await Task.Delay(TimeSpan.FromSeconds(OutboxWorkerConstants.DelaySeconds), stoppingToken);
        }
    }

    private async Task<MessageProcessResult> ProcessMessageAsync(
        OutboxMessage message,
        IOutboxRepository outboxRepository,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            if (message.RetryCount >= OutboxWorkerConstants.MaxRetryCount)
            {
                message.IsFailed = true;
                message.Error = $"Maksimum deneme sayısına ({OutboxWorkerConstants.MaxRetryCount}) ulaşıldı. Mesaj kalıcı olarak başarısız işaretlendi.";
                await outboxRepository.UpdateAsync(message, cancellationToken);

                _logger.LogWarning(
                    "Mesaj maksimum deneme sayısına ulaştı. MessageId: {MessageId}, Type: {Type}, RetryCount: {RetryCount}",
                    message.Id, message.Type, message.RetryCount);

                return new MessageProcessResult { IsSuccess = false, ShouldRetry = false };
            }

            var published = await OutboxPublishHelper.PublishMessagePolymorphicallyAsync(
                message, publishEndpoint, _logger, cancellationToken);

            if (published)
            {
                message.ProcessedOn = DateTime.UtcNow;
                message.Error = null;
                message.RetryCount = 0;
                await outboxRepository.UpdateAsync(message, cancellationToken);
                return new MessageProcessResult { IsSuccess = true, ShouldRetry = false };
            }

            message.RetryCount++;
            message.Error = $"Deserialization veya publish işlemi başarısız oldu. Retry: {message.RetryCount}/{OutboxWorkerConstants.MaxRetryCount}";
            await outboxRepository.UpdateAsync(message, cancellationToken);

            _logger.LogWarning(
                "Mesaj yayınlanamadı, retry yapılacak. MessageId: {MessageId}, Type: {Type}, RetryCount: {RetryCount}",
                message.Id, message.Type, message.RetryCount);

            return new MessageProcessResult
            {
                IsSuccess = false,
                ShouldRetry = message.RetryCount < OutboxWorkerConstants.MaxRetryCount
            };
        }
        catch (Exception ex)
        {
            message.RetryCount++;
            message.Error = $"Hata: {ex.Message} (Retry: {message.RetryCount}/{OutboxWorkerConstants.MaxRetryCount})";

            if (message.RetryCount >= OutboxWorkerConstants.MaxRetryCount)
                message.IsFailed = true;

            await outboxRepository.UpdateAsync(message, cancellationToken);

            _logger.LogError(ex,
                "Mesaj işlenirken hata oluştu. MessageId: {MessageId}, Type: {Type}, RetryCount: {RetryCount}",
                message.Id, message.Type, message.RetryCount);

            return new MessageProcessResult
            {
                IsSuccess = false,
                ShouldRetry = message.RetryCount < OutboxWorkerConstants.MaxRetryCount
            };
        }
    }
}
