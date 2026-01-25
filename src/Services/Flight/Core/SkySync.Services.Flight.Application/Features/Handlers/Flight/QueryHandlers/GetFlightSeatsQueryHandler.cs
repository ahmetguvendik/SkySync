using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Flight.Application.DTOs;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Requests;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Responses;
using SkySync.Services.Flight.Application.Interfaces;
using FlightEntity = SkySync.Services.Flight.Domain.Entities.Flight;
using SeatEntity = SkySync.Services.Flight.Domain.Entities.Seat;

namespace SkySync.Services.Flight.Application.Features.Handlers.Flight.QueryHandlers;

/// <summary>
/// CQRS Query Handler - Belirli bir uçuşun koltuklarını getir
/// Senior Level: Direct DB Query (Dinamik veri, cache yok veya çok kısa süreli)
/// 
/// Neden Cache Yok?
/// - Koltuk durumu çok dinamik (anlık rezervasyonlar)
/// - Kullanıcı koltuk seçerken güncel bilgi görmeli
/// - Cache'lenirse "Hayalet koltuk" problemi oluşur (Rezerve edilmiş ama cache'de boş görünür)
/// </summary>
public class GetFlightSeatsQueryHandler : IRequestHandler<GetFlightSeatsQueryRequest, GetFlightSeatsQueryResponse>
{
    private readonly IGenericRepository<FlightEntity> _flightRepository;
    private readonly IGenericRepository<SeatEntity> _seatRepository;
    private readonly ILogger<GetFlightSeatsQueryHandler> _logger;

    public GetFlightSeatsQueryHandler(
        IGenericRepository<FlightEntity> flightRepository,
        IGenericRepository<SeatEntity> seatRepository,
        ILogger<GetFlightSeatsQueryHandler> logger)
    {
        _flightRepository = flightRepository;
        _seatRepository = seatRepository;
        _logger = logger;
    }

    public async Task<GetFlightSeatsQueryResponse> Handle(GetFlightSeatsQueryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Uçuşun var olup olmadığını kontrol et
            var allFlights = await _flightRepository.GetAllAsync(cancellationToken);
            var flight = allFlights.FirstOrDefault(f => f.Id == request.FlightId && !f.IsDeleted);

            if (flight == null)
            {
                _logger.LogWarning("Flight not found. FlightId: {FlightId}", request.FlightId);
                throw new KeyNotFoundException($"Flight with id {request.FlightId} not found");
            }

            // 2. Uçuşa ait tüm koltukları getir (Soft delete kontrolü ile)
            // NOT: Cache kullanmıyoruz çünkü koltuk durumu çok dinamik
            var allSeats = await _seatRepository.GetAllAsync(cancellationToken);
            var seats = allSeats
                .Where(s => s.FlightId == request.FlightId && !s.IsDeleted)
                .OrderBy(s => s.SeatNumber) // Koltuk numarasına göre sırala (1A, 1B, 2A...)
                .ToList();

            // 3. Domain entity'den DTO'ya map et
            var seatDtos = seats.Select(s => new SeatDto
            {
                Id = s.Id,
                SeatNumber = s.SeatNumber,
                IsReserved = s.IsReserved,
                Price = s.Price,
                UserId = s.UserId
            }).ToList();

            // 4. İstatistikleri hesapla
            var availableSeatsCount = seatDtos.Count(s => !s.IsReserved);
            var reservedSeatsCount = seatDtos.Count(s => s.IsReserved);
            var totalSeatsCount = seatDtos.Count;

            _logger.LogInformation(
                "Flight seats retrieved. FlightId: {FlightId}, Total: {Total}, Available: {Available}, Reserved: {Reserved}",
                request.FlightId, totalSeatsCount, availableSeatsCount, reservedSeatsCount);

            return new GetFlightSeatsQueryResponse
            {
                FlightId = flight.Id,
                FlightNumber = flight.FlightNumber,
                Seats = seatDtos,
                AvailableSeatsCount = availableSeatsCount,
                ReservedSeatsCount = reservedSeatsCount,
                TotalSeatsCount = totalSeatsCount
            };
        }
        catch (KeyNotFoundException)
        {
            throw; // KeyNotFoundException'ı yukarı fırlat
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching flight seats. FlightId: {FlightId}", request.FlightId);
            throw;
        }
    }
}
