using MediatR;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Responses;

namespace SkySync.Services.Flight.Application.Features.Queries.Flight.Requests;

/// <summary>
/// CQRS Query - Tüm uçuşları getir (Cache Aside Pattern ile)
/// </summary>
public class GetAllFlightsQueryRequest : IRequest<GetAllFlightsQueryResponse>
{
    // İleride filtreleme için parametreler eklenebilir
    // public string? Departure { get; set; }
    // public string? Destination { get; set; }
    // public DateTime? DepartureDate { get; set; }
}
