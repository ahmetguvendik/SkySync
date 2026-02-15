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
- [Detaylı API Endpointleri](#skysync-api-endpoints)
- [Eventler, Consumerlar ve Saga](#skysync--eventler-consumerlar-ve-saga--detaylı-doküman)
- [Outbox, Outbox Worker ve Inbox](#outbox-outbox-worker-ve-inbox--detaylı-notlar)
- [Correlation & Transaction ID](#-correlation-id--transaction-id---skysync-implementation)
- [Correlation ID Test Guide](#-correlation-id--transaction-id---test-guide)
- [Correlation ID & Transaction ID – Implementation Complete](#-correlation-id--transaction-id---implementation-complete)
- [Flight Created Notification Feature](#-flight-created-notification-feature)
- [Serilog Yol Haritası](#-serilog-ile-loglama--yol-haritası-5-servis)
- [Gereksiz / Kullanılmayan Öğeler](#gereksiz--kullanılmayan-öğeler-tespiti)
- [Learning & Development Notes](#-skysync-project---learning--development-notes)
- [Gateway Özeti](#skysync-api-gateway)
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
| **Eposta Hatırlatmaları** | Uçuşa 12 saat kala otomatik flight reminder maili gönderilir |

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
| Identity | POST | `/api/auth/verify-email` | Email doğrulama |

Tüm API'ler Gateway üzerinden (`http://localhost:5000` veya konfigüre edilen port) erişilir. Detaylı request/response örnekleri: [API_ENDPOINTS.md](API_ENDPOINTS.md).

---

## Dokümantasyon

Bu README'nin ilerleyen bölümlerinde tüm detaylı dokümantasyon bir araya getirilmiştir:

| Bölüm | İçerik |
|-------|--------|
| [Detaylı API Endpointleri](#skysync-api-endpoints) | API endpoint listesi ve örnek istekler |
| [Eventler, Consumer'lar ve Saga](#skysync--eventler-consumerlar-ve-saga--detaylı-doküman) | Event/command akışları, kuyruklar, Saga state machine |
| [Outbox, Outbox Worker ve Inbox Notları](#outbox-outbox-worker-ve-inbox--detaylı-notlar) | Pattern açıklamaları, worker akışı, IInboxService metotları |
| [Correlation & Transaction ID Dokümantasyonu](#-correlation-id--transaction-id---skysync-implementation) | Kavramsal açıklama, gateway implementasyonu |
| [Correlation ID Test Guide](#-correlation-id--transaction-id---test-guide) | cURL örnekleri ve test checklist'i |
| [Correlation ID & Transaction ID – Implementation Complete](#-correlation-id--transaction-id---implementation-complete) | Middleware uygulaması özeti |

---

## Lisans

Bu proje eğitim ve portfolyo amaçlıdır; ticari kullanım için ek lisans/koşullar uygulanabilir.


---

## SkySync API Endpoints

## API Gateway
**Base URL:** `http://localhost:5000` veya `https://localhost:7000`

### Gateway Info
- **GET** `/` - Gateway bilgileri (Service, Version, Status, Endpoints)

### Health Check
- **GET** `/health` - Health check endpoint

---

## Flight Service
**Base URL:** `http://localhost:5000/api/flight` (Gateway üzerinden)

### Endpoints

#### 1. Uçuş Oluştur
- **Method:** `POST`
- **Path:** `/api/flight`
- **Description:** Yeni uçuş oluşturur (Command - Transaction + Outbox Pattern)
- **Request Body:**
```json
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
- **Response:** `201 Created`
```json
{
  "flightId": "guid",
  "flightNumber": "TK1903",
  "isSuccess": true,
  "message": "Flight created successfully"
}
```

#### 2. Tüm Uçuşları Listele
- **Method:** `GET`
- **Path:** `/api/flight`
- **Description:** Tüm uçuşları listeler (Query - Cache Aside Pattern, Redis cache)
- **Response:** `200 OK`
```json
{
  "flights": [
    {
      "id": "guid",
      "flightNumber": "TK1903",
      "departure": "Istanbul",
      "destination": "Ankara",
      "departureTime": "2026-01-26T10:00:00Z",
      "arrivalTime": "2026-01-26T11:30:00Z",
      "basePrice": 500.00,
      "status": "Active",
      "availableSeats": 150,
      "totalSeats": 180
    }
  ],
  "isFromCache": true,
  "totalCount": 10
}
```

#### 3. Uçuş Koltuklarını Getir
- **Method:** `GET`
- **Path:** `/api/flight/{flightId}/seats`
- **Description:** Belirli bir uçuşun koltuklarını getirir (Query - Direct DB, cache yok)
- **Path Parameters:**
  - `flightId` (Guid) - Uçuş ID'si
- **Response:** `200 OK`
```json
{
  "flightId": "guid",
  "flightNumber": "TK1903",
  "seats": [
    {
      "id": "guid",
      "seatNumber": "1A",
      "isReserved": false,
      "price": 500.00,
      "userId": null
    }
  ],
  "availableSeatsCount": 150,
  "reservedSeatsCount": 30,
  "totalSeatsCount": 180
}
```

---

## Reservation Service
**Base URL:** `http://localhost:5000/api/reservation` (Gateway üzerinden)

### Endpoints

#### 1. Rezervasyon Oluştur
- **Method:** `POST`
- **Path:** `/api/reservation`
- **Description:** Yeni rezervasyon oluşturur (Command - Saga State Machine'i tetikler)
- **Request Body:**
```json
{
  "flightId": "guid",
  "seatNumber": "12A",
  "price": 500.00,
  "passengerName": "Ahmet",
  "passengerSurname": "Güvendik",
  "passengerEmail": "ahmet@example.com"
}
```
- **Response:** `201 Created`
```json
{
  "reservationId": "guid",
  "correlationId": "guid",
  "isSuccess": true,
  "message": "Reservation created successfully"
}
```
- **Saga Akışı:**
  1. ReservationStarted → Flight Service'e `ReserveSeatCommand` gönderilir
  2. FlightReserved → Payment Service'e `ProcessPaymentCommand` gönderilir
  3. PaymentCompleted → Rezervasyon Confirmed, Notification Service'e `ReservationConfirmedEvent` gönderilir
  4. PaymentFailed → Flight Service'e `ReleaseSeatCommand` gönderilir (Compensate)

#### 2. Yolcu Rezervasyonlarını Listele
- **Method:** `GET`
- **Path:** `/api/reservation/passenger/{passengerEmail}`
- **Description:** Belirli bir yolcunun tüm rezervasyonlarını listeler (Query)
- **Path Parameters:**
  - `passengerEmail` (string) - Yolcu email adresi
- **Response:** `200 OK`
```json
{
  "reservations": [
    {
      "id": "guid",
      "flightId": "guid",
      "flightNumber": "N/A",
      "seatNumber": "12A",
      "price": 500.00,
      "status": "Confirmed",
      "passengerName": "Ahmet",
      "passengerSurname": "Güvendik",
      "passengerEmail": "ahmet@example.com",
      "createdTime": "2026-01-25T10:00:00Z"
    }
  ],
  "totalCount": 5
}
```

---

## Identity Service
**Base URL:** `http://localhost:5000/api/auth` (Gateway üzerinden)

### Endpoints

#### 1. Kayıt Ol
- **Method:** `POST`
- **Path:** `/api/auth/register`
- **Description:** Yeni kullanıcı kaydı (JWT oluşturmaz, sadece kullanıcı ekler)
- **Request Body:**
```json
{
  "email": "ahmet@example.com",
  "password": "Passw0rd!",
  "firstName": "Ahmet",
  "lastName": "Güvendik"
}
```
- **Response:** `201 Created`
```json
{
  "userId": "guid",
  "isSuccess": true,
  "message": "User created successfully"
}
```

#### 2. Giriş Yap
- **Method:** `POST`
- **Path:** `/api/auth/login`
- **Description:** JWT token döner; Gateway üzerinden diğer servis çağrılarında kullanılabilir
- **Request Body:**
```json
{
  "email": "ahmet@example.com",
  "password": "Passw0rd!"
}
```
- **Response:** `200 OK`
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-02-03T14:00:00Z",
  "user": {
    "id": "guid",
    "email": "ahmet@example.com",
    "firstName": "Ahmet",
    "lastName": "Güvendik"
  }
}
```

#### 3. Profil Bilgisi
- **Method:** `GET`
- **Path:** `/api/auth/profile`
- **Description:** Kullanıcı profilini getirir; **Authorization: Bearer {token}** header'ı zorunlu
- **Response:** `200 OK`
```json
{
  "id": "guid",
  "email": "ahmet@example.com",
  "firstName": "Ahmet",
  "lastName": "Güvendik",
  "createdAt": "2026-01-20T09:00:00Z"
}
```

#### 4. Çıkış Yap
- **Method:** `POST`
- **Path:** `/api/auth/logout`
- **Description:** Stateless çıkarma; sunucu sadece isteği doğrular ve istemciden mevcut JWT'yi temizlemesini ister. Token saklanmaz.
- **Headers:** `Authorization: Bearer {token}`
- **Response:** `200 OK`
```json
{
  "message": "Oturum kapatıldı. Lütfen istemci tarafında JWT tokenını temizleyin.",
  "code": "LOGOUT_SUCCESS"
}
```

---

## Airport Service
**Base URL:** `http://localhost:5000/api/airport` (Gateway üzerinden)

### Not
- Havalimanı listesinin tamamı tek bir Redis anahtarında (`airports:all`) tutulur; ilk istek veritabanından yüklenir, sonraki ~6 saat boyunca combobox ve yönetim ekranları cache’den beslenir. Sunucu tarafındaki arama/paginasyon işlemleri bu snapshot üzerinde yapılır, isterseniz istemci tarafında da filtreleme uygulayabilirsiniz. Yeni havalimanı oluşturulduğunda bu anahtar temizlenir ve bir sonraki istekte liste yeniden yüklenir.

### Endpoints

#### 1. Havalimanı Listele
- **Method:** `GET`
- **Path:** `/api/airport`
- **Query Params:**
  - `search` (opsiyonel)
  - `page` (varsayılan 1)
  - `pageSize` (varsayılan 10, maks 50)
- **Response:** `200 OK`
```json
{
  "airports": [
    {
      "id": "guid",
      "code": "IST",
      "name": "Istanbul Airport",
      "city": "Istanbul",
      "country": "Türkiye"
    }
  ],
  "isFromCache": true,
  "totalCount": 45,
  "page": 1,
  "pageSize": 10
}
```

#### 2. Havalimanı Ekle
- **Method:** `POST`
- **Path:** `/api/airport`
- **Authorization:** Admin rolü
- **Request Body:**
```json
{
  "code": "NEW",
  "name": "New Airport",
  "city": "Ankara",
  "country": "Türkiye"
}
```
- **Response:** `201 Created`
```json
{
  "airportId": "guid",
  "isSuccess": true,
  "message": "Airport created successfully."
}
```

---

## Payment Service
**Base URL:** `http://localhost:5000/api/payment` (Gateway üzerinden)

### Endpoints
- HTTP endpoint bulunmuyor. Payment servisi yalnızca RabbitMQ üzerinden `ProcessPaymentCommand` consume eder, `PaymentAuthorizedEvent` veya `PaymentFailedEvent` publish eder.

---

## Notification Service
**Base URL:** `http://localhost:5000/api/notification` (Gateway üzerinden)

### Endpoints
- HTTP endpoint bulunmuyor. Notification servisi `FlightCreatedEvent` ve `ReservationConfirmedEvent` tüketerek e-posta bildirimi gönderir (Transactional Inbox).

---

## Notlar

### Authorization
- **GEÇİCİ OLARAK KAPALI** - Tüm endpoint'ler public erişilebilir
- Production'da tekrar aktif edilecek

### Rate Limiting
- Global: 100 istek/dakika
- Global: 1000 istek/saat
- IP bazlı rate limiting aktif

### Resilience
- Timeout: 30 saniye
- Retry: 3 kez (Exponential Backoff: 2s, 4s, 8s)
- Circuit Breaker: 5 hata sonrası açılır, 30 saniye sonra tekrar dener

### Cache
- Flight listing: Redis cache (30 dakika TTL)
- Seat selection: Cache yok (dinamik veri)

---

## Test Senaryoları

### 1. Uçuş Oluştur ve Listele
```bash
# 1. Uçuş oluştur
POST http://localhost:5000/api/flight
{
  "flightNumber": "TK1903",
  "departure": "Istanbul",
  "destination": "Ankara",
  "departureTime": "2026-01-26T10:00:00Z",
  "arrivalTime": "2026-01-26T11:30:00Z",
  "basePrice": 500.00,
  "status": "Active"
}

# 2. Uçuşları listele (cache'den gelecek)
GET http://localhost:5000/api/flight

# 3. Koltukları getir
GET http://localhost:5000/api/flight/{flightId}/seats
```

### 2. Rezervasyon Oluştur
```bash
# 1. Rezervasyon oluştur (Saga başlatır)
POST http://localhost:5000/api/reservation
{
  "flightId": "guid",
  "seatNumber": "12A",
  "price": 500.00,
  "passengerName": "Ahmet",
  "passengerSurname": "Güvendik",
  "passengerEmail": "ahmet@example.com"
}

# 2. Yolcu rezervasyonlarını listele
GET http://localhost:5000/api/reservation/passenger/ahmet@example.com
```


---

## SkySync – Eventler, Consumer'lar ve Saga – Detaylı Doküman

Bu dokümanda projedeki **tüm eventler**, **command’lar**, **consumer’lar** ve **Reservation Saga** state machine açıklanmaktadır.

---

## 1. RabbitMQ Kuyrukları

| Kuyruk | Servis | Açıklama |
|--------|--------|----------|
| `reservation-saga-queue` | Reservation | Saga + 4 status consumer. `ReservationStarted`, `FlightReserved`, `FlightReservationFailed`, `PaymentCompleted`, `PaymentFailed`, `ReservationTimedOut` burada dinlenir. |
| `flight-reserve-seat-queue` | Flight | `ReserveSeatCommand` |
| `flight-release-seat-queue` | Flight | `ReleaseSeatCommand` |
| `payment-process-queue` | Payment | `ProcessPaymentCommand` |
| `notification-confirmed-queue` | Notification | `ReservationConfirmedEvent` |
| `notification-flight-created-queue` | Notification | `FlightCreatedEvent` |

---

## 2. COMMAND’LAR

Command’lar **tek yönlü**: Saga veya başka bir servis **gönderir**, ilgili servis **consumer** ile alıp işler.

| Command | Gönderen | Kuyruk | Açıklama |
|---------|----------|--------|----------|
| **ReserveSeatCommand** | Saga | `flight-reserve-seat-queue` | Koltuk rezerve et. `CorrelationId`, `FlightId`, `SeatNumber`. |
| **ReleaseSeatCommand** | Saga | `flight-release-seat-queue` | Koltuk serbest bırak (compensate). `CorrelationId`, `FlightId`, `SeatNumber`. |
| **ProcessPaymentCommand** | Saga | `payment-process-queue` | Ödeme işlemini başlat. `CorrelationId`, `ReservationId`, `Amount`, `PassengerEmail`. |

---

## 3. EVENT’LER

### 3.1. Uçuş / Rezervasyon Akışı

| Event | Kim yayınlar? | Kim dinler? | Açıklama |
|-------|----------------|-------------|----------|
| **FlightCreatedEvent** | Flight (Outbox → Worker) | Notification | Yeni uçuş eklendi. `FlightId`, `FlightNumber`, `Departure`, `Destination`, `DepartureTime`, `ArrivalTime`, `BasePrice`, `Status`, `CreatedAt`. |
| **ReservationStartedEvent** | Reservation (Outbox → Worker) | **Saga** | Rezervasyon başlatıldı. Saga’yı tetikler. `ReservationId`, `CorrelationId`, `FlightId`, `SeatNumber`, `Price`, `PassengerName`, `PassengerSurname`, `PassengerEmail`, `CreatedAt`. |
| **FlightReservedEvent** | Flight (ReserveSeat consumer) | **Saga** | Koltuk rezerve edildi. `CorrelationId`, `FlightId`, `SeatNumber`, `IsSuccess`, `ErrorMessage?`, `ReservedAt`, `FlightNumber`, `Departure`, `Destination`. |
| **FlightReservationFailedEvent** | Flight (ReserveSeat consumer) | **Saga** + **Reservation** (status) | Koltuk rezervasyonu başarısız. `CorrelationId`, `FlightId`, `SeatNumber`, `ErrorMessage`, `FailedAt`. |
| **ReservationConfirmedEvent** | **Saga** | **Reservation** (status) + **Notification** | Rezervasyon onaylandı (ödeme tamam). `CorrelationId`, `ReservationId`, `FlightId`, `SeatNumber`, yolcu bilgileri, `Price`, `ConfirmedAt`, `FlightNumber`, `Departure`, `Destination`. |
| **ReservationTimedOutEvent** | **Saga** | **Reservation** (status) | Ödeme 5 dk içinde gelmedi (timeout). `CorrelationId`, `ReservationId`, `Reason`, `TimedOutAt`. |

### 3.2. Ödeme Akışı

| Event | Kim yayınlar? | Kim dinler? | Açıklama |
|-------|----------------|-------------|----------|
| **PaymentAuthorizedEvent** | Payment (ProcessPayment consumer) | (Saga dinlemiyor) | Ödeme authorize edildi; para henüz çekilmedi. `CorrelationId`, `ReservationId`, `Amount`, `AuthorizationId`, `AuthorizedAt`. |
| **PaymentCapturedEvent** | (tanımlı, kullanılmıyor) | - | Ödeme capture edildi. |
| **PaymentCompletedEvent** | (şu an kimse yayınlamıyor) | **Saga** | Ödeme tamamlandı. Saga bunu bekliyor; **ProcessPayment şu an `PaymentAuthorizedEvent` publish ediyor.** |
| **PaymentFailedEvent** | Payment (ProcessPayment consumer) | **Saga** + **Reservation** (status) | Ödeme başarısız. `CorrelationId`, `ReservationId`, `Amount`, `ErrorMessage`, `FailedAt`. |
| **PaymentTimeoutEvent** | Saga (Quartz schedule) | Saga’nın kendi `PaymentTimeout.Received` | 5 dk ödeme timeout. Saga içinde kullanılır, dışarıya event olarak gitmez. |

### 3.3. Diğer

| Event | Açıklama |
|-------|----------|
| **ReservationTimeoutEvent** | Genel rezervasyon timeout (projede kullanılmıyor; `PaymentTimeoutEvent` kullanılıyor). |

---

## 4. CONSUMER’LAR

### 4.1. Flight Servisi

| Consumer | Mesaj | Kuyruk | Yaptığı iş |
|----------|--------|--------|------------|
| **ReserveSeatCommandConsumer** | `ReserveSeatCommand` | `flight-reserve-seat-queue` | Inbox + distributed lock. Koltuk müsaitse `IsReserved = true`, `FlightReservedEvent` publish; değilse / hata varsa `FlightReservationFailedEvent` publish. |
| **ReleaseSeatCommandConsumer** | `ReleaseSeatCommand` | `flight-release-seat-queue` | Inbox. İlgili koltuğu bulur, `IsReserved = false` yapar. Event publish etmez. |

### 4.2. Payment Servisi

| Consumer | Mesaj | Kuyruk | Yaptığı iş |
|----------|--------|--------|------------|
| **ProcessPaymentConsumer** | `ProcessPaymentCommand` | `payment-process-queue` | Inbox. Ödeme simülasyonu (Amount &lt; 5000 → başarı). Başarı: `PaymentTransaction` kaydı + `PaymentAuthorizedEvent`. Başarısız: `PaymentFailedEvent`. |

### 4.3. Notification Servisi

| Consumer | Mesaj | Kuyruk | Yaptığı iş |
|----------|--------|--------|------------|
| **FlightCreatedConsumer** | `FlightCreatedEvent` | `notification-flight-created-queue` | Transactional Inbox. Admin email listesine “yeni uçuş” maili gönderir. |
| **ReservationConfirmedConsumer** | `ReservationConfirmedEvent` | `notification-confirmed-queue` | Transactional Inbox. Yolcuya “rezervasyon onaylandı” maili gönderir. |

### 4.4. Reservation Servisi (Saga + Status)

| Consumer | Mesaj | Kuyruk | Yaptığı iş |
|----------|--------|--------|------------|
| **Saga** | `ReservationStartedEvent`, `FlightReservedEvent`, `FlightReservationFailedEvent`, `PaymentCompletedEvent`, `PaymentFailedEvent`, `PaymentTimeout.Received` | `reservation-saga-queue` | Rezervasyon akışını yönetir (aşağıda özet). |
| **ReservationConfirmedStatusConsumer** | `ReservationConfirmedEvent` | `reservation-saga-queue` | Inbox. `Reservation` kaydını `Status = Confirmed` yapar. |
| **ReservationSeatFailedStatusConsumer** | `FlightReservationFailedEvent` | `reservation-saga-queue` | Inbox. `CorrelationId` ile reservation bulur, `Status = Failed` yapar. |
| **ReservationPaymentFailedStatusConsumer** | `PaymentFailedEvent` | `reservation-saga-queue` | Inbox. `CorrelationId` ile reservation bulur, `Status = Failed` yapar. |
| **ReservationTimedOutStatusConsumer** | `ReservationTimedOutEvent` | `reservation-saga-queue` | Inbox. `CorrelationId` ile reservation bulur, `Status = Failed` yapar. |

---

## 5. RESERVATION SAGA – State Machine

### 5.1. Amaç

Rezervasyon sürecini (**koltuk rezervasyonu → ödeme**) dağıtık şekilde yönetmek. Bir adım başarısız olursa **compensate** (örn. koltuğu sal). **5 dakika** içinde ödeme gelmezse **timeout** → koltuğu serbest bırak, rezervasyonu Failed yap.

### 5.2. State’ler

| State | Açıklama |
|-------|----------|
| **Initial** | Saga henüz başlamadı. |
| **AwaitingFlightReservation** | `ReserveSeatCommand` gönderildi; Flight’tan `FlightReserved` veya `FlightReservationFailed` bekleniyor. |
| **AwaitingPayment** | Koltuk rezerve edildi, `ProcessPaymentCommand` gönderildi. `PaymentCompleted`, `PaymentFailed` veya **5 dk timeout** bekleniyor. |
| **Finalized** | Saga bitti (başarı veya başarısızlık). |

### 5.3. State’te Tutulan Veri (ReservationState)

`CorrelationId`, `ReservationId`, `FlightId`, `SeatNumber`, `Price`, `FlightNumber`, `Departure`, `Destination`, `PassengerName`, `PassengerSurname`, `PassengerEmail`, `CreatedAt`, `FlightReservedAt`, `PaymentCompletedAt`, `CompletedAt`, `ErrorMessage`, `TimeoutTokenId` (Quartz schedule için).

### 5.4. Geçişler (Özet)

```
[Initial]
    │
    │  ReservationStartedEvent
    │  → ReserveSeatCommand gönder (flight-reserve-seat-queue)
    ▼
[AwaitingFlightReservation]
    │
    ├─ FlightReservedEvent
    │     → ProcessPaymentCommand publish
    │     → PaymentTimeout schedule (5 dk)
    │     → AwaitingPayment
    │
    └─ FlightReservationFailedEvent
          → Finalize (saga biter, rezervasyon Failed)
    │
[AwaitingPayment]
    │
    ├─ PaymentCompletedEvent
    │     → Unschedule PaymentTimeout
    │     → ReservationConfirmedEvent publish (Notification + Reservation status consumer)
    │     → Finalize
    │
    ├─ PaymentFailedEvent
    │     → Unschedule PaymentTimeout
    │     → ReleaseSeatCommand gönder (flight-release-seat-queue)
    │     → Finalize
    │
    └─ PaymentTimeout.Received (5 dk doldu)
          → ReservationTimedOutEvent publish (Reservation status consumer)
          → ReleaseSeatCommand gönder
          → Finalize
```

### 5.5. Akış Şeması (Metin)

```
CreateReservation API
       │
       ▼
Reservation DB + Outbox (ReservationStartedEvent)
       │
       ▼
Outbox Worker → RabbitMQ (ReservationStartedEvent)
       │
       ▼
SAGA: Initially → ReservationStarted
       │
       ├─ Send ReserveSeatCommand → Flight
       └─ TransitionTo(AwaitingFlightReservation)
       │
       ▼
Flight: ReserveSeatCommandConsumer
       │
       ├─ Başarı → Publish FlightReservedEvent
       └─ Başarısız → Publish FlightReservationFailedEvent
       │
       ▼
SAGA: AwaitingFlightReservation
       │
       ├─ FlightReserved
       │     → Publish ProcessPaymentCommand
       │     → Schedule PaymentTimeout (5 dk)
       │     → AwaitingPayment
       │
       └─ FlightReservationFailed → Finalize
       │
       ▼
Payment: ProcessPaymentConsumer
       │
       ├─ Başarı → Publish PaymentAuthorizedEvent (*)
       └─ Başarısız → Publish PaymentFailedEvent
       │
       ▼
SAGA: AwaitingPayment
       │
       ├─ PaymentCompleted (**) → ReservationConfirmedEvent → Finalize
       ├─ PaymentFailed → ReleaseSeatCommand → Finalize
       └─ PaymentTimeout → ReservationTimedOutEvent + ReleaseSeatCommand → Finalize
```

(\*) **Not:** Payment şu an `PaymentAuthorizedEvent` yayınlıyor; Saga ise `PaymentCompletedEvent` bekliyor. Bu uyumsuzluk giderilmeden ödeme başarılı akışı tam tamamlanmaz.  
(\**) Saga `PaymentCompletedEvent` ile `ReservationConfirmedEvent` publish edip finalize ediyor. Status consumer rezervasyonu Confirmed, Notification consumer mail gönderiyor.

### 5.6. Compensation (Geri Alma)

| Durum | Yapılan |
|-------|---------|
| **FlightReservationFailed** | Sadece saga finalize. Rezervasyon zaten Failed (status consumer günceller). |
| **PaymentFailed** | `ReleaseSeatCommand` → Flight koltuğu serbest bırakır. Status consumer rezervasyonu Failed yapar. |
| **Payment timeout (5 dk)** | `ReleaseSeatCommand` + `ReservationTimedOutEvent`. Rezervasyon Failed, koltuk serbest. |

---

## 6. ÖZET TABLO – Kim Ne Gönderir, Kim Ne Dinler?

| Mesaj | Gönderen | Dinleyen (Consumer / Saga) |
|-------|----------|----------------------------|
| **FlightCreatedEvent** | Flight (Outbox) | Notification |
| **ReservationStartedEvent** | Reservation (Outbox) | Saga |
| **ReserveSeatCommand** | Saga | Flight |
| **FlightReservedEvent** | Flight | Saga |
| **FlightReservationFailedEvent** | Flight | Saga, Reservation (status) |
| **ProcessPaymentCommand** | Saga | Payment |
| **PaymentAuthorizedEvent** | Payment | (Saga dinlemiyor) |
| **PaymentCompletedEvent** | (yok) | Saga |
| **PaymentFailedEvent** | Payment | Saga, Reservation (status) |
| **ReservationConfirmedEvent** | Saga | Reservation (status), Notification |
| **ReservationTimedOutEvent** | Saga | Reservation (status) |
| **ReleaseSeatCommand** | Saga | Flight |

---

## 7. Inbox Kullanımı

Tüm ilgili consumer’lar **Inbox** ile idempotency sağlar (duplicate skip). Notification’da **Transactional Inbox** (`TryProcessInTransactionAsync`), diğerlerinde `MarkAsProcessedAsync` kullanılır.

---

*Bu doküman mevcut kod tabanına göre yazılmıştır. Payment / Saga uyumsuzluğu giderildiğinde güncellenmelidir.*


---

## Outbox, Outbox Worker ve Inbox – Detaylı Notlar

Bu dokümanda SkySync projesindeki **Outbox**, **Outbox Worker** ve **Inbox** yapıları ile **IInboxService** metotları açıklanmaktadır.

---

## 1. OUTBOX – Nedir, Ne İşe Yarar?

### Tanım
**Outbox**, bir servisin **event yayınlamadan önce** o event’i kendi veritabanında bir tabloya yazması pattern’idir.  
Yayınlama (publish) işi **ayrı bir worker** tarafından, **tablodan okuyup** message broker’a (RabbitMQ) gönderilir.

### Neden Kullanılır?
- **Atomicity:** Uçuş ekleme + “FlightCreatedEvent yayınlanacak” bilgisi **aynı transaction** içinde yazılır. Transaction commit olursa hem entity hem outbox kaydı kalır; rollback olursa ikisi de silinir.
- **Güvenilir teslimat:** Doğrudan RabbitMQ’ya publish edersen, DB commit olduktan sonra publish sırasında hata olursa event kaybolabilir. Outbox’ta yazılı olduğu için worker tekrar deneyebilir.
- **Kayıt / audit:** Hangi event’in ne zaman oluştuğu, ne zaman kuyruğa gittiği Outbox tablosunda kalır.

### Outbox Tablosu (OutboxMessage)

| Alan | Açıklama |
|------|----------|
| **Id** | PK. Aynı zamanda RabbitMQ’ya giden mesajın **MessageId**’si olarak kullanılır (idempotency). |
| **Type** | Event tipi (örn. `FlightCreatedEvent`, `ReservationConfirmedEvent`). |
| **Content** | Event’in JSON payload’u. |
| **OccurredOn** | Event’in oluşma zamanı. |
| **ProcessedOn** | Kuyruğa **ne zaman** gönderildiği. `null` = henüz gönderilmedi. |
| **Error** | Gönderim sırasında hata olursa hata mesajı. |
| **RetryCount** | Kaç kez publish denendiği. |
| **IsFailed** | Max retry sonrası kalıcı başarısız işaretlendi mi? |

### Outbox Nerede Tutuluyor?
- **Flight servisi:** Flight DB’de `OutboxMessages` tablosu. Uçuş eklenirken `FlightCreatedEvent` buraya yazılır.
- **Reservation servisi:** Reservation DB’de `OutboxMessages`. Rezervasyon onaylanınca `ReservationConfirmedEvent` buraya yazılır.

### Akış (Özet)
1. Handler (örn. `CreateFlightCommandHandler`) transaction açar.
2. Entity’yi kaydeder (örn. `Flight` + `Seat`’ler).
3. `OutboxMessage` oluşturur (`Type`, `Content`, `OccurredOn`; `ProcessedOn = null`).
4. Aynı transaction’da `OutboxMessages`’a insert eder.
5. Transaction commit.
6. **Publish burada yapılmaz.** Worker ilgili tabloyu okuyup publish edecek.

---

## 2. OUTBOX WORKER – Nedir, Ne İşe Yarar?

### Tanım
**Outbox Worker**, Outbox tablosundaki **henüz kuyruğa gönderilmemiş** kayıtları okuyup **RabbitMQ’ya publish eden** arka planda çalışan servistir.  
SkySync’te `SkySync.Workers.Outbox` host’u içinde **Flight** ve **Reservation** için ayrı worker’lar vardır.

### Worker’lar
- **FlightOutboxPublishWorker:** Flight DB’deki `OutboxMessages`’ı okur, `FlightCreatedEvent` vb. yayınlar.
- **ReservationOutboxPublishWorker:** Reservation DB’deki `OutboxMessages`’ı okur, `ReservationConfirmedEvent` vb. yayınlar.

### Hangi Kayıtlar İşlenir?
```text
ProcessedOn == null  VE  IsFailed == false
```
- `ProcessedOn == null` → Henüz publish edilmemiş.
- `IsFailed == false` → Kalıcı başarısız işaretlenmemiş (max retry aşılmamış).

### Çalışma Mantığı (Flight Worker Örneği)
1. **Periyodik döngü** (örn. her 2 saniye): `GetUnprocessedMessagesAsync` ile `ProcessedOn == null && !IsFailed` kayıtları alınır (batch, örn. 20 adet).
2. **Her mesaj için:**
   - `RetryCount >= MaxRetryCount` (örn. 5) ise → `IsFailed = true`, `Error` set edilir, bir daha işlenmez.
   - Değilse → `Type` + `Content` ile event **deserialize** edilir, `IPublishEndpoint.Publish` ile RabbitMQ’ya gönderilir. **MessageId = OutboxMessage.Id** kullanılır (idempotency).
3. **Başarılı publish:** `ProcessedOn = DateTime.UtcNow`, `Error = null`, `RetryCount = 0`.
4. **Başarısız:** `RetryCount++`, `Error` set edilir. Sonraki döngüde tekrar denenecek.
5. Değişiklikler `UnitOfWork.SaveChangesAsync` ile persist edilir.
6. Döngü `DelaySeconds` (örn. 2 sn) bekleyip tekrarlanır.

### Önemli Noktalar
- **Polymorphic publish:** `Type` string’inden runtime’da event tipi bulunur, JSON `Content` deserialize edilip publish edilir.
- **Sabit MessageId:** `OutboxMessage.Id` publish’te kullanıldığı için, aynı outbox kaydı tekrar publish edilse bile consumer tarafında **aynı MessageId** görülür (Inbox idempotency ile uyumlu).

---

## 3. INBOX – Nedir, Ne İşe Yarar?

### Tanım
**Inbox**, bir servisin **kuyruktan mesaj aldıktan sonra** “bu mesajı işledim / işlemedim” bilgisini kendi veritabanında tutması pattern’idir.  
Amaç: **Aynı mesajın iki kez işlenmesini** (duplicate) engellemek → **idempotency**.

### Neden Kullanılır?
- RabbitMQ **at-least-once** teslimat yapar. Retry, Nack, requeue vb. yüzünden **aynı event birden fazla kez** consumer’a gelebilir.
- Örn. `FlightCreatedEvent` iki kez işlenirse iki kez mail gidebilir; `ReserveSeatCommand` iki kez işlenirse aynı koltuk iki kez rezerve edilmeye çalışılabilir.
- Inbox’ta **“(EventType, BusinessKey) bu mesajı işledim”** kaydı tutulur. İkinci gelişte “zaten işledim” deyip **skip** edilir.

### Inbox Tablosu (InboxMessage)

| Alan | Açıklama |
|------|----------|
| **MessageId** | PK. Genelde MassTransit’in mesaj ID’si (veya `OutboxMessage.Id`). |
| **EventType** | Event/command tipi (örn. `FlightCreatedEvent`, `ReserveSeatCommand`). |
| **BusinessKey** | İş kuralına göre tekil anahtar (örn. `FlightId`, `ReservationId`). **Unique constraint:** `(EventType, BusinessKey)`. |
| **EventPayload** | JSON payload (opsiyonel, debug/audit için). |
| **Status** | `Processing`, `Processed`, `Failed`, `Skipped`. |
| **ProcessedAt** | İşlenme zamanı. |
| **ErrorMessage** | Hata durumunda açıklama. |
| **RetryCount** | (Kullanılıyorsa) retry sayısı. |

### Inbox Nerede Tutuluyor?
- **Flight:** Flight DB, `InboxMessages`. ReserveSeat / ReleaseSeat duplicate önleme.
- **Payment:** Payment DB, `InboxMessages`. Ödeme duplicate önleme.
- **Reservation:** Reservation DB, `InboxMessages`. Status consumer’ları için duplicate önleme.
- **Notification:** Notification DB, `InboxMessages`. FlightCreated / ReservationConfirmed mail duplicate önleme.

### Genel Akış
1. Consumer mesajı alır.
2. **Önce** Inbox’a bakar veya kayıt atar (“bu mesajı işliyorum / işledim”).
3. Duplicate ise (zaten kayıt varsa) → **skip**, iş yapma.
4. Değilse → işi yap (mail at, koltuk rezerve et, ödeme al vb.), gerekirse Inbox’ı güncelle.

---

## 4. IInboxService METOTLARI – Ne İşe Yarar?

Tüm Inbox implementasyonları `IInboxService` üzerinden çalışır. Metotların anlamı:

---

### 4.1. `IsProcessedByBusinessKeyAsync(eventType, businessKey)`

**Ne yapar:**  
`(EventType, BusinessKey)` ile daha önce **herhangi bir Inbox kaydı** var mı diye bakar.

**Döner:**  
- `true` → Bu event + business key için kayıt var (daha önce işlenmiş / işlenmeye alınmış).
- `false` → Kayıt yok.

**Ne zaman kullanılır:**  
Önce “bu mesajı hiç işledim mi?” diye kontrol etmek istersen. Bazı consumer’lar doğrudan `MarkAsProcessedAsync` ile **insert** dener; duplicate’ta unique constraint’ten `false` alıp skip eder. Bu metot alternatif bir “önce kontrol et” yolu.

---

### 4.2. `MarkAsProcessedAsync(messageId, eventType, businessKey, eventPayload?)`

**Ne yapar:**  
Inbox’a **Processed** durumunda yeni bir kayıt ekler. “Bu mesajı işledim / işleyeceğim” anlamına gelir.

**Döner:**  
- `true` → Kayıt eklendi, **işe devam et**.
- `false` → **Duplicate:** unique `(EventType, BusinessKey)` zaten var (başka consumer veya önceki deneme eklemiş). **İşi yapma, skip et.**

**Ne zaman kullanılır:**  
- **Önce** Inbox’a “işledim” de, **sonra** asıl işi yap (mail, rezervasyon, ödeme vb.).
- Flight (ReserveSeat, ReleaseSeat), Payment (ProcessPayment, Refund), Reservation (status consumer’ları) bu metodu kullanır.

**Önemli:**  
Hata olursa (duplicate dışında) implementasyon **exception** fırlatabilir; consumer **retry / Nack** ile mesajı bırakır. Başarılı olana kadar tekrar denenecek.

---

### 4.3. `MarkAsFailedAsync(messageId, eventType, businessKey, errorMessage, eventPayload?)`

**Ne yapar:**  
Inbox’a **Failed** durumunda kayıt ekler. “Bu mesajı işlemeye çalıştım, hata aldım” anlamına gelir.

**Döner:**  
Void. Hata kaydı yazılamazsa loglanır, exception yutulabilir (mesaj kaybı olmasın diye).

**Ne zaman kullanılır:**  
Consumer işi yaptıktan sonra hata alırsa ve **yine de ACK** vermek istemiyorsan (retry için Nack), önce “Failed” diye Inbox’a yazıp sonra **throw** edebilirsin. Böylece hem audit hem retry/DLQ tarafı doğru çalışır.  
Şu anki sade Inbox kullanımında bazı consumer’lar doğrudan **throw** edip `MarkAsFailedAsync` kullanmıyor; Transactional Inbox’ta da rollback + throw tercih ediliyor.

---

### 4.4. `MarkAsSkippedAsync(messageId, eventType, businessKey, eventPayload?)`

**Ne yapar:**  
Inbox’a **Skipped** durumunda kayıt ekler. “Bu mesaj duplicate’tı, işlemedim” anlamına gelir.

**Döner:**  
Void.

**Ne zaman kullanılır:**  
Duplicate tespit edip **skip** ettiğinde audit için “bu mesajı atladım” demek istersen. Zorunlu değil; `MarkAsProcessedAsync` duplicate’ta `false` dönüp skip etmek yeterli olabilir. Skipped kaydı log / raporlama için işe yarar.

---

### 4.5. `TryProcessInTransactionAsync(messageId, eventType, businessKey, eventPayload, work, ct)`

**Ne yapar:**  
**Transactional Inbox** akışı:

1. Transaction başlat.
2. `(EventType, BusinessKey)` için kayıt var mı bak → varsa **rollback**, `false` dön (skip).
3. Inbox’a **Processing** ile insert et, `SaveChanges`.
4. **`work(ct)`** çağır (asıl iş: mail atmak vb.).
5. Kaydı **Processed** yap, `SaveChanges`, **commit**.
6. Herhangi bir adımda hata olursa **rollback**, **throw** (Nack → retry / DLQ).

**Döner:**  
- `true` → İşlem bitti, commit edildi.
- `false` → Duplicate, skip edildi.

**Ne zaman kullanılır:**  
**Sadece Notification servisi.** FlightCreated ve ReservationConfirmed consumer’ları mail işini bu metoda **callback** (`work`) olarak verir. Inbox + iş **tek transaction** içinde; hata olursa ne Inbox’a kesin kayıt düşer ne de mail gider, mesaj Nack ile tekrar kuyruğa döner.

**Diğer servisler:**  
Flight, Payment, Reservation bu metodu **implement etmez**; `NotImplementedException` fırlatır. Onlar `MarkAsProcessedAsync` + iş akışını kullanır.

---

## 5. ÖZET AKIŞ – Baştan Sona

```
[Flight API] CreateFlight
       │
       ▼
 CreateFlightCommandHandler
       │
       ├─ Transaction başlat
       ├─ Flight + Seats → DB
       ├─ OutboxMessage (FlightCreatedEvent) → OutboxMessages tablosu
       └─ Commit
       │
       │  (Publish burada YOK)
       │
       ▼
[Outbox Worker] FlightOutboxPublishWorker
       │
       ├─ ProcessedOn == null && !IsFailed  kayıtları oku
       ├─ Her biri için: Publish RootMQ (MessageId = OutboxMessage.Id)
       ├─ Başarılıysa ProcessedOn = now
       └─ Hata varsa RetryCount++, sonraki döngüde tekrar dene
       │
       ▼
[RabbitMQ] FlightCreatedEvent kuyruğu
       │
       ▼
[Notification] FlightCreatedConsumer
       │
       ├─ TryProcessInTransactionAsync (Transactional Inbox)
       │     ├─ Tx başlat
       │     ├─ (EventType, BusinessKey) duplicate mi? → Evet: rollback, skip
       │     ├─ Inbox INSERT (Processing)
       │     ├─ work() = Mail gönder
       │     ├─ Inbox UPDATE (Processed), Commit
       │     └─ Hata: Rollback, throw → Nack, retry
       └─ Duplicate değilse mail gitti, Inbox’a Processed yazıldı.
```

---

## 6. Kısa Özet Tablo

| Kavram | Nerede | Ne işe yarar |
|--------|--------|----------------|
| **Outbox** | Flight / Reservation DB | Event’i **önce** DB’ye yaz; worker sonra RabbitMQ’ya publish etsin. Kayıp olmasın, atomik olsun. |
| **Outbox Worker** | Workers.Outbox host | Outbox tablosunu okuyup **ProcessedOn == null** kayıtları RabbitMQ’ya publish eder. Retry, batch, MessageId = Outbox.Id. |
| **Inbox** | Her consumer servisinin kendi DB’si | “Bu mesajı işledim” kaydı → **duplicate** gelirse skip et, idempotency. |
| **MarkAsProcessedAsync** | Inbox | Önce Inbox’a yaz; duplicate’sa false dön, skip. Değilse işe devam et. |
| **TryProcessInTransactionAsync** | Sadece Notification | Tx + Inbox (Processing → work → Processed) + commit. Hata = rollback + throw, Nack. |

---

*Bu notlar SkySync kod tabanına göre yazılmıştır. Değişiklik olursa doküman güncellenmelidir.*


---

## 🔍 Correlation ID & Transaction ID - SkySync Implementation

## 📋 İçindekiler
1. [Kavramsal Açıklama](#kavramsal-açıklama)
2. [API Gateway Implementation](#api-gateway-implementation)
3. [Mikroservislerde Kullanım](#mikroservislerde-kullanım)
4. [Test Senaryoları](#test-senaryoları)
5. [Best Practices](#best-practices)

---

## 🎯 Kavramsal Açıklama

### Correlation ID (İlişkilendirme ID)
**Ne İşe Yarar:**
- Bir kullanıcı isteğinin **tüm sistem boyunca** takip edilmesi
- Gateway → Flight Service → Payment Service → Notification Service
- **Aynı iş akışına** ait tüm log kayıtlarını birleştirme

**Özellikleri:**
- ✅ İstek başladığında oluşturulur (API Gateway)
- ✅ Tüm mikroservislere HTTP header ile taşınır
- ✅ İstek tamamlanana kadar yaşar
- ✅ Saga Pattern'de kritik öneme sahip

**Örnek Senaryo:**
```
Kullanıcı Reservation #12345 oluşturuyor
Correlation-ID: abc-123-xyz-789

[Gateway]        15:30:00 | abc-123-xyz-789 | POST /api/reservation
[Reservation]    15:30:01 | abc-123-xyz-789 | Creating reservation
[Flight Service] 15:30:02 | abc-123-xyz-789 | Reserving seat 12A
[Payment]        15:30:05 | abc-123-xyz-789 | Processing payment $150
[Notification]   15:30:08 | abc-123-xyz-789 | Sending email
```

---

### Transaction ID (İşlem ID)
**Ne İşe Yarar:**
- **Her HTTP request** için unique ID
- API Gateway seviyesinde request tracking
- Kısa ömürlü (sadece bu HTTP request)

**Özellikleri:**
- ✅ Her request için yeni ID oluşturulur
- ✅ Gateway logging ve debugging için
- ✅ Client retry durumlarını ayırt etme

**Örnek Senaryo:**
```
Kullanıcı network timeout nedeniyle 3 kez retry yapıyor

Request 1: Transaction-ID: tx-001 | Correlation-ID: abc-123 | TIMEOUT
Request 2: Transaction-ID: tx-002 | Correlation-ID: abc-123 | TIMEOUT
Request 3: Transaction-ID: tx-003 | Correlation-ID: abc-123 | SUCCESS

→ 3 farklı HTTP request (3 Transaction ID)
→ Aynı iş akışı (1 Correlation ID)
```

---

### Ana Farklar

| Özellik | Correlation ID | Transaction ID |
|---------|---------------|----------------|
| **Kapsam** | Tüm sistem (Gateway + tüm mikroservisler) | Sadece Gateway |
| **Ömür** | İstek başından sonuna (end-to-end) | Tek HTTP request |
| **Kullanım** | Distributed tracing, Saga tracking | Request logging, debugging |
| **Oluşturuluyor** | Gateway (client da gönderebilir) | Her zaman Gateway |
| **Sayı** | İş akışı başına 1 | Request başına 1 (retry'da farklı) |

---

## 🚀 API Gateway Implementation

### 1. CorrelationIdMiddleware (Yeni Eklendi! ✅)

```csharp
// Middleware Order (ÖNEMLİ!)
app.UseHttpsRedirection();
app.UseCorrelationId();           // 1. İLK SIRADA (ID'leri oluşturur)
app.UseRequestLogging();          // 2. Sonra (ID'leri loglar)
app.UseRequestTransformation();   // 3. Sonra
```

**Ne Yapıyor:**
1. Client'tan `X-Correlation-ID` header'ı geliyorsa kullanır
2. Yoksa yeni `Correlation ID` oluşturur
3. Her request için yeni `Transaction ID` oluşturur
4. Her ikisini de `HttpContext.Items`'a ekler
5. Response header'a ekler (client görebilir)
6. Request header'a ekler (mikroservislere gönderilir)
7. Activity (OpenTelemetry) için tag'ler

### 2. Header İsimleri (Standart)

```csharp
X-Correlation-ID   → Correlation ID için
X-Transaction-ID   → Transaction ID için
X-Request-ID       → Alternatif (bazı sistemler bunu kullanır)
```

### 3. Client Kullanımı

**Postman/cURL:**
```bash
# Correlation ID OLMADAN (Gateway oluşturur)
curl -X POST http://localhost:5000/api/reservation \
  -H "Content-Type: application/json" \
  -d '{"flightId":1,"seatNumber":"12A"}'

# Response Headers:
# X-Correlation-ID: abc-123-xyz-789
# X-Transaction-ID: tx-001-555

# Correlation ID İLE (Gateway kullanır)
curl -X POST http://localhost:5000/api/reservation \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: my-custom-tracking-id" \
  -d '{"flightId":1,"seatNumber":"12A"}'

# Response Headers:
# X-Correlation-ID: my-custom-tracking-id (aynı ID)
# X-Transaction-ID: tx-002-666 (yeni ID)
```

---

## 🎯 Mikroservislerde Kullanım

### Şu Anda Kullanım Yerleri

#### 1. Saga State Machine ✅
```csharp
// ReservationStateMachine.cs
Event(() => ReservationStarted, x => 
    x.CorrelateById(context => context.Message.CorrelationId));

// Her event Correlation ID ile eşleştirilir
```

#### 2. Commands & Events ✅
```csharp
// ReserveSeatCommand.cs
public Guid CorrelationId { get; set; }

// FlightReservedEvent.cs
public Guid CorrelationId { get; set; }
```

#### 3. Database Entities ✅
```csharp
// Reservation Entity
public Guid CorrelationId { get; set; }

// PaymentTransaction Entity
public Guid CorrelationId { get; set; }
```

#### 4. Logging ✅
```csharp
_logger.LogInformation(
    "Seat reserved. FlightId: {FlightId}, CorrelationId: {CorrelationId}",
    flightId, correlationId);
```

---

### Mikroservislerde Header'dan Okuma (Opsiyonel)

Eğer mikroservisler Gateway'den gelen Correlation ID'yi kullanmak isterse:

```csharp
// Startup.cs veya Program.cs
builder.Services.AddHttpContextAccessor();

// Controller veya Handler
public class ReservationController : ControllerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReservationController(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    [HttpPost]
    public async Task<IActionResult> Create()
    {
        // Gateway'den gelen Correlation ID'yi oku
        var correlationId = _httpContextAccessor.HttpContext?
            .Request.Headers["X-Correlation-ID"]
            .FirstOrDefault();

        var transactionId = _httpContextAccessor.HttpContext?
            .Request.Headers["X-Transaction-ID"]
            .FirstOrDefault();

        _logger.LogInformation(
            "Request received with CorrelationId: {CorrelationId}, TransactionId: {TransactionId}",
            correlationId, transactionId);

        // ...
    }
}
```

---

## 🧪 Test Senaryoları

### Test 1: Normal Flow
```bash
# Request
POST http://localhost:5000/api/reservation
Content-Type: application/json

{
  "userId": 1,
  "flightId": 1,
  "seatNumber": "12A",
  "passengerName": "John Doe"
}

# Response Headers
X-Correlation-ID: 7a3c5f9e-1234-5678-9abc-def012345678
X-Transaction-ID: 2b4d6f8e-5678-9abc-1234-567890abcdef

# Gateway Log:
[15:30:00] Request started. Method: POST, Path: /api/reservation, 
  CorrelationId: 7a3c5f9e-1234-5678-9abc-def012345678, 
  TransactionId: 2b4d6f8e-5678-9abc-1234-567890abcdef

# Reservation Service Log:
[15:30:01] Creating reservation. CorrelationId: 7a3c5f9e-1234-5678-9abc-def012345678

# Flight Service Log:
[15:30:02] Reserving seat. CorrelationId: 7a3c5f9e-1234-5678-9abc-def012345678

# Payment Service Log:
[15:30:05] Processing payment. CorrelationId: 7a3c5f9e-1234-5678-9abc-def012345678
```

### Test 2: Client Retry (Same Correlation ID)
```bash
# Request 1 (TIMEOUT)
POST http://localhost:5000/api/reservation
X-Correlation-ID: user-retry-attempt-xyz

# Response: Timeout

# Request 2 (RETRY - SAME Correlation ID)
POST http://localhost:5000/api/reservation
X-Correlation-ID: user-retry-attempt-xyz

# Gateway Log:
[15:30:00] TransactionId: tx-001, CorrelationId: user-retry-attempt-xyz (TIMEOUT)
[15:30:15] TransactionId: tx-002, CorrelationId: user-retry-attempt-xyz (SUCCESS)

→ Aynı kullanıcı işlemi, 2 farklı HTTP request
```

### Test 3: Multiple Reservations (Different Correlation IDs)
```bash
# User 1 - Reservation 1
POST /api/reservation
# CorrelationId: aaa-111, TransactionId: tx-001

# User 2 - Reservation 2
POST /api/reservation
# CorrelationId: bbb-222, TransactionId: tx-002

# User 1 - Reservation 3
POST /api/reservation
# CorrelationId: aaa-333, TransactionId: tx-003

→ Her rezervasyon farklı Correlation ID
```

---

## 🎯 Best Practices

### 1. Gateway Seviyesinde
✅ **DO:**
- Correlation ID yoksa oluştur, varsa kullan
- Her zaman Transaction ID oluştur
- Response header'a ekle (client debugging)
- Request header'a ekle (downstream services)
- Structured logging kullan

❌ **DON'T:**
- Correlation ID'yi override etme (client gönderirse kullan)
- Transaction ID'yi downstream'e gönderme (sadece logging için)
- Header isimleri değiştirme (standartları kullan)

### 2. Mikroservis Seviyesinde
✅ **DO:**
- Saga event'lerinde Correlation ID kullan
- Database entity'lerinde Correlation ID sakla
- Log'larda Correlation ID göster
- Command/Event'lerde Correlation ID taşı

❌ **DON'T:**
- Correlation ID'yi değiştirme (aynı ID kullan)
- Yeni Correlation ID oluşturma (Gateway'den geleni kullan)

### 3. Loglama
✅ **DO:**
```csharp
// Structured logging (GOOD)
_logger.LogInformation(
    "Payment processed. Amount: {Amount}, CorrelationId: {CorrelationId}",
    amount, correlationId);

// Grep ile bulabilirsin:
// grep "CorrelationId: abc-123" *.log
```

❌ **DON'T:**
```csharp
// String interpolation (BAD)
_logger.LogInformation($"Payment processed {correlationId}");
```

### 4. Database Query
✅ **DO:**
```csharp
// Tüm ilişkili kayıtları bul
var reservation = await _context.Reservations
    .FirstOrDefaultAsync(r => r.CorrelationId == correlationId);

var payment = await _context.PaymentTransactions
    .FirstOrDefaultAsync(p => p.CorrelationId == correlationId);

// İndexleme yapmalısın!
entity.HasIndex(e => e.CorrelationId);
```

---

## 📊 Production'da Debugging

### Senaryo: "Kullanıcı ödeme yaptı ama rezervasyon oluşmadı"

1. **Client'tan Correlation ID al:**
```
User: "Ödeme yaptım ama biletim gelmedi!"
Support: "Lütfen ekran görüntüsündeki Correlation ID'yi gönderin."
User: "X-Correlation-ID: abc-123-xyz"
```

2. **Tüm log'larda ara:**
```bash
# Gateway
grep "abc-123-xyz" gateway.log
# [15:30:00] Request started. CorrelationId: abc-123-xyz

# Flight Service
grep "abc-123-xyz" flight-service.log
# [15:30:02] Seat reserved. CorrelationId: abc-123-xyz

# Payment Service
grep "abc-123-xyz" payment-service.log
# [15:30:05] Payment completed. CorrelationId: abc-123-xyz

# Notification Service
grep "abc-123-xyz" notification-service.log
# NOTHING FOUND → Sorun burada!
```

3. **Database'de ara:**
```sql
-- Reservation var mı?
SELECT * FROM Reservations WHERE CorrelationId = 'abc-123-xyz';

-- Payment var mı?
SELECT * FROM PaymentTransactions WHERE CorrelationId = 'abc-123-xyz';

-- Saga state ne durumda?
SELECT * FROM ReservationState WHERE CorrelationId = 'abc-123-xyz';
```

---

## 🚀 Gelecek İyileştirmeler

### 1. Serilog + Structured Logging
```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.WithProperty("ApplicationName", "SkySync.Gateway")
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss}] [{Level}] [{CorrelationId}] {Message}{NewLine}")
    .CreateLogger();
```

### 2. OpenTelemetry + Jaeger
```csharp
builder.Services.AddOpenTelemetryTracing(builder =>
{
    builder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddJaegerExporter(options =>
        {
            options.AgentHost = "localhost";
            options.AgentPort = 6831;
        });
});
```

### 3. Elasticsearch + Kibana
```bash
# Tüm log'ları Elasticsearch'e gönder
# Kibana'da görselleştir
# Correlation ID ile filtreleme yap
```

---

## 📚 Özet

**Şu Anda:**
- ✅ API Gateway'de Correlation ID middleware eklendi
- ✅ API Gateway'de Transaction ID middleware eklendi
- ✅ Response header'a ID'ler ekleniyor
- ✅ Request header'a ID'ler ekleniyor (mikroservislere gider)
- ✅ Structured logging ile ID'ler loglanıyor
- ✅ Saga Pattern'de Correlation ID kullanılıyor

**Yapılabilir İyileştirmeler:**
- [ ] Serilog + Elasticsearch entegrasyonu
- [ ] OpenTelemetry + Jaeger distributed tracing
- [ ] Prometheus + Grafana ile metrics
- [ ] Correlation ID'yi tüm mikroservislerde otomatik log enrichment

---

**Tarih:** 27 Ocak 2026  
**Proje:** SkySync - Flight Reservation System  
**Ekleyen:** Correlation & Transaction ID Middleware Implementation


---

## 🧪 Correlation ID & Transaction ID - Test Guide

## Hızlı Test Senaryoları

### ✅ Test 1: Temel Kullanım (Correlation ID Gateway Tarafından Oluşturulur)

**Request:**
```bash
curl -X POST http://localhost:5000/api/reservation \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 1,
    "flightId": 1,
    "seatNumber": "12A",
    "passengerName": "Ahmet Güvendik",
    "passengerEmail": "ahmet@test.com",
    "passengerPhone": "+905551234567"
  }' \
  -i
```

**Beklenen Response Headers:**
```
HTTP/1.1 200 OK
X-Correlation-ID: <auto-generated-guid>
X-Transaction-ID: <auto-generated-guid>
Content-Type: application/json
```

**Beklenen Gateway Log:**
```
[15:30:00] [INFO] Request started. Method: POST, Path: /api/reservation, 
  CorrelationId: abc-123-xyz, TransactionId: tx-001-555
```

---

### ✅ Test 2: Client Correlation ID Gönderirse (Aynı ID Kullanılmalı)

**Request:**
```bash
curl -X POST http://localhost:5000/api/reservation \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: my-custom-tracking-id-12345" \
  -d '{
    "userId": 1,
    "flightId": 1,
    "seatNumber": "12B",
    "passengerName": "Test User",
    "passengerEmail": "test@test.com",
    "passengerPhone": "+905559999999"
  }' \
  -i
```

**Beklenen Response Headers:**
```
X-Correlation-ID: my-custom-tracking-id-12345  ← AYNI ID!
X-Transaction-ID: <yeni-guid>
```

**Beklenen Gateway Log:**
```
[15:31:00] [INFO] Correlation ID received from client: my-custom-tracking-id-12345
[15:31:00] [INFO] Request started. Method: POST, Path: /api/reservation, 
  CorrelationId: my-custom-tracking-id-12345, TransactionId: tx-002-666
```

---

### ✅ Test 3: Retry Scenario (Aynı Correlation ID, Farklı Transaction ID)

**Request 1:**
```bash
curl -X POST http://localhost:5000/api/reservation \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: user-retry-attempt-abc" \
  -d '{...}' \
  -i
```

**Request 2 (RETRY):**
```bash
curl -X POST http://localhost:5000/api/reservation \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: user-retry-attempt-abc" \
  -d '{...}' \
  -i
```

**Beklenen Log:**
```
# Request 1
[15:32:00] CorrelationId: user-retry-attempt-abc, TransactionId: tx-003-111

# Request 2 (RETRY)
[15:32:15] CorrelationId: user-retry-attempt-abc, TransactionId: tx-004-222
                            ↑ AYNI                              ↑ FARKLI
```

---

### ✅ Test 4: GET Request (Sadece Log Kontrolü)

**Request:**
```bash
curl -X GET http://localhost:5000/api/flight \
  -i
```

**Beklenen Response Headers:**
```
X-Correlation-ID: <auto-generated>
X-Transaction-ID: <auto-generated>
```

**Beklenen Log:**
```
[15:33:00] [INFO] Request started. Method: GET, Path: /api/flight, 
  CorrelationId: xyz-789-abc, TransactionId: tx-005-333
[15:33:01] [INFO] Request completed. Method: GET, Path: /api/flight | 
  Status: 200 | Duration: 123ms | CorrelationId: xyz-789-abc | TransactionId: tx-005-333
```

---

## 📋 Checklist: Ne Test Etmeliyim?

### Gateway Seviyesinde
- [ ] Correlation ID oluşturuluyor mu?
- [ ] Transaction ID oluşturuluyor mu?
- [ ] Response header'da var mı?
- [ ] Log'larda görünüyor mu?
- [ ] Client Correlation ID gönderirse kullanılıyor mu?

### Mikroservis Seviyesinde (Opsiyonel)
- [ ] Request header'da Correlation ID var mı?
- [ ] Saga event'lerinde Correlation ID kullanılıyor mu?
- [ ] Database'de Correlation ID saklanıyor mu?

---

## 🐛 Debugging

### Log Kontrolü

**Gateway Log'unda Ara:**
```bash
# Gateway loglarını göster
cat /path/to/gateway.log | grep "CorrelationId"

# Belirli bir Correlation ID'yi ara
cat /path/to/gateway.log | grep "abc-123-xyz"

# Son 100 satırda ara
tail -n 100 /path/to/gateway.log | grep "CorrelationId"
```

**Console Output (Development):**
```
info: SkySync.Gateway.Middleware.CorrelationIdMiddleware[0]
      Request started. Method: POST, Path: /api/reservation, 
      CorrelationId: 7a3c5f9e-1234-5678-9abc-def012345678, 
      TransactionId: 2b4d6f8e-5678-9abc-1234-567890abcdef

info: SkySync.Gateway.Middleware.RequestLoggingMiddleware[0]
      Incoming request: POST /api/reservation from 127.0.0.1 | 
      CorrelationId: 7a3c5f9e-1234-5678-9abc-def012345678 | 
      TransactionId: 2b4d6f8e-5678-9abc-1234-567890abcdef
```

### Database Kontrolü

```sql
-- Reservation'da Correlation ID var mı?
SELECT 
    ReservationId, 
    CorrelationId, 
    Status, 
    CreatedAt 
FROM Reservations 
WHERE CorrelationId = '7a3c5f9e-1234-5678-9abc-def012345678';

-- Payment'te Correlation ID var mı?
SELECT 
    PaymentId, 
    CorrelationId, 
    Status, 
    Amount 
FROM PaymentTransactions 
WHERE CorrelationId = '7a3c5f9e-1234-5678-9abc-def012345678';

-- Saga State'de Correlation ID var mı?
SELECT 
    CorrelationId, 
    CurrentState, 
    ReservationId 
FROM ReservationState 
WHERE CorrelationId = '7a3c5f9e-1234-5678-9abc-def012345678';
```

---

## 🎯 Başarı Kriterleri

### ✅ Test Geçti Eğer:
1. Her request Correlation ID alıyor
2. Her request Transaction ID alıyor
3. Response header'da her ikisi var
4. Log'larda her ikisi görünüyor
5. Client Correlation ID gönderirse aynı ID kullanılıyor
6. Retry durumunda Correlation ID aynı, Transaction ID farklı

### ❌ Test Başarısız Eğer:
1. Response header'da ID'ler yok
2. Log'larda "N/A" görünüyor
3. Client Correlation ID gönderdiğinde override ediliyor
4. Exception'da ID'ler loglanmıyor

---

## 🚀 Production'a Alırken

### Checklist:
- [ ] Middleware order doğru mu? (CorrelationId en başta)
- [ ] Structured logging kullanılıyor mu?
- [ ] Exception handling düzgün mü?
- [ ] Response header'lar client'a gidiyor mu?
- [ ] Dokümantasyon güncel mi?

### İzleme (Monitoring):
```bash
# Correlation ID kullanım oranı
grep -c "CorrelationId:" gateway.log

# Transaction ID kullanım oranı
grep -c "TransactionId:" gateway.log

# Exception'larda ID var mı?
grep "Request failed" gateway.log | grep "CorrelationId"
```

---

**Test Tarihi:** 27 Ocak 2026  
**Proje:** SkySync API Gateway  
**Feature:** Correlation ID & Transaction ID Middleware


---

## 🎉 Correlation ID & Transaction ID - Implementation Complete!

## 📦 Ne Eklendi?

### 1. ✅ CorrelationIdMiddleware
**Lokasyon:** `src/Infrastructure/SkySync.Gateway/Middleware/CorrelationIdMiddleware.cs`

**Özellikler:**
- ✅ Correlation ID oluşturma/okuma
- ✅ Transaction ID oluşturma
- ✅ HTTP Header'lara ekleme (request & response)
- ✅ HttpContext.Items'a ekleme
- ✅ Activity (OpenTelemetry) tag'leme
- ✅ Structured logging
- ✅ Exception handling

### 2. ✅ RequestLoggingMiddleware (Güncellendi)
**Lokasyon:** `src/Infrastructure/SkySync.Gateway/Middleware/RequestLoggingMiddleware.cs`

**Değişiklik:**
- ✅ Correlation ID log'lara eklendi
- ✅ Transaction ID log'lara eklendi

### 3. ✅ Program.cs (Güncellendi)
**Lokasyon:** `src/Infrastructure/SkySync.Gateway/Program.cs`

**Middleware Order:**
```csharp
app.UseHttpsRedirection();
app.UseCorrelationId();              // 1. İLK (ID'leri oluşturur)
app.UseRequestLogging();             // 2. SONRA (ID'leri kullanır)
app.UseRequestTransformation();      // 3. EN SON
```

---

## 🎯 Nasıl Çalışıyor?

### Request Flow:
```
1. Client → API Gateway
   Header: X-Correlation-ID (opsiyonel)

2. CorrelationIdMiddleware
   ├─ Correlation ID var mı? → Kullan
   ├─ Yok mu? → Oluştur
   └─ Transaction ID → Her zaman yeni

3. HttpContext.Items
   ├─ CorrelationId: abc-123-xyz
   └─ TransactionId: tx-001-555

4. Response Headers
   ├─ X-Correlation-ID: abc-123-xyz
   └─ X-Transaction-ID: tx-001-555

5. Mikroservislere İlet (YARP)
   Request Header: X-Correlation-ID, X-Transaction-ID
```

---

## 📊 Örnekler

### Örnek 1: Normal Request
```bash
curl -X POST http://localhost:5000/api/reservation \
  -H "Content-Type: application/json" \
  -d '{"flightId":1,"seatNumber":"12A"}'

# Response Headers:
# X-Correlation-ID: 7a3c5f9e-1234-5678-9abc-def012345678
# X-Transaction-ID: 2b4d6f8e-5678-9abc-1234-567890abcdef
```

### Örnek 2: Client Correlation ID Gönderirse
```bash
curl -X POST http://localhost:5000/api/reservation \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: my-tracking-id-12345" \
  -d '{"flightId":1,"seatNumber":"12A"}'

# Response Headers:
# X-Correlation-ID: my-tracking-id-12345 (AYNI ID!)
# X-Transaction-ID: <yeni-guid>
```

---

## 📚 Dokümantasyon

### 📄 Detaylı Açıklama:
**Dosya:** `CORRELATION_AND_TRANSACTION_IDS.md`
- Kavramsal açıklama
- Implementation detayları
- Best practices
- Production debugging

### 🧪 Test Guide:
**Dosya:** `CORRELATION_ID_TEST_GUIDE.md`
- Test senaryoları
- cURL örnekleri
- Beklenen sonuçlar
- Debugging komutları

---

## 🚀 Hemen Test Et!

### 1. Gateway'i Çalıştır:
```bash
cd src/Infrastructure/SkySync.Gateway
dotnet run
```

### 2. Test Request Gönder:
```bash
curl -X GET http://localhost:5000/ -i
```

### 3. Response Header'ları Kontrol Et:
```
HTTP/1.1 200 OK
X-Correlation-ID: <guid>
X-Transaction-ID: <guid>
```

### 4. Console Log'una Bak:
```
[15:30:00] [INFO] Request started. Method: GET, Path: /, 
  CorrelationId: abc-123, TransactionId: tx-001
```

---

## ✅ Başarı Kriterleri

### Gateway Seviyesinde:
- [x] Correlation ID oluşturuluyor
- [x] Transaction ID oluşturuluyor
- [x] Response header'a ekleniyor
- [x] Request header'a ekleniyor (mikroservislere gidiyor)
- [x] Log'larda görünüyor
- [x] Build başarılı (0 warning, 0 error)

### Mikroservis Seviyesinde (Zaten Var):
- [x] Saga Pattern'de Correlation ID kullanılıyor
- [x] Commands/Events'de Correlation ID var
- [x] Database entity'lerinde Correlation ID var
- [x] Log'larda Correlation ID var

---

## 🎓 Öğrenilenler

### 1. Correlation ID vs Transaction ID
| Özellik | Correlation ID | Transaction ID |
|---------|---------------|----------------|
| Kapsam | Tüm sistem | Sadece Gateway |
| Ömür | End-to-end | Tek request |
| Kullanım | Distributed tracing | Request logging |

### 2. Middleware Order
```
UseCorrelationId()     → ID'leri oluştur
UseRequestLogging()    → ID'leri kullan
UseTransformation()    → Diğer işlemler
```

### 3. Production Best Practices
- ✅ Client Correlation ID gönderirse kullan
- ✅ Response header'a ekle (client debugging)
- ✅ Request header'a ekle (downstream services)
- ✅ Structured logging kullan
- ✅ Exception'da ID'leri logla

---

## 🔧 Gelecek İyileştirmeler

### Öncelik: Yüksek
- [ ] Serilog + Elasticsearch entegrasyonu
- [ ] OpenTelemetry + Jaeger distributed tracing
- [ ] Correlation ID'yi tüm mikroservislerde HttpContext'ten okuma

### Öncelik: Orta
- [ ] Prometheus + Grafana ile metrics
- [ ] Custom correlation ID format (daha okunabilir)
- [ ] Correlation ID TTL ve cleanup

### Öncelik: Düşük
- [ ] Correlation ID validation
- [ ] Custom header isimleri (config'den)
- [ ] Correlation ID persistence (Redis)

---

## 📈 Sonraki Adımlar

### 1. Test Et (5 dakika)
```bash
# Gateway'i çalıştır
dotnet run --project src/Infrastructure/SkySync.Gateway

# Test request gönder
curl -X GET http://localhost:5000/ -i
```

### 2. Dokümantasyonu Oku (10 dakika)
- `CORRELATION_AND_TRANSACTION_IDS.md`
- `CORRELATION_ID_TEST_GUIDE.md`

### 3. Mikroservislere Ekle (Opsiyonel, 30 dakika)
```csharp
// Her mikroserviste
builder.Services.AddHttpContextAccessor();

// Controller'da
var correlationId = _httpContextAccessor.HttpContext?
    .Request.Headers["X-Correlation-ID"]
    .FirstOrDefault();
```

---

## 🎊 Tebrikler!

Projenize **production-grade distributed tracing** özelliği eklediniz! 🚀

**Ekleyen:** Correlation ID & Transaction ID Middleware  
**Tarih:** 27 Ocak 2026  
**Status:** ✅ Complete (Build: Success, Warnings: 0)

---

**Soru & Destek:**  
Herhangi bir sorunuz varsa dokümantasyonu okuyun veya test guide'ı takip edin! 💪


---

## 🆕 Flight Created Notification Feature

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


---

## ✉️ Serilog ile Loglama – Yol Haritası (5 Servis)

Senior yaklaşımı: yapılandırılmış loglama, tek tip konfigürasyon, request logging, ortam bazlı seviyeler.

---

## 1. Hedefler

| Hedef | Açıklama |
|-------|----------|
| **Yapılandırılmış log** | JSON / key-value; log agregatörde (Seq, ELK) filtreleme ve arama |
| **Servis kimliği** | Her log satırında `ServiceName` (Flight, Reservation, Identity, Notification, Payment) |
| **Request logging** | Her HTTP isteği: Method, Path, StatusCode, Duration |
| **Konfigürasyondan** | `appsettings.json` ile MinimumLevel, sink ayarları |
| **Ortam ayrımı** | Development: detaylı; Production: sadece gerekli |

---

## 2. Paketler (Her WebApi’ye)

```
Serilog.AspNetCore
Serilog.Enrichers.Environment
Serilog.Enrichers.Thread
Serilog.Sinks.Console
Serilog.Sinks.File
```

Opsiyonel (Seq/ELK kullanılacaksa):

```
Serilog.Sinks.Seq
```

---

## 3. appsettings.json (Her serviste aynı yapı)

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

`appsettings.Development.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.AspNetCore": "Warning"
      }
    }
  }
}
```

---

## 4. Program.cs Akışı (Her serviste)

1. **Builder’dan önce:** Serilog’u `Configuration` ile kur (LoggerConfiguration).
2. **Builder:** `builder.Host.UseSerilog()`.
3. **Pipeline:** `app.UseSerilogRequestLogging()` (request başına bir log satırı).
4. **Kapanış:** `await app.RunAsync()` ve `Log.CloseAndFlush()` (opsiyonel; ASP.NET Core genelde flush eder).

---

## 5. Ortak Konfigürasyon Kalıbı

Her serviste sadece **ServiceName** değişir:

- Flight   → `ServiceName = "Flight"`
- Reservation → `ServiceName = "Reservation"`
- Identity → `ServiceName = "Identity"`
- Notification → `ServiceName = "Notification"`
- Payment  → `ServiceName = "Payment"`

Enricher’lar: `EnvironmentName`, `MachineName`, `ThreadId`, `ServiceName` (custom property).

---

## 6. Uygulama Sırası (Adımlar)

| Adım | İş | Dosyalar |
|------|----|----------|
| 1 | Flight WebApi: Paketler + Serilog config + Program + appsettings | .csproj, Program.cs, appsettings*.json |
| 2 | Reservation WebApi: Aynı yapı, ServiceName = Reservation | Aynı |
| 3 | Identity WebApi: Aynı yapı, ServiceName = Identity | Aynı |
| 4 | Notification WebApi: Aynı yapı, ServiceName = Notification | Aynı |
| 5 | Payment WebApi: Aynı yapı, ServiceName = Payment | Aynı |

---

## 7. Request Logging Örneği

`UseSerilogRequestLogging()` ile örnek çıktı:

```
[INF] HTTP POST /api/flight responded 201 in 245 ms
  ServiceName: Flight, Path: /api/flight, Method: POST, StatusCode: 201, Elapsed: 245
```

İstersen custom template ile daha az/çok alan eklenebilir.

---

## 8. Opsiyonel: Ortak Extension (DRY)

Tekrarı azaltmak için **SkySync.Shared** veya yeni **SkySync.Infrastructure.Logging** projesinde:

- `AddSerilogConfiguration(builder, serviceName)`  
- `UseSerilogRequestLogging(app)` (veya extension içinde)

Tüm servisler bu extension’ı çağırır; sadece `serviceName` parametresi değişir.

---

## 9. Özet Checklist

- [ ] Her WebApi’ye Serilog paketleri ekle
- [ ] appsettings / appsettings.Development’a Serilog bölümünü ekle
- [ ] Program.cs: UseSerilog + enrichers (ServiceName dahil)
- [ ] Program.cs: UseSerilogRequestLogging()
- [ ] (Opsiyonel) File sink: logs/ klasörü, rolling (günlük), JSON format
- [ ] (Opsiyonel) Seq/ELK sink; production’da merkezi log

**Seq Docker (EULA + ilk çalıştırma):**
```bash
docker run -d --name seq \
  -e ACCEPT_EULA=Y \
  -e SEQ_FIRSTRUN_NOAUTHENTICATION=Y \
  -p 5341:80 -p 5342:5341 \
  datalust/seq:latest
```
UI: http://localhost:5341 (giriş yok). Production’da şifre kullan: `SEQ_FIRSTRUN_ADMINPASSWORD=<şifre>`.

Bu yol haritasına göre adım adım uygulayabilirsin; istersen bir sonraki adımda Flight servisi için somut kod örneğini de yazabilirim.


---

## Gereksiz / Kullanılmayan Öğeler Tespiti

## 1. Shared Events (Kaldırılabilir)

| Dosya | Durum | Açıklama |
|-------|-------|----------|
| ~~`PaymentAuthorizedEvent.cs`~~ | ✅ Silindi | Payment artık `PaymentCompletedEvent` publish ediyor |
| ~~`PaymentCapturedEvent.cs`~~ | ✅ Silindi | CapturePaymentCommand kaldırıldı |
| ~~`ReservationTimeoutEvent.cs`~~ | ✅ Silindi | Saga `PaymentTimeoutEvent` kullanıyor |

---

## 2. Gateway

| Öğe | Durum | Açıklama |
|-----|-------|----------|
| `Polly` paketi | ❌ Kullanılmıyor | ResiliencePolicies hiçbir yerde çağrılmıyor |
| `Resilience/CircuitBreakerPolicy.cs` | ❌ Kullanılmıyor | Program.cs'de kullanılmıyor, YARP kendi circuit breaker'ını appsettings'te kullanıyor |
| `ResilienceSettings` (appsettings) | ⚠️ Kullanılmıyor | Sadece kullanılmayan CircuitBreakerPolicy tarafından okunuyordu |
| `JwtConfiguration` + AddAuthentication | ⚠️ Kısmen | JWT ayarlı ama UseAuthentication/UseAuthorization kapalı – ileride kullanılacaksa tutulabilir |

---

## 3. Workers

| Öğe | Durum | Açıklama |
|-----|-------|----------|
| `SkySync.Workers.EventStore` | ❌ Boş/Placeholder | Sadece "Worker running at..." loglayan dummy worker. EventStore mantığı yok |

---

## 4. Payment Servisi

| Öğe | Durum | Açıklama |
|-----|-------|----------|
| `SkySync.Services.Payment.Application` | ❌ Kullanılmıyor | Sadece boş `Class1.cs`, WebApi Persistence'a doğrudan referans veriyor |
| `SkySync.Services.Payment.Infrastructure` | ❌ Boş proje | Hiç dosya yok, hiçbir proje referans vermiyor |

---

## 5. Dokümantasyon

| Dosya | Öneri |
|-------|-------|
| 8 adet .md dosyası | LEARNING_NOTES, README_CORRELATION... vb. eğitim/not amaçlı; silmek zorunda değilsin |
| EVENTS_CONSUMERS_SAGA_DOKUMAN.md | PaymentAuthorizedEvent vs PaymentCompletedEvent notunu güncelle (artık düzeltildi) |

---

## Özet Aksiyon Listesi

### Kolay / Düşük Risk
1. ~~`PaymentAuthorizedEvent.cs`~~ ✅ Silindi
2. ~~`PaymentCapturedEvent.cs`~~ ✅ Silindi
3. ~~`ReservationTimeoutEvent.cs`~~ ✅ Silindi
4. ~~Gateway'den `Polly` paketini kaldır~~ ✅ Yapıldı
5. ~~Gateway'den `Resilience/CircuitBreakerPolicy.cs` → Sil~~ ✅ Yapıldı
6. Gateway appsettings'ten `ResilienceSettings` → (zaten yoktu)

### Orta Risk (Proje yapısı değişikliği)
7. `SkySync.Workers.EventStore` → Solution'dan çıkar veya gerçek EventStore işlevi ekle
8. `SkySync.Services.Payment.Application` → Kaldır veya CQRS ekle
9. `SkySync.Services.Payment.Infrastructure` → Solution'dan çıkar (boş proje)

### Tutulması Önerilen
- JWT config → İleride auth açılacaksa kalmalı
- RequestTransformationMiddleware, CorrelationIdMiddleware → Kullanılıyor
- RequestLoggingMiddleware → Kullanılıyor


---

## 📚 SKYSYNC PROJECT - LEARNING & DEVELOPMENT NOTES

**Proje:** SkySync - Uçak Bileti Rezervasyon Sistemi  
**Tarih:** 26 Ocak 2026  
**Mimari:** Microservices + Event-Driven + Saga Pattern  
**Teknoloji Stack:** .NET 9, RabbitMQ, Redis, PostgreSQL, MongoDB, YARP

---

## 🎯 PROJE ÖZETI

### Ne Yaptık?
Production-grade bir uçak bileti rezervasyon sistemi:
- **4 Microservice**: Flight, Reservation, Payment, Notification
- **API Gateway**: YARP ile load balancing, rate limiting, circuit breaker
- **Saga Pattern**: Distributed transaction management
- **Event-Driven**: RabbitMQ ile asenkron messaging
- **Distributed Lock**: Redis ile race condition önleme
- **Caching**: Redis Cache Aside Pattern

### Proje Puanı
- **Mimari Kalitesi:** 9/10
- **Code Quality:** 8/10
- **Production Readiness:** 5/10 (Test, Docker, Observability eksik)
- **Senior Pattern Usage:** 9/10

---

## 📖 ÖĞRENDIĞIM KONSEPTLER VE PATTERN'LER

### 1. MICROSERVICES ARCHITECTURE ✅

#### Ne Öğrendim?
- Her servisin kendi veritabanı olması (Database per Service)
- Servisler arası gevşek bağlantı (Loose Coupling)
- Bağımsız deploy edilebilme (Independent Deployment)
- Servis sınırları belirleme (Bounded Context)

#### Projede Nasıl Uygulandı?
```
Flight Service → PostgreSQL (Uçuş ve Koltuk bilgileri)
Reservation Service → PostgreSQL (Rezervasyon bilgileri)
Payment Service → PostgreSQL (Ödeme transaction'ları)
Notification Service → Email gönderimi
Saga State Machine → MongoDB (Saga state persistence)
```

#### Kritik Sorular Kendime:
- ✅ Neden her servisin kendi DB'si var? → **Loose coupling, independent scaling**
- ✅ Servisler birbirinin DB'sine direkt bağlanabilir mi? → **HAYIR! Anti-pattern**
- ⚠️ Service boundaries'leri nasıl belirledin? → **Domain-Driven Design prensipleri**
- ❓ Microservice sayısını nasıl optimize edersin? → **Araştırmalıyım**

#### Eksik Kalan:
- [ ] Service Mesh (Istio, Linkerd) deneyimi yok
- [ ] Service versioning stratejisi belirsiz
- [ ] Service discovery (Eureka, Consul) yok (ekleyeceğiz)

---

### 2. SAGA PATTERN (ORCHESTRATION) ✅✅

#### Ne Öğrendim?
- Distributed transaction'ları yönetme
- Compensating transactions (geri alma)
- State Machine ile workflow yönetimi
- CorrelationId ile işlem takibi

#### Projede Nasıl Uygulandı?
```
Rezervasyon Akışı:
1. User → ReservationStarted Event
2. Saga → ReserveSeatCommand (Flight Service)
3. Flight → FlightReserved Event
4. Saga → ProcessPaymentCommand (Payment Service)
5. Payment → PaymentCompleted Event
6. Saga → ReservationConfirmed Event
7. Notification → Email gönder

HATA DURUMU (Compensate):
5. Payment → PaymentFailed Event
6. Saga → ReleaseSeatCommand (Flight Service)
7. Flight → Koltuğu geri aç
```

#### Kritik Sorular Kendime:
- ✅ Saga neden gerekli? → **Microservices'te atomic transaction yok**
- ✅ Compensate ne demek? → **Hata durumunda geri alma işlemi**
- ✅ CorrelationId neden önemli? → **Saga instance'ını takip etmek için**
- ⚠️ Saga timeout olursa ne olur? → **Araştırmalıyım**
- ❓ Saga vs 2PC (Two-Phase Commit) farkı? → **Öğrenmeliyim**

#### MassTransit State Machine Code:
```csharp
// Başlangıç: Reservation Started
Initially(
    When(ReservationStarted)
        .Send(ReserveSeatCommand)
        .TransitionTo(AwaitingFlightReservation));

// Success: Payment Completed → Finalize
When(PaymentCompleted)
    .Publish(ReservationConfirmedEvent)
    .Finalize()

// Failure: Payment Failed → Compensate → Finalize
When(PaymentFailed)
    .Publish(ReleaseSeatCommand)  // ← COMPENSATE!
    .Finalize()
```

#### Eksik Kalan:
- [ ] Saga timeout handling yok
- [ ] Saga retry mechanism detayları belirsiz
- [ ] Saga visualization tool yok (process diagram)
- [ ] Choreography pattern ile karşılaştırma yapamadım

---

### 3. DISTRIBUTED LOCK (RACE CONDITION PREVENTION) ✅✅✅

#### Ne Öğrendim?
- Race condition nedir ve nasıl önlenir
- Redis ile distributed lock mekanizması
- Lock timeout ve deadlock prevention
- Atomic operations (SET NX EX)

#### Projede Nasıl Uygulandı?
```csharp
// Problem: 2 kullanıcı aynı anda son koltuğu alıyor
// Çözüm: Redis distributed lock

var lockKey = $"seat:{FlightId}:{SeatNumber}";
var lock = await _cacheService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(5));

if (lock == null || !lock.IsAcquired)
{
    // Başka biri bu koltuğu işliyor, reddedildi
    return FlightReservationFailedEvent;
}

try
{
    // Lock alındı, güvenli şekilde rezervasyon yap
    seat.IsReserved = true;
    await SaveChangesAsync();
}
finally
{
    // Lock'u mutlaka serbest bırak
    await _cacheService.ReleaseLockAsync(lock);
}
```

#### Redis Lock Implementation:
```csharp
// SET NX EX - Atomic lock acquisition
await database.StringSetAsync(lockKey, lockValue, expiry, When.NotExists);

// Lua script ile safe release (sadece kendi lock'unu sil)
var script = @"
    if redis.call('get', KEYS[1]) == ARGV[1] then
        return redis.call('del', KEYS[1])
    else
        return 0
    end";
```

#### Kritik Sorular Kendime:
- ✅ Lock timeout neden gerekli? → **Deadlock önleme**
- ✅ Lock key pattern'i nasıl? → **seat:{FlightId}:{SeatNumber}**
- ✅ Finally block neden kritik? → **Exception olsa bile lock release edilmeli**
- ⚠️ Redis cluster'da clock skew sorunu? → **RedLock algoritması (araştır)**
- ❓ Postgres advisory lock vs Redis lock? → **Trade-off'ları öğren**

#### Eksik Kalan:
- [ ] RedLock algorithm detayları (multi-master Redis)
- [ ] Lock monitoring/alerting yok
- [ ] ReleaseSeatCommandConsumer'da lock yok (eklenebilir)

---

### 4. IDEMPOTENCY PATTERN ✅

#### Ne Öğrendim?
- Aynı işlemin tekrar edilmesi durumunda duplicate işlem yapılmaması
- Payment'te özellikle kritik (2 kez ödeme çekilmemeli)
- Idempotency key ile işlem kontrolü

#### Projede Nasıl Uygulandı?
```csharp
// Payment Service - ProcessPaymentConsumer
public async Task Consume(ConsumeContext<ProcessPaymentCommand> context)
{
    // 1. İdempotency Check
    var existingTransaction = await _context.PaymentTransactions
        .FirstOrDefaultAsync(x => 
            x.ReservationId == msg.ReservationId && 
            x.Status == "Success");

    if (existingTransaction != null)
    {
        // Zaten işlendi, cached sonuç döndür
        return PaymentCompletedEvent; // Duplicate işlem yapma!
    }

    // 2. Yeni işlem, ödeme yap
    // ...
}
```

#### Kritik Sorular Kendime:
- ✅ Idempotency neden önemli? → **Network retry, message duplicate durumları**
- ✅ Idempotency key nedir? → **ReservationId (unique identifier)**
- ⚠️ Cache ne kadar süre tutulmalı? → **Business rule'a göre (30 gün?)**
- ❓ GET request'ler doğal idempotent mi? → **Evet, ama side-effect varsa dikkat**

#### Eksik Kalan:
- [ ] Idempotency TTL (cache expiration) belirlenmemiş
- [ ] Idempotency key tracking table yok (audit için)
- [ ] Failed transaction retry'lar idempotent mi? (kontrol et)

---

### 5. EVENT-DRIVEN ARCHITECTURE ✅

#### Ne Öğrendim?
- Servisler arası asenkron iletişim
- Publish-Subscribe pattern
- Event versioning
- Eventually consistent data

#### Projede Kullanılan Event'ler:
```
Commands (Imperative - "Şunu yap"):
├── ReserveSeatCommand → Flight Service'e gönder
├── ProcessPaymentCommand → Payment Service'e gönder
└── ReleaseSeatCommand → Compensate için

Events (Past tense - "Şu oldu"):
├── ReservationStartedEvent → Saga başlatır
├── FlightReservedEvent → Saga'ya bildirir
├── PaymentCompletedEvent → Saga'ya başarı bildirir
├── PaymentFailedEvent → Compensate tetikler
└── ReservationConfirmedEvent → Notification'a bildirir
```

#### Command vs Event Farkı:
```
COMMAND:
- Imperative (emir kipi): "ReserveSeat"
- Tek consumer: Flight Service
- Sync/async olabilir
- Response bekleyebilir

EVENT:
- Past tense (geçmiş zaman): "FlightReserved"
- Multiple consumer: Saga, Analytics, Audit...
- Async: Fire-and-forget
- Response beklenmez
```

#### Kritik Sorular Kendime:
- ✅ Event vs Command farkı? → **Yukarıda ✓**
- ✅ Event versioning neden gerekli? → **Schema değişikliklerinde backward compatibility**
- ⚠️ Event ordering guarantee var mı? → **RabbitMQ default sıralı, ama partition'da?**
- ❓ Event Sourcing vs Event-Driven farkı? → **Araştır**

#### Eksik Kalan:
- [ ] Event versioning strategy yok (v1, v2 event'leri)
- [ ] Event schema registry yok (Avro, Protobuf)
- [ ] Dead letter queue handling minimal
- [ ] Event replay mechanism yok

---

### 6. CQRS (COMMAND QUERY RESPONSIBILITY SEGREGATION) ✅

#### Ne Öğrendim?
- Read ve Write işlemlerini ayırma
- Farklı veri modelleri kullanma
- Performance optimization

#### Projede Nasıl Uygulandı?
```csharp
// WRITE (Command):
CreateFlightCommandHandler
- Transactional write
- Outbox pattern ile event publish
- Domain validation

// READ (Query):
GetAllFlightsQueryHandler
- Cache Aside pattern
- Distributed lock ile cache stampede prevention
- Read-optimized model
```

#### Cache Aside Pattern:
```csharp
// 1. Cache'e bak
var cachedFlights = await _cacheService.GetAsync<List<Flight>>("flights:all");
if (cachedFlights != null)
    return cachedFlights; // Cache hit

// 2. Lock al (cache stampede prevention)
var lock = await _cacheService.AcquireLockAsync("lock:flights:all");

// 3. DB'den oku
var flights = await _context.Flights.ToListAsync();

// 4. Cache'e yaz
await _cacheService.SetAsync("flights:all", flights, TimeSpan.FromMinutes(10));

// 5. Lock'u bırak
```

#### Kritik Sorular Kendime:
- ✅ CQRS neden kullanılır? → **Read/Write farklı scale etmek için**
- ⚠️ Tam CQRS var mı? → **Kısmi, sadece query'lerde cache var**
- ❓ Event Sourcing ile CQRS ilişkisi? → **Araştır**
- ❓ Read DB ayrı olabilir mi? → **Evet, replica veya farklı DB (ElasticSearch)**

#### Eksik Kalan:
- [ ] Tam CQRS implementation yok (read/write DB ayrı değil)
- [ ] Event Sourcing yok
- [ ] Query projection'lar yok

---

### 7. API GATEWAY PATTERN (YARP) ✅

#### Ne Öğrendim?
- Reverse proxy kullanımı
- Request routing
- Load balancing
- Rate limiting

#### Gateway Özellikleri:
```
✅ JWT Authentication (geçici kapalı)
✅ CORS Policy
✅ Rate Limiting (100 req/min)
✅ Circuit Breaker (Polly)
✅ Retry Policy
✅ Timeout Policy
✅ Load Balancing (RoundRobin)
✅ Health Checks
✅ Request/Response Transformation
```

#### Routing Configuration:
```json
{
  "Routes": {
    "flight-route": {
      "ClusterId": "flight-cluster",
      "Match": { "Path": "/api/flight/{**catch-all}" }
    }
  },
  "Clusters": {
    "flight-cluster": {
      "Destinations": {
        "flight1": { "Address": "http://localhost:5041" }
      },
      "LoadBalancingPolicy": "RoundRobin"
    }
  }
}
```

#### Kritik Sorular Kendime:
- ✅ Gateway neden gerekli? → **Single entry point, centralized security/monitoring**
- ✅ YARP vs Ocelot? → **YARP Microsoft'un resmi projesi, daha performanslı**
- ⚠️ Gateway single point of failure mı? → **Evet, HA için multiple gateway instance**
- ❓ Service mesh vs Gateway? → **Araştır (Istio, Linkerd)**

#### 🆕 YENI EKLENDİ (27 Ocak 2026):
- ✅ **Correlation ID Middleware** - Distributed tracing için
- ✅ **Transaction ID Middleware** - Request tracking için
- ✅ Response header'a ID'ler ekleniyor
- ✅ Request header'a ID'ler ekleniyor (mikroservislere propagate)
- ✅ RequestLoggingMiddleware ID'leri kullanıyor
- 📄 Detaylı dokümantasyon: `CORRELATION_AND_TRANSACTION_IDS.md`

#### Eksik Kalan:
- [ ] Gateway HA (High Availability) yok
- [ ] API versioning yok
- [ ] Request throttling per-user yok
- [ ] Gateway metrics/monitoring yok
- [ ] OpenTelemetry + Jaeger integration yok

---

### 8. RESILIENCE PATTERNS ✅

#### Ne Öğrendim?
- Circuit Breaker pattern
- Retry with exponential backoff
- Timeout policy
- Fail-fast vs Fail-safe

#### Polly Implementation:
```csharp
// 1. Timeout Policy
var timeout = Policy
    .TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));

// 2. Retry Policy (Exponential backoff)
var retry = Policy
    .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// 3. Circuit Breaker
var circuitBreaker = Policy
    .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30));

// 4. Combine (Wrap)
var resilience = Policy.WrapAsync(timeout, retry, circuitBreaker);
```

#### Circuit Breaker States:
```
CLOSED → Service healthy, requests geçiyor
    ↓ (5 failure)
OPEN → Service unhealthy, requests direkt fail
    ↓ (30 seconds)
HALF-OPEN → Test request, service recovered mi?
    ↓ (success)
CLOSED → Recovered!
```

#### Kritik Sorular Kendime:
- ✅ Circuit breaker neden gerekli? → **Cascading failure önleme**
- ✅ Retry her zaman iyi mi? → **HAYIR, idempotent işlemler için**
- ⚠️ Timeout değerini nasıl belirledin? → **30s default, ama optimize edilmeli**
- ❓ Bulkhead pattern nedir? → **Araştır**

#### Eksik Kalan:
- [ ] Bulkhead pattern yok (resource isolation)
- [ ] Fallback responses yok
- [ ] Resilience metrics yok

---

### 9. OUTBOX PATTERN ✅

#### Ne Öğrendim?
- Transactional outbox pattern
- Event publishing güvenliği
- Worker ile background processing

#### Nasıl Çalışıyor?
```
1. Transaction başla
   ├─ Entity kaydet (Flight)
   ├─ OutboxMessage kaydet (FlightCreatedEvent)
   └─ Commit

2. Worker (Background Job):
   - OutboxMessage'ları oku (Processed = false)
   - RabbitMQ'ya publish et
   - Processed = true yap
   
→ Event kaybı olmaz, eventual consistency garantisi
```

#### Kritik Sorular Kendime:
- ✅ Neden DB'ye event yazıyoruz? → **Transaction atomicity, message kaybı yok**
- ⚠️ Worker fail olursa? → **Retry eder, idempotent olmalı**
- ❓ Inbox pattern ile farkı? → **Araştır (consumer tarafında deduplication)**

#### Eksik Kalan:
- [ ] Inbox pattern yok (duplicate event handling)
- [ ] Outbox cleanup job yok (eski mesajları sil)
- [ ] Outbox monitoring yok

---

## 🛠️ KULLANILAN TEKNOLOJİLER

### Backend Framework
- **.NET 9** - Latest LTS version
- **ASP.NET Core Web API** - RESTful API
- **Entity Framework Core** - ORM

### Messaging & Events
- **MassTransit** - Saga orchestration, messaging abstraction
- **RabbitMQ** - Message broker

### Data Storage
- **PostgreSQL** - Relational DB (Flight, Reservation, Payment)
- **MongoDB** - Document DB (Saga state persistence)
- **Redis** - Distributed cache & lock

### Gateway & Proxy
- **YARP (Yet Another Reverse Proxy)** - API Gateway
- **Polly** - Resilience & transient fault handling

### Cross-Cutting
- **AspNetCoreRateLimit** - Rate limiting
- **MediatR** - CQRS, Command/Query pattern
- **Microsoft.Extensions.Logging** - Logging abstraction

---

## ⚠️ EKSİK OLAN ALANLAR (ÖĞRENMELIYIM)

### 1. TESTING (🔴 Kritik)
```
❌ Unit Tests yok
❌ Integration Tests yok
❌ E2E Tests yok
❌ Test Coverage: %0
```

**Yapmalıyım:**
- [ ] xUnit ile unit test yazma
- [ ] MassTransit Test Harness (Saga testing)
- [ ] WebApplicationFactory (Integration testing)
- [ ] Testcontainers (Docker-based testing)

**Kaynak:**
- Martin Fowler - Test Pyramid
- Microsoft Docs - Testing in ASP.NET Core

---

### 2. OBSERVABILITY & MONITORING (🔴 Kritik)
```
❌ Distributed Tracing yok (OpenTelemetry, Jaeger)
❌ Centralized Logging yok (Serilog + ELK)
❌ Metrics yok (Prometheus, Grafana)
❌ APM yok (Application Performance Monitoring)
```

**Yapmalıyım:**
- [ ] OpenTelemetry + Jaeger kurulumu
- [ ] Serilog + Elasticsearch + Kibana
- [ ] Prometheus + Grafana
- [ ] Trace context propagation

**Neden Önemli:**
Production'da "Neden yavaş?" sorusuna cevap veremem.

---

### 3. CONTAINERIZATION & ORCHESTRATION (🔴 Kritik)
```
❌ Dockerfile yok
❌ docker-compose.yml yok
❌ Kubernetes manifests yok
❌ Helm charts yok
```

**Yapmalıyım:**
- [ ] Her service için Dockerfile
- [ ] docker-compose.yml (tüm stack)
- [ ] Kubernetes deployment/service/ingress
- [ ] Helm chart

**Şu anki sorun:**
Yeni developer projeyi nasıl çalıştıracak? 10 adım manuel kurulum.

---

### 4. CI/CD PIPELINE (🔴 Kritik)
```
❌ GitHub Actions yok
❌ Azure DevOps pipeline yok
❌ Automated testing yok
❌ Automated deployment yok
```

**Yapmalıyım:**
- [ ] GitHub Actions workflow (.NET build, test, publish)
- [ ] Docker image build & push
- [ ] Kubernetes deployment automation
- [ ] Environment management (dev, staging, prod)

---

### 5. SECURITY (🟡 Önemli)
```
⚠️ JWT Authentication var ama kapalı
❌ Authorization policies minimal
❌ API Key yok
❌ OAuth2 / OpenID Connect yok
❌ Secrets management (Azure Key Vault, AWS Secrets Manager) yok
```

**Yapmalıyım:**
- [ ] JWT production'a hazır hale getir
- [ ] Role-based authorization
- [ ] Azure Key Vault integration
- [ ] HTTPS enforcement
- [ ] Input validation & sanitization

---

### 6. API VERSIONING (🟡 Önemli)
```
❌ API versioning yok
❌ Breaking change strategy yok
❌ Deprecation policy yok
```

**Yapmalıyım:**
```csharp
[Route("api/v1/[controller]")]
[ApiVersion("1.0")]
public class FlightController : ControllerBase
{
    [MapToApiVersion("1.0")]
    public IActionResult Get() { }
    
    [MapToApiVersion("2.0")]
    public IActionResult GetV2() { }
}
```

---

### 7. ERROR HANDLING (🟡 Önemli)
```
⚠️ Try-catch var ama standardize değil
❌ Global exception handler yok
❌ Problem Details (RFC 7807) yok
❌ Error codes standardize edilmemiş
```

**Yapmalıyım:**
```csharp
// Problem Details (RFC 7807)
{
  "type": "https://api.skysync.com/errors/seat-unavailable",
  "title": "Seat Already Reserved",
  "status": 409,
  "detail": "Seat 12A on flight TK1903 is already reserved",
  "instance": "/api/reservation/123",
  "traceId": "00-abc123-xyz789-00"
}
```

---

### 8. DATABASE STRATEGY (🟡 Önemli)
```
⚠️ EF Migrations var ama stratejisi belirsiz
❌ Seed data yok
❌ Backup strategy yok
❌ Connection pooling configure edilmemiş
❌ Database sharding/partitioning yok
```

**Yapmalıyım:**
- [ ] Migration scripts organize et
- [ ] Seed data ile test ortamı hazırla
- [ ] Backup/restore procedure
- [ ] Connection pool tuning

---

### 9. SERVICE DISCOVERY (🟢 Gelecek Özellik)
```
❌ Eureka yok
❌ Consul yok
❌ Dynamic service registration yok
```

**Ekleyeceğiz:**
- [ ] Eureka Server (Docker)
- [ ] Steeltoe.Discovery.Eureka (her service'te)
- [ ] Gateway dynamic routing

---

### 10. DOCUMENTATION (🟡 Önemli)
```
✅ API_ENDPOINTS.md var
✅ Gateway README.md var
❌ Architecture diagram yok
❌ Sequence diagram yok
❌ Developer onboarding guide yok
❌ Detailed Swagger docs yok
```

**Yapmalıyım:**
- [ ] C4 Model architecture diagram
- [ ] Saga flow sequence diagram
- [ ] Developer setup guide
- [ ] API documentation (detailed Swagger)

---

## 🎯 INTERVIEW HAZIRLIĞI

### Teknik Sorular ve Cevapları

#### Microservices

**Q: Microservices nedir? Avantajları ve dezavantajları?**

**A:**
```
Avantajlar:
✅ Independent deployment
✅ Technology heterogeneity
✅ Scalability (service başına scale)
✅ Fault isolation
✅ Team autonomy

Dezavantajlar:
❌ Distributed system complexity
❌ Network latency
❌ Data consistency challenges
❌ Testing complexity
❌ Operational overhead
```

---

**Q: Bu projede kaç microservice var ve neden?**

**A:**
```
4 ana servis:
1. Flight Service - Uçuş ve koltuk yönetimi
2. Reservation Service - Rezervasyon orchestration
3. Payment Service - Ödeme işlemleri
4. Notification Service - Email/SMS bildirimleri

Ayrıca:
- API Gateway (YARP)
- Saga State Machine (MassTransit)
- Workers (Outbox publisher)

Neden bu kadar?
→ Her servis tek bir domain responsibility
→ Independent scaling (Payment farklı scale)
→ Fault isolation (Payment fail olsa bile Flight çalışır)
```

---

**Q: Service-to-service communication nasıl?**

**A:**
```
Sync Communication: Yok (anti-pattern)
Async Communication: RabbitMQ
  - Commands: Point-to-point (saga → service)
  - Events: Pub/Sub (service → multiple consumers)

Neden sadece async?
→ Loose coupling
→ Resilience (service down olsa message queue'da bekler)
→ Scalability (backpressure handling)
```

---

#### Saga Pattern

**Q: Saga Pattern nedir? Orchestration vs Choreography?**

**A:**
```
Saga: Distributed transaction management pattern

Orchestration (Bizim kullandığımız):
- Merkezi orchestrator (Saga State Machine)
- Workflow kontrolü tek yerden
- Kolay debug, görselleştirme
- Single point of failure riski

Choreography:
- Her servis kendi event'ini dinler
- Decentralized
- Kompleks workflow'da takip zor
- No single point of failure

Neden Orchestration seçtik?
→ Kolay debug ve görselleştirme
→ İş akışı tek yerden yönetiliyor
→ State persistence ile resume edilebilir
```

---

**Q: Compensating transaction nedir?**

**A:**
```
Örnek: Ödeme başarısız oldu
1. Flight Service koltuğu rezerve etmişti
2. Şimdi geri almak gerekiyor
3. Saga: ReleaseSeatCommand gönderir
4. Flight: seat.IsReserved = false

Bu bir "compensate" işlemidir.
→ Rollback yapamadığımız için "ters işlem" yapıyoruz
→ Business logic ile geri alma
```

---

**Q: Saga timeout olursa ne olur?**

**A:**
```
⚠️ Şu an projede timeout handling eksik!

Olması gereken:
1. Saga state: "AwaitingPayment" (30 dakika timeout)
2. Timeout oldu → Saga: Compensate başlat
3. ReleaseSeatCommand gönder
4. Saga: Failed state'e geç

MassTransit'te:
.TransitionTo(AwaitingPayment)
.RequestTimeout(TimeSpan.FromMinutes(30))
.IfElapsed(TimeSpan.FromMinutes(30), x => x
    .Publish(ReleaseSeatCommand)
    .Finalize())
```

---

#### Distributed Lock

**Q: Distributed lock neden gerekli?**

**A:**
```
Problem: Race condition
- 2 kullanıcı aynı anda son koltuğu alıyor
- if (seat.IsReserved == false) → İkisi de true görüyor
- İkisi de koltuğu alıyor → Overbooking!

Çözüm: Redis distributed lock
- Lock key: seat:{FlightId}:{SeatNumber}
- İlk gelen lock'u alır
- İkinci gelen lock alamaz, reddedilir
- Lock timeout: 5 saniye (deadlock önleme)
```

---

**Q: Redis lock implementation detayları?**

**A:**
```csharp
// 1. Atomic lock acquisition
await database.StringSetAsync(
    lockKey, 
    lockValue, // Unique GUID
    expiry,    // Timeout (5s)
    When.NotExists); // NX flag

// 2. Safe release (Lua script)
// Sadece kendi lock'unu sil (başkasınınkini silme)
if redis.call('get', KEYS[1]) == ARGV[1] then
    return redis.call('del', KEYS[1])
end

// 3. Try-finally ile guarantee release
```

---

**Q: Alternatif çözümler?**

**A:**
```
1. Database Pessimistic Lock:
   SELECT ... FOR UPDATE
   → Avantaj: Transaction içinde
   → Dezavantaj: DB load, deadlock riski

2. Optimistic Locking:
   Version/RowVersion column
   → Avantaj: Lock-free
   → Dezavantaj: Retry gerekebilir

3. Redis RedLock (Multi-master):
   → Avantaj: HA, clock skew resistant
   → Dezavantaj: Kompleks

4. Postgres Advisory Lock:
   → Avantaj: Transaction-scoped
   → Dezavantaj: DB dependency

Neden Redis seçtik?
→ Zaten Redis var (cache için)
→ Lightweight, fast
→ TTL ile auto-release
```

---

#### Idempotency

**Q: Idempotency nedir? Neden önemli?**

**A:**
```
Idempotent: Aynı işlem n kez tekrar edilse de sonuç aynı

Örnek: Payment
- Network timeout oldu
- Client retry yaptı
- Aynı reservation için 2. payment request geldi
- Idempotency olmasa: 2 kez ödeme çekilir!

Projede çözüm:
1. ReservationId'yi check et
2. Zaten başarılı transaction var mı?
3. Varsa: Cached sonuç döndür (duplicate yapma)
4. Yoksa: Yeni işlem yap

Hangi işlemler idempotent olmalı?
✅ Payment (kritik!)
✅ Reservation
✅ Seat release
❌ Query'ler (zaten idempotent)
```

---

**Q: Idempotency key ne kadar saklanmalı?**

**A:**
```
⚠️ Projede TTL belirlenmemiş!

Best practice:
- Payment: 30-90 gün (iade/dispute için)
- Reservation: 1-7 gün (cancel window)
- Logs: 1-2 yıl (audit)

Trade-off:
→ Uzun süre: Storage cost
→ Kısa süre: Duplicate risk
```

---

#### CQRS & Caching

**Q: CQRS nedir? Neden kullanılır?**

**A:**
```
CQRS: Command Query Responsibility Segregation

Command (Write):
- CreateFlight
- ReserveSeat
- Transactional
- Strong consistency

Query (Read):
- GetAllFlights
- GetFlightSeats
- Cache'li
- Eventual consistency OK

Neden?
→ Read ve Write farklı optimize edilebilir
→ Read DB ayrı olabilir (replica, ElasticSearch)
→ Scalability (read >> write)

Projede:
⚠️ Kısmi CQRS var (sadece query'lerde cache)
✅ Tam CQRS: Read/Write DB ayrı değil
```

---

**Q: Cache Aside Pattern nedir?**

**A:**
```
1. Cache'e bak
   └─ Hit: Return cached data

2. Cache miss: Lock al (stampede prevention)

3. DB'den oku

4. Cache'e yaz (10 dakika TTL)

5. Lock'u bırak

Cache Stampede (Thundering Herd):
- Cache expire oldu
- 1000 request aynı anda geldi
- Hepsi DB'ye gidiyor → DB overload!

Çözüm: Distributed lock
- İlk request lock alır, DB'ye gider
- Diğerleri bekler veya cache'den alır
```

---

#### Resilience

**Q: Circuit breaker pattern nedir?**

**A:**
```
Circuit Breaker States:

CLOSED (Normal):
- Requests geçiyor
- Success rate tracked

↓ (5 consecutive failures)

OPEN (Service down):
- Requests direkt fail (DB'ye gitme)
- Fail-fast

↓ (30 seconds timeout)

HALF-OPEN (Testing):
- 1 test request
- Success → CLOSED
- Failure → OPEN again

Neden gerekli?
→ Cascading failure önleme
→ Unhealthy service'e istek gönderme
→ Fast failure (30s beklemek yerine direkt fail)
```

---

**Q: Retry policy ne zaman kullanılır?**

**A:**
```
✅ Kullan:
- Transient failures (network timeout, 503)
- Idempotent işlemler
- External API calls

❌ Kullanma:
- Non-idempotent işlemler (payment retry → 2x charge)
- Validation errors (400, 422)
- Business logic errors

Exponential backoff:
Attempt 1: Wait 1s
Attempt 2: Wait 2s
Attempt 3: Wait 4s
→ Backpressure, service'e nefes alma
```

---

#### API Gateway

**Q: API Gateway neden gerekli?**

**A:**
```
Gateway Responsibilities:

1. Routing: /api/flight → Flight Service
2. Authentication: JWT validation
3. Authorization: Role-based access
4. Rate Limiting: 100 req/min per IP
5. Circuit Breaker: Unhealthy service'i bypass
6. Load Balancing: RoundRobin
7. Request/Response Transformation
8. Centralized Logging/Monitoring

Neden?
→ Clients her service URL'ini bilmemeli
→ Security tek noktadan
→ Cross-cutting concerns (CORS, logging)
```

---

**Q: Gateway single point of failure mı?**

**A:**
```
⚠️ Evet, şu an öyle!

Production'da:
1. Multiple gateway instances
2. Load balancer (Nginx, HAProxy)
3. Health checks (liveness, readiness)
4. Auto-scaling (K8s HPA)

   User
    ↓
LoadBalancer
    ↓
├─ Gateway 1
├─ Gateway 2
└─ Gateway 3
    ↓
 Services
```

---

## 📚 DERİNLEŞMEK İÇİN ARAŞTIRMAM GEREKENLER

### 1. Service Mesh
- [ ] Istio nedir? Service mesh vs API Gateway
- [ ] Linkerd, Consul Connect
- [ ] Sidecar pattern
- [ ] Traffic management, observability, security

### 2. Event Sourcing
- [ ] Event store nedir?
- [ ] Event sourcing vs Event-driven
- [ ] CQRS + Event Sourcing
- [ ] Snapshotting

### 3. Advanced Distributed Systems
- [ ] CAP Theorem (Consistency, Availability, Partition tolerance)
- [ ] Two-Phase Commit (2PC) vs Saga
- [ ] Consensus algorithms (Raft, Paxos)
- [ ] Vector clocks, Lamport timestamps

### 4. Database Patterns
- [ ] Database sharding & partitioning
- [ ] Read replicas
- [ ] Multi-master replication
- [ ] Polyglot persistence

### 5. Kubernetes Deep Dive
- [ ] Pods, Deployments, Services
- [ ] ConfigMaps, Secrets
- [ ] Ingress, Network Policies
- [ ] StatefulSets, DaemonSets
- [ ] Helm charts

### 6. Observability
- [ ] OpenTelemetry (traces, metrics, logs)
- [ ] Jaeger, Zipkin (distributed tracing)
- [ ] Prometheus + Grafana (metrics)
- [ ] ELK Stack (Elasticsearch, Logstash, Kibana)

### 7. Security
- [ ] OAuth2 / OpenID Connect
- [ ] JWT best practices
- [ ] API Key management
- [ ] Secrets management (Vault, AWS Secrets Manager)
- [ ] mTLS (mutual TLS)

### 8. Testing
- [ ] Test Pyramid (Unit, Integration, E2E)
- [ ] Contract testing (Pact)
- [ ] Chaos engineering (Chaos Monkey)
- [ ] Performance testing (k6, JMeter)

---

## 🎯 NEXT STEPS - KISA VADEDE YAPMALISIN

### 1. Docker & Docker Compose (1-2 gün)
```bash
# Öncelik: En yüksek
✅ Her service için Dockerfile yaz
✅ docker-compose.yml oluştur (tüm stack)
✅ Projeyi tek komutla çalıştırabilir hale getir

docker-compose up
→ Redis, RabbitMQ, PostgreSQL, MongoDB, tüm servisler
```

### 2. Basic Tests (2-3 gün)
```bash
✅ Unit tests: ReserveSeatCommandConsumer
✅ Integration test: POST /api/reservation
✅ Saga test: MassTransit test harness

Test coverage hedef: %50
```

### 3. Service Discovery (Eureka) (1-2 gün)
```bash
✅ Eureka Server (Docker)
✅ Steeltoe.Discovery.Eureka (her service)
✅ Gateway dynamic routing
```

### 4. Observability Basics (2-3 gün)
```bash
✅ Serilog + Console
✅ OpenTelemetry + Jaeger (distributed tracing)
✅ Basic metrics
```

### 5. Documentation (1 gün)
```bash
✅ Architecture diagram (C4 Model)
✅ Saga sequence diagram
✅ Developer setup guide (README.md)
```

---

## 🏆 ORTA-UZUN VADEDE YAPMALISIN

### Phase 1: Production Hardening (1-2 hafta)
- [ ] CI/CD pipeline (GitHub Actions)
- [ ] Kubernetes deployment
- [ ] API versioning
- [ ] Global exception handler
- [ ] Comprehensive testing (%80 coverage)

### Phase 2: Advanced Features (2-3 hafta)
- [ ] Event Sourcing
- [ ] Full CQRS (separate read DB)
- [ ] Service mesh (Istio)
- [ ] Advanced observability (Grafana dashboards)

### Phase 3: Alternative Implementations (1-2 hafta)
- [ ] Yeni bir microservice projesi (E-Commerce)
- [ ] Choreography Saga pattern dene
- [ ] gRPC ile service communication
- [ ] GraphQL gateway

---

## 💼 RESUME / PORTFOLIO İÇİN

### Bu Projeyi Nasıl Anlatırsın?

**Elevator Pitch (30 saniye):**
```
"Microservices mimarisi ile production-grade uçak bileti 
rezervasyon sistemi geliştirdim. Saga Pattern ile distributed 
transaction management, Redis distributed lock ile race condition 
önleme, YARP ile API Gateway, MassTransit ile event-driven 
architecture uyguladım. RabbitMQ, PostgreSQL, MongoDB, Redis 
teknolojilerini kullandım."
```

**Detaylı Açıklama (Interview):**
```
1. Problem: Monolitten microservices'e geçiş simülasyonu

2. Çözüm:
   - 4 microservice (Flight, Reservation, Payment, Notification)
   - Saga Pattern (orchestration)
   - Event-driven architecture (RabbitMQ)
   - API Gateway (YARP) - rate limiting, circuit breaker
   - Distributed lock (Redis) - overbooking önleme
   - Idempotency pattern (duplicate payment önleme)
   - CQRS (cache aside pattern)

3. Teknik Stack:
   - .NET 9, ASP.NET Core, EF Core
   - MassTransit, RabbitMQ
   - Redis, PostgreSQL, MongoDB
   - YARP, Polly

4. Challenges:
   - Race condition (2 user aynı koltuk) → Distributed lock
   - Distributed transaction → Saga Pattern
   - Cache stampede → Lock-based cache aside
   - Service failure → Circuit breaker, retry

5. Öğrendiklerim:
   - Saga orchestration vs choreography
   - CAP theorem tradeoffs
   - Eventually consistent data
   - Compensating transactions
```

### GitHub README.md Örnek:
```markdown
# SkySync - Flight Reservation System

Production-grade microservices architecture with Saga Pattern.

## Features
- 🔄 Saga Pattern (Orchestration)
- 🔒 Distributed Lock (Race condition prevention)
- 🚀 Event-Driven Architecture
- 🛡️ Circuit Breaker & Resilience
- 📦 CQRS & Caching
- 🚪 API Gateway (YARP)

## Architecture
[Architecture Diagram]

## Tech Stack
.NET 9 | RabbitMQ | Redis | PostgreSQL | MongoDB | Docker

## Quick Start
```bash
docker-compose up
```

[Detaylı documentation...]
```

---

## 📝 SON NOTLAR

### Güçlü Yönlerin
✅ Senior-level pattern usage (Saga, Distributed Lock, CQRS)
✅ Clean code, SOLID principles
✅ Microservices best practices
✅ Resilience patterns
✅ Event-driven thinking

### Gelişim Alanların
⚠️ Testing (kritik eksiklik)
⚠️ Observability (production debug)
⚠️ DevOps (Docker, K8s, CI/CD)
⚠️ Deep debugging experience
⚠️ Production troubleshooting

### Öğrenme Yolculuğun
```
Başlangıç: Microservices kavramları
    ↓
Bu Proje: Pattern implementation (AI yardımıyla)
    ↓
Şimdi: Derinleştirme zamanı
    ├─ From scratch implementation
    ├─ Testing & debugging
    ├─ Production deployment
    └─ Performance optimization
    ↓
Hedef: Senior developer (1-2 yıl practice)
```

### Tavsiyelerim
1. **Bu projeyi Docker'a taşı** (en önemli)
2. **Test yaz** (muscle memory için)
3. **Başka bir proje yap** (aynı pattern'ler, sıfırdan)
4. **Blog yaz** (öğrendiklerini paylaş)
5. **Hata yap, düzelt** (deneyim kazanmak için)

---

## 🎓 KAYNAKLAR

### Kitaplar
- [ ] "Microservices Patterns" - Chris Richardson
- [ ] "Building Microservices" - Sam Newman
- [ ] "Designing Data-Intensive Applications" - Martin Kleppmann
- [ ] "Domain-Driven Design" - Eric Evans

### Online Courses
- [ ] Microsoft Learn - Microservices Architecture
- [ ] Pluralsight - Microservices in .NET
- [ ] Udemy - Docker & Kubernetes

### Blogs & Docs
- [ ] Microsoft Docs - Microservices
- [ ] Martin Fowler - martinfowler.com
- [ ] Chris Richardson - microservices.io
- [ ] MassTransit Documentation

---

**Tarih:** 26 Ocak 2026  
**Revizyon:** v1.0  
**Güncellenecek:** Testing, Docker, Service Discovery eklenince

---

Bu dökümanı düzenli olarak güncelle!


---

## SkySync API Gateway

YARP (Yet Another Reverse Proxy) ile oluşturulmuş production-ready API Gateway.

## Özellikler

### ✅ 1. JWT Authentication (Environment Variable Support)
- Production'da `JWT_SECRET_KEY` environment variable kullanılır
- Development'ta `appsettings.json` kullanılır
- Fallback mekanizması ile güvenli

### ✅ 2. CORS Policy (Production Safe)
- Development: Allow All
- Production: Sadece whitelist'teki origin'ler

### ✅ 3. Rate Limiting
- 100 istek/dakika (global)
- 1000 istek/saat (global)
- IP bazlı rate limiting

### ✅ 4. Request/Response Transformation
- Gateway version header ekleme
- X-Forwarded-For header koruma
- Response time tracking

### ✅ 5. Circuit Breaker Pattern
- YARP built-in circuit breaker
- Health check ile entegre
- Automatic failover

### ✅ 6. Load Balancing
- RoundRobin policy
- Flight service için 2 instance desteği

## Environment Variables (Production)

```bash
export JWT_SECRET_KEY="YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!"
export JWT_ISSUER="SkySync"
export JWT_AUDIENCE="SkySyncUsers"
```

## Routing

```
/api/flight/* → Flight Service (5041) [Public]
/api/reservation/* → Reservation Service (5062) [Authenticated]
/api/payment/* → Payment Service (5124) [Authenticated]
/api/notification/* → Notification Service (5130) [Authenticated]
```

## Health Check

```
GET /health
```

## Gateway Info

```
GET /
```
