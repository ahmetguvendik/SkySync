using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Reservation.Application.Features.Queries.Reservation.Requests;
using SkySync.Services.Reservation.Application.Features.Queries.Reservation.Responses;
using SkySync.Services.Reservation.Application.Interfaces;

namespace SkySync.Services.Reservation.Application.Features.Handlers.Reservation.QueryHandlers;

/// <summary>
/// Yolcu rezervasyonlarını getiren query handler (FlightSummary join ile FlightNumber dahil).
/// CQRS Query Side - Read operations
/// </summary>
public class GetPassengerReservationsQueryHandler : IRequestHandler<GetPassengerReservationsQueryRequest, GetPassengerReservationsQueryResponse>
{
    private readonly IPassengerReservationsRepository _passengerReservationsRepository;
    private readonly ILogger<GetPassengerReservationsQueryHandler> _logger;

    public GetPassengerReservationsQueryHandler(
        IPassengerReservationsRepository passengerReservationsRepository,
        ILogger<GetPassengerReservationsQueryHandler> logger)
    {
        _passengerReservationsRepository = passengerReservationsRepository;
        _logger = logger;
    }

    public async Task<GetPassengerReservationsQueryResponse> Handle(GetPassengerReservationsQueryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var reservationDtos = await _passengerReservationsRepository
                .GetByPassengerEmailAsync(request.PassengerEmail, cancellationToken);

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
