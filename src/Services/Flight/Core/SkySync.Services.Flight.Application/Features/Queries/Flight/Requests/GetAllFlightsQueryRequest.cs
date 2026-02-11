using System;
using MediatR;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Responses;

namespace SkySync.Services.Flight.Application.Features.Queries.Flight.Requests;

/// <summary>
/// CQRS Query - Tüm uçuşları getir (Cache Aside Pattern ile)
/// </summary>
public class GetAllFlightsQueryRequest : IRequest<GetAllFlightsQueryResponse>
{
    public string? Departure { get; set; }
    public string? Destination { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public DateOnly? ReturnDate { get; set; } // Round-trip planlandığında kullanılacak
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
