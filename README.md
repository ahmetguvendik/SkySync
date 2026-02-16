# SkySync

SkySync, uçuş rezervasyon sürecini mikro servislerle yöneten .NET 9 tabanlı bir örnek projedir. Saga, Outbox/Inbox, RabbitMQ mesajlaşması, YARP tabanlı API Gateway ve gözlemlenebilirlik araçları birlikte kullanılarak uçtan uca bir rezervasyon yolculuğu gösterilir.

---

## Neler Var?

- **Mikroservisler:** Flight, Reservation, Payment, Notification, Identity ve bu servislerin önüne konumlanan API Gateway.
- **Saga Orkestrasyonu:** Rezervasyon → Koltuk tutma → Ödeme → Onay/İptal akışı MassTransit tabanlı `SkySync.SagaStateMachine` ile yönetilir (Reservation servisi host eder).
- **Dayanıklılık:** Gateway’de Polly tabanlı timeout/retry/circuit breaker kombinasyonu, IP rate limiting ve JWT tabanlı policy kontrolleri.
- **Outbox & Inbox:** Tüm domain event’leri Outbox’a yazılır, worker publish eder; consumer tarafı Inbox ile idempotent.
- **Observability:** Serilog + Seq, OpenTelemetry (OTLP exporter), Correlation/Transaction ID middleware ve Health Check uçları.

---

## Mimari Özet

```
Client → API Gateway (YARP)
   ├─ Flight Service (HTTP + Outbox + Redis cache)
   ├─ Reservation Service (HTTP + Saga tetikleyici)
   ├─ Payment Service (HTTP + Inbox)
   ├─ Notification Service (HTTP + Inbox)
   └─ Identity Service (HTTP + JWT)
RabbitMQ ↔ Saga State Machine (Reservation) & Outbox Worker
```

Gateway tüm HTTP trafiğini yönetir, Eureka’ya register olur ve her isteğe Correlation/Transaction ID ekler. Reservation servisindeki MassTransit tabanlı saga state machine RabbitMQ event’lerini dinleyip komutları servisler arasında koordine eder.

---

## Servis Özeti

| Servis | Port (varsayılan) | Gateway Path | Notlar |
| --- | --- | --- | --- |
| API Gateway | 5000 (HTTP), 7000 (HTTPS) | `/api/v1/*` ve legacy `/api/*` | YARP, Polly, JWT, CORS, rate limiting |
| Flight | 5041 | `/api/v1/flight`, `/api/v1/airport` | Koltuk yönetimi, Redis cache, Outbox |
| Reservation | 5062 | `/api/v1/reservation` | Saga başlangıcı, Outbox + status consumer’lar; saga state machine burada host edilir |
| Payment | 5124 | `/api/v1/payment` | Ödeme simülasyonu, Inbox, Payment events |
| Notification | 5042 | `/api/v1/notification` | Mail gönderimi, transactional inbox |
| Identity | 5050 | `/api/v1/auth` | Register/Login/Profile, JWT üretimi |
| Outbox Worker | – | – | Outbox publish işleri |

Gateway rotaları ve cluster adresleri `src/Infrastructure/SkySync.Gateway/appsettings.json` içinde tanımlıdır; tablo yukarıdaki değerlerle eşleşir. Saga state machine implementation kodu `src/Infrastructure/SkySync.SagaStateMachine` klasöründedir ve Reservation servisinin MassTransit konfigürasyonuna referanslanır.

---

## Çözüm Yapısı

```
src/
 ├─ Services/
 │   ├─ Flight
 │   ├─ Reservation
 │   ├─ Payment
 │   ├─ Notification
 │   └─ Identity
 ├─ Infrastructure/
 │   ├─ SkySync.Gateway
 │   ├─ SkySync.Infrastructure.Logging
 │   ├─ SkySync.SagaStateMachine
 │   └─ SkySync.Shared
 └─ Workers/
     └─ SkySync.Workers.Outbox
tests/
 └─ … (servis bazlı test projeleri)
```

Her servis `Core` (Domain + Application), `Infrastructure` ve `Presentation` katmanlarına ayrılır. Saga state machine kodu `src/Infrastructure/SkySync.SagaStateMachine` altında yer alır ve Reservation servisi içindeki MassTransit kurulumu tarafından host edilir; Outbox worker RabbitMQ ile konuşarak event publish eder.

---

## API Gateway Detayları

- **Routing:** `ReverseProxy.Routes` bölümünde tüm v1/legacy yolları, hedef cluster’lar, method kısıtları ve authorization policy’leri bulunur.
- **Resilience:** `PollyResilience` ayarları (toplam timeout, retry deneme sayısı, circuit breaker parametreleri) `PollyResilienceForwarderHttpClientFactory` tarafından okunur.
- **Güvenlik:** JWT ayarları environment variable → `JwtSettings` sıralamasıyla alınır; `Public`, `Authenticated`, `AdminOnly` policy’leri route seviyesinde uygulanır.
- **CORS:** Development profili localhost portlarını serbest bırakır; Production profili `CorsSettings:AllowedOrigins` listesi tanımlanmadıysa `https://skysync.com` ile sınırlıdır.
- **Rate Limiting:** `IpRateLimiting` içinde dakikada 100 istek sınırı varsayılır; gerekirse ek endpoint bazlı kurallar eklenebilir.
- **Observability:** Serilog + Seq, OpenTelemetry OTLP exporter, Correlation/Transaction ID middleware, RequestTransformation middleware ve `/health` uçları hazırdır.

---

## Çalıştırma

1. **Önkoşullar**
   - .NET 9 SDK
   - RabbitMQ (varsayılan ayarlarla)
   - Geliştirme için Seq (istenirse), Redis (Flight cache için)
2. **Veritabanları**
   - Her servis kendi DbContext’ine sahiptir; `dotnet ef database update` komutları ilgili servis projelerinde çalıştırılır.
3. **Servisleri Başlatma**
   ```bash
   # Tüm servisleri ayrı terminal pencerelerinde başlatın
   dotnet run --project src/Infrastructure/SkySync.Gateway/SkySync.Gateway.csproj
   dotnet run --project src/Services/Identity/Presentation/SkySync.Services.Identity.WebApi/SkySync.Services.Identity.WebApi.csproj
   dotnet run --project src/Services/Flight/Presentation/SkySync.Services.Flight.WebApi/SkySync.Services.Flight.WebApi.csproj
   dotnet run --project src/Services/Reservation/Presentation/SkySync.Services.Reservation.WebApi/SkySync.Services.Reservation.WebApi.csproj
   dotnet run --project src/Services/Payment/Presentation/SkySync.Services.Payment.WebApi/SkySync.Services.Payment.WebApi.csproj
   dotnet run --project src/Services/Notification/Presentation/SkySync.Services.Notification.WebApi/SkySync.Services.Notification.WebApi.csproj
   dotnet run --project src/Workers/SkySync.Workers.Outbox/SkySync.Workers.Outbox.csproj
   ```
4. **Gateway Üzerinden İstek Gönderme**
   - Base URL: `http://localhost:5000`
   - Örnek: `POST /api/v1/auth/login`, `POST /api/v1/reservation`

---

## Konfigürasyon

### Ortam Değişkenleri

- `JWT_SECRET_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`: Gateway ve Identity servisleri için.
- `SEQ_SERVERURL`, `OpenTelemetry__Endpoint` gibi ayarlar environment üzerinden override edilebilir.
- Bağlantı stringleri her servis için `ConnectionStrings` bölümünden yönetilir.

### Gateway Ayarları (`src/Infrastructure/SkySync.Gateway/appsettings.json`)

- `PollyResilience`: Timeout/retry/circuit breaker değerleri (default toplam timeout 30 s, 3 retry, 5 s break vs.).
- `IpRateLimiting`: Dakikada 100 istek genel kuralı.
- `ReverseProxy.Clusters`: Her servisin gerçek adresi (ör. Flight → `http://localhost:5041`).
- `ReverseProxy.Routes`: v1 ve legacy yollar + authorization policy + transform bilgileri.
- `CorsSettings:AllowedOrigins`: Production’da açık olacak domain listesi; tanımlanmazsa varsayılan `https://skysync.com` kullanılır.
- `Seq.ServerUrl`: Serilog sink hedefi.

---

## Test & Geliştirme Önerileri

- **Gateway Sağlığı:** `GET /health` uç noktası circuit breaker ve Seq durumunu döner.
- **Correlation Takibi:** Her isteğin response header’ında `X-Correlation-ID` ve `X-Transaction-ID` bulunur; loglarda aynı ID ile arama yapılabilir.
- **Saga Akışı:** `POST /api/v1/reservation` çağrısından sonra RabbitMQ üzerinden ilerleyen event’leri izlemek için Reservation ve Saga loglarına bakın.

---

## Lisans

SkySync eğitim ve portfolyo amaçlıdır. Ticari kullanım veya genişletilmiş destek için ek koşullar gerekebilir.
