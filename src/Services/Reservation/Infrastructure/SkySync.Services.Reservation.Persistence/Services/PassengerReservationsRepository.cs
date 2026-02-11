using Microsoft.EntityFrameworkCore;
using SkySync.Services.Reservation.Application.DTOs;
using SkySync.Services.Reservation.Application.Interfaces;
using SkySync.Services.Reservation.Persistence.Contexts;

namespace SkySync.Services.Reservation.Persistence.Services;

public class PassengerReservationsRepository : IPassengerReservationsRepository
{
    private readonly ReservationServiceDbContext _db;

    public PassengerReservationsRepository(ReservationServiceDbContext db)
    {
        _db = db;
    }

    public async Task<(List<ReservationDto> Reservations, int TotalCount)> GetByPassengerEmailAsync(
        string passengerEmail,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page > 0 ? page : 1;
        var normalizedPageSize = pageSize > 0 ? pageSize : 10;
        var skip = (normalizedPage - 1) * normalizedPageSize;

        var query = from r in _db.Reservations.AsNoTracking()
                    where r.PassengerEmail == passengerEmail && !r.IsDeleted
                    join f in _db.FlightSummaries on r.FlightId equals f.FlightId into fj
                    from f in fj.DefaultIfEmpty()
                    orderby r.CreatedTime descending
                    select new ReservationDto
                    {
                        Id = r.Id,
                        FlightId = r.FlightId,
                        FlightNumber = f != null ? f.FlightNumber : "N/A",
                        Departure = f != null ? f.Departure : null,
                        Arrival = f != null ? f.Destination : null,
                        DepartureTime = f != null ? f.DepartureTime : (DateTime?)null,
                        SeatNumber = r.SeatNumber,
                        Price = r.Price,
                        Status = r.Status.ToString(),
                        PassengerName = r.PassengerName,
                        PassengerSurname = r.PassengerSurname,
                        PassengerEmail = r.PassengerEmail,
                        CreatedTime = r.CreatedTime
                    };

        var totalCount = await query.CountAsync(cancellationToken);
        var reservations = await query.Skip(skip).Take(normalizedPageSize).ToListAsync(cancellationToken);

        return (reservations, totalCount);
    }
}
