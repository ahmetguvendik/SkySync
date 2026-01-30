# SkySync – Eventler, Consumer’lar ve Saga – Detaylı Doküman

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
