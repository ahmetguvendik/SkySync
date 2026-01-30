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
            "💰 Payment command received. MessageId: {MessageId}, ReservationId: {ResId}, Amount: {Amount}",
            messageId, msg.ReservationId, msg.Amount);

        // ✅ INBOX PATTERN - Idempotency Marker (CRITICAL for payments!)
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
                "🛑 Duplicate payment blocked! ReservationId: {ResId}, MessageId: {MessageId}",
                msg.ReservationId, messageId);
            return;
        }

        _logger.LogInformation(
            "✅ Payment locked for processing. ReservationId: {ResId}, MessageId: {MessageId}",
            msg.ReservationId, messageId);

        try
        {
            // YENİ AKIŞ: Authorize işlemi (para çekilmez, sadece rezerve)
            // Ödeme İşlemini Simüle Et (5000 TL üzeri fail olsun)
            bool isSuccess = msg.Amount < 5000;
            var authorizationId = Guid.NewGuid().ToString(); // Payment gateway'den gelen authorization ID

            var paymentTransaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                ReservationId = msg.ReservationId,
                CorrelationId = msg.CorrelationId,
                Amount = msg.Amount,
                Status = isSuccess ? PaymentStatus.Pending : PaymentStatus.Failed, // Authorize için Pending
                ExternalTransactionId = authorizationId,
                ErrorMessage = isSuccess ? null : "Yetersiz bakiye veya limit aşımı.",
                CreatedDate = DateTime.UtcNow
            };

            await _context.PaymentTransactions.AddAsync(paymentTransaction);
            await _context.SaveChangesAsync();

            if (isSuccess)
            {
                _logger.LogInformation(
                    "✅ Payment authorized (not captured yet). ReservationId: {ResId}, AuthorizationId: {AuthId}",
                    msg.ReservationId, authorizationId);

                // YENİ: PaymentAuthorizedEvent publish et (para çekilmedi, sadece authorize)
                await context.Publish(new PaymentAuthorizedEvent
                {
                    CorrelationId = msg.CorrelationId,
                    ReservationId = msg.ReservationId,
                    Amount = msg.Amount,
                    AuthorizationId = authorizationId,
                    AuthorizedAt = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogWarning("❌ Payment authorization failed! ReservationId: {ResId}", msg.ReservationId);

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
                "❌ Payment processing failed. ReservationId: {ResId}, MessageId: {MessageId}",
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
