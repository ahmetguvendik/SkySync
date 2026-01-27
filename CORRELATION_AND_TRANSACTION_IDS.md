# 🔍 Correlation ID & Transaction ID - SkySync Implementation

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
