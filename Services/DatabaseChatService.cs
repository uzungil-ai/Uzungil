using Microsoft.EntityFrameworkCore;
using Roma.Data;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Roma.Services
{
    public class DatabaseChatService : IChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseChatService> _logger;
        private static readonly ConcurrentDictionary<string, byte> GreetedSessions = new();
        private static readonly List<CityTemplate> CityTemplates = new()
        {
            new("İstanbul", new[] { "istanbul" }, "Ayasofya, Sultanahmet, Topkapı Sarayı, Galata Kulesi ve Kadıköy sahil hattı.", "Balık-ekmek, tarihi yarımadada kebap, Karaköy'de kahvaltı ve sokak lezzetleri.", "Sultanahmet ve Taksim merkezi; Kadıköy ise sosyal yaşam için güçlü alternatiftir.", "Marmaray, metro ve vapur kombinasyonu en verimli şehir içi ulaşımı sağlar."),
            new("Ankara", new[] { "ankara", "saraçoğlu", "saracoglu", "saraçoğlu mahallesi", "saracoglu mahallesi" }, "Anıtkabir, Ankara Kalesi, Hamamönü, Anadolu Medeniyetleri Müzesi.", "Ankara tava, yaprak döner, Beypazarı mutfağı ve esnaf lokantaları.", "Kızılay ulaşım için, Kavaklıdere/Tunalı sosyal yaşam için, Çukurambar konfor için uygundur.", "Metro + EGO otobüs ağı ile merkezde hızlı geçiş yapılabilir."),
            new("Bursa", new[] { "bursa" }, "Ulu Cami, Cumalıkızık, Tophane, Gölyazı ve Uludağ.", "İskender kebap, pideli köfte, kestane şekeri ve tahinli pide.", "Heykel ve Nilüfer çevresi konaklama için dengeli bölgeler sunar.", "Bursaray ve tramvay merkez hatlarda pratiktir; Uludağ için teleferik değerlendirilebilir."),
            new("Mudanya", new[] { "mudanya" }, "Mudanya Mütareke Evi, Tirilye, Güzelyalı sahili, Mütareke Meydanı ve tarihi Rum evleri.", "Deniz ürünleri, zeytinyağlılar, Tirilye usulü yerel mezeler ve sahil kahvaltıları.", "Sahil hattına yakın butik oteller ve pansiyonlar kısa konaklama için uygundur.", "Bursa merkezden otobüs/dolmuş ile erişim kolaydır; Tirilye ve çevre rotalar için araç kullanımı daha esnektir."),
            new("Van", new[] { "van" }, "Akdamar Adası, Van Kalesi, Van Gölü kıyıları ve Muradiye Şelalesi.", "Van kahvaltısı, otlu peynir, keledoş ve yerel tandır ürünleri.", "Merkezde Cumhuriyet Caddesi çevresi kısa konaklamalar için idealdir.", "Merkezde minibüs/dolmuş ağı yaygındır; çevre rotalar için araç kiralama rahatlık sağlar."),
            new("Şanlıurfa", new[] { "şanlıurfa", "sanliurfa", "urfa" }, "Balıklıgöl, Göbeklitepe, Harran ve Halfeti tekne turu.", "Urfa kebabı, çiğ köfte, lahmacun, katmer ve isotlu yöresel lezzetler.", "Balıklıgöl ve merkez çarşı çevresi tarihi dokuya yakın konaklama sunar.", "Merkez hatlar toplu taşıma ile erişilebilir; Göbeklitepe/Harran için tur veya özel araç önerilir."),
            new("Diyarbakır", new[] { "diyarbakır", "diyarbakir" }, "Surlar, Hevsel Bahçeleri, Ulu Cami, Hasanpaşa Hanı ve On Gözlü Köprü.", "Ciğer kebabı, meftune, kaburga dolması ve burma kadayıf.", "Sur içi tarih severler için, yenişehir tarafı modern konaklama için uygundur.", "Merkezde otobüs ve taksi erişimi kolay; Sur içi yürüyerek keşif için uygundur."),
            new("Mardin", new[] { "mardin" }, "Eski Mardin sokakları, Deyrulzafaran Manastırı, Midyat ve Dara Antik Kenti.", "Kaburga dolması, sembusek, ikbebet ve badem şekeri.", "Eski Mardin manzaralı butik oteller deneyim odaklı konaklama sunar.", "Eski şehirde yürüyüş temel ulaşım şeklidir; çevre ilçeler için araç/tur önerilir."),
            new("Rize", new[] { "rize" }, "Ayder Yaylası, Fırtına Deresi, Zilkale ve Çamlıhemşin rotası.", "Muhlama, hamsili pilav, Rize kavurması ve Laz böreği.", "Merkez kısa konaklama için, yayla bölgeleri doğa deneyimi için uygundur.", "Yayla rotalarında toplu taşıma sınırlı olduğundan araç veya tur planı önemlidir."),
            new("Samsun", new[] { "samsun" }, "Bandırma Vapuru, Amisos Tepesi, Atakum sahil hattı ve Tekkeköy mağaraları.", "Bafra pidesi, nokul, hamsili ürünler ve Karadeniz mutfağı.", "Atakum deniz hattı, İlkadım merkezi erişim için avantajlıdır.", "Tramvay hattı sahil ve merkez ulaşımında etkilidir."),
            new("Ladik", new[] { "ladik" }, "Ladik Gölü, Akdağ Kayak Merkezi ve Ambarköy açık hava müzesi.", "Yöresel et yemekleri, mısır bazlı lezzetler ve köy kahvaltıları.", "Doğa odaklı konaklama için göl çevresi ve dağ tesisleri öne çıkar.", "İlçe içi kısa mesafeler araçla rahattır; Samsun merkez bağlantısı otobüsle sağlanır."),
            new("Bolu", new[] { "bolu" }, "Abant, Gölcük, Yedigöller ve Kartalkaya.", "Mengen mutfağı, et yemekleri, yayla kahvaltısı ve orman ürünleri.", "Merkez otelleri ulaşım için, göl çevresi tesisleri doğa deneyimi için uygundur.", "Doğa noktaları arası geçişte araç kullanımı zaman kazandırır."),
            new("Burdur", new[] { "burdur" }, "Salda Gölü, Sagalassos Antik Kenti, İnsuyu Mağarası, Burdur Arkeoloji Müzesi ve Susuz Kervansarayı.", "Burdur şiş, ceviz ezmesi, haşhaşlı pide ve yöresel köy kahvaltıları.", "Burdur merkez kısa konaklamalar için pratik; Salda çevresi doğa odaklı gezi için uygundur.", "Merkezde minibüs ve otobüs ağı yeterlidir; Salda ve antik kent rotalarında araçla ulaşım daha rahattır."),
            new("Gaziantep", new[] { "gaziantep", "antep" }, "Zeugma Mozaik Müzesi, Bakırcılar Çarşısı, Rumkale ve Kale çevresi.", "Baklava, beyran, katmer, lahmacun ve kebap çeşitleri.", "Şahinbey-Şehitkamil hatları merkezi konaklama için uygundur.", "Tramvay ve otobüs ile merkez noktalar erişilebilir; yoğun saatlerde yürüme+tramvay idealdir."),
            new("Edirne", new[] { "edirne" }, "Selimiye Camii, Meriç Köprüsü, Karaağaç ve tarihi çarşılar.", "Ciğer tava, badem ezmesi, peynir helvası ve Balkan etkili mutfak.", "Selimiye çevresi ve merkez bölge kısa gezi için avantajlıdır.", "Merkez yürüyerek rahat gezilir; çevre noktalar için kısa minibüs/taksi geçişleri yeterlidir."),
            new("Sinop", new[] { "sinop" }, "Tarihi Sinop Cezaevi, Hamsilos Koyu, İnceburun ve Erfelek Şelaleleri.", "Sinop mantısı, nokul, deniz ürünleri ve yerel kahvaltılar.", "Liman ve merkez çevresi konaklama için pratiktir.", "Merkez kompakt olduğu için yürüyüş uygundur; koy ve şelale için araç gerekir."),
            new("Amasya", new[] { "amasya" }, "Kral Kaya Mezarları, Yeşilırmak kıyısı, Amasya Kalesi ve müzeler.", "Amasya çöreği, etli bamya, toyga çorbası ve yöresel tatlılar.", "Nehir hattına yakın tesisler hem manzara hem erişim sağlar.", "Tarihi merkez yürüyerek gezilebilir; kale ve çevre için kısa araç geçişi gerekir."),
            new("Tokat", new[] { "tokat" }, "Tokat Kalesi, Ballıca Mağarası, Sulusokak ve tarihi hanlar.", "Tokat kebabı, yaprak sarma, çökelekli gözleme ve yöresel hamur işleri.", "Merkezde konaklama, çevre ilçelere günübirlik çıkış için avantajlıdır.", "Şehir içi minibüs hatları yaygındır; Ballıca gibi noktalar için araç daha rahattır."),
            new("Sivas", new[] { "sivas" }, "Divriği Ulu Camii, Çifte Minareli Medrese, Gök Medrese ve kongre binası.", "Sivas köftesi, madımak, peskütan çorbası ve katmer.", "Merkezde tarihi noktalara yakın oteller kısa gezi için idealdir.", "Merkezde yürüyüş ve belediye otobüsleri yeterli; Divriği için planlı yolculuk gerekir."),
            new("Venedik", new[] { "venedik", "venice" }, "San Marco Meydanı, Büyük Kanal, Rialto Köprüsü ve Murano-Burano adaları.", "Deniz ürünlü risotto, cicchetti ve yerel trattoria menüleri.", "San Marco pahalı, Cannaregio daha dengeli konaklama sunar.", "Vaporetto hatları şehir içi ana ulaşım aracıdır."),
            new("Como Gölü", new[] { "como gölü", "como golu", "lake como", "como" }, "Bellagio, Varenna, Como merkezi ve göl çevresi manzara noktaları.", "Göl balıkları, risotto ve kuzey İtalya mutfağı.", "Como merkezi ulaşım kolaylığı, Bellagio ise manzara odaklı konaklama sunar.", "Feribot + tren kombinasyonu göl çevresi için en pratik yöntemdir."),
            new("Floransa", new[] { "floransa", "florence" }, "Duomo, Uffizi, Ponte Vecchio, Boboli Bahçeleri ve Piazzale Michelangelo.", "Bistecca alla fiorentina, ribollita ve gelato.", "Duomo çevresi merkezi, Santa Maria Novella tarafı ulaşım için uygundur.", "Tarihi merkez yürüyerek gezilir; şehirler arası geçişte tren idealdir."),
            new("Bologna", new[] { "bologna" }, "Piazza Maggiore, Asinelli Kulesi, portikolar ve üniversite bölgesi.", "Tagliatelle al ragù, tortellini, mortadella ve yerel şarküteri ürünleri.", "Merkez ve tren garı arası bölgeler kısa konaklama için pratiktir.", "Yürüyüş + otobüs yeterlidir; çevre şehirlere trenle hızlı geçiş olur."),
            new("Verona", new[] { "verona" }, "Arena di Verona, Juliet'in Evi, Adige Nehri kıyısı ve tarihi merkez.", "Risotto all'Amarone, polenta ve Veneto mutfağı.", "Arena çevresi turistik, nehir kenarı daha sakin konaklama sunar.", "Merkez yürüyerek rahat gezilir; günübirlik çevre gezileri trenle yapılabilir."),
            new("Milano", new[] { "milano", "milan" }, "Duomo, Galleria Vittorio Emanuele II, Navigli ve Sforza Kalesi.", "Risotto alla milanese, cotoletta ve aperitivo kültürü.", "Duomo/Brera merkezi, Centrale hattı ulaşım odaklı konaklama sağlar.", "Metro ağı güçlüdür; havalimanı bağlantıları tren/servisle rahattır."),
            new("Aachen", new[] { "aachen", "achen" }, "Aachen Katedrali, tarihi belediye binası ve Carolus Thermen.", "Alman fırın ürünleri, Printen ve yerel sıcak yemekler.", "Merkez çevresi yürüyüş odaklı kısa konaklama için uygundur.", "Yürüyüş + bölgesel tren bağlantıları ile şehir içi/dışı rahat planlanır."),
            new("Mekke", new[] { "mekke", "mecca" }, "Mescid-i Haram çevresi ve dini ziyaret odaklı rotalar.", "Ortadoğu mutfağı, pirinç-et menüleri ve uluslararası zincir seçenekleri.", "Harem'e yakın bölgeler ibadet planı için avantaj sağlar.", "Yürüyüş, servis ringleri ve taksi en sık kullanılan ulaşım yöntemleridir."),
            new("Medine", new[] { "medine", "medina" }, "Mescid-i Nebevi çevresi ve dini ziyaret alanları.", "Arap mutfağı, hurma ürünleri ve hafif menüler.", "Mescid-i Nebevi çevresindeki oteller ibadet erişimi için idealdir.", "Merkezde yürüyüş + taksi kombinasyonu pratiktir."),
            new("Cidde", new[] { "cidde", "jeddah", "ciddeh" }, "Cidde sahil hattı, Al-Balad tarihi bölgesi ve corniche rotası.", "Deniz ürünleri, Ortadoğu mutfağı ve uluslararası restoranlar.", "Sahil ve merkez bölgeler farklı bütçelerde konaklama sunar.", "Taksi ve uygulama tabanlı araçlar şehir içi ulaşımda yaygındır."),
            new("Paris", new[] { "paris" }, "Eyfel, Louvre, Notre-Dame çevresi, Montmartre ve Seine hattı.", "Kruvasan, soğan çorbası, steak-frites ve pastane ürünleri.", "Merkez arrondissement'ler turistik; 11-12. bölge fiyat/erişim dengesi sunar.", "Metro ağı çok gelişmiştir; şehir içi geçişler hızlıdır."),
            new("Amsterdam", new[] { "amsterdam" }, "Kanal turları, Rijksmuseum, Van Gogh Müzesi ve Jordaan bölgesi.", "Stroopwafel, haring, peynir çeşitleri ve modern Avrupa mutfağı.", "Centrum canlı ama pahalıdır; De Pijp ve Oud-West dengeli seçenekler sunar.", "Tramvay + bisiklet şehir içi ulaşımın ana omurgasıdır."),
            new("Brüksel", new[] { "brüksel", "bruksel", "brussels" }, "Grand Place, Atomium, Avrupa Mahallesi ve çizgi roman rotaları.", "Belçika waffle, midye-patates, çikolata ve yerel biralar.", "Grand Place çevresi merkezi; Ixelles bölgesi sosyal yaşam için güçlüdür.", "Metro-tramvay-otobüs ağı ile şehir içi bağlantı güçlüdür."),
            new("Moskova", new[] { "moskova", "moscow", "moscha" }, "Kızıl Meydan, Kremlin, Arbat Caddesi ve metro istasyonları.", "Borscht, pelmeni, blini ve Rus mutfağı klasikleri.", "Tverskaya ve merkez hatlar konaklama için ulaşım avantajı sunar.", "Metro sistemi çok kapsamlıdır; merkezde en hızlı seçenektir."),
            new("Kudüs", new[] { "kudüs", "kudus", "jerusalem" }, "Eski Şehir, Mescid-i Aksa çevresi, tarihi surlar ve pazar alanları.", "Ortadoğu mutfağı, humus, falafel ve fırın ürünleri.", "Eski şehir çevresi kültürel erişim sağlar; yeni şehir daha modern konaklama sunar.", "Yürüyüş + tramvay/otobüs kombinasyonu öne çıkar."),
            new("Kopenhag", new[] { "kopenhag", "copenhagen" }, "Nyhavn, Tivoli, Christiania ve Amalienborg Sarayı.", "Smørrebrød, deniz ürünleri ve İskandinav mutfağı.", "Indre By merkezi, Vesterbro ise sosyal yaşam için popülerdir.", "Bisiklet ve metro-tren ağı çok etkilidir."),
            new("İran", new[] { "iran" }, "Tahran, İsfahan, Şiraz ve Yezd rotaları kültür odaklı gezi için öne çıkar.", "Kebap çeşitleri, safranlı pilavlar ve geleneksel İran mutfağı.", "Büyük şehir merkezlerinde konaklama ve ulaşım seçenekleri daha çeşitlidir.", "Şehirler arası uçuş/otobüs, şehir içinde metro-taksi kombinasyonu yaygındır."),
            new("Suriye", new[] { "suriye", "syria" }, "Kültürel gezi planı yapmadan önce güncel güvenlik ve resmi seyahat duyuruları mutlaka kontrol edilmelidir.", "Levant mutfağı, humus, kebap ve fırın ürünleri öne çıkar.", "Konaklama kararı verirken güvenlik ve resmi tavsiyeler birinci öncelik olmalıdır.", "Ulaşım planı yaparken yalnızca güncel ve güvenli hatlar tercih edilmelidir."),
            new("Irak", new[] { "ırak", "irak", "iraq" }, "Seyahat planında şehir bazlı güncel güvenlik bilgisi ve resmi duyurular esas alınmalıdır.", "Ortadoğu mutfağı, pirinç-et tabakları ve yerel fırın ürünleri yaygındır.", "Konaklama için güvenli bölge seçimi ve yerel rehber desteği önemlidir.", "Ulaşımda resmi ve doğrulanmış güzergâhlar tercih edilmelidir."),
            new("Rusya", new[] { "rusya", "russia" }, "Moskova ve St. Petersburg ağırlıklı kültür-sanat rotaları öne çıkar.", "Borscht, pelmeni, blini ve bölgesel mutfak çeşitleri.", "Merkezi metro hatlarına yakın konaklama şehir içi zaman kazandırır.", "Tren + metro kombinasyonu uzun ve kısa mesafelerde etkilidir."),
            new("Arnavutluk", new[] { "arnavutluk", "albania" }, "Tiran, Berat, Gjirokastër ve sahil hattı (Ksamil/Saranda) öne çıkar.", "Byrek, deniz ürünleri ve Balkan mutfağı lezzetleri.", "Tiran merkez ve sahil şehirleri farklı bütçelerde konaklama sunar.", "Şehirler arası otobüs-minibüs ağı yaygındır; araç kiralama esneklik sağlar."),
            new("Bulgaristan", new[] { "bulgaristan", "bulgaria" }, "Sofya, Plovdiv, Veliko Tarnovo ve Karadeniz kıyı rotaları.", "Banitsa, shopska salata, kebapche ve Balkan tatları.", "Merkezi bölgeler toplu taşımaya yakın olduğu için avantajlıdır.", "Tren-otobüs ağı şehirler arası geçişte kullanılabilir."),
            new("Amerika", new[] { "amerika", "abd", "amerika birleşik devletleri", "usa", "united states" }, "New York, Washington, Boston, Chicago ve Batı yakası kombinasyonları popülerdir.", "Bölgeye göre dünya mutfağı çeşitliliği çok geniştir.", "Büyük şehir merkezlerinde erken rezervasyon bütçe avantajı sağlar.", "İç hat uçuşları + metro/banliyö hatları birlikte planlanmalıdır."),
            new("New York", new[] { "newyork", "new york", "nyc" }, "Times Square, Central Park, Brooklyn Köprüsü, MET ve Özgürlük Heykeli.", "Bagel, pizza, pastrami sandviç ve çok kültürlü restoranlar.", "Manhattan merkezi ama pahalıdır; Brooklyn/Queens dengeli alternatifler sunar.", "Metro 24 saat ana ulaşım omurgasıdır."),
            new("Yunanistan", new[] { "yunanistan", "greece", "yunan" }, "Atina, Selanik ve adalar (Santorini, Girit, Rodos) öne çıkar.", "Gyros, souvlaki, deniz ürünleri ve Akdeniz mutfağı.", "Atina merkezi şehir gezisi için, adalarda liman çevresi konaklama için pratiktir.", "Feribot + iç hat uçuşu ada geçişlerinde temel yöntemdir.")
        };

        public DatabaseChatService(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<DatabaseChatService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GetReplyAsync(string input, string? sessionId = null)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return "Lütfen bir mesaj yazın.";
            }

            var normalizedInput = Normalize(input);
            var inputTokens = Tokenize(normalizedInput);

            var seasonalClothingReply = BuildSeasonalClothingReply(normalizedInput);
            if (!string.IsNullOrWhiteSpace(seasonalClothingReply))
            {
                return FinalizeReply(seasonalClothingReply, sessionId);
            }

            var travelPreferenceReply = BuildTravelPreferenceReply(normalizedInput);
            if (!string.IsNullOrWhiteSpace(travelPreferenceReply))
            {
                return FinalizeReply(travelPreferenceReply, sessionId);
            }

            var cityTemplateReply = BuildCityTemplateReply(normalizedInput);
            if (!string.IsNullOrWhiteSpace(cityTemplateReply))
            {
                return FinalizeReply(cityTemplateReply, sessionId);
            }

            var countryTemplateReply = await BuildCountryTemplateReplyAsync(normalizedInput);
            if (!string.IsNullOrWhiteSpace(countryTemplateReply))
            {
                return FinalizeReply(countryTemplateReply, sessionId);
            }

            // Listede olmayan konum/rota sorularında OpenAI öncelikli cevap verir.
            string? openAiReply = null;
            var openAiAttempted = false;
            if (IsTravelLocationIntent(normalizedInput))
            {
                openAiReply = await TryGetOpenAiReplyAsync(input);
                openAiAttempted = true;
                if (!string.IsNullOrWhiteSpace(openAiReply))
                {
                    return FinalizeReply(openAiReply, sessionId);
                }
            }

            // Rule-based template/DB eşleşmeleri bulunamazsa OpenAI yanıtı kullanılır.
            if (!openAiAttempted)
            {
                openAiReply = await TryGetOpenAiReplyAsync(input);
            }

            if (!string.IsNullOrWhiteSpace(openAiReply))
            {
                return FinalizeReply(openAiReply, sessionId);
            }

            var baseQuery = _context.DemoChatServices!
                .AsNoTracking()
                .Where(x => !string.IsNullOrWhiteSpace(x.Input) && !string.IsNullOrWhiteSpace(x.Reply));

            var candidates = await baseQuery
                .Select(x => new { x.Input, x.Reply, x.CreatedAt })
                .ToListAsync();

            var containsReply = candidates
                .Select(x =>
                {
                    var normalizedCandidate = Normalize(x.Input!);
                    var candidateTokens = Tokenize(normalizedCandidate);
                    var tokenMatchCount = candidateTokens.Count(t => inputTokens.Contains(t));

                    var score = tokenMatchCount;
                    if (normalizedCandidate == normalizedInput)
                    {
                        score += 100;
                    }

                    if (!string.IsNullOrWhiteSpace(normalizedCandidate) && normalizedInput.Contains(normalizedCandidate))
                    {
                        score += 3;
                    }

                    return new
                    {
                        x.Reply,
                        x.CreatedAt,
                        Score = score
                    };
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.CreatedAt)
                .Select(x => x.Reply)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(containsReply))
            {
                return FinalizeReply(containsReply, sessionId);
            }

            return FinalizeReply("(Bot) Bu mesaj için kayıtlı bir yanıt bulunamadı. Daha kısa ve net yazmayı deneyin.", sessionId);
        }

        private async Task<string?> TryGetOpenAiReplyAsync(string input)
        {
            var apiKey = _configuration["OpenAI:ApiKey"]
                ?? _configuration["OPENAI_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            var model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
            var maxTokens = _configuration.GetValue<int?>("OpenAI:MaxOutputTokens") ?? 220;

            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var payload = new
                {
                    model,
                    max_output_tokens = maxTokens,
                    temperature = 0.4,
                    input = new object[]
                    {
                        new
                        {
                            role = "system",
                            content = new object[]
                            {
                                new { type = "input_text", text = "Sen seyahat asistanısın. Türkçe, kısa, net ve uygulanabilir cevap ver." }
                            }
                        },
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "input_text", text = input }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync("https://api.openai.com/v1/responses", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("OpenAI request failed. StatusCode: {StatusCode}, Body: {Body}", (int)response.StatusCode, errorBody);
                    return null;
                }

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);

                if (doc.RootElement.TryGetProperty("output_text", out var outputTextEl) &&
                    outputTextEl.ValueKind == JsonValueKind.String)
                {
                    var outputText = outputTextEl.GetString();
                    return string.IsNullOrWhiteSpace(outputText) ? null : outputText.Trim();
                }

                if (doc.RootElement.TryGetProperty("output", out var outputEl) && outputEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in outputEl.EnumerateArray())
                    {
                        if (!item.TryGetProperty("content", out var contentEl) || contentEl.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var contentItem in contentEl.EnumerateArray())
                        {
                            if (contentItem.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                            {
                                var text = textEl.GetString();
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    return text.Trim();
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI request exception.");
                return null;
            }
        }

        private async Task<string?> BuildCountryTemplateReplyAsync(string normalizedInput)
        {
            var countries = await _context.Destinations!
                .AsNoTracking()
                .Where(d => d.Country != null && d.Country.Trim() != string.Empty)
                .Select(d => d.Country!.Trim())
                .Distinct()
                .ToListAsync();

            var matchedCountry = countries
                .Select(c => new { Original = c, Normalized = Normalize(c) })
                .Where(x => !string.IsNullOrWhiteSpace(x.Normalized) && normalizedInput.Contains(x.Normalized))
                .OrderByDescending(x => x.Normalized.Length)
                .FirstOrDefault();

            if (matchedCountry == null)
            {
                return null;
            }

            var normalizedCountry = Normalize(matchedCountry.Original);
            var isItaly = ContainsAny(normalizedCountry, "italya", "italy", "italia");
            var isEnglishRegion = ContainsAny(normalizedCountry,
                "ingiltere", "united kingdom", "uk", "amerika", "united states", "usa", "kanada", "avustralya", "irlanda");
            var isGermanRegion = ContainsAny(normalizedCountry,
                "almanya", "germany", "avusturya", "austria", "isvicre", "switzerland");
            var isArabicRegion = ContainsAny(normalizedCountry,
                "suudi arabistan", "birlesik arap emirlikleri", "uae", "katar", "kuveyt", "bahreyn", "umman", "misir", "suriye", "irak", "urdun", "fas", "tunus", "cezayir");

            var italySupportSuffix = isItaly
                ? " Eğer İtalya'da seyahat ediyorsanız Türkçe sorularınız için İtalyanca karşılıklar da verebilirim."
                : isEnglishRegion
                    ? " Eğer bu bölgede seyahat ediyorsanız Türkçe sorularınız için İngilizce karşılıklar da verebilirim."
                    : isGermanRegion
                        ? " Eğer bu bölgede seyahat ediyorsanız Türkçe sorularınız için Almanca karşılıklar da verebilirim."
                        : isArabicRegion
                            ? " Eğer bu bölgede seyahat ediyorsanız Türkçe sorularınız için Arapça karşılıklar da verebilirim."
                            : string.Empty;

            var matchedCountryLower = matchedCountry.Original.ToLower();
            var destinationNames = await _context.Destinations!
                .AsNoTracking()
                .Where(d => d.Country != null && d.Country.ToLower() == matchedCountryLower)
                .Select(d => d.Name)
                .Where(n => n != null && n.Trim() != string.Empty)
                .Distinct()
                .OrderBy(n => n)
                .Take(3)
                .ToListAsync();

            var wantsAccommodation = ContainsAny(normalizedInput,
                "konaklama", "otel", "nerede kalınır", "nerede kalinir", "gecelik", "kalacak yer", "otel öner", "otel oner");

            if (wantsAccommodation)
            {
                if (destinationNames.Count > 0)
                {
                    return $"{matchedCountry.Original} için konaklama planı hazırlayabilirim. Merkez, ulaşım erişimi ve bütçe dengesine göre otel/pansiyon/Airbnb seçeneklerini karşılaştırabilirim. Başlangıç için {string.Join(", ", destinationNames)} destinasyonlarından birini seçebilirsin.{italySupportSuffix}";
                }

                return $"{matchedCountry.Original} için konaklama planı hazırlayabilirim. Bütçe, bölge ve ulaşım önceliğine göre otel, pansiyon ve Airbnb alternatiflerini sıralayabilirim.{italySupportSuffix}";
            }

            var wantsFood = ContainsAny(normalizedInput,
                "yeme", "içme", "yeme içme", "restoran", "restaurant", "ne yenir", "kahvaltı", "kahvalti", "akşam yemeği", "aksam yemegi");

            if (wantsFood)
            {
                if (destinationNames.Count > 0)
                {
                    return $"{matchedCountry.Original} için yeme-içme önerileri hazırlayabilirim. Yerel lezzetler, uygun fiyatlı restoranlar ve kahvaltı/akşam yemeği seçeneklerini {string.Join(", ", destinationNames)} odağında önerebilirim.{italySupportSuffix}";
                }

                return $"{matchedCountry.Original} için yeme-içme önerileri hazırlayabilirim. Yerel lezzet, uygun fiyatlı mekan ve kahvaltı/akşam yemeği alternatiflerini bütçene göre sıralayabilirim.{italySupportSuffix}";
            }

            var wantsMobileSim = ContainsAny(normalizedInput,
                "telefon", "mobil", "sim", "sim kart", "internet paketi", "roaming", "hat", "mobil ücret", "mobil ucret", "tarife");

            if (wantsMobileSim)
            {
                return $"{matchedCountry.Original} için telefon ve SIM önerisi: eSIM veya yerel fiziksel SIM tercih edebilirsin. Ortalama turist paketleri genelde 10-30 GB için 10-35€ aralığında, havaalanı satış noktalarında fiyatlar biraz daha yüksek olabilir. Kısa seyahatlerde eSIM hızlıdır; uzun konaklamada şehir merkezindeki operatör mağazaları daha avantajlı olabilir.{italySupportSuffix}";
            }

            var wantsPrice = ContainsAny(normalizedInput,
                "fiyat", "fiyatlar", "bütçe", "butce", "pahalı", "pahali", "ucuz", "maliyet", "ne kadar", "gezi bütçesi", "gezi butcesi");

            if (wantsPrice)
            {
                if (destinationNames.Count > 0)
                {
                    return $"{matchedCountry.Original} için fiyat/bütçe karşılaştırması yapabilirim. Konaklama (otel/pansiyon/Airbnb), ulaşım ve yeme-içme kalemlerini {string.Join(", ", destinationNames)} odağında düşük-orta-yüksek bütçe olarak çıkarabilirim.{italySupportSuffix}";
                }

                return $"{matchedCountry.Original} için fiyat/bütçe planı hazırlayabilirim. Konaklama (otel/pansiyon/Airbnb), ulaşım ve yeme-içme için düşük-orta-yüksek bütçe aralıklarıyla net bir maliyet tablosu oluşturabilirim.{italySupportSuffix}";
            }

            var wantsSouvenir = ContainsAny(normalizedInput,
                "hediyelik", "souvenir", "hatıra", "hatira", "ne alınır", "ne alinir", "hediye", "alinacak seyler", "alınacak şeyler");

            if (wantsSouvenir)
            {
                return $"{matchedCountry.Original} için hediyelik önerileri: magnet/kartpostal, yerel atıştırmalık, el işi-seramik/tekstil ve bölgeye özgü küçük dekor ürünleri. Ortalama fiyatlar: magnet 2-8€, yerel yiyecek paketi 5-20€, tekstil/aksesuar 15-60€, el yapımı ürünler 20-120€ aralığında değişebilir (şehir ve sezona göre farklılık olur).{italySupportSuffix}";
            }

            var wantsShoppingDeals = ContainsAny(normalizedInput,
                "indirim", "indirim dönemi", "indirim donemi", "outlet", "mağaza", "magaza", "alışveriş", "alisveris", "sale", "kampanya");

            if (wantsShoppingDeals)
            {
                if (destinationNames.Count > 0)
                {
                    return $"{matchedCountry.Original} için indirim dönemleri ve mağaza önerileri verebilirim. Genelde sezon sonu (kış/yaz) ve özel kampanya haftalarında fiyatlar düşer; {string.Join(", ", destinationNames)} çevresinde merkez mağazalar + outlet noktalarını birlikte planlamak avantaj sağlar.{italySupportSuffix}";
                }

                return $"{matchedCountry.Original} için indirim dönemleri ve mağaza önerileri verebilirim. Sezon sonu indirimleri ve outlet bölgeleri genelde daha avantajlıdır; şehir adı verirsen hedefli alışveriş rotası çıkarabilirim.{italySupportSuffix}";
            }

            var wantsTaxFree = ContainsAny(normalizedInput,
                "tax free", "vergi iadesi", "kdv iadesi", "vat refund", "tax refund", "taxfree", "vergi geri");

            if (wantsTaxFree)
            {
                return $"{matchedCountry.Original} için tax free oranı ve iade kuralları mağaza türü, ürün grubu ve güncel mevzuata göre değişir. Genel olarak tax free formlu mağazadan alışveriş yapıp fatura/pasaport ile çıkışta onay alman gerekir. İstersen {matchedCountry.Original} için adım adım tax free sürecini yazabilirim.{italySupportSuffix}";
            }

            var wantsStolenBag = ContainsAny(normalizedInput,
                "çantam çalındı", "cantam calindi", "bagajım çalındı", "bagajim calindi", "hırsızlık", "hirsizlik", "çantamı çaldılar", "cantami caldilar");

            if (wantsStolenBag)
            {
                return $"{matchedCountry.Original} için acil eylem planı: 1) En yakın polis merkezine gidip tutanak tuttur, 2) Kartlarını hemen bloke et, 3) Telefon ve e-posta şifrelerini değiştir, 4) Konaklama ve seyahat sağlayıcına bilgi ver. Kullanabileceğin cümleler: EN: 'My bag was stolen. I need police assistance.' IT: 'Mi hanno rubato la borsa. Ho bisogno della polizia.' AR: 'تمت سرقة حقيبتي، أحتاج مساعدة الشرطة'.{italySupportSuffix}";
            }

            var wantsStolenPassport = ContainsAny(normalizedInput,
                "pasaportum çalındı", "pasaportum calindi", "pasaportumu çaldılar", "pasaportumu caldilar", "pasaport kayıp", "pasaport kayip", "pasaportumu kaybettim");

            if (wantsStolenPassport)
            {
                return $"{matchedCountry.Original} için pasaport kayıp/çalıntı adımları: 1) Polise kayıp/çalıntı tutanağı al, 2) En yakın Türkiye Cumhuriyeti Büyükelçiliği/Konsolosluğu ile iletişime geç, 3) Geçici seyahat belgesi için gerekli evrakları hazırla (kimlik, fotoğraf, tutanak), 4) Uçuş ve otel kayıtlarını güncelle. Kullanabileceğin cümleler: EN: 'My passport was stolen. I need consular help.' IT: 'Mi hanno rubato il passaporto. Ho bisogno di aiuto consolare.' AR: 'تمت سرقة جواز سفري، أحتاج مساعدة قنصلية'.{italySupportSuffix}";
            }

            var wantsClothing = ContainsAny(normalizedInput,
                "kıyafet", "kiyafet", "giyim", "ne giyilir", "mevsim", "hava durumuna göre", "yazın ne giyilir", "kışın ne giyilir", "kis", "yaz", "sonbahar", "ilkbahar");

            if (wantsClothing)
            {
                return $"{matchedCountry.Original} için mevsime göre katmanlı giyinme önerilir: yazın nefes alan ince kumaşlar, ilkbahar/sonbaharda ince ceket-hırka, kışın suya dayanıklı mont ve sıcak tutan iç katman tercih et. İstersen şehir ve ay bilgisine göre daha net liste çıkarayım.";
            }

            var wantsWeather = ContainsAny(normalizedInput,
                "hava durumu", "hava nasıl", "hava nasil", "yağmur", "yagmur", "sıcaklık", "sicaklik", "derece", "forecast");

            if (wantsWeather)
            {
                var weatherLink = BuildWeatherLink(matchedCountry.Original);
                return $"{matchedCountry.Original} için güncel hava durumu bilgisini şu bağlantıdan kontrol edebilirsin: {weatherLink}{italySupportSuffix}";
            }

            var wantsDestinationQaSuggestion = ContainsAny(normalizedInput,
                "destinasyon", "rota", "gezi planı", "gezi plani", "soru cevap", "öneri", "oneri");

            if (wantsDestinationQaSuggestion)
            {
                if (destinationNames.Count > 0)
                {
                    return $"{matchedCountry.Original} için destinasyon bazlı soru-cevap önerileri hazırlayabilirim. Örnek sorular: '{destinationNames.First()} için kaç gün yeterli?', '{destinationNames.First()} için ulaşım nasıl?', '{destinationNames.First()} için günlük bütçe ne kadar?'. İstersen bu soruların cevaplarını da tek tek çıkarayım.{italySupportSuffix}";
                }

                return $"{matchedCountry.Original} için destinasyon bazlı soru-cevap önerileri hazırlayabilirim. Örnek sorular: 'Hangi şehirden başlamalıyım?', 'Kaç günlük rota uygun?', 'Ulaşım ve konaklama için ortalama bütçe nedir?'. İstersen her soruya net yanıtları da üreteyim.{italySupportSuffix}";
            }

            var wantsTaxi = ContainsAny(normalizedInput,
                "taksi", "taxi", "uber", "bolt", "bi taksi", "bitaksi", "taksi ücreti", "taksi ucreti");

            if (wantsTaxi)
            {
                if (destinationNames.Count > 0)
                {
                    return $"{matchedCountry.Original} için taksi planı yapabilirim. Havalimanı-şehir merkezi sabit ücret/alternatif uygulamalar ve yoğun saatlerde taksi yerine toplu taşıma seçeneklerini {string.Join(", ", destinationNames)} odağında karşılaştırabilirim.{italySupportSuffix}";
                }

                return $"{matchedCountry.Original} için taksi kullanımı hakkında yönlendirme yapabilirim. Havalimanı transferi, ortalama taksi maliyeti ve uygulama tabanlı alternatifleri bütçene göre sıralayabilirim.{italySupportSuffix}";
            }

            var wantsTransport = ContainsAny(normalizedInput,
                "ulaşım", "ulasim", "nasıl giderim", "nasil giderim", "metro", "otobüs", "otobus", "tren", "havalimanı", "havaalanı", "transfer", "taksi", "taxi", "uber", "bolt");

            if (wantsTransport)
            {
                if (destinationNames.Count > 0)
                {
                    return $"{matchedCountry.Original} için ulaşım planı hazırlayabilirim. Şehir içi için toplu taşıma kartı ve metro/otobüs hatları, şehirler arası için tren/uçak seçeneklerini karşılaştırırım. Başlangıç noktası olarak {string.Join(", ", destinationNames)} destinasyonlarından birini seçebilirsin.{italySupportSuffix}";
                }

                return $"{matchedCountry.Original} için ulaşım planı hazırlayabilirim. Şehir içi toplu taşıma, havalimanı transferi ve şehirler arası tren/uçak seçeneklerini bütçene göre karşılaştırabilirim.{italySupportSuffix}";
            }

            if (destinationNames.Count > 0)
            {
                return $"{matchedCountry.Original} için gezi planını konaklama, ulaşım, yeme-içme ve günlük rota olarak hazırlayabilirim. Öne çıkan destinasyonlar: {string.Join(", ", destinationNames)}. İstersen 3 günlük örnek plan da oluşturabilirim.{italySupportSuffix}";
            }

            return $"{matchedCountry.Original} için gezi planını konaklama, ulaşım, yeme-içme ve günlük rota olarak hazırlayabilirim. İstersen bütçe ve gün sayına göre örnek plan oluşturayım.{italySupportSuffix}";
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var lowered = text.Trim().ToLowerInvariant();
            var sb = new StringBuilder(lowered.Length);

            foreach (var ch in lowered)
            {
                if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        private static HashSet<string> Tokenize(string text)
        {
            return text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length >= 2)
                .ToHashSet();
        }

        private static bool ContainsAny(string normalizedInput, params string[] phrases)
        {
            return phrases.Any(p => normalizedInput.Contains(Normalize(p)));
        }

        private static string? BuildCityTemplateReply(string normalizedInput)
        {
            var city = CityTemplates
                .Select(c => new { City = c, Aliases = c.Aliases.Select(Normalize).ToList() })
                .FirstOrDefault(x => x.Aliases.Any(a => !string.IsNullOrWhiteSpace(a) && normalizedInput.Contains(a)))
                ?.City;

            if (city == null)
            {
                return null;
            }

            var wantsPlaces = ContainsAny(normalizedInput, "gezilecek", "nereler gezilir", "görülmesi gereken", "gezi noktaları", "rota");
            var wantsFood = ContainsAny(normalizedInput, "yeme", "içme", "ne yenir", "restoran", "kahvaltı", "kahvalti");
            var wantsAccommodation = ContainsAny(normalizedInput, "konaklama", "otel", "nerede kalınır", "nerede kalinir", "kalacak yer");
            var wantsTaxi = ContainsAny(normalizedInput, "taksi", "taxi", "uber", "bolt", "bitaksi", "bi taksi", "taksi ücreti", "taksi ucreti");
            var wantsTransport = ContainsAny(normalizedInput, "ulaşım", "ulasim", "nasıl giderim", "nasil giderim", "metro", "otobüs", "otobus", "transfer", "taksi", "taxi", "uber", "bolt");
            var wantsMobileSim = ContainsAny(normalizedInput, "telefon", "mobil", "sim", "sim kart", "internet paketi", "roaming", "hat", "mobil ücret", "mobil ucret", "tarife");
            var wantsPrice = ContainsAny(normalizedInput, "fiyat", "bütçe", "butce", "maliyet", "ne kadar", "ucuz", "pahalı", "pahali");
            var wantsSouvenir = ContainsAny(normalizedInput, "hediyelik", "souvenir", "hatıra", "hatira", "ne alınır", "ne alinir", "hediye", "alınacak şeyler", "alinacak seyler");
            var wantsShoppingDeals = ContainsAny(normalizedInput, "indirim", "indirim dönemi", "indirim donemi", "outlet", "mağaza", "magaza", "alışveriş", "alisveris", "sale", "kampanya");
            var wantsStolenBag = ContainsAny(normalizedInput, "çantam çalındı", "cantam calindi", "bagajım çalındı", "bagajim calindi", "hırsızlık", "hirsizlik", "çantamı çaldılar", "cantami caldilar");
            var wantsStolenPassport = ContainsAny(normalizedInput, "pasaportum çalındı", "pasaportum calindi", "pasaportumu çaldılar", "pasaportumu caldilar", "pasaport kayıp", "pasaport kayip", "pasaportumu kaybettim");
            var wantsTaxFree = ContainsAny(normalizedInput, "tax free", "vergi iadesi", "kdv iadesi", "vat refund", "tax refund", "taxfree", "vergi geri");
            var wantsWeather = ContainsAny(normalizedInput, "hava durumu", "hava nasıl", "hava nasil", "yağmur", "yagmur", "sıcaklık", "sicaklik", "derece", "forecast");
            var wantsDays = ContainsAny(normalizedInput, "kaç gün", "kac gun", "kaç günde", "kac gunde", "ne kadar kalınır", "ne kadar kalinir");
            var wantsClothing = ContainsAny(normalizedInput, "kıyafet", "kiyafet", "giyim", "ne giyilir", "mevsim", "yazın", "kis", "kışın", "ilkbahar", "sonbahar", "hava");

            var sections = new List<string>();

            if (wantsPlaces)
            {
                sections.Add($"{city.Name} gezilecek yerler: {city.Places}");
            }

            if (wantsFood)
            {
                sections.Add($"{city.Name} yeme-içme önerileri: {city.Foods}");
            }

            if (wantsAccommodation)
            {
                sections.Add($"{city.Name} konaklama önerisi: {city.Stay} Ayrıca otel, pansiyon ve Airbnb seçeneklerini bütçene göre karşılaştırabilirim.");
            }

            if (wantsTaxi)
            {
                sections.Add($"{city.Name} taksi önerisi: Yoğun saatlerde ücret artabilir; kısa mesafede toplu taşıma/yürüyüş daha ekonomik olur. Havalimanı transferinde taksi ücretini önceden doğrulamak ve mümkünse uygulama tabanlı araç çağırma seçeneklerini karşılaştırmak faydalıdır.");
            }

            if (wantsTransport)
            {
                sections.Add($"{city.Name} ulaşım önerisi: {city.Transport}");
            }

            if (wantsMobileSim)
            {
                sections.Add($"{city.Name} için mobil kullanım önerisi: eSIM veya yerel SIM kart alabilirsin. Ortalama turist paketleri 10-30 GB için 10-35€ aralığında değişebilir. Kısa seyahatte eSIM, uzun konaklamada operatör mağazasından fiziksel SIM daha uygun olabilir.");
            }

            if (wantsPrice)
            {
                sections.Add($"{city.Name} için bütçe planı düşük/orta/yüksek seviyede konaklama (otel/pansiyon/Airbnb), ulaşım ve yeme-içme kalemleriyle çıkarılabilir. İstersen gün sayısı ver, net maliyet aralığı paylaşayım.");
            }

            if (wantsSouvenir)
            {
                sections.Add($"{city.Name} için hediyelik önerileri: magnet/kartpostal, yerel tatlar, el işi ürünler ve bölgesel tekstil. Ortalama fiyatlar: magnet 2-8€, yerel ürün kutusu 5-20€, tekstil/aksesuar 15-60€, el yapımı ürünler 20-120€ aralığında değişebilir.");
            }

            if (wantsShoppingDeals)
            {
                sections.Add($"{city.Name} için alışveriş/indirim önerisi: sezon sonu indirim dönemleri ve outlet bölgeleri daha avantajlı olabilir. Merkez caddelerde yeni sezon, outletlerde önceki sezon ürünleri daha uygun bulunur. İstersen bütçene göre mağaza-outlet dengeli rota çıkarayım.");
            }

            if (wantsStolenBag)
            {
                sections.Add($"{city.Name} için çanta hırsızlığı acil planı: polis tutanağı al, kartlarını bloke et, hesap güvenliğini güncelle ve konaklama yerine bildir. Dil desteği: EN 'My bag was stolen.' IT 'Mi hanno rubato la borsa.' AR 'تمت سرقة حقيبتي'.");
            }

            if (wantsStolenPassport)
            {
                sections.Add($"{city.Name} için pasaport kayıp/çalıntı planı: polis tutanağı al, en yakın T.C. temsilciliği ile iletişime geç, geçici seyahat belgesi için evrakları hazırla. Dil desteği: EN 'My passport was stolen.' IT 'Mi hanno rubato il passaporto.' AR 'تمت سرقة جواز سفري'.");
            }

            if (wantsTaxFree)
            {
                sections.Add($"{city.Name} için tax free iadesinde genelde tax free anlaşmalı mağazadan alışveriş, fatura/form saklama ve çıkış noktasında onay adımları gerekir. İade oranı ürün ve ülke kuralına göre değişir; alışveriş öncesi mağazadan net oranı teyit etmen en doğru yöntemdir.");
            }

            if (wantsWeather)
            {
                var weatherLink = BuildWeatherLink(city.Name);
                sections.Add($"{city.Name} için güncel hava durumu bağlantısı: {weatherLink}");
            }

            if (wantsClothing)
            {
                sections.Add($"{city.Name} için mevsime göre kıyafet önerisi: yazın ince/nefes alan kıyafet + güneş koruması, ilkbahar-sonbaharda katmanlı kombin (tişört + ince ceket), kışın rüzgar/yağmura dayanıklı mont ve sıcak tutan ayakkabı tercih et. İstersen hangi ay gideceğini yaz, net liste çıkarayım.");
            }

            if (wantsDays)
            {
                sections.Add($"{city.Name} için ideal gezi süresi genelde 2-3 gündür. Yoğun müze/doğa planında 4 güne çıkarabilirsin.");
            }

            if (sections.Count > 0)
            {
                return string.Join("\n\n", sections);
            }

            return $"{city.Name} için soru-cevap önerileri: 'Gezilecek yerler neler?', 'Ne yenir?', 'Nerede kalınır?', 'Ulaşım nasıl?', 'Taksi kullanımı nasıl?', 'SIM kart ve mobil internet nasıl?', 'İndirim dönemleri ve mağazalar nerede?', 'Tax free oranı nasıl?', 'Hava durumu nasıl?', 'Ortalama bütçe ne kadar?'";
        }

        private static string? BuildSeasonalClothingReply(string normalizedInput)
        {
            var asksClothing = ContainsAny(normalizedInput,
                "kıyafet", "kiyafet", "giyim", "ne giyilir", "mevsime göre", "mevsim", "hava durumuna göre");

            if (!asksClothing)
            {
                return null;
            }

            if (ContainsAny(normalizedInput, "yaz", "yazın", "yazin"))
            {
                return "Yaz için: ince ve nefes alan kumaş, şapka/güneş gözlüğü, konforlu yürüyüş ayakkabısı ve yüksek SPF güneş koruyucu önerilir.";
            }

            if (ContainsAny(normalizedInput, "kış", "kisin", "kışın", "kis"))
            {
                return "Kış için: termal iç katman, kalın/su geçirmez mont, atkı-bere-eldiven ve kaydırmaz tabanlı ayakkabı önerilir.";
            }

            if (ContainsAny(normalizedInput, "ilkbahar", "bahar", "sonbahar"))
            {
                return "İlkbahar/sonbahar için: katmanlı giyim (tişört + hırka/ince ceket), hafif yağmurluk ve rahat yürüyüş ayakkabısı önerilir.";
            }

            return "Mevsime göre genel öneri: katmanlı giyin, yağmur ihtimaline karşı ince yağmurluk bulundur, uzun yürüyüş için rahat ayakkabı seç. Ay ve şehir bilgisini paylaşırsan daha net liste verebilirim.";
        }

        private static bool IsTravelLocationIntent(string normalizedInput)
        {
            return ContainsAny(normalizedInput,
                "gezilecek", "görülmesi gereken", "rota", "ne yenir", "yeme", "içme", "konaklama", "otel", "ulaşım", "taksi",
                "hava durumu", "kaç gün", "destinasyon", "şehir", "sehir", "ilçe", "ilce", "mahalle", "semt");
        }

        private static string BuildWeatherLink(string location)
        {
            return $"https://www.accuweather.com/tr/search-locations?query={Uri.EscapeDataString(location)}";
        }

        private static string ApplyPreferencePrompt(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply))
            {
                return reply;
            }

            var normalizedReply = Normalize(reply);
            var isLanguagePhraseReply = normalizedReply.Contains("italyanca") && normalizedReply.Contains("okunus");
            if (normalizedReply.Contains("seyahatte en cok neyi seversin") ||
                normalizedReply.Contains("tercihine gore odak alanlarin") ||
                isLanguagePhraseReply)
            {
                return reply;
            }

            return $"{reply} Seyahatte en çok neyi seversin: müze, doğal alanlar, kale, alışveriş?";
        }

        private static string FinalizeReply(string reply, string? sessionId)
        {
            var withPreferencePrompt = ApplyPreferencePrompt(reply);
            return AddTimeGreetingIfFirstMessage(withPreferencePrompt, sessionId);
        }

        private static string AddTimeGreetingIfFirstMessage(string reply, string? sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return reply;
            }

            if (!GreetedSessions.TryAdd(sessionId, 0))
            {
                return reply;
            }

            var hour = DateTime.Now.Hour;
            var greeting = hour switch
            {
                >= 6 and < 12 => "Günaydın",
                >= 12 and < 18 => "İyi tünaydın",
                >= 18 and < 23 => "İyi akşamlar",
                _ => "İyi geceler"
            };

            return $"{greeting}! {reply}";
        }

        private static string? BuildTravelPreferenceReply(string normalizedInput)
        {
            var asksPreferencePrompt = ContainsAny(normalizedInput,
                "seyahatte en cok", "seyahatte en çok", "seyahatte neyi seversin", "seyahat tercihi", "ne seviyorsun", "tercih sor");

            if (asksPreferencePrompt)
            {
                return "Seyahatte en çok neyi seversin: müze, doğal alanlar, kale, alışveriş? Seçimine göre net rota çıkarabilirim.";
            }

            var likesMuseum = ContainsAny(normalizedInput, "müze", "muze", "sanat", "tarih");
            var likesNature = ContainsAny(normalizedInput, "doğa", "doga", "doğal alan", "dogal alan", "yayla", "göl", "orman");
            var likesCastle = ContainsAny(normalizedInput, "kale", "hisar", "sur");
            var likesShopping = ContainsAny(normalizedInput, "alışveriş", "alisveris", "shopping", "çarşı", "carsi", "avm");

            var selected = new List<string>();
            if (likesMuseum) selected.Add("müze");
            if (likesNature) selected.Add("doğal alanlar");
            if (likesCastle) selected.Add("kale");
            if (likesShopping) selected.Add("alışveriş");

            if (selected.Count == 0)
            {
                return null;
            }

            return $"Tercihine göre odak alanların: {string.Join(", ", selected)}. Buna uygun gezi planı hazırlayabilirim; şehir adı yazarsan direkt rota ve bütçe çıkarayım.";
        }

        private sealed record CityTemplate(
            string Name,
            string[] Aliases,
            string Places,
            string Foods,
            string Stay,
            string Transport);
    }
}
