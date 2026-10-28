using Microsoft.EntityFrameworkCore;
using Roma.Data;
using System.Threading.Channels;
using Roma.Models;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Console.Error.WriteLine($"[FATAL] Unhandled exception: {e.ExceptionObject}");
};

TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Console.Error.WriteLine($"[FATAL] Unobserved task exception: {e.Exception}");
    e.SetObserved();
};

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
// In-memory cache used by upload rate-limiting and other features
builder.Services.AddMemoryCache();
// Session for tracking anonymous users' first-download free rule
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
// Response compression for faster transfers on mobile
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] {
        "image/svg+xml",
        "application/javascript",
        "application/json"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);
// Image processing service
builder.Services.AddSingleton<Roma.Services.ImageProcessingService>();
// AI services (demo implementations)
builder.Services.AddSingleton<Roma.Services.IAiService, Roma.Services.DemoAiService>();
builder.Services.AddSingleton<Roma.Services.IVisionService, Roma.Services.DemoVisionService>();
builder.Services.AddScoped<Roma.Services.IChatService, Roma.Services.DatabaseChatService>();
builder.Services.AddScoped<Roma.Services.IOpenAiQaService, Roma.Services.OpenAiQaService>();
builder.Services.AddSingleton<Roma.Services.IEmailComposer, Roma.Services.DemoEmailComposer>();
// AI job channel + background processor
builder.Services.AddSingleton(Channel.CreateUnbounded<Roma.Services.AiJob>());
builder.Services.AddHostedService<Roma.Services.BackgroundAiProcessor>();
// Payment service (demo)
builder.Services.AddSingleton<Roma.Services.IPaymentService, Roma.Services.DemoPaymentService>();
// Email infrastructure: channel, queue and background sender
builder.Services.AddSingleton(Channel.CreateUnbounded<Roma.Services.EmailMessage>());
builder.Services.AddSingleton<Roma.Services.IEmailQueue, Roma.Services.EmailQueue>();
builder.Services.AddSingleton<Roma.Services.IEmailSenderService, Roma.Services.EmailSenderService>();
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, Roma.Services.IdentityEmailSender>();
builder.Services.AddHostedService<Roma.Services.BackgroundEmailSender>();
builder.Services.AddDefaultIdentity<User>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.Lockout.AllowedForNewUsers = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
});
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
// Destination image saving service
builder.Services.AddScoped<Roma.Services.IDestinationImageService, Roma.Services.DestinationImageService>();

var app = builder.Build();

app.Logger.LogInformation("Startup Environment: {EnvironmentName}", app.Environment.EnvironmentName);

var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? Environment.GetEnvironmentVariable("OpenAI__ApiKey")
    ?? app.Configuration["OpenAI:ApiKey"];

if (string.IsNullOrWhiteSpace(openAiKey))
{
    app.Logger.LogWarning("OpenAI API key is missing. Set OPENAI_API_KEY or OpenAI__ApiKey in production environment.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Use response compression early
app.UseResponseCompression();

// Serve static files with caching for assets and uploaded images
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? string.Empty;
        // Cache static assets for 30 days
        if (path.StartsWith("/lib/") || path.StartsWith("/css/") || path.StartsWith("/js/") || path.StartsWith("/uploads/"))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=2592000"; // 30 days
        }
        else
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();

// Session middleware (after app is built and before endpoints)
app.UseSession();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Fallback default route for non-area controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

// Seed roles and optional admin user from configuration (App:AdminEmail, App:AdminPassword)
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            // RoleManager may not be registered when AddDefaultIdentity was used without roles.
            var roleManager = services.GetService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<User>>();
            var configuration = services.GetRequiredService<IConfiguration>();
            var dbContext = services.GetRequiredService<ApplicationDbContext>();

            var adminRole = "Admin";
            var editorRole = "Editor";
            if (roleManager != null)
            {
                if (!roleManager.RoleExistsAsync(adminRole).GetAwaiter().GetResult())
                {
                    roleManager.CreateAsync(new IdentityRole(adminRole)).GetAwaiter().GetResult();
                }
                if (!roleManager.RoleExistsAsync(editorRole).GetAwaiter().GetResult())
                {
                    roleManager.CreateAsync(new IdentityRole(editorRole)).GetAwaiter().GetResult();
                }
            }

            var adminEmail = configuration["App:AdminEmail"];
            var adminPassword = configuration["App:AdminPassword"];
            if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
            {
                var adminUser = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
                if (adminUser == null)
                {
                    adminUser = new User { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                    var createResult = userManager.CreateAsync(adminUser, adminPassword).GetAwaiter().GetResult();
                    // ignore failures here; they will be logged
                }

                if (adminUser != null && roleManager != null && !userManager.IsInRoleAsync(adminUser, adminRole).GetAwaiter().GetResult())
                {
                    userManager.AddToRoleAsync(adminUser, adminRole).GetAwaiter().GetResult();
                }
            }

            // Optionally seed an Editor user if configured
            var editorEmail = configuration["App:EditorEmail"];
            var editorPassword = configuration["App:EditorPassword"];
            if (!string.IsNullOrEmpty(editorEmail) && !string.IsNullOrEmpty(editorPassword))
            {
                var editorUser = userManager.FindByEmailAsync(editorEmail).GetAwaiter().GetResult();
                if (editorUser == null)
                {
                    editorUser = new User { UserName = editorEmail, Email = editorEmail, EmailConfirmed = true };
                    var createResult = userManager.CreateAsync(editorUser, editorPassword).GetAwaiter().GetResult();
                }

                if (editorUser != null && roleManager != null && !userManager.IsInRoleAsync(editorUser, editorRole).GetAwaiter().GetResult())
                {
                    userManager.AddToRoleAsync(editorUser, editorRole).GetAwaiter().GetResult();
                }
            }

            // Seed demo chat replies (add only missing inputs)
            var samples = new (string Input, string Reply)[]
            {
                // Selamlama
                ("merhaba", "Merhaba! Roma gezi planın için yardımcı olabilirim."),

                // Rota talebi (ülke/şehir/destinasyon)
                ("gezmek istediğim yerler", "Harika! Sana net öneri verebilmem için lütfen şu 3 bilgiyi yaz: 1) Ülke 2) Şehir 3) Destinasyon türü (tarih/doğa/deniz/yeme-içme/alışveriş). Örnek: İtalya, Roma, tarih."),
                ("gezmek istiyorum", "Memnuniyetle yardımcı olurum. Lütfen ülke, şehir ve destinasyon türünü birlikte yaz. Örnek: Türkiye, İzmir, deniz."),
                ("ülke şehir destinasyon", "Lütfen bu formatta yaz: Ülke: ... | Şehir: ... | Destinasyon: ... Ardından sana uygun gezi önerisi hazırlayayım."),
                ("ülke şehiir destinasyon", "Lütfen bu formatta yaz: Ülke: ... | Şehir: ... | Destinasyon: ... Bilgileri paylaştığında rota ve önerileri net şekilde verebilirim."),

                // Vize ve resmi işlemler
                ("Vize nasıl alınır?", "Vize süreci genelde şu adımlarla ilerler: 1) Gideceğin ülkenin vize türünü belirle (turistik/ticari/öğrenci), 2) Randevu al, 3) Evrakları hazırla, 4) Başvuru merkezi veya konsolosluğa teslim et, 5) Biyometri işlemlerini tamamla, 6) Sonucu takip et."),
                ("Vize isteyen ülkeler hangileri?", "Türkiye Cumhuriyeti pasaportu için vize gereksinimi ülkeye ve pasaport tipine göre değişir. Schengen ülkeleri, ABD, Birleşik Krallık ve Kanada gibi birçok ülke başvuru ister. Güncel liste için ilgili ülkenin resmi konsolosluk sayfasını kontrol etmelisin."),
                ("Schengen vizesi için gerekli evraklar nelerdir?", "Schengen için yaygın evraklar: Pasaport, biyometrik fotoğraf, başvuru formu, seyahat sağlık sigortası, uçuş/konaklama rezervasyonu, banka dökümü, çalışma/öğrenci belgeleri, nüfus kayıt örneği ve varsa davetiye."),
                ("Vize için banka hesabında ne kadar para olmalı?", "Net tutar ülkeye ve seyahat süresine göre değişir. Genel yaklaşım günlük harcama + konaklama + ulaşım bütçesini karşılayacak bakiye göstermektir. Konsolosluklar düzenli gelir ve hesap hareketine de bakar."),
                ("Vize ücretleri ne kadar?", "Vize ücretleri ülkeye, vize türüne ve yaş grubuna göre değişir. Schengen kısa süreli başvurularda konsolosluk harcı + başvuru merkezi hizmet bedeli alınır. Güncel tutar için resmi konsolosluk ve aracı kurum sayfasını kontrol etmelisin."),
                ("ABD vizesi için hangi evraklar gerekli?", "ABD turist vizesinde DS-160 formu, pasaport, biyometrik fotoğraf, randevu onayı, ödeme dekontu ve seyahat planı temel belgelerdir. Ek olarak gelir/iş/öğrenci belgeleri ve Türkiye'ye geri dönüş bağını gösteren evraklar faydalıdır."),
                ("İngiltere vizesi için evraklar neler?", "İngiltere ziyaretçi vizesinde online form, pasaport, finansal belgeler (banka dökümü/maaş), konaklama ve uçuş planı, iş/öğrenci belgeleri ve varsa sponsor evrakları gerekir."),
                ("Vize reddi olursa ne yapmalıyım?", "Ret gerekçesini dikkatle inceleyip eksik/uyumsuz noktaları düzeltmelisin. Gerekirse yeni ve güçlendirilmiş evrakla tekrar başvuru yapılır. Bazı ülkelerde itiraz hakkı da bulunur."),
                ("Vize kaç günde çıkar?", "Sonuç süresi ülkeye, döneme ve başvuru yoğunluğuna göre değişir. Turistik vizelerde genelde birkaç iş günü ile birkaç hafta arası sürer. Seyahat tarihinden önce erken başvuru önerilir."),
                ("Vize randevusu nasıl alınır?", "Randevu, ilgili ülkenin resmi vize portalı veya yetkili başvuru merkezi üzerinden alınır. Ülke, vize türü ve şehir seçilerek uygun tarih/saat belirlenir; onay belgesi saklanmalıdır."),

                // Bütçe planlama
                ("Yemek ücretleri ne olur?", "Yemek bütçesi şehir ve ülkeye göre değişir. Ortalama hesapla: ekonomik öğün, orta segment restoran ve premium restoran olarak 3 seviyede plan yap. İstersen gideceğin ülkeye göre günlük net aralık çıkarabilirim."),
                ("Otel ücretleri ne kadar olur?", "Otel ücretleri sezona ve bölgeye göre değişir. Genel plan için gecelik düşük/orta/yüksek segment ayırıp merkez-çevre fiyat farkını birlikte hesaplamak en sağlıklı yöntemdir."),
                ("Ulaşım ücretleri ne kadar olur?", "Ulaşımda şehir içi kart, tek biniş, havalimanı transferi ve şehirler arası bilet kalemlerini ayrı hesaplamak gerekir. Böylece toplam maliyet daha net çıkar."),
                ("Yemek otel ulaşım toplam ne kadar tutar?", "Toplam bütçe için 3 ana kalem öneririm: konaklama + yeme-içme + ulaşım. Gün sayısı ve şehir bilgisi verirsen düşük/orta/yüksek senaryoda günlük ve toplam maliyeti net çıkarabilirim."),
                ("Günlük yemek bütçesi ne kadar olmalı?", "Günlük yemek bütçesini kahvaltı-öğle-akşam ve atıştırmalık olarak ayrı düşünmek gerekir. Ekonomik, orta ve yüksek bütçe senaryosuyla daha gerçekçi sonuç alırsın."),

                // Ulaşım
                ("Ulaşım kartı mı taksi mi daha uygun?", "Kısa ve çoklu duraklı rotalarda toplu taşıma kartı çoğu zaman daha ekonomiktir. Gece geç saat veya kapıdan kapıya ihtiyaçta taksi/uygulama araçları avantaj sağlar."),
                ("İtalya'da metro otobüs tramvay nasıl kullanılır?", "İtalya'da şehir içi ulaşımda metro, otobüs ve tramvay için şehirlerin toplu taşıma ağını tek uygulamadan takip etmek faydalıdır. Biletinizi araç öncesi alın/aktive edin ve denetim için saklayın."),
                ("İtalya toplu taşıma biletleri nasıl alınır?", "Biletleri metro istasyon otomatları, tabacchi noktaları veya resmi mobil uygulamalardan alabilirsiniz. Tek biniş yanında günlük/48-72 saatlik kartlar kısa gezilerde daha avantajlı olabilir."),
                ("İtalya'da metro otobüs tramvay bilet ücretleri ne kadar?", "Ücretler şehre göre değişir. Genelde tek bilet + süreli kart (24/48/72 saat) seçenekleri bulunur. En doğru fiyat için bulunduğunuz şehrin resmi toplu taşıma sitesini kontrol etmelisiniz."),
                ("Roma'da metro otobüs tramvay için hangi bilet alınmalı?", "Roma'da gün içi yoğun gezi planında 24 saatlik veya çoklu biniş seçenekleri pratik olur. Az duraklı planlarda tek bilet yeterli olabilir; biletinizi ilk kullanımda doğrulamayı unutmayın."),
                ("havaalanından merkeze nasıl giderim", "Fiumicino'dan merkeze Leonardo Express treni en pratik seçeneklerden biridir."),
                ("roma ulaşım kartı", "Yoğun gezi planında 24/48/72 saatlik toplu taşıma kartları maliyet avantajı sağlayabilir."),

                // İletişim ve bağlantı
                ("Yurt dışında telefon nasıl kullanılır?", "Yurt dışında telefon için üç temel seçenek var: operatör roaming paketi, yerel SIM kart veya eSIM. Kısa seyahatte eSIM pratik, uzun konaklamada yerel SIM genelde daha ekonomik olur."),
                ("Mobil internet ücretleri ne kadar olur?", "Mobil internet ücretleri ülkeye ve operatöre göre değişir. Turist paketlerinde 10-30 GB aralığı için genelde 10-35€ bandı görülür; havaalanı satışları merkez mağazalara göre biraz daha pahalı olabilir."),
                ("SIM kart nereden alınır?", "SIM kartı havaalanı kiosklarından, şehir merkezindeki operatör mağazalarından veya eSIM uygulamalarından alabilirsiniz. Pasaport gereksinimi ülkeye göre değişebilir; satın almadan önce kimlik şartlarını kontrol edin."),
                ("Roaming mi yerel SIM mi daha uygun?", "Genelde kısa ve düşük kullanımda roaming paketi pratik olur; veri tüketimi yüksek veya uzun seyahatlerde yerel SIM/eSIM daha uygun maliyet sağlar. Karar için gün sayısı ve internet ihtiyacını birlikte değerlendirmek gerekir."),

                // Alışveriş ve tax free
                ("İndirim dönemleri ne zaman olur?", "İndirim dönemleri ülkeden ülkeye değişse de genelde sezon sonlarında (kış/yaz) ve özel kampanya haftalarında yoğunlaşır. Seyahat tarihini yazarsan daha net alışveriş dönemi önerisi verebilirim."),
                ("Destinasyonda uygun mağazalar nerede olur?", "Uygun mağazalar için merkez alışveriş caddeleri + outlet bölgeleri birlikte değerlendirilmelidir. Merkezde yeni sezon, outlette önceki sezon ürünleri daha avantajlı bulunur."),
                ("Outlet mi merkez mağaza mı daha uygun?", "Fiyat odaklı alışverişte outlet genelde daha uygundur; yeni sezon ve güncel koleksiyon için merkez mağazalar öne çıkar. En iyi sonuç için ikisini aynı gün planlamak faydalı olur."),
                ("Tax free oranları nedir?", "Tax free oranları ülkeye, ürün grubuna ve iade sağlayıcı kuruma göre değişir. Net oran için alışveriş yapacağın ülkedeki güncel resmi tax free kurallarını ve mağaza iade oranını kontrol etmelisin."),
                ("Hangi ülkelerde tax free var?", "Birçok ülkede turistlere yönelik tax free iadesi bulunur; ancak minimum alışveriş tutarı, ürün kapsamı ve iade yöntemi farklıdır. Gideceğin ülkeyi yazarsan temel kuralları özetleyebilirim."),
                ("Tax free nasıl alınır?", "Genelde tax free anlaşmalı mağazadan alışveriş yapıp pasaport bilgisiyle form doldurulur. Çıkış noktasında gümrük onayı alınır ve iade nakit veya karta yapılır."),
                ("Tax free için gerekli evraklar neler?", "Genelde pasaport, tax free formu ve alışveriş faturası gerekir. Ürünü kullanılmamış halde göstermen istenebilir; ülkeye göre ek belge talebi olabilir."),

                // Acil durum ve güvenlik
                ("roma güvenli mi", "Roma genel olarak güvenlidir; kalabalık alanlarda çanta ve cüzdan için dikkatli olun."),
                ("Seyahat çantasında neler olmalı?", "Seyahat çantası için temel liste: pasaport/kimlik, bilet ve rezervasyon çıktıları, ilaçlar, şarj cihazı-powerbank, kişisel bakım ürünleri (100 ml kuralına uygun), yedek kıyafet, rahat ayakkabı, adaptör ve küçük ilk yardım seti."),
                ("Pasaportum çalındı ne yapmalıyım?", "Polis tutanağı al, en yakın Türkiye Cumhuriyeti temsilciliğiyle iletişime geç ve geçici seyahat belgesi sürecini başlat. Çok dilli cümleler: EN: My passport was stolen. IT: Mi hanno rubato il passaporto. AR: تمت سرقة جواز سفري."),
                ("Uçak için kabin bavul ölçüsü nedir?", "Kabin bavul ölçüsü havayoluna göre değişir; yaygın ölçü 55x40x20 cm civarındadır. Kesin ölçü ve kilo limitini bilet aldığınız havayolunun resmi kurallarından kontrol etmelisiniz."),
                ("Uçak bagaj ücreti ne kadar?", "Bagaj ücreti havayolu, rota, sınıf ve satın alma zamanına göre değişir. Online erken bagaj satın alma genelde havaalanına göre daha uygun olur."),
                ("Ekstra bavul ücreti nasıl hesaplanır?", "Ekstra bavul ücreti adet, kilo aşımı ve uçuş bölgesine göre hesaplanır. En doğru tutar için havayolunun bagaj hesaplama ekranında PNR ile kontrol önerilir."),
                ("Çantam çalındı ne yapmalıyım?", "Önce en yakın polis merkezine gidip tutanak al, kartlarını hemen bloke et, telefon/e-posta güvenliğini güncelle ve konaklama yerine bilgi ver. Çok dilli cümleler: EN: My bag was stolen. IT: Mi hanno rubato la borsa. AR: تمت سرقة حقيبتي."),

                // Konaklama
                ("Otel pansiyon airbnb ücretleri ne kadar olur?", "Konaklama maliyetinde otel genelde hizmet seviyesiyle birlikte daha yüksek olabilir; pansiyon daha ekonomik seçenek sunar; Airbnb ise konum ve sezona göre değişkenlik gösterir. Ülke, şehir ve tarih verirsen net düşük/orta/yüksek fiyat aralığı çıkarabilirim."),
                ("Hangi ülkede otel pansiyon airbnb daha uygun?", "Konaklama uygunluğu ülkeye, şehre ve sezona göre değişir. Genel olarak merkez dışı bölgelerde pansiyon ve bazı Airbnb seçenekleri daha uygun olabilir. Gideceğin ülke/destinasyonu yazarsan karşılaştırmalı tablo hazırlayabilirim."),
                ("Destinasyon için otel pansiyon airbnb karşılaştırması yap", "Destinasyon bazlı karşılaştırmada otel, pansiyon ve Airbnb için gecelik ortalama, konum avantajı, iptal esnekliği ve ulaşım maliyeti birlikte değerlendirilmelidir. Şehir adı ve gün sayısını yaz, net tablo çıkarayım."),

                // Ülke karşılaştırmaları
                ("italya türkiye", "İtalya ve Türkiye karşılaştırmasında bütçe için Türkiye, sanat ve tarihi şehir yoğunluğu için İtalya öne çıkar. İlgi alanını yazarsan net rota önerebilirim."),
                ("italya türkiye karşılaştır", "Türkiye'de ulaşım ve yeme-içme genelde daha ekonomiktir; İtalya'da müze, mimari ve şehir içi yürüyüş deneyimi daha öne çıkar."),

                // Türkiye genel
                ("türkiye", "Türkiye için başlangıç rotası: İstanbul 2 gün, Kapadokya 2 gün, İzmir-Efes 1 gün. İstersen bunu bütçene göre özelleştirebilirim."),
                ("türkiye gezisi", "Türkiye gezisinde mevsime göre rota seç: İlkbahar/sonbahar için Ege-Akdeniz, kış için Kapadokya ve Doğu Ekspresi hattı iyi seçeneklerdir."),

                // İstanbul önerileri
                ("istanbul için gezilecek yerler", "İstanbul'da gezilecek başlıca yerler: Ayasofya, Sultanahmet Camii, Topkapı Sarayı, Galata Kulesi, Kapalıçarşı, Dolmabahçe Sarayı ve Boğaz hattıdır."),
                ("istanbul gezilecek yerler", "İstanbul için kısa rota: 1) Sultanahmet-Ayasofya-Topkapı 2) Eminönü-Kapalıçarşı 3) Galata-Karaköy-Beyoğlu 4) Akşam Boğaz turu."),
                ("istanbulda nereler gezilir", "Tarihi yarımada için Sultanahmet-Eminönü hattı, modern şehir deneyimi için Karaköy-Galata-Cihangir-Beyoğlu hattı iyi bir plan sunar."),
                ("istanbulda ne yenir", "İstanbul'da balık ekmek, döner, kuru fasulye-pilav, simit, ıslak hamburger ve semt fırınlarından yerel tatlıları deneyebilirsin."),
                ("istanbul kahvaltı önerisi", "İstanbul'da kahvaltı için Beşiktaş, Kadıköy Yeldeğirmeni ve Emirgan çevresi hem manzara hem de farklı bütçelerde seçenek sunar."),
                ("istanbul akşam ne yapılır", "Akşam için Galataport-Karaköy yürüyüşü, Kadıköy Moda sahili, canlı müzik mekanları veya kısa bir Boğaz tekne turu iyi seçeneklerdir."),
                ("istanbul için konaklama önerisi", "İlk kez gelenler için Sultanahmet tarihi noktalara yakınlık sağlar; ulaşım ve sosyal hayat için Karaköy-Beyoğlu; Anadolu yakasında ise Kadıköy iyi bir konaklama bölgesidir."),

                // Bursa önerileri
                ("bursa için gezilecek yerler", "Bursa'da gezilecek başlıca yerler: Ulu Cami, Koza Han, Tophane, Cumalıkızık, Yeşil Türbe ve Uludağ'dır."),
                ("bursa gezilecek yerler", "Bursa için kısa rota: 1) Ulu Cami-Kapalı Çarşı-Koza Han 2) Tophane-Osman Gazi ve Orhan Gazi türbeleri 3) Cumalıkızık 4) Uludağ teleferik hattı."),
                ("bursada nereler gezilir", "Tarihi merkez için Hanlar Bölgesi ve Tophane, doğa için Uludağ ve Gölyazı, kültürel gezi için Cumalıkızık iyi seçeneklerdir."),
                ("bursada ne yenir", "Bursa'da İskender kebap, pideli köfte, cantık, kestane şekeri ve tahinli pide öne çıkan lezzetlerdir."),
                ("bursa kahvaltı önerisi", "Bursa'da kahvaltı için özellikle Cumalıkızık ve Nilüfer çevresindeki serpme kahvaltı mekanları tercih edilir."),
                ("bursa akşam ne yapılır", "Akşam için Tophane'de şehir manzarası, Mudanya sahil yürüyüşü veya FSM/Bademli hattında kafe-restoran planı yapılabilir."),
                ("bursa için konaklama önerisi", "Merkezi gezi için Osmangazi-Heykel çevresi, sosyal yaşam için Nilüfer, kayak planı için ise Uludağ oteller bölgesi uygundur."),

                // Mardin önerileri
                ("mardin için gezilecek yerler", "Mardin'de gezilecek başlıca yerler: Eski Mardin sokakları, Zinciriye Medresesi, Kasımiye Medresesi, Deyrulzafaran Manastırı ve Dara Antik Kenti'dir."),
                ("mardin gezilecek yerler", "Mardin için kısa rota: 1) Eski Mardin ve Ulu Cami 2) Zinciriye ve Kasımiye Medreseleri 3) Deyrulzafaran Manastırı 4) Dara Antik Kenti ve gün batımı manzarası."),
                ("mardinde nereler gezilir", "Tarihi doku için Eski Mardin ve medreseler, inanç turizmi için manastır hattı, kültürel keşif için Midyat ve çevresi iyi seçenekler sunar."),
                ("mardinde ne yenir", "Mardin'de kaburga dolması, sembusek, ikbebet, alluciye ve harire tatlısı gibi yöresel lezzetleri deneyebilirsin."),
                ("mardin kahvaltı önerisi", "Mardin'de kahvaltı için manzaralı teras mekanlar ve yöresel ürün sunan eski şehirdeki işletmeler iyi bir deneyim sağlar."),
                ("mardin akşam ne yapılır", "Akşam için taş sokaklarda yürüyüş, mezopotamya manzaralı teraslarda kahve ve yöresel müzikli mekanlarda yemek planı yapılabilir."),
                ("mardin için konaklama önerisi", "Tarihi atmosfer için Eski Mardin'deki taş konak oteller, ulaşım kolaylığı için ise Yenişehir bölgesi uygun bir konaklama seçeneğidir."),

                // Diğer Türkiye şehir önerileri
                ("urfa için gezilecek yerler", "Urfa'da gezilecek başlıca yerler: Balıklıgöl, Göbeklitepe, Urfa Kalesi, Haleplibahçe Mozaik Müzesi ve Tarihi Çarşılar bölgesidir."),
                ("urfa gezilecek yerler", "Urfa için kısa rota: 1) Balıklıgöl ve çevresi 2) Göbeklitepe 3) Harran Kümbet Evleri 4) Akşam sıra gecesi deneyimi."),
                ("urfada nereler gezilir", "Tarihi ve inanç turizmi için Balıklıgöl-Hz. İbrahim hattı, arkeoloji için Göbeklitepe ve şehir dışı kültür rotası için Harran öne çıkar."),
                ("urfada ne yenir", "Urfa'da ciğer kebabı, lahmacun, borani, ağzı açık ve şıllık tatlısı öne çıkan lezzetlerdir."),
                ("urfa akşam ne yapılır", "Akşam için Balıklıgöl çevresinde yürüyüş, yerel müzikli mekanlarda sıra gecesi ve çarşıda kahve molası iyi bir plan olur."),
                ("urfa için konaklama önerisi", "Tarihi dokuya yakın olmak için Balıklıgöl çevresi, daha modern konaklama için Karaköprü bölgesi tercih edilebilir."),

                ("diyarbakır için gezilecek yerler", "Diyarbakır'da gezilecek başlıca yerler: Sur içi, Diyarbakır Surları, Ulu Cami, Hasanpaşa Hanı, On Gözlü Köprü ve Hevsel Bahçeleri'dir."),
                ("diyarbakır gezilecek yerler", "Diyarbakır için kısa rota: 1) Sur içi-Ulu Cami 2) Hasanpaşa Hanı ve tarihi çarşı 3) Keçi Burcu ve sur yürüyüşü 4) On Gözlü Köprü."),
                ("diyarbakırda nereler gezilir", "Tarihi keşif için Sur içi, manzara için Keçi Burcu-Hevsel hattı, kültürel gezi için müzeler ve hanlar bölgesi önerilir."),
                ("diyarbakırda ne yenir", "Diyarbakır'da kaburga dolması, meftune, ciğer kebabı, içli köfte ve burma kadayıf denenebilir."),
                ("diyarbakır akşam ne yapılır", "Akşam için Sur içinde yürüyüş, tarihi hanlarda çay-kahve molası ve yerel mutfak restoranlarında yemek planı yapılabilir."),
                ("diyarbakır için konaklama önerisi", "Turistik noktalara yakınlık için Sur çevresi, daha yeni otel seçenekleri için Kayapınar ve Yenişehir bölgeleri uygundur."),

                ("bolu için gezilecek yerler", "Bolu'da gezilecek başlıca yerler: Abant Gölü, Gölcük Tabiat Parkı, Yedigöller, Mudurnu ve Kartalkaya bölgesidir."),
                ("bolu gezilecek yerler", "Bolu için kısa rota: 1) Gölcük 2) Abant 3) Yedigöller 4) Mudurnu tarihi sokaklar."),
                ("boluda nereler gezilir", "Doğa odaklı gezi için Abant-Gölcük-Yedigöller hattı, kültürel gezi için Mudurnu-Göynük rotası iyi seçenekler sunar."),
                ("boluda ne yenir", "Bolu'da mengen pilavı, keşli cevizli erişte, etli yemekler ve yöresel kahvaltı ürünleri öne çıkar."),
                ("bolu akşam ne yapılır", "Akşam için göl çevresi yürüyüşü, dağ otellerinde dinlenme ve merkezde sakin kafe-restoran planı yapılabilir."),
                ("bolu için konaklama önerisi", "Doğa tatili için Abant ve Gölcük çevresi, kış sezonu için Kartalkaya, şehir erişimi için Bolu merkez önerilir."),

                ("rize için gezilecek yerler", "Rize'de gezilecek başlıca yerler: Ayder Yaylası, Pokut-Sal yaylaları, Fırtına Deresi, Zil Kale ve Çamlıhemşin hattıdır."),
                ("rize gezilecek yerler", "Rize için kısa rota: 1) Çamlıhemşin-Fırtına Vadisi 2) Zil Kale 3) Ayder Yaylası 4) Yayla manzara noktaları."),
                ("rizede nereler gezilir", "Doğa ve yayla deneyimi için Ayder ve çevre yaylalar, dere kenarı aktiviteleri için Fırtına Vadisi önerilir."),
                ("rizede ne yenir", "Rize'de muhlama, hamsili pilav, laz böreği, karalahana yemekleri ve yöresel çay ürünleri denenebilir."),
                ("rize akşam ne yapılır", "Akşam için dere kenarında dinlenme, yayla manzarasında kahve ve yöresel restoranlarda yemek planı uygundur."),
                ("rize için konaklama önerisi", "Doğa odaklı gezi için Ayder ve Çamlıhemşin, şehir merkezine yakın kalmak için Rize merkez konaklaması tercih edilebilir."),

                ("samsun için gezilecek yerler", "Samsun'da gezilecek başlıca yerler: Bandırma Vapuru Müzesi, Amisos Tepesi, Atakum sahili, Tekkeköy Mağaraları ve Amazon Köyü'dür."),
                ("samsun gezilecek yerler", "Samsun için kısa rota: 1) Bandırma Vapuru ve Doğu Park 2) Amisos Tepesi 3) Atakum sahil yürüyüşü 4) Akşam marina hattı."),
                ("samsunda nereler gezilir", "Tarihi rota için İlkadım çevresi, deniz ve sosyal yaşam için Atakum sahili, aile gezisi için park ve müze alanları önerilir."),
                ("samsunda ne yenir", "Samsun'da pide çeşitleri, nokul, kaz tiridi ve Karadeniz mutfağından balık ürünleri öne çıkar."),
                ("samsun akşam ne yapılır", "Akşam için Atakum sahilinde yürüyüş, marina çevresi kafeler ve canlı müzik mekanları iyi seçeneklerdir."),
                ("samsun için konaklama önerisi", "Sahil ve sosyal yaşam için Atakum, ulaşım kolaylığı ve merkezi konum için İlkadım bölgesi tercih edilebilir."),

                ("ladik için gezilecek yerler", "Ladik'te gezilecek başlıca yerler: Ladik Gölü, Akdağ Kayak Merkezi, tarihi kaplıca alanları ve doğal yürüyüş rotalarıdır."),
                ("ladik gezilecek yerler", "Ladik için kısa rota: 1) Ladik Gölü 2) Akdağ telesiyej hattı 3) Kaplıca bölgesi 4) Yerel pazar ve ilçe merkezi."),
                ("ladikte nereler gezilir", "Kış turizmi için Akdağ, doğa gezisi için göl çevresi ve dinlenme için kaplıca hattı öne çıkar."),
                ("ladikte ne yenir", "Ladik'te yöresel et yemekleri, hamur işleri ve Karadeniz mutfağından bölgesel lezzetler denenebilir."),
                ("ladik akşam ne yapılır", "Akşam için göl çevresinde sakin yürüyüş, ilçe merkezinde yerel mekanlarda yemek ve mevsimde dağ otellerinde dinlenme planlanabilir."),
                ("ladik için konaklama önerisi", "Kış sezonunda Akdağ çevresi, daha sakin bir plan için ilçe merkezi ve göl çevresindeki tesisler uygun olabilir."),

                ("göreme için gezilecek yerler", "Göreme'de gezilecek başlıca yerler: Göreme Açık Hava Müzesi, Aşk Vadisi, Güvercinlik Vadisi, Uçhisar ve gün doğumu balon noktalarıdır."),
                ("göreme gezilecek yerler", "Göreme için kısa rota: 1) Açık Hava Müzesi 2) Vadi yürüyüşü 3) Uçhisar gün batımı 4) Ertesi sabah balon izleme veya tur."),
                ("göremede nereler gezilir", "Kaya oluşumları için vadi hatları, tarih için açık hava müzesi, manzara için Uçhisar seyir noktaları önerilir."),
                ("göremede ne yenir", "Göreme'de testi kebabı, çömlek yemekleri, gözleme çeşitleri ve Kapadokya şarap eşleşmeleri öne çıkar."),
                ("göreme akşam ne yapılır", "Akşam için gün batımı noktalarında manzara, kaya restoranlarda yemek ve canlı müzikli mekanlarda vakit planlanabilir."),
                ("göreme için konaklama önerisi", "Bölge deneyimi için mağara oteller, daha ekonomik seçenekler için Göreme merkez pansiyonları tercih edilebilir."),

                ("sivas için gezilecek yerler", "Sivas'ta gezilecek başlıca yerler: Divriği Ulu Camii ve Darüşşifası, Çifte Minareli Medrese, Buruciye Medresesi, Gök Medrese ve Eğri Köprü'dür."),
                ("sivas gezilecek yerler", "Sivas için kısa rota: 1) Kent meydanı ve medreseler 2) Arkeoloji-etnografya müzeleri 3) Divriği günübirlik gezi 4) Akşam yöresel mutfak."),
                ("sivasta nereler gezilir", "Tarihi gezi için şehir merkezi medrese hattı, UNESCO odaklı kültür turu için Divriği rotası önerilir."),
                ("sivasta ne yenir", "Sivas'ta Sivas köftesi, madımak, peskütan çorbası, hingel ve katmer öne çıkan lezzetlerdir."),
                ("sivas akşam ne yapılır", "Akşam için meydan çevresinde yürüyüş, yerel tatlı-kafe molası ve yöresel restoranlarda yemek planı yapılabilir."),
                ("sivas için konaklama önerisi", "Tarihi alanlara yakınlık için şehir merkezi, araçlı seyahat için ana ulaşım akslarına yakın bölgeler tercih edilebilir."),

                // İtalya şehir önerileri
                ("milano için gezilecek yerler", "Milano'da gezilecek başlıca yerler: Duomo, Galleria Vittorio Emanuele II, Sforza Kalesi, Brera ve Navigli bölgesidir."),
                ("milano gezilecek yerler", "Milano için kısa rota: 1) Duomo ve çevresi 2) Brera-Sforza hattı 3) Moda caddeleri 4) Akşam Navigli."),
                ("milanoda nereler gezilir", "Tarihi merkez için Duomo çevresi, sanat için Brera, akşam sosyal yaşam için Navigli hattı önerilir."),
                ("milanoda ne yenir", "Milano'da risotto alla milanese, ossobuco, cotoletta ve panettone gibi yerel tatlar denenebilir."),
                ("milano akşam ne yapılır", "Akşam için Navigli kanalları çevresinde yürüyüş ve aperitivo kültürü deneyimi popülerdir."),
                ("milano için konaklama önerisi", "Merkezi gezi için Duomo-Brera hattı, ulaşım kolaylığı için Centrale çevresi, daha sakin plan için Porta Romana uygundur."),

                ("venedik için gezilecek yerler", "Venedik'te gezilecek başlıca yerler: San Marco Meydanı, Rialto Köprüsü, Büyük Kanal, Dükler Sarayı ve Murano-Burano adalarıdır."),
                ("venedik gezilecek yerler", "Venedik için kısa rota: 1) San Marco-Dükler Sarayı 2) Rialto ve kanal hattı 3) Accademia çevresi 4) Murano/Burano turu."),
                ("venedikte nereler gezilir", "Ana adada tarihi meydan ve köprüler, ada deneyimi için Murano-Burano, romantik rota için kanal kenarı yürüyüşler önerilir."),
                ("venedikte ne yenir", "Venedik'te cicchetti atıştırmalıkları, deniz ürünlü makarnalar, risotto al nero di seppia ve tiramisu öne çıkar."),
                ("venedik akşam ne yapılır", "Akşam için kalabalığın azaldığı ara sokaklarda yürüyüş, kanal kenarında yemek ve klasik müzik etkinlikleri tercih edilebilir."),
                ("venedik için konaklama önerisi", "İlk kez gelenler için San Marco-San Polo hattı, daha ekonomik ve sakin konaklama için Mestre bölgesi değerlendirilebilir."),

                ("floransa için gezilecek yerler", "Floransa'da gezilecek başlıca yerler: Duomo, Ponte Vecchio, Uffizi Galerisi, Piazza della Signoria ve Boboli Bahçeleri'dir."),
                ("floransa gezilecek yerler", "Floransa için kısa rota: 1) Duomo ve merkez meydanlar 2) Uffizi-Accademia müzeleri 3) Arno kıyısı-Ponte Vecchio 4) Piazzale Michelangelo gün batımı."),
                ("floransada nereler gezilir", "Rönesans sanatı için müze hattı, tarihi yürüyüş için merkez sokaklar, manzara için tepe noktaları önerilir."),
                ("floransada ne yenir", "Floransa'da bistecca alla fiorentina, ribollita, pappa al pomodoro ve cantucci tatlıları denenebilir."),
                ("floransa akşam ne yapılır", "Akşam için Arno nehri çevresinde yürüyüş, canlı meydanlarda kahve molası ve manzara noktalarında gün batımı planı uygundur."),
                ("floransa için konaklama önerisi", "Yürüyerek gezi için tarihi merkez, daha sakin konaklama için Santa Croce ve Oltrarno bölgeleri uygun seçeneklerdir."),

                ("verona için gezilecek yerler", "Verona'da gezilecek başlıca yerler: Arena di Verona, Piazza delle Erbe, Castelvecchio, Adige kıyısı ve Juliet Evi'dir."),
                ("verona gezilecek yerler", "Verona için kısa rota: 1) Arena ve merkez meydanlar 2) Castelvecchio köprü hattı 3) Juliet Evi 4) Akşam nehir kenarı yürüyüş."),
                ("veronada nereler gezilir", "Tarihi merkez yürüyüşleri, Roma döneminden kalma yapılar ve manzara için nehir hattı çevresi önerilir."),
                ("veronada ne yenir", "Verona'da risotto all'amarone, polenta yemekleri, gnocchi ve bölgesel şarap eşleşmeleri öne çıkar."),
                ("verona akşam ne yapılır", "Akşam için piazza çevresinde canlı atmosfer, nehir kenarında yürüyüş ve yaz aylarında arena etkinlikleri tercih edilebilir."),
                ("verona için konaklama önerisi", "Yürüyüş odaklı gezi için centro storico, daha uygun fiyat için istasyon çevresi ve merkez dışı bölgeler değerlendirilebilir."),

                ("napoli için gezilecek yerler", "Napoli'de gezilecek başlıca yerler: Tarihi merkez, Spaccanapoli, Napoli Katedrali, Castel dell'Ovo, Pompeii ve Amalfi çıkış noktalarıdır."),
                ("napoli gezilecek yerler", "Napoli için kısa rota: 1) Tarihi merkez-Spaccanapoli 2) Müzeler hattı 3) Castel dell'Ovo ve sahil 4) Pompeii günübirlik gezi."),
                ("napolide nereler gezilir", "Kültürel gezi için centro storico, deniz manzarası için lungomare hattı, yakın çevre turu için Pompeii-Sorrento bağlantısı önerilir."),
                ("napolide ne yenir", "Napoli'de napoliten pizza, sfogliatella, deniz ürünleri makarnaları ve espresso kültürü öne çıkar."),
                ("napoli akşam ne yapılır", "Akşam için sahil hattında yürüyüş, tarihi merkezde sokak lezzetleri ve canlı meydanlarda sosyal ortam tercih edilebilir."),
                ("napoli için konaklama önerisi", "Merkezi gezi için tarihi merkez ve Toledo çevresi, daha sakin konaklama için Chiaia ve Vomero bölgeleri uygundur."),

                // Diğer Avrupa şehir önerileri
                ("amsterdam için gezilecek yerler", "Amsterdam'da gezilecek başlıca yerler: Dam Meydanı, Rijksmuseum, Van Gogh Müzesi, Anne Frank Evi ve kanal hattıdır."),
                ("masterdam için gezilecek yerler", "Amsterdam'da gezilecek başlıca yerler: Dam Meydanı, Rijksmuseum, Van Gogh Müzesi, Anne Frank Evi ve kanal hattıdır."),
                ("amsterdamda ne yenir", "Amsterdam'da stroopwafel, haring, bitterballen ve yerel peynir çeşitleri öne çıkar."),
                ("amsterdam akşam ne yapılır", "Akşam için kanal turu, Jordaan çevresinde yürüyüş ve müze/konser etkinlikleri tercih edilebilir."),
                ("amsterdam için konaklama önerisi", "Merkezi gezi için Centrum ve Jordaan, daha sakin plan için De Pijp ve Amsterdam Zuid bölgeleri uygundur."),

                ("moskova için gezilecek yerler", "Moskova'da gezilecek başlıca yerler: Kızıl Meydan, Kremlin, Aziz Vasil Katedrali, Arbat Caddesi ve Gorki Parkı'dır."),
                ("moscha için gezilecek yerler", "Moskova'da gezilecek başlıca yerler: Kızıl Meydan, Kremlin, Aziz Vasil Katedrali, Arbat Caddesi ve Gorki Parkı'dır."),
                ("moskovada ne yenir", "Moskova'da borsch, pelmeni, blini ve beef stroganoff gibi klasik Rus yemekleri denenebilir."),
                ("moskova akşam ne yapılır", "Akşam için Tverskaya hattında yürüyüş, nehir turu ve kültür merkezlerinde etkinlik planı yapılabilir."),
                ("moskova için konaklama önerisi", "İlk kez gelenler için Kızıl Meydan çevresi, ulaşım kolaylığı için metro hatlarına yakın merkez bölgeler önerilir."),

                ("gent için gezilecek yerler", "Gent'te gezilecek başlıca yerler: Gravensteen Kalesi, Saint Bavo Katedrali, Graslei-Korenlei hattı ve tarihi merkezdir."),
                ("gentte ne yenir", "Gent'te waterzooi, stoofvlees, Belçika waffle'ı ve yerel çikolata çeşitleri öne çıkar."),
                ("gent akşam ne yapılır", "Akşam için kanal kenarında yürüyüş, tarihi merkezde meydanlar ve yerel kafelerde dinlenme önerilir."),
                ("gent için konaklama önerisi", "Tarihi alanlara yakınlık için şehir merkezi, daha ekonomik seçenekler için merkez dışı tramvay bağlantılı bölgeler uygundur."),

                ("brüksel için gezilecek yerler", "Brüksel'de gezilecek başlıca yerler: Grand Place, Atomium, Manneken Pis, Kraliyet Sarayı ve Avrupa Parlamentosu çevresidir."),
                ("brükselde ne yenir", "Brüksel'de waffle, moules-frites, Belçika çikolatası ve çeşitli craft bira deneyimleri öne çıkar."),
                ("brüksel akşam ne yapılır", "Akşam için Grand Place çevresinde yürüyüş, canlı meydanlar ve gastronomi odaklı restoranlar iyi seçeneklerdir."),
                ("brüksel için konaklama önerisi", "Merkez gezi için Grand Place çevresi, iş ve ulaşım için Avrupa bölgesi, daha sakin konaklama için Ixelles değerlendirilebilir."),

                // Suudi Arabistan şehir önerileri
                ("mekke için gezilecek yerler", "Mekke'de ziyaret odaklı başlıca noktalar: Mescid-i Haram çevresi, Cebel-i Nur ve Hira Mağarası hattıdır."),
                ("mekkede ne yenir", "Mekke'de Arap mutfağından kabsa, mutabbaq, hurma çeşitleri ve yöresel çorba yemekleri öne çıkar."),
                ("mekke akşam ne yapılır", "Akşam saatlerinde ibadet programı, çevre çarşılarda kısa alışveriş ve dinlenme odaklı plan tercih edilebilir."),
                ("mekke için konaklama önerisi", "Mescid-i Haram'a yürüme mesafesindeki oteller ibadet odaklı seyahatlerde zaman avantajı sağlar."),

                ("medine için gezilecek yerler", "Medine'de ziyaret odaklı başlıca noktalar: Mescid-i Nebevi çevresi, Kuba Mescidi, Uhud bölgesi ve Kıbleteyn Mescidi'dir."),
                ("medinede ne yenir", "Medine'de Arap usulü pilavlar, et yemekleri, hurma ürünleri ve yerel tatlı çeşitleri öne çıkar."),
                ("medine akşam ne yapılır", "Akşam için Mescid-i Nebevi çevresinde sakin yürüyüş, ibadet programı ve yerel çarşı ziyareti planlanabilir."),
                ("medine için konaklama önerisi", "Mescid-i Nebevi çevresindeki oteller yürüme kolaylığı sağlar; aileler için servisli oteller tercih edilebilir."),

                ("cidde için gezilecek yerler", "Cidde'de gezilecek başlıca yerler: Al-Balad tarihi bölgesi, Cidde Corniche, Kral Fahd Çeşmesi ve sahil yürüyüş alanlarıdır."),
                ("ciddede ne yenir", "Cidde'de deniz ürünleri, Arap pilavları, kebap çeşitleri ve hurma bazlı tatlılar öne çıkar."),
                ("cidde akşam ne yapılır", "Akşam için Corniche hattında yürüyüş, sahil kafeleri ve alışveriş merkezlerinde vakit planı yapılabilir."),
                ("cidde için konaklama önerisi", "Deniz manzarası ve sosyal yaşam için Corniche çevresi, iş odaklı seyahat için merkezi iş bölgeleri uygundur."),

                // Ek Türkiye şehir önerileri
                ("eskişehir için gezilecek yerler", "Eskişehir'de gezilecek başlıca yerler: Odunpazarı Evleri, Sazova Bilim Kültür Parkı, Porsuk Çayı ve Kentpark bölgesidir."),
                ("eskişehirde nereler gezilir", "Kültürel gezi için Odunpazarı, şehir içi yürüyüş için Porsuk hattı ve aile gezisi için Sazova çevresi önerilir."),
                ("eskişehirde ne yenir", "Eskişehir'de çibörek, balaban kebabı, met helvası ve haşhaşlı çörek öne çıkan lezzetlerdir."),
                ("eskişehir akşam ne yapılır", "Akşam için Porsuk kenarında yürüyüş, Adalar bölgesinde kafe-restoran planı ve canlı müzik mekanları tercih edilebilir."),
                ("eskişehir için konaklama önerisi", "Merkezi gezi için Tepebaşı ve Adalar çevresi, daha sakin plan için Odunpazarı çevresi uygun olabilir."),

                ("antalya için gezilecek yerler", "Antalya'da gezilecek başlıca yerler: Kaleiçi, Düden Şelalesi, Konyaaltı-Lara sahili, Perge-Aspendos ve Olympos hattıdır."),
                ("antalyada nereler gezilir", "Şehir merkezi için Kaleiçi ve marina, deniz tatili için Konyaaltı-Lara, antik kent rotası için Perge ve Side önerilir."),
                ("antalyada ne yenir", "Antalya'da piyaz, tahinli kabak tatlısı, hibeş, deniz ürünleri ve yöresel serpme kahvaltı öne çıkar."),
                ("antalya akşam ne yapılır", "Akşam için Kaleiçi sokaklarında yürüyüş, marina çevresinde yemek ve sahil hattında gün batımı planı yapılabilir."),
                ("antalya için konaklama önerisi", "Tarihi doku için Kaleiçi, deniz tatili için Lara-Konyaaltı, aile odaklı tesisler için Kundu-Belek hattı uygundur."),

                ("izmir için gezilecek yerler", "İzmir'de gezilecek başlıca yerler: Kordon, Saat Kulesi, Kemeraltı, Asansör, Alaçatı-Çeşme ve Efes çevresidir."),
                ("izmirde nereler gezilir", "Şehir gezi için Konak-Kordon hattı, deniz kasabası deneyimi için Alaçatı-Urla, tarih için Efes-Selçuk rotası önerilir."),
                ("izmirde ne yenir", "İzmir'de boyoz, kumru, gevrek, söğüş ve deniz ürünleri öne çıkan lezzetlerdir."),
                ("izmir akşam ne yapılır", "Akşam için Kordon yürüyüşü, Alsancak'ta sosyal yaşam ve sahil kasabalarında gün batımı planı uygundur."),
                ("izmir için konaklama önerisi", "Şehir merkezi için Alsancak-Konak, tatil odaklı plan için Çeşme-Alaçatı, sakin konaklama için Urla tercih edilebilir."),

                ("konya için gezilecek yerler", "Konya'da gezilecek başlıca yerler: Mevlana Müzesi, Alaeddin Tepesi, İnce Minareli Medrese, Sille ve Tropikal Kelebek Bahçesi'dir."),
                ("konyada nereler gezilir", "İnanç ve tarih için Mevlana-Selçuklu hattı, kültürel gezi için Sille bölgesi ve aile gezisi için park alanları uygundur."),
                ("konyada ne yenir", "Konya'da etli ekmek, fırın kebabı, bamya çorbası, sac arası ve höşmerim gibi lezzetler denenebilir."),
                ("konya akşam ne yapılır", "Akşam için Mevlana çevresinde sakin yürüyüş, tarihi çarşı gezisi ve yerel mutfak restoranları tercih edilebilir."),
                ("konya için konaklama önerisi", "Merkezi gezi için Selçuklu-Karatay hattı, iş seyahati için ana ulaşım akslarına yakın modern oteller uygundur."),

                ("erzurum için gezilecek yerler", "Erzurum'da gezilecek başlıca yerler: Çifte Minareli Medrese, Yakutiye Medresesi, Erzurum Kalesi, Tortum Şelalesi ve Palandöken'dir."),
                ("erzurumda nereler gezilir", "Tarihi merkez için medrese-kale hattı, kış sporları için Palandöken ve doğa için Tortum çevresi önerilir."),
                ("erzurumda ne yenir", "Erzurum'da cağ kebabı, kadayıf dolması, ayran aşı çorbası, su böreği ve civil peyniri öne çıkar."),
                ("erzurum akşam ne yapılır", "Akşam için şehir merkezinde yürüyüş, yerel lezzet durakları ve kış sezonunda kayak sonrası dinlenme planı yapılabilir."),
                ("erzurum için konaklama önerisi", "Kış tatili için Palandöken otelleri, şehir gezisi için Yakutiye merkez ve çevresi uygun seçeneklerdir."),

                ("sinop için gezilecek yerler", "Sinop'ta gezilecek başlıca yerler: Sinop Kalesi, Hamsilos Koyu, Erfelek Şelaleleri, İnceburun ve tarihi cezaevi müzesidir."),
                ("sinopta nereler gezilir", "Doğa için Hamsilos-Erfelek hattı, şehir tarihi için kale ve müze çevresi, kıyı yürüyüşü için liman bölgesi önerilir."),
                ("sinopta ne yenir", "Sinop'ta mantı, nokul, mısır çorbası, balık çeşitleri ve yöresel hamur işleri öne çıkar."),
                ("sinop akşam ne yapılır", "Akşam için liman çevresinde yürüyüş, sahil kafeleri ve gün batımı manzara noktaları tercih edilebilir."),
                ("sinop için konaklama önerisi", "Merkezde liman çevresi kısa gezi için avantajlıdır; doğa odaklı plan için kıyı köyleri ve butik tesisler değerlendirilebilir."),

                ("trabzon için gezilecek yerler", "Trabzon'da gezilecek başlıca yerler: Sümela Manastırı, Uzungöl, Atatürk Köşkü, Boztepe ve Ayasofya Camii çevresidir."),
                ("trabzun için gezilecek yerler", "Trabzon'da gezilecek başlıca yerler: Sümela Manastırı, Uzungöl, Atatürk Köşkü, Boztepe ve Ayasofya Camii çevresidir."),
                ("trabzonda nereler gezilir", "Doğa için Uzungöl ve yaylalar, tarih için Sümela-Manastır hattı, şehir manzarası için Boztepe önerilir."),
                ("trabzonda ne yenir", "Trabzon'da kuymak, Akçaabat köftesi, hamsili pilav, kara lahana çorbası ve laz böreği öne çıkar."),
                ("trabzon akşam ne yapılır", "Akşam için Boztepe'de manzara, sahil hattında yürüyüş ve yerel mutfak restoranlarında yemek planı yapılabilir."),
                ("trabzon için konaklama önerisi", "Merkez gezisi için Ortahisar çevresi, doğa tatili için Uzungöl ve yayla bölgesindeki tesisler uygundur."),

                ("ordu için gezilecek yerler", "Ordu'da gezilecek başlıca yerler: Boztepe, teleferik hattı, Perşembe Yaylası, Yason Burnu ve sahil yürüyüş alanlarıdır."),
                ("orduda nereler gezilir", "Şehir manzarası için Boztepe, doğa için yayla rotaları, kıyı deneyimi için Perşembe ve Yason hattı önerilir."),
                ("orduda ne yenir", "Ordu'da melocan, pancar çorbası, hamsi yemekleri, fındık bazlı tatlılar ve yöresel pide çeşitleri denenebilir."),
                ("ordu akşam ne yapılır", "Akşam için teleferikle Boztepe manzarası, sahil yürüyüşü ve merkezde kafe-restoran planı uygun olur."),
                ("ordu için konaklama önerisi", "Merkez konaklama için Altınordu, doğa odaklı tatil için yayla çevresindeki butik tesisler tercih edilebilir."),

                ("aydın için gezilecek yerler", "Aydın'da gezilecek başlıca yerler: Tralleis Antik Kenti, Afrodisias (il sınırına yakın), Kuşadası kıyısı ve Dilek Yarımadası hattıdır."),
                ("aydında nereler gezilir", "Tarih için antik kent rotaları, deniz tatili için Kuşadası-Davutlar hattı, doğa için milli park çevresi önerilir."),
                ("aydında ne yenir", "Aydın'da çöp şiş, keşkek, yuvarlama, incir tatlıları ve zeytinyağlı Ege yemekleri öne çıkar."),
                ("aydın akşam ne yapılır", "Akşam için sahil kasabalarında yürüyüş, merkezde yöresel restoranlar ve yaz döneminde açık hava etkinlikleri tercih edilebilir."),
                ("aydın için konaklama önerisi", "Deniz odaklı tatilde Kuşadası hattı, şehir içi erişim için Aydın merkez ve çevresi konaklama açısından uygundur."),

                ("amasya için gezilecek yerler", "Amasya'da gezilecek başlıca yerler: Kral Kaya Mezarları, Yalıboyu evleri, Amasya Kalesi, II. Bayezid Külliyesi ve Borabay Gölü'dür."),
                ("amasyada nereler gezilir", "Tarihi merkez için Yeşilırmak kıyısı ve Yalıboyu hattı, kültürel gezi için müze-külliye çevresi, doğa için Borabay Gölü önerilir."),
                ("amasyada ne yenir", "Amasya'da keşkek, bakla dolması, etli bamya, toyga çorbası ve elma tatlıları öne çıkar."),
                ("amasya akşam ne yapılır", "Akşam için Yeşilırmak kıyısında yürüyüş, ışıklandırılmış kaya mezarlarını seyir ve nehir çevresinde kafe molası iyi bir plandır."),
                ("amasya için konaklama önerisi", "Tarihi dokuya yakınlık için merkezde Yalıboyu çevresi, daha sakin plan için şehir merkezine yakın butik oteller tercih edilebilir."),

                ("almanya için gezilecek yerler", "Almanya'da gezilecek başlıca yerler: Berlin, Münih, Hamburg, Köln, Heidelberg ve Kara Orman hattıdır."),
                ("almanyada nereler gezilir", "Tarih için Berlin, kültür ve mimari için Münih-Köln, doğa için Bavyera Alpleri ve Kara Orman bölgesi önerilir."),
                ("almanyada ne yenir", "Almanya'da bratwurst, schnitzel, pretzel, kartoffelsalat ve bölgesel fırın ürünleri öne çıkar."),
                ("almanya akşam ne yapılır", "Akşam için şehir merkezlerinde yürüyüş, nehir kıyısı bölgelerinde sosyal yaşam ve kültür-sanat etkinlikleri tercih edilebilir."),
                ("almanya için konaklama önerisi", "Şehir gezi odaklı planlarda merkez ve toplu taşımaya yakın bölgeler, daha sakin konaklama için merkez dışı bağlantılı semtler uygundur."),

                ("ingiltere için gezilecek yerler", "İngiltere'de gezilecek başlıca yerler: Londra, Oxford, Cambridge, Bath, Manchester ve Liverpool hattıdır."),
                ("ingilterede nereler gezilir", "Tarih ve müze odaklı gezi için Londra-Bath, üniversite şehir deneyimi için Oxford-Cambridge, futbol ve müzik kültürü için Manchester-Liverpool önerilir."),
                ("ingilterede ne yenir", "İngiltere'de fish and chips, Sunday roast, pie çeşitleri, full English breakfast ve afternoon tea öne çıkar."),
                ("ingiltere akşam ne yapılır", "Akşam için şehir merkezlerinde yürüyüş, tiyatro-müzikal programları, nehir kıyısı bölgelerinde yemek ve kültür etkinlikleri tercih edilebilir."),
                ("ingiltere için konaklama önerisi", "Londra ve büyük şehirlerde toplu taşımaya yakın merkez bölgeler gezi için avantajlıdır; daha uygun bütçe için merkez dışı ama metro/tren bağlantılı bölgeler tercih edilebilir."),

                ("fransa için gezilecek yerler", "Fransa'da gezilecek başlıca yerler: Paris, Nice, Lyon, Bordeaux, Strasbourg ve Loire Vadisi hattıdır."),
                ("fransada nereler gezilir", "Sanat ve tarih için Paris-Lyon, sahil için Nice-Cannes, bağ rotaları için Bordeaux ve Alsace bölgesi önerilir."),
                ("fransada ne yenir", "Fransa'da croissant, quiche, ratatouille, coq au vin, crème brûlée ve bölgesel peynir çeşitleri öne çıkar."),
                ("fransa akşam ne yapılır", "Akşam için şehir merkezlerinde yürüyüş, müze ve gösteri programları, nehir kıyısı restoranlarında yemek planı yapılabilir."),
                ("fransa için konaklama önerisi", "Büyük şehirlerde metro/tramvay hatlarına yakın merkez bölgeler gezi için avantajlıdır; daha uygun bütçe için merkez dışı ama toplu taşımaya bağlı semtler tercih edilebilir."),

                ("paris için gezilecek yerler", "Paris'te gezilecek başlıca yerler: Eyfel Kulesi, Louvre Müzesi, Notre-Dame çevresi, Montmartre, Şanzelize ve Seine Nehri hattıdır."),
                ("pariste nereler gezilir", "İlk gezi için Eyfel-Louvre-Şanzelize hattı, sanat ve bohem atmosfer için Montmartre, sakin yürüyüş için Seine kıyısı önerilir."),
                ("pariste ne yenir", "Paris'te kruvasan, baget sandviç, soğan çorbası, makaron ve Fransız pastacılığı ürünleri öne çıkar."),
                ("paris akşam ne yapılır", "Akşam için Seine nehir turu, Eyfel çevresinde gece manzarası, Latin Mahallesi ve canlı sokaklarda yürüyüş iyi seçeneklerdir."),
                ("paris için konaklama önerisi", "İlk kez gelenler için merkezi bölgeler (1-7. arrondissement), daha uygun bütçe için metro bağlantısı güçlü 9-11. bölgeler değerlendirilebilir."),

                ("belçika için gezilecek yerler", "Belçika'da gezilecek başlıca yerler: Brüksel, Brugge, Gent, Anvers ve Leuven hattıdır."),
                ("belçikada nereler gezilir", "Tarihi doku için Brugge-Gent, şehir ve kurum merkezleri için Brüksel, sanat ve moda için Anvers önerilir."),
                ("belçikada ne yenir", "Belçika'da waffle, çikolata, moules-frites, çeşitli bira kültürü ve yerel peynir ürünleri öne çıkar."),
                ("belçika akşam ne yapılır", "Akşam için tarihi meydanlarda yürüyüş, kanal çevresi restoranları ve kültür-sanat etkinlikleri tercih edilebilir."),
                ("belçika için konaklama önerisi", "Brüksel merkez kısa şehir gezileri için avantajlıdır; Brugge/Gent odaklı planlarda tarihi merkeze yakın butik oteller iyi bir seçenektir."),

                ("özbekistan için gezilecek yerler", "Özbekistan'da gezilecek başlıca yerler: Taşkent, Semerkant, Buhara, Hive ve Fergana Vadisi hattıdır."),
                ("özbekistanda nereler gezilir", "Tarihi İpek Yolu rotası için Semerkant-Buhara-Hive, modern şehir deneyimi için Taşkent, kültürel çeşitlilik için Fergana önerilir."),
                ("özbekistanda ne yenir", "Özbekistan'da plov, samsa, lagman, şaşlık ve yöresel tandır ekmekleri öne çıkan lezzetlerdir."),
                ("özbekistan akşam ne yapılır", "Akşam için tarihi meydanlarda yürüyüş, yerel çayhanelerde dinlenme ve geleneksel müzikli mekanlarda vakit planlanabilir."),
                ("özbekistan için konaklama önerisi", "Şehir gezileri için merkezde toplu taşımaya yakın oteller, tarihi atmosfer için eski şehir bölgelerindeki butik konaklamalar uygundur."),

                ("kıbrıs için gezilecek yerler", "Kıbrıs'ta gezilecek başlıca yerler: Lefkoşa Surlariçi, Girne Kalesi, Bellapais Manastırı, Gazimağusa surları ve Karpaz hattıdır."),
                ("kıbrısta nereler gezilir", "Tarihi gezi için Lefkoşa ve Gazimağusa, deniz tatili için Girne-Karpaz sahilleri, kültürel rota için Bellapais ve çevresi önerilir."),
                ("kıbrısta ne yenir", "Kıbrıs'ta şeftali kebabı, hellim, molehiya, kolokas ve ceviz macunu gibi yerel lezzetler öne çıkar."),
                ("kıbrıs akşam ne yapılır", "Akşam için Girne limanında yürüyüş, sahil restoranlarında yemek ve merkezde canlı müzikli mekanlar tercih edilebilir."),
                ("kıbrıs için konaklama önerisi", "Deniz odaklı tatil için Girne ve Bafra hattı, şehir gezisi ve ulaşım kolaylığı için Lefkoşa çevresi uygun seçeneklerdir."),

                ("türkmenistan için gezilecek yerler", "Türkmenistan'da gezilecek başlıca yerler: Aşkabat, Merv Antik Kenti, Köneürgenç, Darvaza Krateri ve Avaza sahil hattıdır."),
                ("türkmenistanda nereler gezilir", "Tarih için Merv ve Köneürgenç, modern şehir deneyimi için Aşkabat, doğa ve sıra dışı deneyim için Darvaza bölgesi önerilir."),
                ("türkmenistanda ne yenir", "Türkmenistan'da plov, mantı, şaşlık, içli hamur işleri ve yerel çay kültürü öne çıkan lezzetlerdir."),
                ("türkmenistan akşam ne yapılır", "Akşam için şehir merkezlerinde sakin yürüyüş, yerel restoranlarda yemek ve kültürel etkinlik alanlarında vakit planlanabilir."),
                ("türkmenistan için konaklama önerisi", "Şehir gezileri için Aşkabat merkezde ulaşımı kolay oteller, bölgesel turlar için tur planına yakın konaklama noktaları tercih edilebilir."),

                ("bosna hersek için gezilecek yerler", "Bosna Hersek'te gezilecek başlıca yerler: Saraybosna Başçarşı, Mostar Köprüsü, Blagaj Tekkesi, Travnik ve Jajce şelaleleridir."),
                ("bosna hersekte nereler gezilir", "Tarih için Saraybosna-Mostar hattı, doğa için Blagaj ve Jajce, kültürel rota için Travnik bölgesi önerilir."),
                ("bosna hersekte ne yenir", "Bosna Hersek'te cevapi, börek çeşitleri, begova çorbası, dolma ve baklava öne çıkan lezzetlerdir."),
                ("bosna hersek akşam ne yapılır", "Akşam için Başçarşı ve Mostar eski şehirde yürüyüş, nehir kenarında yemek ve yerel kafelerde vakit planlanabilir."),
                ("bosna hersek için konaklama önerisi", "Saraybosna merkez kısa şehir gezileri için avantajlıdır; Mostar odaklı planlarda eski şehir çevresi iyi bir konaklama seçeneğidir."),

                ("karadağ için gezilecek yerler", "Karadağ'da gezilecek başlıca yerler: Kotor, Budva, Perast, Cetinje ve Durmitor Milli Parkı'dır."),
                ("karadağda nereler gezilir", "Sahil ve tarih için Kotor-Budva-Perast hattı, doğa ve dağ rotası için Durmitor ve Tara Kanyonu önerilir."),
                ("karadağda ne yenir", "Karadağ'da deniz ürünleri, Njeguški pršut, kačamak, yerel peynirler ve Balkan mutfağına yakın et yemekleri öne çıkar."),
                ("karadağ akşam ne yapılır", "Akşam için Kotor körfezi çevresinde yürüyüş, Budva eski şehirde sosyal yaşam ve sahil restoranlarında yemek tercih edilebilir."),
                ("karadağ için konaklama önerisi", "Sahil tatili için Budva-Kotor hattı, daha sakin ve doğa odaklı plan için kuzey bölgelerdeki dağ otelleri uygundur."),

                ("singapur için gezilecek yerler", "Singapur'da gezilecek başlıca yerler: Marina Bay Sands, Gardens by the Bay, Sentosa Adası, Merlion Park ve Chinatown-Kampong Glam hattıdır."),
                ("singapurda nereler gezilir", "Şehir ikonu noktaları için Marina Bay, eğlence ve deniz için Sentosa, kültürel gezi için Chinatown-Little India-Kampong Glam önerilir."),
                ("singapurda ne yenir", "Singapur'da chicken rice, laksa, chili crab, satay ve hawker center sokak lezzetleri öne çıkar."),
                ("singapur akşam ne yapılır", "Akşam için Marina Bay ışık gösterileri, nehir kıyısında yürüyüş ve rooftop manzara noktalarında vakit planlanabilir."),
                ("singapur için konaklama önerisi", "İlk kez gelenler için Marina Bay ve City Hall çevresi ulaşım açısından avantajlıdır; daha uygun bütçe için Bugis ve Chinatown yakınları tercih edilebilir."),

                ("hatay için gezilecek yerler", "Hatay'da gezilecek başlıca yerler: Antakya Uzun Çarşı, Habib-i Neccar Camii, St. Pierre Kilisesi, Harbiye Şelaleleri ve Samandağ sahil hattıdır."),
                ("hatayda nereler gezilir", "Kültürel gezi için Antakya tarihi merkez, inanç turizmi için St. Pierre ve çevresi, doğa için Harbiye-Samandağ hattı önerilir."),
                ("hatayda ne yenir", "Hatay'da tepsi kebabı, kağıt kebabı, oruk, humus, zahter salatası ve künefe öne çıkan lezzetlerdir."),
                ("hatay akşam ne yapılır", "Akşam için tarihi çarşı çevresinde yürüyüş, yerel meze-kebap restoranlarında yemek ve sahil hattında sakin bir plan tercih edilebilir."),
                ("hatay için konaklama önerisi", "Şehir gezisi için Antakya merkez, deniz ve doğa odaklı plan için Samandağ-Arsuz hattı konaklama açısından uygun seçenekler sunar."),

                ("kütahya için gezilecek yerler", "Kütahya'da gezilecek başlıca yerler: Kütahya Kalesi, Aizanoi Antik Kenti, Çini Müzesi, Germiyan Sokak ve Frig Vadisi hattıdır."),
                ("kütahyada nereler gezilir", "Tarih için Aizanoi ve kale çevresi, kültürel gezi için çini atölyeleri ve Germiyan Sokak, doğa rotası için Frig Vadisi önerilir."),
                ("kütahyada ne yenir", "Kütahya'da cimcik, sıkıcık çorbası, dolamber böreği, güveç yemekleri ve yöresel hamur işleri öne çıkar."),
                ("kütahya akşam ne yapılır", "Akşam için tarihi merkezde yürüyüş, çini çarşısı çevresinde kafe molası ve yerel restoranlarda yemek planı yapılabilir."),
                ("kütahya için konaklama önerisi", "Şehir gezisi için merkez ve kale çevresi, termal tatil için ise Ilıca ve çevresindeki tesisler uygun seçeneklerdir."),

                ("rusya için gezilecek yerler", "Rusya'da gezilecek başlıca yerler: Moskova, St. Petersburg, Kazan, Soçi ve Altın Halka şehirleridir."),
                ("rusyada nereler gezilir", "Tarihi ve kültürel gezi için Moskova-St. Petersburg hattı, doğa ve kış turizmi için Soçi ve kuzey rotaları önerilir."),
                ("rusyada ne yenir", "Rusya'da borsch çorbası, pelmeni, blini, beef stroganoff ve çeşitli fırın ürünleri öne çıkar."),
                ("rusya akşam ne yapılır", "Akşam için şehir merkezlerinde yürüyüş, nehir kıyısı turları, tiyatro-bale etkinlikleri ve yerel restoran deneyimi tercih edilebilir."),
                ("rusya için konaklama önerisi", "Büyük şehirlerde metroya yakın merkezi bölgeler gezi için avantajlıdır; bütçe odaklı planlarda merkez dışı ama toplu taşımaya bağlı semtler tercih edilebilir."),

                // Ankara ve Ulus önerileri
                ("ankara için gezilecek yerler", "Ankara'da gezilecek başlıca yerler: Anıtkabir, Ankara Kalesi, Hamamönü, Anadolu Medeniyetleri Müzesi, Eymir Gölü ve Atakule."),
                ("ankara gezilecek yerler", "Ankara için kısa rota: 1) Anıtkabir 2) Anadolu Medeniyetleri Müzesi 3) Ankara Kalesi-Hamamönü 4) Akşam Atakule."),
                ("ankarada nereler gezilir", "Ankara'da tarihi ve kültürel noktalar için Ulus çevresi, doğa için Eymir Gölü ve Seğmenler Parkı iyi seçeneklerdir."),
                ("ankara yeme içme önerileri", "Ankara'da yeme-içme için Tunalı, Bahçelievler 7. Cadde ve Çayyolu bölgeleri farklı bütçelerde iyi seçenekler sunar."),
                ("ankarada ne yenir", "Ankara'da Ankara tava, Beypazarı güveci, yaprak döner ve yöresel kahvaltı seçeneklerini deneyebilirsin."),
                ("ankara kahvaltı önerisi", "Ankara'da hafta sonu kahvaltısı için İncek-Çayyolu hattı ve şehir merkezindeki serpme kahvaltı mekanları tercih edilebilir."),
                ("ankara uygun fiyatlı yemek", "Ankara'da uygun fiyatlı yemek için Kızılay çevresi, öğrenci bölgeleri ve esnaf lokantaları iyi alternatiflerdir."),
                ("ankara akşam ne yapılır", "Ankara'da akşam için Atakule manzarası, Kuğulu Park-Tunalı yürüyüşü veya canlı müzik mekanları iyi seçeneklerdir."),
                ("ankara için konaklama önerisi", "Ankara'da konaklama için iş ve ulaşım kolaylığı açısından Kızılay, restoran ve sosyal yaşam için Tunalı-Kavaklıdere, daha sakin ve yeni oteller için Çukurambar-Eskişehir Yolu hattı önerilir."),
                ("ulus", "Ulus'ta gezmek için Ankara Kalesi, Anadolu Medeniyetleri Müzesi, Hacı Bayram Camii ve çevresindeki tarihi çarşı rotasını takip edebilirsin."),
                ("ulusda nereler gezilir", "Ulus bölgesinde Hacı Bayram, Roma Hamamı kalıntıları, Ankara Kalesi ve Rahmi Koç Müzesi çevresi iyi bir günlük gezi planı sunar."),
                ("yeme içme önerileri", "Yeme-içme için yerel ve yoğun olmayan bölgelerdeki esnaf lokantaları genelde daha iyi fiyat/performans sunar; öğle menülerini tercih ederek bütçeyi koruyabilirsin."),
                ("ne yiyelim", "Şehir keşfinde denge için kahvaltıda hafif, öğlen yerel lezzet, akşam ise rezervasyonlu restoran planı yapman iyi olur."),
                ("uygun fiyatlı yemek", "Uygun fiyatlı yemek için turistik ana caddelerden 1-2 sokak içeriye bak, günlük menü ve sabit fiyatlı öğle seçeneklerini tercih et."),
                ("kahvaltı önerisi", "Kahvaltıda yöresel ürün sunan mekanları tercih et; yoğun saatlerden kaçmak için 09:00 öncesi gitmek avantaj sağlar."),

                // Roma özel
                ("roma için kaç gün yeterli", "Roma için ideal süre 3-4 gün. İlk kez gidiyorsanız 4 gün önerilir."),
                ("kolezyum bilet", "Kolezyum biletini online ve saatli almanız önerilir; sabah saatleri daha sakindir."),
                ("vatikan ne zaman gidilir", "Vatikan için hafta içi erken saatler en uygunudur; hafta sonu daha kalabalık olur."),
                ("trevi çeşmesi ne zaman sakin", "Trevi Çeşmesi genelde sabah erken saatlerde ve gece geç saatlerde daha sakindir."),
                ("italyan restoran öner", "Turistik alanlardan bir sokak içerideki trattoria'lar genelde daha iyi fiyat/performans sunar."),

                // Kapanış
                ("teşekkürler", "Rica ederim! İstersen günlük gezi rotanı da birlikte çıkarabiliriz.")
            };

            var sampleInputs = samples.Select(s => s.Input).ToList();
            var existingInputs = dbContext.DemoChatServices!
                .AsNoTracking()
                .Where(x => x.Input != null && sampleInputs.Contains(x.Input))
                .Select(x => x.Input!)
                .ToList()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var toAdd = samples
                .Where(s => !existingInputs.Contains(s.Input))
                .Select(s => new Roma.Services.DemoChatService
                {
                    Input = s.Input,
                    Reply = s.Reply,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            if (toAdd.Count > 0)
            {
                dbContext.DemoChatServices!.AddRange(toAdd);
                dbContext.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            var logger = services.GetService<ILogger<Program>>();
            logger?.LogError(ex, "Error while seeding roles or admin user");
        }
    }

app.Run();
