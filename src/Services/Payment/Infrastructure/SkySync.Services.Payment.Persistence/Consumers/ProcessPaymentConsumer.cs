using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkySync.Services.Payment.Domain.Entities;
using SkySync.Services.Payment.Domain.Enums;
using SkySync.Services.Payment.Persistence.Contexts;
using SkySync.Shared.Commands;
using SkySync.Shared.Events;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Payment.Persistence.Consumers;

public class ProcessPaymentConsumer : IConsumer<ProcessPaymentCommand>
{
    private readonly PaymentServiceDbContext _context;
    private readonly IInboxService _inboxService;
    private readonly ILogger<ProcessPaymentConsumer> _logger;

    public ProcessPaymentConsumer(
        PaymentServiceDbContext context,
        IInboxService inboxService,
        ILogger<ProcessPaymentConsumer> logger)
    {
        _context = context;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessPaymentCommand> context)
    {
        var msg = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();

        _logger.LogInformation(
            "Payment command received. MessageId: {MessageId}, ReservationId: {ReservationId}, Amount: {Amount}",
            messageId, msg.ReservationId, msg.Amount);

        // INBOX PATTERN - Idempotency Marker (CRITICAL for payments)
        var businessKey = msg.ReservationId.ToString();
        var markedSuccess = await _inboxService.MarkAsProcessedAsync(
            messageId,
            nameof(ProcessPaymentCommand),
            businessKey,
            JsonSerializer.Serialize(msg));

        if (!markedSuccess)
        {
            // Duplicate payment attempt blocked!
            _logger.LogWarning(
                "Duplicate payment blocked. ReservationId: {ReservationId}, MessageId: {MessageId}",
                msg.ReservationId, messageId);
            return;
        }

        _logger.LogInformation(
            "Payment locked for processing. ReservationId: {ReservationId}, MessageId: {MessageId}",
            msg.ReservationId, messageId);

        try
        {
            // Ödeme simülasyonu: 2000 TL üzeri red
            bool isSuccess = msg.Amount <= 2000;
            var authorizationId = Guid.NewGuid().ToString(); // Payment gateway'den gelen authorization ID

            var paymentTransaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                ReservationId = msg.ReservationId,
                CorrelationId = msg.CorrelationId,
                Amount = msg.Amount,
                Status = isSuccess ? PaymentStatus.Pending : PaymentStatus.Failed, // Authorize için Pending
                ExternalTransactionId = authorizationId,
                ErrorMessage = isSuccess ? null : "2000 TL limit aşımı. Tutar: " + msg.Amount + " TL",
                CreatedDate = DateTime.UtcNow
            };

            await _context.PaymentTransactions.AddAsync(paymentTransaction);
            await _context.SaveChangesAsync();

            if (isSuccess)
            {
                _logger.LogInformation(
                    "Payment authorized. ReservationId: {ReservationId}, AuthorizationId: {AuthorizationId}",
                    msg.ReservationId, authorizationId);

                // Saga PaymentCompletedEvent bekliyor - authorize başarılı = ödeme tamamlandı
                await context.Publish(new PaymentCompletedEvent
                {
                    CorrelationId = msg.CorrelationId,
                    ReservationId = msg.ReservationId,
                    Amount = msg.Amount,
                    PaymentMethod = "Card",
                    TransactionId = authorizationId,
                    CompletedAt = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogWarning("Payment authorization failed. ReservationId: {ReservationId}", msg.ReservationId);

                await context.Publish(new PaymentFailedEvent
                {
                    CorrelationId = msg.CorrelationId,
                    ReservationId = msg.ReservationId,
                    Amount = msg.Amount,
                    ErrorMessage = paymentTransaction.ErrorMessage ?? "Unknown Error",
                    FailedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Payment processing failed. ReservationId: {ReservationId}, MessageId: {MessageId}",
                msg.ReservationId, messageId);

            await _inboxService.MarkAsFailedAsync(
                messageId,
                nameof(ProcessPaymentCommand),
                businessKey,
                ex.Message,
                JsonSerializer.Serialize(msg));

            throw;
        }
    }
}
