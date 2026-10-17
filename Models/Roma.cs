namespace Roma.Models
{
    using Microsoft.AspNetCore.Identity;

    /// Uygulama kullanıcılarını temsil eder. IdentityUser'dan türetilmiştir.
    public class User : IdentityUser
    {
        // IdentityUser zaten Id (string), UserName ve Email sağlar.
        // Ek ilişki: kullanıcının oluşturduğu seyahat planları
        public List<Itinerary> Itineraries { get; set; } = new List<Itinerary>();
    }

    /// <summary>
    /// Audit log for database changes.
    /// </summary>
    public class AuditLog
    {
        public int Id { get; set; }
        public required string TableName { get; set; }
        public string? KeyValues { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public required string Action { get; set; }
        public DateTime PerformedAt { get; set; }
    }

    /// <summary>
    /// E-posta ile bildirim almak isteyen aboneler.
    /// </summary>
    public class Subscriber
    {
        public int Id { get; set; }
        public required string Email { get; set; }
        /// <summary>Abonenin açık rızası (işaretlediğinde true).</summary>
        public bool ConsentGiven { get; set; }
        /// <summary>Onaylı aboneler bildirim alır.</summary>
        public bool IsConfirmed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>E-posta doğrulama için oluşturulan token.</summary>
        public string? ConfirmationToken { get; set; }
        /// <summary>Confirmation token geçerlilik süresi (UTC).</summary>
        public DateTime? ConfirmationExpiresAt { get; set; }
        /// <summary>Token used to unsubscribe without login.</summary>
        public string? UnsubscribeToken { get; set; }
    }

    /// <summary>
    /// Failed email (dead-letter) for messages that couldn't be sent after retries.
    /// </summary>
    public class FailedEmail
    {
        public int Id { get; set; }
        public required string To { get; set; }
        public required string Subject { get; set; }
        public string? Body { get; set; }
        public int Attempts { get; set; }
        public string? LastError { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Feedback
    {
        public int Id { get; set; }
        public string? Email { get; set; }
        public required string Message { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Temporary two-factor authentication codes for login.
    /// </summary>
    public class TwoFactorCode
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
        public required string UserId { get; set; }
        // Hashed code (HMAC-SHA256 or salted hash)
        public required string HashedCode { get; set; }
        public required string Salt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool Used { get; set; }
        public int Attempts { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// Gezi hedefini (destinasyonu) temsil eder; örn. bir şehir veya bölge.
    public class Destination
    {
        /// Veritabanı kimliği
        public int Id { get; set; }

        /// Destinasyonun adı (örn. Kapadokya)
        public required string Name { get; set; }

        /// Ülke veya bölge bilgisi.
        public required string Country { get; set; }

        /// Destinasyonun açıklaması ve öne çıkan bilgiler.
        public required string Description { get; set; }

        /// Bu destinasyona ait birden fazla görselin URL'leri (geçici, veritabanında ayrı tablo kullanılır).
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public List<string> ImageUrls { get; set; } = new List<string>();

        /// Sosyal medya bağlantıları (isteğe bağlı).
        public string? InstagramUrl { get; set; }
        public string? FacebookUrl { get; set; }

        /// Coğrafi koordinatlar; hava durumu sorgulamaları için kullanılabilir (isteğe bağlı).
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        /// Giriş ücreti (örn. yetişkin başı). null ise bilgi yok.
        public decimal? EntranceFee { get; set; }

        /// Bu destinasyona ait yemek / restoran önerileri.
        public List<Meal> Meals { get; set; } = new List<Meal>();

        /// Bölgeye özgü lezzetler / tatlar (ör. mantı, baklava).
        public List<Flavor> Flavors { get; set; } = new List<Flavor>();

        /// Yakındaki veya tavsiye edilen oteller.
        public List<Hotel> Hotels { get; set; } = new List<Hotel>();

        /// Otopark bilgileri (birden fazla olabilir).
        public List<Parking> Parkings { get; set; } = new List<Parking>();

        /// Kullanıcı veya editör önerileri / ipuçları bu destinasyon için.
        public List<Recommendation> Recommendations { get; set; } = new List<Recommendation>();

        /// Bu destinasyona ait görseller (kalıcı, ayrı tablo).
        public List<DestinationImage> Images { get; set; } = new List<DestinationImage>();

        /// Bu destinasyona ait gezilecek noktalar (ilişkisel gezinme).
        public List<Attraction> Attractions { get; set; } = new List<Attraction>();
    }


    /// Destinasyona yüklenen her bir görseli temsil eder.
    public class DestinationImage
    {
        public int Id { get; set; }
        public required string Url { get; set; }

        public int DestinationId { get; set; }
        public required Destination Destination { get; set; }
        // Order for display/sorting in UI
        public int Order { get; set; }
        // Generated alternative text (AI/alt-text)
        public string? AltText { get; set; }
    }

    /// <summary>
    /// Tracks image downloads per user/session for free/paid gating.
    /// </summary>
    public class DownloadRecord
    {
        public int Id { get; set; }
        public int DestinationId { get; set; }
        public int ImageId { get; set; }
        public string? UserId { get; set; }
        public string? SessionId { get; set; }
        public bool IsPaid { get; set; }
        public decimal Amount { get; set; }
        public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Destinasyon için öneri, ipucu veya tavsiye içeriği.
    /// </summary>
    public class Recommendation
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        /// <summary>Opsiyonel: daha fazla bilgi için dış link.</summary>
        public string? Url { get; set; }

        /// <summary>Öneriyi ekleyen kullanıcı (varsa).</summary>
        public string? UserId { get; set; }

        public int DestinationId { get; set; }
        public required Destination Destination { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>Resmi / editoryal bir öneri ise true.</summary>
        public bool IsOfficial { get; set; } = false;
    }

    /// <summary>
    /// Bir destinasyondaki yemek / restoran önerisini temsil eder.
    /// </summary>
    public class Meal
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        /// <summary>Ortalama veya tahmini fiyat (isteğe bağlı).</summary>
        public decimal? Price { get; set; }

        public int DestinationId { get; set; }
        public required Destination Destination { get; set; }
    }

    /// <summary>
    /// Bölgeye özgü lezzet veya tat bilgisi (etiket olarak kullanılabilir).
    /// </summary>
    public class Flavor
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public int DestinationId { get; set; }
        public required Destination Destination { get; set; }
    }

    /// <summary>
    /// Otel bilgisi.
    /// </summary>
    public class Hotel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Website { get; set; }
        public decimal? PriceFrom { get; set; }
        /// <summary>Otel için tahmini üst fiyat aralığı (isteğe bağlı).</summary>
        public decimal? PriceTo { get; set; }

        public int DestinationId { get; set; }
        public required Destination Destination { get; set; }
    }

    /// <summary>
    /// Otopark bilgileri.
    /// </summary>
    public class Parking
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int? Capacity { get; set; }
        public bool IsPaid { get; set; }
        /// <summary>Sabit ücret (varsa).</summary>
        public decimal? Fee { get; set; }
        /// <summary>Saatlik ücret (varsa).</summary>
        public decimal? HourlyRate { get; set; }

        public int DestinationId { get; set; }
        public required Destination Destination { get; set; }
    }

   
    /// Bir destinasyon içindeki gezilebilecek nokta veya etkinliği temsil eder.

    public class Attraction
    {
        /// Veritabanı kimliği.
        public int Id { get; set; }

        /// Gezilecek noktanın adı (örn. Göreme Açık Hava Müzesi).
        public required string Name { get; set; }

        /// Kategori (Tarihi Yer, Doğa, Restoran vb.).
        public required string Category { get; set; }

        /// <summary>Adres veya konum bilgisi.
        public required string Address { get; set; }

        /// Ortalama puan (kullanıcı değerlendirmelerine göre).
        public double AverageRating { get; set; }

        /// İlişkili destinasyonun veritabanı kimliği (foreign key).
        public int DestinationId { get; set; }

        /// Bu gezilecek noktanın ait olduğu destinasyon (ilişkisel gezinme).
        public required Destination Destination { get; set; }

        /// Gezilecek nokta üzerine yapılmış kullanıcı yorumları.
        public List<Review> Reviews { get; set; } = new List<Review>();
    }


    /// Kullanıcının seyahat planını (itinerary) temsil eder.

    public class Itinerary
    {
        /// Veritabanı kimliği.
        public int Id { get; set; }

        /// Planın başlığı (örn. "Kapadokya Haftasonu Kaçamağı").
        public required string Title { get; set; }

        /// Planın başlangıç tarihi.
        public DateTime StartDate { get; set; }

        /// Planın bitiş tarihi.
        public DateTime EndDate { get; set; }

        /// Planın sahibi olan kullanıcı kimliği.
        public string UserId { get; set; } = null!;

        /// Planın sahibi kullanıcı (ilişkisel gezinme).
        public required User User { get; set; }

        /// Rota üzerindeki destinasyonlar.
        public List<Destination> Destinations { get; set; } = new List<Destination>();
    }

  
    /// Gezilecek nokta üzerine yapılan kullanıcı yorumunu temsil eder.

    public class Review
    {
        /// Veritabanı kimliği.
        public int Id { get; set; }

        /// Kullanıcının yorumu.
        public required string Comment { get; set; }

        /// Puanlama (1-5 arası).
        public int Rating { get; set; }

        /// Yorumun oluşturulma zamanı.
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// Yorumu yazan kullanıcının kimliği.
        public string UserId { get; set; } = null!;

        /// Yorumu yazan kullanıcı (ilişkisel gezinme).
        public required User User { get; set; }

        /// Yorumun ait olduğu gezilecek nokta kimliği.
        public int AttractionId { get; set; }

        /// Yorumun ait olduğu gezilecek nokta (ilişkisel gezinme).
        public required Attraction Attraction { get; set; }
    }
}