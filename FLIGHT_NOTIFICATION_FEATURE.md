# 🆕 Flight Created Notification Feature

## 📋 Özellik Özeti

Yeni bir uçuş eklendiğinde admin/operasyon ekibine otomatik email bildirimi gönderilir.

---

## 🎯 Nasıl Çalışır?

### Akış Diagramı

```
1. Admin → POST /api/flight (CreateFlight)
       ↓
2. Flight Service → DB'ye kaydet + FlightCreatedEvent publish
       ↓
3. RabbitMQ → Event'i queue'ya at
       ↓
4. Notification Service → FlightCreatedConsumer event'i dinler
       ↓
5. Email Service → Admin listesine email gönder
```

### Event Flow

```
Flight.Service
  └─ CreateFlightCommandHandler
      ├─ Flight kaydet
      ├─ OutboxMessage kaydet (FlightCreatedEvent)
      └─ Worker → RabbitMQ'ya publish

Notification.Service
  └─ FlightCreatedConsumer
      ├─ FlightCreatedEvent dinle
      ├─ Admin email listesini al (appsettings.json)
      ├─ Email şablonu hazırla
      └─ Her admin'e email gönder
```

---

## 📂 Eklenen/Değiştirilen Dosyalar

### 1. Yeni Consumer
```
📁 src/Services/Notification/Infrastructure/SkySync.Services.Notification.Persistence/Consumers/
   └─ FlightCreatedConsumer.cs (YENİ)
```

**Özellikler:**
- ✅ FlightCreatedEvent dinler
- ✅ Admin email listesini configuration'dan okur
- ✅ Güzel HTML email şablonu
- ✅ Hata handling (bir admin'e gönderilemezse diğerlerine devam)
- ✅ Logging (her adımda log)

### 2. MassTransit Registration
```
📁 ServiceRegistration.cs (GÜNCELLENDİ)
   ├─ FlightCreatedConsumer register edildi
   └─ "notification-flight-created-queue" endpoint eklendi
```

### 3. Configuration
```
📁 appsettings.json (GÜNCELLENDİ)
   └─ AdminNotificationEmails: ["admin@skysync.com", "operations@skysync.com", ...]

📁 appsettings.Development.json (GÜNCELLENDİ)
   └─ AdminNotificationEmails: ["ahmetguvendik011348@gmail.com"]
```

---

## ⚙️ Konfigürasyon

### appsettings.json

```json
{
  "AdminNotificationEmails": [
    "admin@skysync.com",
    "operations@skysync.com",
    "manager@skysync.com"
  ]
}
```

### Environment Variable (Production)

```bash
export ADMIN_NOTIFICATION_EMAILS="admin@prod.com,ops@prod.com"
```

---

## 📧 Email Şablonu Önizlemesi

### Konu
```
🛫 Yeni Uçuş Eklendi: TK1903
```

### İçerik
```
┌──────────────────────────────────┐
│     ✈️ Yeni Uçuş Eklendi         │
└──────────────────────────────────┘

Uçuş Detayları
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🛫 Uçuş Numarası:  TK1903
📍 Kalkış:         Istanbul
📍 Varış:          Ankara
🕐 Kalkış Zamanı:  26 Ocak 2026 10:00
🕐 Varış Zamanı:   26 Ocak 2026 11:30
⏱️ Uçuş Süresi:    1s 30d
💰 Baz Fiyat:      500.00 TL
📊 Durum:          Active
🆔 Uçuş ID:        abc-123-def-456
📅 Oluşturulma:    26 Ocak 2026 09:00:00

ℹ️ Bu uçuş sisteme eklenmiştir ve rezervasyonlara açıktır.

[🔍 Uçuş Detaylarını Görüntüle]

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
SkySync Operations Team
Bu otomatik bir bildirimdir.
```

---

## 🧪 Test Senaryosu

### 1. Yeni Uçuş Ekle

```bash
POST http://localhost:5000/api/flight
Content-Type: application/json

{
  "flightNumber": "TK1903",
  "departure": "Istanbul",
  "destination": "Ankara",
  "departureTime": "2026-01-26T10:00:00Z",
  "arrivalTime": "2026-01-26T11:30:00Z",
  "basePrice": 500.00,
  "status": "Active"
}
```

### 2. Beklenen Sonuç

✅ Flight Service:
- Uçuş DB'ye kaydedilir
- FlightCreatedEvent OutboxMessage'a yazılır
- Worker event'i RabbitMQ'ya publish eder

✅ RabbitMQ:
- Event "notification-flight-created-queue" kuyruğuna düşer

✅ Notification Service:
- FlightCreatedConsumer event'i consume eder
- Log: "FlightCreated event received. Sending notification email. FlightNumber: TK1903"

✅ Email:
- appsettings.json'daki her admin'e email gönderilir
- Log: "Flight creation notification sent to ahmetguvendik011348@gmail.com"

### 3. Kontrol Noktaları

```bash
# 1. Flight Service logs kontrol et
# "FlightCreatedEvent published to Outbox"

# 2. Notification Service logs kontrol et
# "FlightCreated event received. Sending notification email..."
# "Flight creation notification sent to ..."

# 3. Email inbox kontrol et
# Yeni uçuş bildirimi mail'i gelmiş olmalı
```

---

## 🔧 Troubleshooting

### Problem: Email gelmiyor

**Kontroller:**
1. ✅ Admin email listesi doğru mu? (appsettings.json)
2. ✅ Notification Service çalışıyor mu?
3. ✅ RabbitMQ bağlantısı var mı?
4. ✅ Gmail SMTP ayarları doğru mu?

**Logları kontrol et:**
```bash
# Notification Service logs
docker logs notification-service

# Aramak için:
grep "FlightCreated event received" logs.txt
grep "notification sent to" logs.txt
```

### Problem: Sadece bazı admin'lere gidiyor

**Açıklama:** 
Bir admin'e email gönderilirken hata olsa bile diğerlerine devam eder.

**Çözüm:**
Logs'ta hangi email'e gönderilemediğini kontrol et:
```
ERROR: Failed to send flight creation notification to invalid@email.com
```

### Problem: Duplicate email geliyor

**Açıklama:**
Eğer Worker birden fazla kez çalışırsa veya event retry olursa.

**Çözüm:**
⚠️ Şu an idempotency yok. Gerekirse eklenebilir:
```csharp
// FlightCreatedConsumer'da:
var alreadySent = await _cache.GetAsync($"flight-notif-sent:{msg.FlightId}");
if (alreadySent != null) return; // Skip

// Email gönder...

await _cache.SetAsync($"flight-notif-sent:{msg.FlightId}", "true", TimeSpan.FromHours(24));
```

---

## 🚀 Production Deployment

### 1. Environment Variables

```bash
# Production'da appsettings.json yerine env var kullan
export ADMIN_NOTIFICATION_EMAILS="admin@prod.skysync.com,ops@prod.skysync.com"
```

### 2. Email Template Customization

Email şablonunu özelleştirmek için:
```csharp
// FlightCreatedConsumer.cs → GenerateEmailBody() metodunu düzenle
```

### 3. Monitoring

```bash
# Email gönderim metriği ekle (Prometheus)
_metrics.IncrementFlightNotificationSent();

# Alert kuralı ekle (Grafana)
# "Son 1 saatte hiç flight notification gönderilmedi" → Alert
```

---

## 📊 İstatistikler & Metrics (Gelecek)

Eklenebilir:
```
✅ Kaç admin'e email gönderildi
✅ Email gönderim süresi
✅ Hata oranı (failed email count)
✅ Son 24 saatte kaç yeni uçuş eklendi
```

---

## 🎯 Gelecek İyileştirmeler

### 1. Email Template Engine
```csharp
// Razor Pages veya Liquid template kullan
var html = await _templateEngine.RenderAsync("FlightCreated.cshtml", flight);
```

### 2. Multi-Channel Notification
```
✅ Email (şu an var)
✅ SMS (Twilio)
✅ Slack webhook
✅ Microsoft Teams webhook
✅ Push notification (mobile app)
```

### 3. Notification Preferences
```csharp
// Admin'ler hangi bildirimleri almak istiyor?
{
  "admin@skysync.com": {
    "FlightCreated": true,
    "FlightCancelled": true,
    "PaymentFailed": false
  }
}
```

### 4. Batch Notifications
```
Çok sayıda uçuş eklenirse (bulk import):
→ Her uçuş için ayrı mail yerine
→ "10 yeni uçuş eklendi" özet mail gönder
```

### 5. Idempotency
```
Duplicate event gelirse tekrar email gönderme
→ Redis cache ile track et
```

---

## 📝 Notlar

- ✅ Event-driven architecture sayesinde Flight Service'in Notification Service'den haberi yok
- ✅ Loosely coupled: Notification service çökse bile uçuş eklenir
- ✅ Scalable: Email gönderimi async, non-blocking
- ✅ Extensible: Yeni consumer'lar kolayca eklenebilir

---

**Tarih:** 26 Ocak 2026  
**Feature Status:** ✅ Completed  
**Test Status:** ⏳ Pending (Manuel test gerekiyor)
