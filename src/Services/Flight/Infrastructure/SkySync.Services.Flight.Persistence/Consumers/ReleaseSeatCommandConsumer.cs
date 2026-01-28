using MassTransit;
using Microsoft.EntityFrameworkCore;
using SkySync.Services.Flight.Persistence.Contexts;
using SkySync.Shared.Commands;

namespace SkySync.Services.Flight.Persistence.Consumers;

public class ReleaseSeatCommandConsumer : IConsumer<ReleaseSeatCommand>
{
    private readonly FlightServiceDbContext _context;

    public ReleaseSeatCommandConsumer(FlightServiceDbContext context)
    {
        _context = context;
    }

    public async Task Consume(ConsumeContext<ReleaseSeatCommand> context)
    {
        var seat = await _context.Seats.FirstOrDefaultAsync(x => 
            x.FlightId == context.Message.FlightId && 
            x.SeatNumber == context.Message.SeatNumber);

        if (seat != null)
        {
            seat.IsReserved = false;
            await _context.SaveChangesAsync();
        }
    }
}
