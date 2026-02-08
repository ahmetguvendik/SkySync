using MassTransit;
using SkySync.Services.Payment.Application.Interfaces;
using SkySync.Shared.Events;

namespace SkySync.Services.Payment.Persistence.Services;

public class EventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public EventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishPaymentCompletedAsync(PaymentCompletedEvent evt, CancellationToken cancellationToken = default)
    {
        return _publishEndpoint.Publish(evt, cancellationToken);
    }

    public Task PublishPaymentFailedAsync(PaymentFailedEvent evt, CancellationToken cancellationToken = default)
    {
        return _publishEndpoint.Publish(evt, cancellationToken);
    }
}
