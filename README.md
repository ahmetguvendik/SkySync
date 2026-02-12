# SkySync

Uçuş ve rezervasyon akışlarını yöneten, **.NET 9** tabanlı **microservices** mimarisinde bir demo projesidir. Saga, Outbox/Inbox, CQRS ve mesajlaşma (RabbitMQ) pattern'leri kullanılarak tasarlanmıştır; eğitim ve portfolyo amaçlıdır.

---

## İçindekiler

- [Özellikler](#özellikler)
- [Mimari Genel Bakış](#mimari-genel-bakış)
- [Teknoloji Stack](#teknoloji-stack)
- [Proje Yapısı](#proje-yapısı)
- [Servisler](#servisler)
- [Rezervasyon Saga Akışı](#rezervasyon-saga-akışı)
- [Outbox & Inbox](#outbox--inbox)
- [Kurulum ve Çalıştırma](#kurulum-ve-çalıştırma)
- [Ortam Değişkenleri ve Konfigürasyon](#ortam-değişkenleri-ve-konfigürasyon)
- [API Özeti](#api-özeti)
- [Dokümantasyon](#dokümantasyon)
- [Lisans](#lisans)

---

## Özellikler

| Özellik | Açıklama |
|--------|----------|
| **Microservices** | Flight, Reservation, Payment, Notification, Identity + API Gateway (YARP) |
| **Rezervasyon Saga** | Koltuk rezervasyonu → Ödeme → Onay/İptal; başarısızlıkta compensation (ReleaseSeat) |
| **Outbox Pattern** | Event'ler önce DB'de Outbox tablosuna yazılır; worker RabbitMQ'ya publish eder (at-least-once) |
| **Inbox Pattern** | Consumer'larda MessageId ile idempotency; duplicate mesajlar atlanır |
| **CQRS (MediatR)** | Command/Query ayrımı, Request/Response DTO'lar, handler'lar |
| **Cache-Aside (Redis)** | Uçuş listesi cache'lenir; uçuş oluşturulunca cache invalidate edilir |
| **Correlation ID & Transaction ID** | Gateway'de header'lara eklenir; dağıtık istek takibi |
| **Resilience** | Gateway'de timeout, retry (exponential backoff), circuit breaker, rate limiting |

---

## Mimari Genel Bakış

```
                    ┌─────────────────────────────────────────────────────────┐
                    │                    API Gateway (YARP)                    │
                    │         CorrelationId, Rate Limit, Retry, Circuit Breaker  │
                    └───────────────────────────┬─────────────────────────────┘
                                                │
        ┌───────────┬───────────┬───────────┬──┴──┬───────────┬───────────┐
        │           │           │           │     │           │           │
        ▼           ▼           ▼           ▼     ▼           ▼           ▼
   ┌─────────┐ ┌──────────┐ ┌────────┐ ┌──────┐ ┌──────────┐ ┌──────────┐
   │ Flight  │ │Reservation│ │Payment │ │Notif.│ │ Identity │ │  Saga    │
   │ WebApi  │ │  WebApi   │ │ WebApi │ │WebApi│ │  WebApi  │ │  Host    │
   └────┬────┘ └────┬─────┘ └───┬────┘ └──┬───┘ └──────────┘ └────┬─────┘
        │           │            │         │                        │
        │     Outbox│            │         │                        │
        │     ─────┼────────────┼─────────┼────────────────────────┼──────
        │           │            │         │                        │
        ▼           ▼            ▼         ▼                        ▼
   ┌─────────┐ ┌──────────┐ ┌────────┐ ┌──────┐              ┌──────────┐
   │ Flight  │ │Reservation│ │Payment │ │Notif.│              │ RabbitMQ │
   │   DB    │ │    DB     │ │  DB    │ │ DB   │              │ (Events, │
   │ Outbox  │ │  Outbox   │ │ Inbox  │ │Inbox │              │Commands) │
   └────┬────┘ └────┬─────┘ └───┬────┘ └──┬───┘              └────┬─────┘
        │            │            │         │                        │
        └────────────┴────────────┴─────────┴────────────────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    ▼                               ▼
            ┌───────────────┐               (Consumers: Flight,
            │ Outbox Worker │                Payment, Notification,
            │ (Flight +     │                Reservation Saga +
            │  Reservation) │                Status Consumers)
            └───────────────┘
```

- **Gateway:** Tüm HTTP istekleri buradan geçer; routing, correlation ID, resilience uygulanır.
- **Servisler:** Kendi veritabanlarına sahip; birbirleriyle **sadece RabbitMQ** üzerinden event/command ile konuşur.
- **Saga:** Rezervasyon başlatıldığında `ReservationStartedEvent` ile tetiklenir; sırayla koltuk rezervasyonu ve ödeme adımlarını yönetir, hata durumunda compensate eder.
- **Outbox Worker:** Flight ve Reservation DB'deki Outbox tablolarını periyodik okuyup event'leri RabbitMQ'ya publish eder.

---

## Teknoloji Stack

| Bileşen | Teknoloji |
|--------|------------|
| **Runtime** | .NET 9 |
| **API** | ASP.NET Core Web API |
| **Gateway** | YARP (Yet Another Reverse Proxy) |
| **Mesajlaşma** | RabbitMQ, MassTransit |
| **Saga** | MassTransit State Machine, Quartz (timeout) |
| **Veritabanı** | PostgreSQL, Entity Framework Core (Npgsql) |
| **Cache** | Redis (Cache-Aside, distributed lock) |
| **CQRS** | MediatR |
| **Kimlik Doğrulama** | JWT (Identity servisi) |

---

## Proje Yapısı

```
SkySync.sln
└── src/
    ├── Infrastructure/
    │   ├── SkySync.Gateway/                 # API Gateway (YARP, middleware)
    │   ├── SkySync.Shared/                   # Events, Commands, OutboxMessage, InboxMessage
    │   └── SkySync.SagaStateMachine/         # Reservation Saga (MassTransit state machine)
    │
    ├── Services/
    │   ├── Flight/                           # Uçuş & koltuk yönetimi
    │   │   ├── Core/
    │   │   │   ├── Domain/                   # Flight, Seat entity
    │   │   │   └── Application/              # CQRS (CreateFlight, GetAllFlights, GetFlightSeats), DTO, Interfaces
    │   │   ├── Infrastructure/
    │   │   │   ├── Persistence/              # DbContext, Migrations, OutboxRepository, ReserveSeat/ReleaseSeat consumers
    │   │   │   └── *Infrastructure/         # Redis, external services
    │   │   └── Presentation/WebApi/          # FlightController
    │   │
    │   ├── Reservation/                      # Rezervasyon & Saga tetikleyici
    │   │   ├── Core/ (Domain, Application - CQRS, Outbox)
    │   │   ├── Infrastructure/ (Persistence: DbContext, Inbox, Saga + status consumers)
    │   │   └── Presentation/WebApi/
    │   │
    │   ├── Payment/                          # Ödeme simülasyonu
    │   │   ├── Core/ (Domain, Application)
    │   │   ├── Infrastructure/ (Persistence: ProcessPaymentConsumer, Inbox)
    │   │   └── Presentation/WebApi/
    │   │
    │   ├── Notification/                      # E-posta bildirimleri
    │   │   ├── Core/ (Domain, Application)
    │   │   ├── Infrastructure/ (FlightCreated, ReservationConfirmed consumers; transactional inbox)
    │   │   └── Presentation/WebApi/
    │   │
    │   └── Identity/                         # Auth (Register, Login, GetProfile)
    │       ├── Core/ (Domain, Application)
    │       ├── Infrastructure/ (Persistence, JWT)
    │       └── Presentation/WebApi/
    │
    └── Workers/
        └── SkySync.Workers.Outbox/           # Outbox publish worker
            ├── Jobs/
            │   ├── Common/                   # MessageProcessResult, OutboxWorkerConstants, OutboxPublishHelper
            │   ├── Flight/                   # FlightOutboxPublishWorker
            │   └── Reservation/              # ReservationOutboxPublishWorker
            └── Program.cs
```

---

## Servisler

| Servis | Açıklama | Öne çıkanlar |
|--------|----------|---------------|
| **Flight** | Uçuş CRUD, koltuk listesi, koltuk rezervasyonu/serbest bırakma | Outbox (FlightCreatedEvent), Cache-Aside (GetAllFlights), ReserveSeat/ReleaseSeat consumer |
| **Reservation** | Rezervasyon oluşturma, yolcu rezervasyonları listesi, saga tetikleme | Outbox (ReservationStartedEvent), Saga + 4 status consumer (Confirmed, SeatFailed, PaymentFailed, TimedOut) |
| **Payment** | Ödeme işleme (simülasyon) | ProcessPaymentConsumer, Inbox, PaymentAuthorizedEvent / PaymentFailedEvent |
| **Notification** | E-posta bildirimleri | FlightCreatedConsumer, ReservationConfirmedConsumer, transactional inbox |
| **Identity** | Kayıt, giriş, profil | JWT, CQRS (Register, Login, GetProfile) |
| **Gateway** | Tek giriş noktası | YARP routing, CorrelationId/TransactionId, retry, circuit breaker, rate limiting |
| **Saga (State Machine)** | Rezervasyon orkestrasyonu | ReserveSeat → ProcessPayment → Confirmed/Failed/Timeout; 5 dk ödeme timeout, compensation |

---

## Rezervasyon Saga Akışı

Saga **orkestrasyon (orchestration)** modeliyle çalışır: merkezi state machine komutları servislere gönderir (`ReserveSeatCommand`, `ProcessPaymentCommand`, `ReleaseSeatCommand`), event'lere göre state'i günceller ve hata durumunda compensation tetikler.

1. **Rezervasyon oluşturulur** (POST /api/reservation) → Reservation DB + Outbox'a `ReservationStartedEvent` yazılır.
2. **Outbox Worker** event'i RabbitMQ'ya publish eder.
3. **Saga** `ReservationStartedEvent` alır → **ReserveSeatCommand** Flight servisine gönderilir; state: `AwaitingFlightReservation`.
4. **Flight** koltuk rezerve eder veya başarısız döner:
   - **FlightReservedEvent** → Saga `ProcessPaymentCommand` Payment'a gönderir, 5 dk timeout schedule eder; state: `AwaitingPayment`.
   - **FlightReservationFailedEvent** → Saga finalize; Reservation status consumer rezervasyonu Failed yapar.
5. **Payment** ödeme dener:
   - Başarı: `PaymentAuthorizedEvent` (veya `PaymentCompletedEvent` uyumluluğa göre).
   - Başarısız: `PaymentFailedEvent` → Saga `ReleaseSeatCommand` gönderir (compensate), finalize.
6. **AwaitingPayment** içinde:
   - **PaymentCompletedEvent** → Saga `ReservationConfirmedEvent` publish eder (Notification + Reservation status); finalize.
   - **PaymentTimeout** (5 dk) → Saga `ReservationTimedOutEvent` + `ReleaseSeatCommand`; finalize.

Detaylı state diyagramı ve mesaj tabloları: [EVENTS_CONSUMERS_SAGA_DOKUMAN.md](EVENTS_CONSUMERS_SAGA_DOKUMAN.md).

---

## Outbox & Inbox

### Outbox

- **Amaç:** Event'leri veritabanı transaction'ı ile birlikte yazmak; publish ayrı bir worker'da yapılır. Böylece DB commit olduktan sonra broker hatası olsa bile event kaybolmaz.
- **Kullanım:** Flight (FlightCreatedEvent), Reservation (ReservationStartedEvent vb.).
- **Worker:** `SkySync.Workers.Outbox` — Flight ve Reservation DB'deki `OutboxMessages` tablolarını okur, batch halinde RabbitMQ'ya publish eder. Polymorphic publish (Type + Content), retry (max 5), MessageId = OutboxMessage.Id (idempotency).

### Inbox

- **Amaç:** Aynı mesajın (MessageId) birden fazla işlenmesini engellemek.
- **Kullanım:** Tüm consumer'larda (Flight ReserveSeat/ReleaseSeat, Payment ProcessPayment, Notification, Reservation status consumer'lar). Notification'da transactional inbox kullanılır.

Detay: [OUTBOX_INBOX_NOTLAR.md](OUTBOX_INBOX_NOTLAR.md).

---

## Kurulum ve Çalıştırma

### Gereksinimler

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- PostgreSQL
- RabbitMQ (yerel veya [CloudAMQP](https://www.cloudamqp.com/) vb.)
- Redis (uygulama listesi cache için; isteğe bağlı — cache yoksa DB'den okunur)

### Adımlar

1. **Repoyu klonlayın**
   ```bash
   git clone https://github.com/ahmetguvendik/SkySync.git
   cd SkySync
   ```

2. **Connection string ve RabbitMQ ayarlarını güncelleyin**  
   Her servisin ve Outbox Worker'ın `appsettings.json` / `appsettings.Development.json` dosyalarında:
   - `ConnectionStrings:DefaultConnection` (veya ilgili DbContext adı)
   - `RabbitMQ:ConnectionString` veya `RabbitMQ:Host`, `Username`, `Password`, `VHost`
   - İsteğe bağlı: `Redis` connection

3. **Migration'ları uygulayın**  
   Her servisin Persistence projesinde:
   ```bash
   dotnet ef database update --project src/Services/Flight/Infrastructure/SkySync.Services.Flight.Persistence --startup-project src/Services/Flight/Presentation/SkySync.Services.Flight.WebApi
   dotnet ef database update --project src/Services/Reservation/Infrastructure/SkySync.Services.Reservation.Persistence --startup-project src/Services/Reservation/Presentation/SkySync.Services.Reservation.WebApi
   # Payment, Notification, Identity için benzer şekilde
   ```

4. **Çalıştırma sırası (örnek)**  
   - RabbitMQ çalışır olmalı.
   - Gateway: `src/Infrastructure/SkySync.Gateway`
   - Flight, Reservation, Payment, Notification, Identity WebApi'ler (portlar appsettings/launchSettings'ta).
   - Outbox Worker: `src/Workers/SkySync.Workers.Outbox`
   - Saga: MassTransit state machine genelde Reservation veya ayrı bir host içinde konfigüre edilir; dokümana göre saga queue'yu dinleyen host'u ayağa kaldırın.

5. **Çözümü derleyin**
   ```bash
   dotnet build SkySync.sln
   ```

---

## Ortam Değişkenleri ve Konfigürasyon

| Ayar | Açıklama | Örnek |
|------|----------|--------|
| **RabbitMQ:ConnectionString** | Tek connection string (CloudAMQP vb.) | `amqps://user:pass@host/vhost` |
| **RabbitMQ:Host / Username / Password / VHost** | Yerel RabbitMQ için ayrı ayrı | `localhost`, `guest`, `guest`, `/` |
| **Redis** | Cache için (Flight listesi) | `localhost:6379` |

Gateway'de routes, rate limit, retry, circuit breaker değerleri `appsettings.json` içinde tanımlıdır.

---

## API Özeti

| Servis | Method | Endpoint | Açıklama |
|--------|--------|----------|----------|
| Gateway | GET | `/`, `/health` | Bilgi ve health check |
| Flight | POST | `/api/flight` | Uçuş oluştur (Command, Outbox) |
| Flight | GET | `/api/flight` | Tüm uçuşlar (Query, Cache-Aside) |
| Flight | GET | `/api/flight/{id}/seats` | Uçuş koltukları (Query) |
| Flight | GET | `/api/airports` | Havalimanlarını listele (Query) |
| Flight | POST | `/api/airports` | Yeni havalimanı ekle (Command) |
| Reservation | POST | `/api/reservation` | Rezervasyon oluştur (Saga tetiklenir) |
| Reservation | GET | `/api/reservation/passenger/{email}` | Yolcu rezervasyonları (Query) |
| Identity | POST | `/api/auth/register`, `/api/auth/login` | Kayıt, giriş |
| Identity | GET | `/api/auth/profile` | Profil (Query) |

Tüm API'ler Gateway üzerinden (`http://localhost:5000` veya konfigüre edilen port) erişilir. Detaylı request/response örnekleri: [API_ENDPOINTS.md](API_ENDPOINTS.md).

---

## Dokümantasyon

| Dosya | İçerik |
|-------|--------|
| [EVENTS_CONSUMERS_SAGA_DOKUMAN.md](EVENTS_CONSUMERS_SAGA_DOKUMAN.md) | Event'ler, Command'lar, Consumer'lar, Saga state machine, kuyruklar |
| [OUTBOX_INBOX_NOTLAR.md](OUTBOX_INBOX_NOTLAR.md) | Outbox/Inbox pattern, worker mantığı, IInboxService |
| [CORRELATION_AND_TRANSACTION_IDS.md](CORRELATION_AND_TRANSACTION_IDS.md) | Correlation ID & Transaction ID kullanımı |
| [API_ENDPOINTS.md](API_ENDPOINTS.md) | API endpoint listesi, örnek istekler |
| [README_CORRELATION_TRANSACTION_IDS.md](README_CORRELATION_TRANSACTION_IDS.md) | Correlation/Transaction ID implementasyon özeti |

---

## Lisans

Bu proje eğitim ve portfolyo amaçlıdır; ticari kullanım için ek lisans/koşullar uygulanabilir.
