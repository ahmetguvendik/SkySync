# Gereksiz / Kullanılmayan Öğeler Tespiti

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
