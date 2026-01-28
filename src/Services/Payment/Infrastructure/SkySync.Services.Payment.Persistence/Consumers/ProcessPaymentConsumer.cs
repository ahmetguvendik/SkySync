using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkySync.Services.Payment.Domain.Entities;
using SkySync.Services.Payment.Persistence.Contexts;
using SkySync.Shared.Commands;
using SkySync.Shared.Events;

namespace SkySync.Services.Payment.Persistence.Consumers;

public class ProcessPaymentConsumer : IConsumer<ProcessPaymentCommand>
{
    private readonly PaymentServiceDbContext _context;
    private readonly ILogger<ProcessPaymentConsumer> _logger;

    public ProcessPaymentConsumer(PaymentServiceDbContext context, ILogger<ProcessPaymentConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessPaymentCommand> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Processing payment... ReservationId: {ResId}, Amount: {Amount}", msg.ReservationId, msg.Amount);

        // 1. Idempotency Check: Zaten başarılı bir işlem var mı?
        var existingTransaction = await _context.PaymentTransactions
            .FirstOrDefaultAsync(x => x.ReservationId == msg.ReservationId && x.Status == "Success");

        if (existingTransaction != null)
        {
            _logger.LogWarning("Payment already processed for ReservationId: {ResId}. Skipping...", msg.ReservationId);
            
            await context.Publish(new PaymentCompletedEvent
            {
                CorrelationId = msg.CorrelationId,
                ReservationId = msg.ReservationId,
                Amount = existingTransaction.Amount,
                PaymentMethod = "Cached",
                TransactionId = existingTransaction.ExternalTransactionId ?? "N/A",
                CompletedAt = existingTransaction.CreatedDate
            });
            return;
        }

        // 2. Ödeme İşlemini Simüle Et (5000 TL üzeri fail olsun)
        bool isSuccess = msg.Amount < 5000;
        var transactionId = Guid.NewGuid().ToString();

        var paymentTransaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            ReservationId = msg.ReservationId,
            CorrelationId = msg.CorrelationId,
            Amount = msg.Amount,
            Status = isSuccess ? "Success" : "Failed",
            ExternalTransactionId = transactionId,
            ErrorMessage = isSuccess ? null : "Yetersiz bakiye veya limit aşımı.",
            CreatedDate = DateTime.UtcNow
        };

        await _context.PaymentTransactions.AddAsync(paymentTransaction);
        await _context.SaveChangesAsync();

        if (isSuccess)
        {
            _logger.LogInformation("Payment successful. ReservationId: {ResId}", msg.ReservationId);
            
            await context.Publish(new PaymentCompletedEvent
            {
                CorrelationId = msg.CorrelationId,
                ReservationId = msg.ReservationId,
                Amount = msg.Amount,
                PaymentMethod = "CreditCard",
                TransactionId = transactionId,
                CompletedAt = DateTime.UtcNow
            });
        }
        else
        {
            _logger.LogWarning("Payment rejected! ReservationId: {ResId}", msg.ReservationId);

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
}
