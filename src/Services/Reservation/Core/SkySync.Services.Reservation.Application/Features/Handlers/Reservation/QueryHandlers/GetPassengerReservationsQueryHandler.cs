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
    private const int DefaultPageSize = 10;

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
            var page = request.Page > 0 ? request.Page : 1;
            var (reservationDtos, totalCount) = await _passengerReservationsRepository
                .GetByPassengerEmailAsync(request.PassengerEmail, page, DefaultPageSize, cancellationToken);

            _logger.LogInformation(
                "Passenger reservations retrieved. Email: {Email}, Count: {Count}",
                request.PassengerEmail, reservationDtos.Count);

            return new GetPassengerReservationsQueryResponse
            {
                Reservations = reservationDtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = DefaultPageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching passenger reservations. Email: {Email}", request.PassengerEmail);
            throw;
        }
    }
}
