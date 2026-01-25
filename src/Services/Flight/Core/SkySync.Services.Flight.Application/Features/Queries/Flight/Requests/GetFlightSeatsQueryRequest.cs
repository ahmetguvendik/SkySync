using MediatR;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Responses;

namespace SkySync.Services.Flight.Application.Features.Queries.Flight.Requests;

/// <summary>
/// CQRS Query - Belirli bir uçuşun koltuklarını getir (Koltuk Seçimi için)
/// </summary>
public class GetFlightSeatsQueryRequest : IRequest<GetFlightSeatsQueryResponse>
{
    public Guid FlightId { get; set; }
}
