using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Shared.Events;

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
    private readonly IConfiguration _configuration;

    public FlightCreatedConsumer(
        IEmailService emailService,
        IInboxService inboxService,
        ILogger<FlightCreatedConsumer> logger,
        IConfiguration configuration)
    {
        _emailService = emailService;
        _inboxService = inboxService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task Consume(ConsumeContext<FlightCreatedEvent> context)
    {
        var msg = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();

        _logger.LogInformation(
            "📩 FlightCreated event received. MessageId: {MessageId}, FlightId: {FlightId}, FlightNumber: {FlightNumber}, Route: {Departure} → {Destination}",
            messageId, msg.FlightId, msg.FlightNumber, msg.Departure, msg.Destination);

        // ✅ IDEMPOTENCY MARKER: Önce inbox'a kaydet (email göndermeden ÖNCE)
        // Duplicate kontrolü burada yapılıyor (PK + unique index)
        var businessKey = msg.FlightId.ToString();
        var markedSuccess = await _inboxService.MarkAsProcessedAsync(
            messageId,
            nameof(FlightCreatedEvent),
            businessKey,
            JsonSerializer.Serialize(msg));

        if (!markedSuccess)
        {
            // Duplicate yakalandı (başka consumer zaten işledi/işliyor)
            _logger.LogWarning(
                "⏭️  Duplicate event detected, skipping. FlightId: {FlightId}, MessageId: {MessageId}",
                msg.FlightId, messageId);
            return;
        }

        _logger.LogInformation(
            "✅ Event locked for processing. FlightId: {FlightId}, MessageId: {MessageId}",
            msg.FlightId, messageId);

        try
        {
            // Admin email listesi
            var adminEmails = GetAdminEmails();

            _logger.LogInformation(
                "🔍 DEBUG: Admin email count: {Count}, Emails: {Emails}, FlightId: {FlightId}",
                adminEmails.Count,
                string.Join(", ", adminEmails),
                msg.FlightId);

            if (!adminEmails.Any())
            {
                _logger.LogWarning("No admin emails configured for flight creation notifications.");
                
                // Inbox'a kaydet (email yok ama işlendi sayılır)
                await _inboxService.MarkAsProcessedAsync(
                    messageId,
                    nameof(FlightCreatedEvent),
                    businessKey,
                    JsonSerializer.Serialize(msg));
                return;
            }

            var subject = $"🛫 Yeni Uçuş Eklendi: {msg.FlightNumber}";
            var body = GenerateEmailBody(msg);

            // Her admin'e email gönder
            var emailsSent = 0;
            var emailsFailed = 0;

            foreach (var adminEmail in adminEmails)
            {
                try
                {
                    _logger.LogInformation(
                        "📧 DEBUG: Sending email to {Email} for FlightId: {FlightId}",
                        adminEmail, msg.FlightId);

                    await _emailService.SendEmailAsync(adminEmail, subject, body);
                    emailsSent++;
                    _logger.LogInformation("✅ Flight creation notification sent to {Email}", adminEmail);
                }
                catch (Exception ex)
                {
                    emailsFailed++;
                    _logger.LogError(ex, "Failed to send flight creation notification to {Email}", adminEmail);
                    // Hata olsa bile diğer admin'lere göndermeye devam et
                }
            }

            _logger.LogInformation(
                "Flight creation notifications completed. Sent: {Sent}, Failed: {Failed}, MessageId: {MessageId}",
                emailsSent, emailsFailed, messageId);

            // Not: Inbox'a zaten kayıt yapıldı (idempotency marker olarak)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing FlightCreated event. MessageId: {MessageId}, FlightId: {FlightId}, FlightNumber: {FlightNumber}",
                messageId, msg.FlightId, msg.FlightNumber);

            // ❌ INBOX PATTERN: Hatalı olarak kaydet
            await _inboxService.MarkAsFailedAsync(
                messageId,
                nameof(FlightCreatedEvent),
                businessKey,
                ex.Message,
                JsonSerializer.Serialize(msg));

            // Exception'ı tekrar fırlat (MassTransit retry veya DLQ'ya gönderir)
            throw;
        }
    }

    private List<string> GetAdminEmails()
    {
        // appsettings.json'dan admin email listesini oku
        // Format: "AdminNotificationEmails": ["admin@skysync.com", "operations@skysync.com"]
        var emailsConfig = _configuration.GetSection("AdminNotificationEmails").Get<string[]>();
        
        if (emailsConfig != null && emailsConfig.Any())
        {
            return emailsConfig.ToList();
        }

        // Fallback: Environment variable'dan oku (production için)
        var envEmails = Environment.GetEnvironmentVariable("ADMIN_NOTIFICATION_EMAILS");
        if (!string.IsNullOrEmpty(envEmails))
        {
            return envEmails.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(e => e.Trim())
                           .ToList();
        }

        // Default: Boş liste (loglama için)
        return new List<string>();
    }

    private string GenerateEmailBody(FlightCreatedEvent flight)
    {
        var duration = flight.ArrivalTime - flight.DepartureTime;
        var durationText = $"{(int)duration.TotalHours}s {duration.Minutes}d";

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
                                <strong style='color: #27ae60; font-size: 18px;'>{flight.BasePrice:N2} TL</strong>
                            </td>
                        </tr>
                        <tr style='background-color: #f9f9f9;'>
                            <td style='padding: 15px; border: 1px solid #ddd; font-weight: bold;'>
                                📊 Durum
                            </td>
                            <td style='padding: 15px; border: 1px solid #ddd;'>
                                <span style='background-color: #27ae60; color: white; padding: 5px 15px; border-radius: 20px; font-weight: bold;'>
                                    {flight.Status}
                                </span>
                            </td>
                        </tr>
                        <tr style='background-color: white;'>
                            <td style='padding: 15px; border: 1px solid #ddd; font-weight: bold;'>
                                🆔 Uçuş ID
                            </td>
                            <td style='padding: 15px; border: 1px solid #ddd; font-family: monospace; font-size: 12px;'>
                                {flight.FlightId}
                            </td>
                        </tr>
                        <tr style='background-color: #f9f9f9;'>
                            <td style='padding: 15px; border: 1px solid #ddd; font-weight: bold;'>
                                📅 Oluşturulma
                            </td>
                            <td style='padding: 15px; border: 1px solid #ddd;'>
                                {flight.CreatedAt:dd MMMM yyyy HH:mm:ss}
                            </td>
                        </tr>
                    </table>

                    <div style='margin-top: 30px; padding: 20px; background-color: #e3f2fd; border-left: 4px solid #2196f3; border-radius: 5px;'>
                        <p style='margin: 0; color: #1976d2;'>
                            <strong>ℹ️ Bilgi:</strong> Bu uçuş sisteme eklenmiştir ve rezervasyonlara açıktır.
                        </p>
                    </div>

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
                    </p>
                </div>
            </div>";
    }
}
