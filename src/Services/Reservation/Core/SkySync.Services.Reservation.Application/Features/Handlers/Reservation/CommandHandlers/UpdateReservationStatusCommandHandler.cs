using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Reservation.Application.Features.Commands.Reservation.Requests;
using SkySync.Services.Reservation.Application.Features.Commands.Reservation.Responses;
using SkySync.Services.Reservation.Application.Interfaces;
using SkySync.Services.Reservation.Application.UnitOfWorks;
using ReservationEntity = SkySync.Services.Reservation.Domain.Entities.Reservation;

namespace SkySync.Services.Reservation.Application.Features.Handlers.Reservation.CommandHandlers;

/// <summary>
/// Rezervasyon durumunu güncelleme handler'ı
/// Saga'dan gelen event'lere göre rezervasyon durumunu günceller
/// </summary>
public class UpdateReservationStatusCommandHandler : IRequestHandler<UpdateReservationStatusCommandRequest, UpdateReservationStatusCommandResponse>
{
    private readonly IGenericRepository<ReservationEntity> _reservationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateReservationStatusCommandHandler> _logger;

    public UpdateReservationStatusCommandHandler(
        IGenericRepository<ReservationEntity> reservationRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateReservationStatusCommandHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<UpdateReservationStatusCommandResponse> Handle(UpdateReservationStatusCommandRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Rezervasyonu bul
            var reservations = await _reservationRepository.GetAllAsync(cancellationToken);
            var reservation = reservations.FirstOrDefault(r => r.Id == request.ReservationId && !r.IsDeleted);

            if (reservation == null)
            {
                _logger.LogWarning("Reservation not found. ReservationId: {ReservationId}", request.ReservationId);
                return new UpdateReservationStatusCommandResponse
                {
                    ReservationId = request.ReservationId,
                    IsSuccess = false,
                    Message = $"Reservation with id {request.ReservationId} not found"
                };
            }

            // Durumu güncelle
            reservation.Status = request.Status;
            reservation.ModifiedTime = DateTime.UtcNow;

            // Hata mesajı varsa logla (Failed durumu için)
            if (!string.IsNullOrEmpty(request.ErrorMessage))
            {
                _logger.LogWarning(
                    "Reservation status updated to {Status} with error. ReservationId: {ReservationId}, Error: {Error}",
                    request.Status, request.ReservationId, request.ErrorMessage);
            }

            await _reservationRepository.UpdateAsync(reservation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Reservation status updated successfully. ReservationId: {ReservationId}, Status: {Status}",
                request.ReservationId, request.Status);

            return new UpdateReservationStatusCommandResponse
            {
                ReservationId = request.ReservationId,
                IsSuccess = true,
                Message = $"Reservation status updated to {request.Status}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error occurred while updating reservation status. ReservationId: {ReservationId}, Status: {Status}",
                request.ReservationId, request.Status);

            return new UpdateReservationStatusCommandResponse
            {
                ReservationId = request.ReservationId,
                IsSuccess = false,
                Message = $"Error occurred while updating reservation status: {ex.Message}"
            };
        }
    }
}
