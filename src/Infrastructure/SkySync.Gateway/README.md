# SkySync API Gateway

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
