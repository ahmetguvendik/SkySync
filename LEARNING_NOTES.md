# 📚 SKYSYNC PROJECT - LEARNING & DEVELOPMENT NOTES

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
