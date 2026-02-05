# Serilog ile Loglama – Yol Haritası (5 Servis)

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
