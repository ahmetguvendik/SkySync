using System.Threading.Channels;
using FluentAssertions;
using MassTransit;
using MassTransit.Saga;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using SkySync.SagaStateMachine.StateInstances;
using SkySync.SagaStateMachine.StateMachines;
using SkySync.Shared;
using SkySync.Shared.Commands;
using SkySync.Shared.Events;
using Xunit;

namespace SkySync.SagaStateMachine.Tests;

public class ReservationStateMachineTests : IAsyncLifetime
{
    private readonly Channel<ReserveSeatCommand> _reserveSeatChannel = Channel.CreateUnbounded<ReserveSeatCommand>();
    private readonly Channel<ReleaseSeatCommand> _releaseSeatChannel = Channel.CreateUnbounded<ReleaseSeatCommand>();
    private readonly Channel<ReservationConfirmedEvent> _reservationConfirmedChannel = Channel.CreateUnbounded<ReservationConfirmedEvent>();
    private readonly Channel<ReservationTimedOutEvent> _reservationTimedOutChannel = Channel.CreateUnbounded<ReservationTimedOutEvent>();

    private ReservationStateMachine _stateMachine = null!;
    private InMemorySagaRepository<ReservationState> _repository = null!;
    private IBusControl _bus = null!;

    public async Task InitializeAsync()
    {
        var configuration = new TestConfiguration(new Dictionary<string, string?>
        {
            ["Payment:TimeoutMinutes"] = "1"
        });

        _stateMachine = new ReservationStateMachine(configuration, NullLogger<ReservationStateMachine>.Instance);
        _repository = new InMemorySagaRepository<ReservationState>();

        _bus = MassTransit.Bus.Factory.CreateUsingInMemory(cfg =>
        {
            cfg.UseDelayedMessageScheduler();

            cfg.ReceiveEndpoint(RabbitMqSettings.ReservationSagaQueue, e =>
            {
                e.StateMachineSaga(_stateMachine, _repository);
            });

            cfg.ReceiveEndpoint(RabbitMqSettings.FlightReserveSeatQueue, e =>
            {
                e.Handler<ReserveSeatCommand>(context =>
                {
                    _reserveSeatChannel.Writer.TryWrite(context.Message);
                    return Task.CompletedTask;
                });
            });

            cfg.ReceiveEndpoint(RabbitMqSettings.FlightReleaseSeatQueue, e =>
            {
                e.Handler<ReleaseSeatCommand>(context =>
                {
                    _releaseSeatChannel.Writer.TryWrite(context.Message);
                    return Task.CompletedTask;
                });
            });

            cfg.ReceiveEndpoint("reservation-confirmed-test", e =>
            {
                e.Handler<ReservationConfirmedEvent>(context =>
                {
                    _reservationConfirmedChannel.Writer.TryWrite(context.Message);
                    return Task.CompletedTask;
                });
            });

            cfg.ReceiveEndpoint("reservation-timeout-test", e =>
            {
                e.Handler<ReservationTimedOutEvent>(context =>
                {
                    _reservationTimedOutChannel.Writer.TryWrite(context.Message);
                    return Task.CompletedTask;
                });
            });
        });

        await _bus.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_bus != null)
            await _bus.StopAsync();
    }

    [Fact]
    public async Task ReservationStarted_CreatesSagaAndSendsReserveSeatCommand()
    {
        var correlationId = NewId.NextGuid();
        var started = CreateReservationStartedEvent(correlationId);

        await _bus.Publish(started);

        var reserveCommand = await ReadChannelAsync(_reserveSeatChannel);
        reserveCommand.CorrelationId.Should().Be(correlationId);
        reserveCommand.FlightId.Should().Be(started.FlightId);
        reserveCommand.SeatNumber.Should().Be(started.SeatNumber);

        var sagaState = await _repository.Load(correlationId);
        sagaState.Should().NotBeNull();
        sagaState!.CurrentState.Should().Be(_stateMachine.AwaitingFlightReservation.Name);
        sagaState.PassengerEmail.Should().Be(started.PassengerEmail);
    }

    [Fact]
    public async Task PaymentCompleted_PublishesReservationConfirmedEvent()
    {
        var correlationId = NewId.NextGuid();
        var started = await StartReservationAsync(correlationId);
        await _bus.Publish(CreateFlightReservedEvent(correlationId, started));

        var paymentEvent = new PaymentCompletedEvent
        {
            CorrelationId = correlationId,
            ReservationId = started.ReservationId,
            Amount = started.Price,
            PaymentMethod = "CreditCard",
            TransactionId = "txn-42",
            CompletedAt = DateTime.UtcNow
        };

        await _bus.Publish(paymentEvent);

        var confirmed = await ReadChannelAsync(_reservationConfirmedChannel);
        confirmed.ReservationId.Should().Be(started.ReservationId);
        confirmed.SeatNumber.Should().Be(started.SeatNumber);
        confirmed.PassengerEmail.Should().Be(started.PassengerEmail);

        var sagaState = await _repository.Load(correlationId);
        sagaState.Should().BeNull("saga must finalize after confirmation");
    }

    [Fact]
    public async Task PaymentFailed_ReleasesSeatAndFinalizesSaga()
    {
        var correlationId = NewId.NextGuid();
        var started = await StartReservationAsync(correlationId);
        await _bus.Publish(CreateFlightReservedEvent(correlationId, started));

        await _bus.Publish(new PaymentFailedEvent
        {
            CorrelationId = correlationId,
            ReservationId = started.ReservationId,
            Amount = started.Price,
            ErrorMessage = "Declined",
            FailedAt = DateTime.UtcNow
        });

        var releaseCommand = await ReadChannelAsync(_releaseSeatChannel);
        releaseCommand.CorrelationId.Should().Be(correlationId);
        releaseCommand.SeatNumber.Should().Be(started.SeatNumber);

        var sagaState = await _repository.Load(correlationId);
        sagaState.Should().BeNull("saga must finalize after release");
    }

    [Fact]
    public async Task PaymentTimeout_PublishesTimeoutEventAndCompensatesSeat()
    {
        var correlationId = NewId.NextGuid();
        var started = await StartReservationAsync(correlationId);
        await _bus.Publish(CreateFlightReservedEvent(correlationId, started));

        await _bus.Publish(new PaymentTimeoutEvent
        {
            CorrelationId = correlationId,
            ReservationId = started.ReservationId,
            Amount = started.Price,
            TimeoutAt = DateTime.UtcNow,
            Reason = "Test timeout"
        });

        var timeoutEvent = await ReadChannelAsync(_reservationTimedOutChannel);
        timeoutEvent.ReservationId.Should().Be(started.ReservationId);
        timeoutEvent.CorrelationId.Should().Be(correlationId);

        var releaseCommand = await ReadChannelAsync(_releaseSeatChannel);
        releaseCommand.CorrelationId.Should().Be(correlationId);

        var sagaState = await _repository.Load(correlationId);
        sagaState.Should().BeNull("timeout should complete the saga");
    }

    private async Task<ReservationStartedEvent> StartReservationAsync(Guid correlationId)
    {
        var started = CreateReservationStartedEvent(correlationId);
        await _bus.Publish(started);
        await ReadChannelAsync(_reserveSeatChannel); // ensure initial command triggered
        return started;
    }

    private static ReservationStartedEvent CreateReservationStartedEvent(Guid correlationId) =>
        new()
        {
            CorrelationId = correlationId,
            ReservationId = Guid.NewGuid(),
            FlightId = Guid.NewGuid(),
            SeatNumber = "12A",
            Price = 199.99m,
            PassengerName = "Ada",
            PassengerSurname = "Lovelace",
            PassengerEmail = "ada@example.com",
            CreatedAt = DateTime.UtcNow
        };

    private static FlightReservedEvent CreateFlightReservedEvent(Guid correlationId, ReservationStartedEvent started) =>
        new()
        {
            CorrelationId = correlationId,
            FlightId = started.FlightId,
            SeatNumber = started.SeatNumber,
            ReservedAt = DateTime.UtcNow,
            FlightNumber = "SKY-100",
            Departure = "IST",
            Destination = "LHR",
            IsSuccess = true
        };

    private static async Task<T> ReadChannelAsync<T>(Channel<T> channel)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await channel.Reader.ReadAsync(cts.Token);
    }
}

internal sealed class TestConfiguration : IConfiguration
{
    private readonly Dictionary<string, string?> _values;

    public TestConfiguration(IDictionary<string, string?> values)
    {
        _values = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
    }

    public string? this[string key]
    {
        get => _values.TryGetValue(key, out var value) ? value : null;
        set => _values[key] = value;
    }

    public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();

    public IChangeToken GetReloadToken() => NoopChangeToken.Instance;

    public IConfigurationSection GetSection(string key) => new TestConfigurationSection(key, this);

    private sealed class TestConfigurationSection : IConfigurationSection
    {
        private readonly string _key;
        private readonly TestConfiguration _parent;

        public TestConfigurationSection(string key, TestConfiguration parent)
        {
            _key = key;
            _parent = parent;
        }

        public string Key => _key;

        public string Path => _key;

        public string? Value
        {
            get => _parent[_key];
            set => _parent[_key] = value;
        }

        public string? this[string key]
        {
            get => _parent[key];
            set => _parent[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();

        public IChangeToken GetReloadToken() => NoopChangeToken.Instance;

        public IConfigurationSection GetSection(string key) => new TestConfigurationSection(key, _parent);
    }

    private sealed class NoopChangeToken : IChangeToken
    {
        public static readonly IChangeToken Instance = new NoopChangeToken();

        public bool HasChanged => false;

        public bool ActiveChangeCallbacks => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => Disposable.Instance;

        private sealed class Disposable : IDisposable
        {
            public static readonly Disposable Instance = new Disposable();
            public void Dispose()
            {
            }
        }
    }
}
