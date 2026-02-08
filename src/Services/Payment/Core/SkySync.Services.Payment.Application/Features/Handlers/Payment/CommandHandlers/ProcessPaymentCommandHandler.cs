using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Payment.Application.Features.Commands.Payment.Requests;
using SkySync.Services.Payment.Application.Features.Commands.Payment.Responses;
using SkySync.Services.Payment.Application.Interfaces;
using SkySync.Services.Payment.Application.UnitOfWorks;
using SkySync.Services.Payment.Domain.Entities;
using SkySync.Services.Payment.Domain.Enums;
using SkySync.Shared.Events;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Payment.Application.Features.Handlers.Payment.CommandHandlers;

public class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommandRequest, ProcessPaymentCommandResponse>
{
    private readonly IGenericRepository<PaymentTransaction> _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInboxService _inboxService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ProcessPaymentCommandHandler> _logger;

    public ProcessPaymentCommandHandler(
        IGenericRepository<PaymentTransaction> paymentRepository,
        IUnitOfWork unitOfWork,
        IInboxService inboxService,
        IEventPublisher eventPublisher,
        ILogger<ProcessPaymentCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
        _inboxService = inboxService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<ProcessPaymentCommandResponse> Handle(ProcessPaymentCommandRequest request, CancellationToken cancellationToken)
    {
        if (request.CorrelationId == Guid.Empty || request.ReservationId == Guid.Empty)
            return new ProcessPaymentCommandResponse
            {
                IsSuccess = false,
                Message = "CorrelationId ve ReservationId gerekli.",
                Code = "INVALID_REQUEST",
                StatusCode = 400
            };

        if (request.Amount <= 0)
            return new ProcessPaymentCommandResponse
            {
                IsSuccess = false,
                Message = "Amount geçerli olmalı.",
                Code = "INVALID_AMOUNT",
                StatusCode = 400
            };

        if (DateTime.UtcNow > request.ExpiresAt)
        {
            _logger.LogWarning("Payment rejected - expired. ReservationId: {ReservationId}", request.ReservationId);
            return new ProcessPaymentCommandResponse
            {
                IsSuccess = false,
                Message = "Ödeme süresi doldu. Lütfen yeni rezervasyon yapın.",
                Code = "PAYMENT_EXPIRED",
                StatusCode = 400
            };
        }

        var businessKey = request.ReservationId.ToString();
        var messageId = Guid.NewGuid();

        var markedSuccess = await _inboxService.MarkAsProcessedAsync(
            messageId,
            "ProcessPaymentApi",
            businessKey,
            JsonSerializer.Serialize(request),
            cancellationToken);

        if (!markedSuccess)
        {
            _logger.LogWarning("Duplicate payment blocked. ReservationId: {ReservationId}", request.ReservationId);
            return new ProcessPaymentCommandResponse
            {
                IsSuccess = false,
                Message = "Bu rezervasyon için ödeme zaten yapıldı.",
                Code = "DUPLICATE_PAYMENT",
                StatusCode = 409
            };
        }

        bool isSuccess = request.Amount <= 2000; // Demo: 2000 TL üzeri red
        var authorizationId = Guid.NewGuid().ToString();

        var paymentTransaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            ReservationId = request.ReservationId,
            CorrelationId = request.CorrelationId,
            Amount = request.Amount,
            Status = isSuccess ? PaymentStatus.Pending : PaymentStatus.Failed,
            ExternalTransactionId = authorizationId,
            ErrorMessage = isSuccess ? null : "2000 TL limit aşımı. Tutar: " + request.Amount + " TL",
        };

        await _paymentRepository.CreateAsync(paymentTransaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (isSuccess)
        {
            _logger.LogInformation(
                "Payment authorized via API. ReservationId: {ReservationId}, AuthorizationId: {AuthorizationId}",
                request.ReservationId, authorizationId);

            await _eventPublisher.PublishPaymentCompletedAsync(new PaymentCompletedEvent
            {
                CorrelationId = request.CorrelationId,
                ReservationId = request.ReservationId,
                Amount = request.Amount,
                PaymentMethod = "Card",
                TransactionId = authorizationId,
                CompletedAt = DateTime.UtcNow
            }, cancellationToken);

            return new ProcessPaymentCommandResponse
            {
                IsSuccess = true,
                TransactionId = authorizationId,
                Message = "Ödeme başarıyla tamamlandı.",
                StatusCode = 200
            };
        }

        _logger.LogWarning("Payment authorization failed via API. ReservationId: {ReservationId}", request.ReservationId);

        await _eventPublisher.PublishPaymentFailedAsync(new PaymentFailedEvent
        {
            CorrelationId = request.CorrelationId,
            ReservationId = request.ReservationId,
            Amount = request.Amount,
            ErrorMessage = paymentTransaction.ErrorMessage ?? "Unknown Error",
            FailedAt = DateTime.UtcNow
        }, cancellationToken);

        return new ProcessPaymentCommandResponse
        {
            IsSuccess = false,
            Message = paymentTransaction.ErrorMessage ?? "Ödeme reddedildi.",
            Code = "PAYMENT_FAILED",
            StatusCode = 400
        };
    }
}
