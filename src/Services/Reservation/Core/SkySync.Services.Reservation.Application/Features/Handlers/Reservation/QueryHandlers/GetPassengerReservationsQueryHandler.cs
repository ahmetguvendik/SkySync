using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Reservation.Application.DTOs;
using SkySync.Services.Reservation.Application.Features.Queries.Reservation.Requests;
using SkySync.Services.Reservation.Application.Features.Queries.Reservation.Responses;
using SkySync.Services.Reservation.Application.Interfaces;
using ReservationEntity = SkySync.Services.Reservation.Domain.Entities.Reservation;

namespace SkySync.Services.Reservation.Application.Features.Handlers.Reservation.QueryHandlers;

/// <summary>
/// Yolcu rezervasyonlarını getiren query handler
/// CQRS Query Side - Read operations
/// </summary>
public class GetPassengerReservationsQueryHandler : IRequestHandler<GetPassengerReservationsQueryRequest, GetPassengerReservationsQueryResponse>
{
    private readonly IGenericRepository<ReservationEntity> _reservationRepository;
    private readonly ILogger<GetPassengerReservationsQueryHandler> _logger;

    public GetPassengerReservationsQueryHandler(
        IGenericRepository<ReservationEntity> reservationRepository,
        ILogger<GetPassengerReservationsQueryHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _logger = logger;
    }

    public async Task<GetPassengerReservationsQueryResponse> Handle(GetPassengerReservationsQueryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Yolcu email'ine göre rezervasyonları getir
            var allReservations = await _reservationRepository.GetAllAsync(cancellationToken);
            var passengerReservations = allReservations
                .Where(r => r.PassengerEmail == request.PassengerEmail && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedTime)
                .ToList();

            // Domain entity'den DTO'ya map et
            var reservationDtos = passengerReservations.Select(r => new ReservationDto
            {
                Id = r.Id,
                FlightId = r.FlightId,
                FlightNumber = "N/A", // Flight Service'den join veya event ile beslenir (şimdilik N/A)
                SeatNumber = r.SeatNumber,
                Price = r.Price,
                Status = r.Status.ToString(),
                PassengerName = r.PassengerName,
                PassengerSurname = r.PassengerSurname,
                PassengerEmail = r.PassengerEmail,
                CreatedTime = r.CreatedTime
            }).ToList();

            _logger.LogInformation(
                "Passenger reservations retrieved. Email: {Email}, Count: {Count}",
                request.PassengerEmail, reservationDtos.Count);

            return new GetPassengerReservationsQueryResponse
            {
                Reservations = reservationDtos,
                TotalCount = reservationDtos.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching passenger reservations. Email: {Email}", request.PassengerEmail);
            throw;
        }
    }
}
