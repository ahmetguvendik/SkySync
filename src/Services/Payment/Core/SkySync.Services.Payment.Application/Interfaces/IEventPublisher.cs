using SkySync.Shared.Events;

namespace SkySync.Services.Payment.Application.Interfaces;

public interface IEventPublisher
{
    Task PublishPaymentCompletedAsync(PaymentCompletedEvent evt, CancellationToken cancellationToken = default);
    Task PublishPaymentFailedAsync(PaymentFailedEvent evt, CancellationToken cancellationToken = default);
}
