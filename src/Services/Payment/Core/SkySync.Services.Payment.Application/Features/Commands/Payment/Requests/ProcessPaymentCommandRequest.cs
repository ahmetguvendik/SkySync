using MediatR;
using SkySync.Services.Payment.Application.Features.Commands.Payment.Responses;

namespace SkySync.Services.Payment.Application.Features.Commands.Payment.Requests;

public class ProcessPaymentCommandRequest : IRequest<ProcessPaymentCommandResponse>
{
    public Guid CorrelationId { get; set; }
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    /// <summary>
    /// Rezervasyon response'dan gelen ExpiresAt. Süre aşılmışsa ödeme reddedilir.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    /// <summary>
    /// Demo: Kart numarası (simülasyon için)
    /// </summary>
    public string? CardNumber { get; set; }
}
