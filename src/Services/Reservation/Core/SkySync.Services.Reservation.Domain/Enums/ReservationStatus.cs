namespace SkySync.Services.Reservation.Domain.Enums;

public enum ReservationStatus
{
    Pending = 1,      // İlk oluşturulduğunda
    Confirmed = 2,    // Ödeme alındı ve koltuk onaylandı
    Cancelled = 3,    // Kullanıcı iptal etti
    Failed = 4        // Ödeme veya koltuk aşamasında hata çıktı
}
