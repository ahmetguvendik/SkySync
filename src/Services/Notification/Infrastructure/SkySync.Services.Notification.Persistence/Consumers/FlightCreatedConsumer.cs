using System.Globalization;
using System.Linq;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Services.Notification.Domain.Entities;
using SkySync.Shared.Events;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Notification.Persistence.Consumers;

/// <summary>
/// FlightCreatedEvent consumer - Yeni uçuş eklendiğinde admin/operasyon ekibine bildirim gönderir
/// Inbox Pattern ile duplicate event handling
/// </summary>
public class FlightCreatedConsumer : IConsumer<FlightCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly IInboxService _inboxService;
    private readonly ILogger<FlightCreatedConsumer> _logger;
    private readonly INotificationUserRepository _notificationUserRepository;
    private readonly string _unsubscribeBaseUrl;

    public FlightCreatedConsumer(
        IEmailService emailService,
        IInboxService inboxService,
        ILogger<FlightCreatedConsumer> logger,
        IConfiguration configuration,
        INotificationUserRepository notificationUserRepository)
    {
        _emailService = emailService;
        _inboxService = inboxService;
        _logger = logger;
        _notificationUserRepository = notificationUserRepository;
        _unsubscribeBaseUrl = configuration["NotificationSettings:UnsubscribeBaseUrl"]
            ?? "https://localhost:7000/api/v1/notification/preferences/unsubscribe";
    }

    public async Task Consume(ConsumeContext<FlightCreatedEvent> context)
    {
        var msg = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();
        var businessKey = msg.FlightId.ToString();

        _logger.LogInformation(
            "FlightCreated event received. MessageId: {MessageId}, FlightId: {FlightId}, FlightNumber: {FlightNumber}, Route: {Departure} to {Destination}",
            messageId, msg.FlightId, msg.FlightNumber, msg.Departure, msg.Destination);

        var processed = await _inboxService.TryProcessInTransactionAsync(
            messageId,
            nameof(FlightCreatedEvent),
            businessKey,
            JsonSerializer.Serialize(msg),
            async ct => await SendFlightCreatedEmailsAsync(msg, ct),
            context.CancellationToken);

        if (!processed)
        {
            _logger.LogWarning("Duplicate FlightCreated skipped. FlightId: {FlightId}, MessageId: {MessageId}", msg.FlightId, messageId);
        }
    }

    private async Task SendFlightCreatedEmailsAsync(FlightCreatedEvent msg, CancellationToken ct)
    {
        var recipients = await GetOperationalRecipientsAsync(ct);
        _logger.LogInformation("Operational contact count: {Count}, FlightId: {FlightId}", recipients.Count, msg.FlightId);

        if (!recipients.Any())
        {
            _logger.LogWarning("No operational contacts found for flight creation notifications.");
            return;
        }

        var subject = $"🛫 Yeni Uçuş Eklendi: {msg.FlightNumber}";

        foreach (var recipient in recipients)
        {
            ct.ThrowIfCancellationRequested();
            var body = GenerateEmailBody(msg, BuildUnsubscribeLink(recipient.UnsubscribeToken));
            await _emailService.SendEmailAsync(recipient.Email, subject, body);
            _logger.LogInformation("Flight creation notification sent to {Email}", recipient.Email);
        }
    }

    private async Task<IReadOnlyList<NotificationUser>> GetOperationalRecipientsAsync(CancellationToken cancellationToken)
    {
        return await _notificationUserRepository.GetOperationalContactsAsync(cancellationToken);
    }

    private string BuildUnsubscribeLink(Guid token)
    {
        var baseUrl = _unsubscribeBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{token}";
    }

    private string GenerateEmailBody(FlightCreatedEvent flight, string unsubscribeLink)
    {
        var duration = flight.ArrivalTime - flight.DepartureTime;
        var durationText = $"{(int)duration.TotalHours}s {duration.Minutes}d";
        var priceText = flight.BasePrice.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));

        return $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; text-align: center;'>
                    <h1 style='color: white; margin: 0;'>✈️ Yeni Uçuş Eklendi</h1>
                </div>
                
                <div style='padding: 30px; background-color: #f9f9f9;'>
                    <h2 style='color: #333; border-bottom: 2px solid #667eea; padding-bottom: 10px;'>
                        Uçuş Detayları
                    </h2>
                    
                    <table style='width: 100%; border-collapse: collapse; margin-top: 20px;'>
                        <tr style='background-color: white;'>
                            <td style='padding: 15px; border: 1px solid #ddd; font-weight: bold; width: 40%;'>
                                🛫 Uçuş Numarası
                            </td>
                            <td style='padding: 15px; border: 1px solid #ddd;'>
                                <strong style='color: #667eea; font-size: 18px;'>{flight.FlightNumber}</strong>
                            </td>
                        </tr>
                        <tr style='background-color: #f9f9f9;'>
                            <td style='padding: 15px; border: 1px solid #ddd; font-weight: bold;'>
                                📍 Kalkış
                            </td>
                            <td style='padding: 15px; border: 1px solid #ddd;'>
                                {flight.Departure}
                            </td>
                        </tr>
                        <tr style='background-color: white;'>
                            <td style='padding: 15px; border: 1px solid #ddd; font-weight: bold;'>
                                📍 Varış
                            </td>
                            <td style='padding: 15px; border: 1px solid #ddd;'>
                                {flight.Destination}
                            </td>
                        </tr>
                        <tr style='background-color: #f9f9f9;'>
                            <td style='padding: 15px; border: 1px solid #ddd; font-weight: bold;'>
                                🕐 Kalkış Zamanı
                            </td>
                            <td style='padding: 15px; border: 1px solid #ddd;'>
                                {flight.DepartureTime:dd MMMM yyyy HH:mm}
                            </td>
                        </tr>
                        <tr style='background-color: white;'>
                            <td style='padding: 15px; border: 1px solid #ddd; font-weight: bold;'>
                                🕐 Varış Zamanı
                            </td>
                            <td style='padding: 15px; border: 1px solid #ddd;'>
                                {flight.ArrivalTime:dd MMMM yyyy HH:mm}
                            </td>
                        </tr>
                        <tr style='background-color: #f9f9f9;'>
                            <td style='padding: 15px; border: 1px solid #ddd; font-weight: bold;'>
                                ⏱️ Uçuş Süresi
                            </td>
                            <td style='padding: 15px; border: 1px solid #ddd;'>
                                {durationText}
                            </td>
                        </tr>
                        <tr style='background-color: white;'>
                            <td style='padding: 15px; border: 1px solid #ddd; font-weight: bold;'>
                                💰 Baz Fiyat
                            </td>
                            <td style='padding: 15px; border: 1px solid #ddd;'>
                                <strong style='color: #27ae60; font-size: 18px;'>{priceText} TL</strong>
                            </td>
                        </tr>
                    </table>

                    <div style='margin-top: 20px; text-align: center;'>
                        <a href='http://localhost:5000/api/flight/{flight.FlightId}' 
                           style='display: inline-block; padding: 12px 30px; background-color: #667eea; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                            🔍 Uçuş Detaylarını Görüntüle
                        </a>
                    </div>
                </div>

                <div style='padding: 20px; text-align: center; background-color: #333; color: white;'>
                    <p style='margin: 0;'>
                        <em>SkySync Operations Team</em>
                    </p>
                    <p style='margin: 5px 0 0 0; font-size: 12px; color: #999;'>
                        Bu otomatik bir bildirimdir.
                        Bildirim almak istemiyorsanız 
                        <a href='{unsubscribeLink}' style='color:#fff;text-decoration:underline;'>buraya tıklayın</a>.
                    </p>
                </div>
            </div>";
    }
}
