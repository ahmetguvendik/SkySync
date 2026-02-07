using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Reservation.Application.Features.Commands.Reservation.Requests;
using SkySync.Services.Reservation.Application.Features.Commands.Reservation.Responses;
using SkySync.Services.Reservation.Application.Interfaces;
using SkySync.Services.Reservation.Application.UnitOfWorks;
using SkySync.Services.Reservation.Domain.Entities;
using SkySync.Services.Reservation.Domain.Enums;
using SkySync.Shared.Events;
using SkySync.Shared.OutboxTable;
using ReservationEntity = SkySync.Services.Reservation.Domain.Entities.Reservation;

namespace SkySync.Services.Reservation.Application.Features.Handlers.Reservation.CommandHandlers;

/// <summary>
/// Rezervasyon oluşturma handler'ı
/// Senior Level: Transaction Management, Outbox Pattern, Saga CorrelationId
/// </summary>
public class CreateReservationCommandHandler : IRequestHandler<CreateReservationCommandRequest, CreateReservationCommandResponse>
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IGenericRepository<ReservationEntity> _reservationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateReservationCommandHandler> _logger;

    public CreateReservationCommandHandler(
        IOutboxRepository outboxRepository,
        IGenericRepository<ReservationEntity> reservationRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateReservationCommandHandler> logger)
    {
        _outboxRepository = outboxRepository;
        _reservationRepository = reservationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CreateReservationCommandResponse> Handle(CreateReservationCommandRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Transaction başlat
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // 0. ADIM: LOCK KONTROLÜ - Aynı koltuğun zaten rezerve edilip edilmediğini kontrol et
            // Senior Level: Pessimistic Locking - Concurrent reservation prevention
            var existingReservations = await _reservationRepository.GetAllAsync(
                r => r.FlightId == request.FlightId 
                     && r.SeatNumber == request.SeatNumber 
                     && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed),
                cancellationToken);

            if (existingReservations.Any())
            {
                var existingReservation = existingReservations.First();
                _logger.LogWarning(
                    "Seat already reserved. FlightId: {FlightId}, SeatNumber: {SeatNumber}, ExistingReservationId: {ReservationId}, Status: {Status}",
                    request.FlightId, request.SeatNumber, existingReservation.Id, existingReservation.Status);

                await _unitOfWork.RollbackTransactionAsync(cancellationToken);

                return new CreateReservationCommandResponse
                {
                    ReservationId = Guid.Empty,
                    CorrelationId = Guid.Empty,
                    IsSuccess = false,
                    Message = $"Seat {request.SeatNumber} on flight {request.FlightId} is already reserved or pending confirmation."
                };
            }

            var reservationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid(); // Saga için correlation ID
            var now = DateTime.UtcNow;

            // 1. ADIM: Rezervasyon Entity'sini Oluştur ve Kaydet (Pending durumunda)
            var reservation = new ReservationEntity
            {
                Id = reservationId,
                FlightId = request.FlightId,
                SeatNumber = request.SeatNumber,
                Price = request.Price,
                PassengerName = request.PassengerName,
                PassengerSurname = request.PassengerSurname,
                PassengerEmail = request.PassengerEmail,
                Status = ReservationStatus.Pending, // İlk durum: Pending
                CorrelationId = correlationId, // Saga takibi için
                CreatedTime = now,
                ModifiedTime = now,
                IsDeleted = false
            };

            await _reservationRepository.CreateAsync(reservation, cancellationToken);

            // 2. ADIM: Outbox Mesajını Oluştur (ReservationStartedEvent)
            var reservationStartedEvent = new ReservationStartedEvent
            {
                ReservationId = reservationId,
                CorrelationId = correlationId,
                FlightId = request.FlightId,
                SeatNumber = request.SeatNumber,
                Price = request.Price,
                PassengerName = request.PassengerName,
                PassengerSurname = request.PassengerSurname,
                PassengerEmail = request.PassengerEmail,
                CreatedAt = now
            };

            // Event'i JSON'a çevir
            var eventContent = JsonSerializer.Serialize(reservationStartedEvent);

            // OutboxMessage oluştur - Trace context (distributed tracing için)
            var activity = Activity.Current;
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(ReservationStartedEvent),
                Content = eventContent,
                OccurredOn = now,
                ProcessedOn = null,
                Error = null,
                RetryCount = 0,
                IsFailed = false,
                Traceparent = activity?.Id,
                Tracestate = activity?.TraceStateString
            };

            await _outboxRepository.CreateAsync(outboxMessage, cancellationToken);

            // 3. ADIM: Hepsini Tek Transaction'da Bitir
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Reservation created successfully. ReservationId: {ReservationId}, CorrelationId: {CorrelationId}, FlightId: {FlightId}, SeatNumber: {SeatNumber}",
                reservationId, correlationId, request.FlightId, request.SeatNumber);

            return new CreateReservationCommandResponse
            {
                ReservationId = reservationId,
                CorrelationId = correlationId,
                IsSuccess = true,
                Message = "Reservation created successfully"
            };
        }
        catch (Exception ex)
        {
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" Inner Exception: {ex.InnerException.Message}";
            }

            _logger.LogError(ex, 
                "Error occurred while creating reservation. FlightId: {FlightId}, SeatNumber: {SeatNumber}, Error: {Error}",
                request.FlightId, request.SeatNumber, errorMessage);

            // Transaction'ı rollback et
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);

            return new CreateReservationCommandResponse
            {
                ReservationId = Guid.Empty,
                CorrelationId = Guid.Empty,
                IsSuccess = false,
                Message = $"Error occurred while creating reservation: {errorMessage}"
            };
        }
    }
}
