namespace SkySync.Services.Payment.Domain.Enums;

public enum PaymentStatus
{
    Pending = 1,
    Success = 2,
    Failed = 3,
    Expired = 4   // ValidUntil aşıldı, ödeme işlenmedi (timeout)
}

