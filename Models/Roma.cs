namespace Roma.Models
{
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

    /// <summary>
    /// Uygulama kullanıcılarını temsil eder. IdentityUser'dan türetilmiştir.
    /// </summary>
    public class User : IdentityUser
    {
        /// <summary>
        /// Kullanıcının oluşturduğu seyahat planları.
        /// </summary>
        public List<Itinerary> Itineraries { get; set; } = new List<Itinerary>();
    }

    /// <summary>
    /// Veritabanı değişikliklerinin denetim kayıtlarını tutar.
    /// </summary>
    public class AuditLog
    {
        /// <summary>
        /// Denetim kaydının benzersiz kimliğidir.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// İşlem yapılan tablonun adıdır.
        /// </summary>
        public required string TableName { get; set; }

        /// <summary>
        /// Etkilenen kaydın anahtar değerlerini JSON/metin olarak tutar.
        /// </summary>
        public string? KeyValues { get; set; }

        /// <summary>
        /// Güncelleme/ silme öncesi eski değerleri JSON/metin olarak tutar.
        /// </summary>
        public string? OldValues { get; set; }

        /// <summary>
        /// Ekleme/güncelleme sonrası yeni değerleri JSON/metin olarak tutar.
        /// </summary>
        public string? NewValues { get; set; }

        /// <summary>
        /// Gerçekleşen işlem türüdür (Insert, Update, Delete vb.).
        /// </summary>
        public required string Action { get; set; }

        /// <summary>
        /// İşlemin yapıldığı tarih ve saattir.
        /// </summary>
        public DateTime PerformedAt { get; set; }
    }

    /// <summary>
    /// E-posta ile bildirim almak isteyen aboneler.
    /// </summary>
    public class Subscriber
    {
        /// <summary>
        /// Abone kaydının benzersiz kimliğidir.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Abonenin e-posta adresidir.
        /// </summary>
        public required string Email { get; set; }

        /// <summary>Abonenin açık rızası (işaretlediğinde true).</summary>
        public bool ConsentGiven { get; set; }

        /// <summary>Onaylı aboneler bildirim alır.</summary>
        public bool IsConfirmed { get; set; } = false;

        /// <summary>
        /// Abonelik kaydının oluşturulduğu tarih ve saattir (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>E-posta doğrulama için oluşturulan token.</summary>
        public string? ConfirmationToken { get; set; }

        /// <summary>Confirmation token geçerlilik süresi (UTC).</summary>
        public DateTime? ConfirmationExpiresAt { get; set; }

        /// <summary>Token used to unsubscribe without login.</summary>
        public string? UnsubscribeToken { get; set; }
    }

    /// <summary>
    /// Yeniden denemeler sonrası gönderilemeyen e-postaların (dead-letter) kaydı.
    /// </summary>
    public class FailedEmail
    {
        /// <summary>
        /// Başarısız e-posta kaydının benzersiz kimliğidir.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Alıcı e-posta adresidir.
        /// </summary>
        public required string To { get; set; }

        /// <summary>
        /// E-posta konu başlığıdır.
        /// </summary>
        public required string Subject { get; set; }

        /// <summary>
        /// E-posta içeriğidir.
        /// </summary>
        public string? Body { get; set; }

        /// <summary>
        /// Gönderim deneme sayısıdır.
        /// </summary>
        public int Attempts { get; set; }

        /// <summary>
        /// Son denemede alınan hata mesajıdır.
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Kaydın oluşturulma tarihidir (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Kullanıcı geri bildirimini temsil eder.
    /// </summary>
    public class Feedback
    {
        /// <summary>
        /// Geri bildirim kaydının benzersiz kimliğidir.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Geri bildirimi gönderen kullanıcının e-posta adresidir (isteğe bağlı).
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Geri bildirim mesaj içeriğidir.
        /// </summary>
        public required string Message { get; set; }

        /// <summary>
        /// Geri bildirimin oluşturulduğu tarih ve saattir (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Giriş sırasında kullanılan geçici iki faktörlü doğrulama kodlarını tutar.
    /// </summary>
    public class TwoFactorCode
    {
        /// <summary>
        /// İki faktör kodu kaydının benzersiz kimliğidir.
        /// </summary>
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N");

        /// <summary>
        /// Kodun ait olduğu kullanıcı kimliğidir.
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Kodun hashlenmiş halidir (HMAC-SHA256 veya benzeri).
        /// </summary>
        public required string HashedCode { get; set; }

        /// <summary>
        /// Hash işlemi için kullanılan salt değeridir.
        /// </summary>
        public required string Salt { get; set; }

        /// <summary>
        /// Kodun geçerlilik bitiş tarihidir.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Kodun kullanılıp kullanılmadığını belirtir.
        /// </summary>
        public bool Used { get; set; }

        /// <summary>
        /// Hatalı deneme sayısıdır.
        /// </summary>
        public int Attempts { get; set; }

        /// <summary>
        /// Kod kaydının oluşturulma tarihidir (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Gezi hedefini (destinasyonu) temsil eder; örn. bir şehir veya bölge.
    /// </summary>
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

        /// Destinasyonun Instagram bağlantısı (isteğe bağlı).
        public string? InstagramUrl { get; set; }

        /// Destinasyonun Facebook bağlantısı (isteğe bağlı).
        public string? FacebookUrl { get; set; }

        /// Destinasyonun enlem (latitude) değeridir (isteğe bağlı).
        public double? Latitude { get; set; }

        /// Destinasyonun boylam (longitude) değeridir (isteğe bağlı).
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


    /// <summary>
    /// Destinasyona yüklenen her bir görseli temsil eder.
    /// </summary>
    public class DestinationImage
    {
        /// <summary>
        /// Görsel kaydının benzersiz kimlik (primary key) değeridir.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Görsel dosyasının uygulama içindeki erişim yolunu veya URL bilgisini tutar.
        /// Örnek: /uploads/destinations/9/abc123.jpg
        /// </summary>
        public required string Url { get; set; }

        /// <summary>
        /// Görselin bağlı olduğu destinasyon kaydının kimlik (foreign key) değeridir.
        /// </summary>
        public int DestinationId { get; set; }

        /// <summary>
        /// Görselin bağlı olduğu destinasyon nesnesinin navigasyon alanıdır.
        /// </summary>
        [ValidateNever]
        public required Destination Destination { get; set; }

        /// <summary>
        /// Görsellerin arayüzde hangi sırayla gösterileceğini belirler.
        /// Küçük değerler önce listelenir.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Erişilebilirlik ve SEO için kullanılan alternatif metindir (alt text).
        /// AI ile üretilebilir veya kullanıcı tarafından girilebilir.
        /// </summary>
        public string? AltText { get; set; }
    }

    /// <summary>
    /// Görsel indirmelerini kullanıcı/oturum bazında takip eder.
    /// </summary>
    public class DownloadRecord
    {
        /// <summary>
        /// İndirme kaydının benzersiz kimliğidir.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// İndirmenin yapıldığı destinasyon kimliğidir.
        /// </summary>
        public int DestinationId { get; set; }

        /// <summary>
        /// İndirilen görselin kimliğidir.
        /// </summary>
        public int ImageId { get; set; }

        /// <summary>
        /// İşlem yapan giriş yapmış kullanıcının kimliğidir (varsa).
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// İşlem yapan anonim oturumun kimliğidir (varsa).
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// İndirmenin ücretli olup olmadığını belirtir.
        /// </summary>
        public bool IsPaid { get; set; }

        /// <summary>
        /// Ücretli indirme için tahsil edilen tutardır.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// İndirmenin gerçekleştiği tarih ve saattir (UTC).
        /// </summary>
        public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Destinasyon için öneri, ipucu veya tavsiye içeriği.
    /// </summary>
    public class Recommendation
    {
        /// <summary>
        /// Öneri kaydının benzersiz kimliğidir.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Öneri başlığıdır.
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// Önerinin açıklama/metin içeriğidir.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>Opsiyonel: daha fazla bilgi için dış link.</summary>
        public string? Url { get; set; }

        /// <summary>Öneriyi ekleyen kullanıcı (varsa).</summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Önerinin ait olduğu destinasyonun kimliğidir.
        /// </summary>
        public int DestinationId { get; set; }

        /// <summary>
        /// Önerinin ait olduğu destinasyon nesnesidir.
        /// </summary>
        public required Destination Destination { get; set; }

        /// <summary>
        /// Öneri kaydının oluşturulma tarihidir (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>Resmi / editoryal bir öneri ise true.</summary>
        public bool IsOfficial { get; set; } = false;
    }

    /// <summary>
    /// Bir destinasyondaki yemek / restoran önerisini temsil eder.
    /// </summary>
    public class Meal
    {
        /// <summary>
        /// Yemek/restoran kaydının benzersiz kimliğidir.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Yemek veya restoran adıdır.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Yemek/restoran açıklamasıdır.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Adres bilgisidir.
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// İletişim telefonudur.
        /// </summary>
        public string? Phone { get; set; }
        /// <summary>Ortalama veya tahmini fiyat (isteğe bağlı).</summary>
        public decimal? Price { get; set; }

        /// <summary>
        /// Kaydın ait olduğu destinasyon kimliğidir.
        /// </summary>
        public int DestinationId { get; set; }

        /// <summary>
        /// Kaydın ait olduğu destinasyon nesnesidir.
        /// </summary>
        public required Destination Destination { get; set; }
    }

    /// <summary>
    /// Bölgeye özgü lezzet veya tat bilgisi (etiket olarak kullanılabilir).
    /// </summary>
    public class Flavor
    {
        /// <summary>
        /// Lezzet kaydının benzersiz kimliğidir.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Lezzet veya tat adıdır.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Kaydın ait olduğu destinasyon kimliğidir.
        /// </summary>
        public int DestinationId { get; set; }

        /// <summary>
        /// Kaydın ait olduğu destinasyon nesnesidir.
        /// </summary>
        public required Destination Destination { get; set; }
    }

    /// <summary>
    /// Otel bilgisi.
    /// </summary>
    public class Hotel
    {
        /// <summary>
        /// Otel kaydının benzersiz kimliğidir.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Otel adıdır.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Otelin adres bilgisidir.
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Otelin iletişim telefonudur.
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Otelin web sitesi adresidir.
        /// </summary>
        public string? Website { get; set; }

        /// <summary>
        /// Tahmini başlangıç fiyatıdır (isteğe bağlı).
        /// </summary>
        public decimal? PriceFrom { get; set; }
        /// <summary>Otel için tahmini üst fiyat aralığı (isteğe bağlı).</summary>
        public decimal? PriceTo { get; set; }

        /// <summary>
        /// Kaydın ait olduğu destinasyon kimliğidir.
        /// </summary>
        public int DestinationId { get; set; }

        /// <summary>
        /// Kaydın ait olduğu destinasyon nesnesidir.
        /// </summary>
        public required Destination Destination { get; set; }
    }

    /// <summary>
    /// Otopark bilgileri.
    /// </summary>
    public class Parking
    {
        /// <summary>
        /// Otopark kaydının benzersiz kimliğidir.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Otopark adıdır.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Otopark kapasitesidir (araç sayısı, isteğe bağlı).
        /// </summary>
        public int? Capacity { get; set; }

        /// <summary>
        /// Otoparkın ücretli olup olmadığını belirtir.
        /// </summary>
        public bool IsPaid { get; set; }
        /// <summary>Sabit ücret (varsa).</summary>
        public decimal? Fee { get; set; }
        /// <summary>Saatlik ücret (varsa).</summary>
        public decimal? HourlyRate { get; set; }

        /// <summary>
        /// Kaydın ait olduğu destinasyon kimliğidir.
        /// </summary>
        public int DestinationId { get; set; }

        /// <summary>
        /// Kaydın ait olduğu destinasyon nesnesidir.
        /// </summary>
        public required Destination Destination { get; set; }
    }

    /// <summary>
    /// Bir destinasyon içindeki gezilebilecek nokta veya etkinliği temsil eder.
    /// </summary>
    public class Attraction
    {
        /// Veritabanı kimliği.
        public int Id { get; set; }

        /// Gezilecek noktanın adı (örn. Göreme Açık Hava Müzesi).
        public required string Name { get; set; }

        /// Kategori (Tarihi Yer, Doğa, Restoran vb.).
        public required string Category { get; set; }

        /// <summary>
        /// Adres veya konum bilgisidir.
        /// </summary>
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
    /// <summary>
    /// Kullanıcının seyahat planını (itinerary) temsil eder.
    /// </summary>
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
    /// <summary>
    /// Gezilecek nokta üzerine yapılan kullanıcı yorumunu temsil eder.
    /// </summary>
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

        /// Yorumun yönetici tarafından onaylanma durumu.
        public bool IsApproved { get; set; } = false;

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