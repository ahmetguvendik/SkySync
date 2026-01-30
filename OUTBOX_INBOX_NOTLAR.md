# Outbox, Outbox Worker ve Inbox – Detaylı Notlar

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
