# SkySync API Endpoints

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

## Payment Service
**Base URL:** `http://localhost:5000/api/payment` (Gateway üzerinden)

### Endpoints
- Henüz implement edilmedi (Saga'dan `ProcessPaymentCommand` consume edilecek)

---

## Notification Service
**Base URL:** `http://localhost:5000/api/notification` (Gateway üzerinden)

### Endpoints
- Henüz implement edilmedi (Saga'dan `ReservationConfirmedEvent` consume edilecek)

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
