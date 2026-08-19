# Dosya Takip ve İşleme Servisi

Belirlenen bir klasördeki dosyaları periyodik olarak tarayan, her dosya hakkında bilgi (ad, uzantı, boyut, oluşturulma/değiştirilme tarihi, SHA-256 hash) çıkarıp bir veritabanına kaydeden ve bu bilgileri bir REST API üzerinden sunan .NET uygulaması.

## Özellikler

- Ayarlanabilir aralıklarla otomatik klasör taraması (Background Service)
- Aynı dosyanın tekrar işlenmemesi (dosya yolu bazlı tekrar kontrolü)
- İçerik değiştiğinde SHA-256 hash'in yeniden hesaplanması
- Hash üzerinden yinelenen (duplicate) dosya tespiti
- Sayfalama ve sıralama destekli listeleme
- Uzantıya göre arama (büyük/küçük harf ve nokta yazımından bağımsız)
- Global hata yakalama ve loglama
- Docker ve docker-compose ile tek komutla ayağa kaldırma
- Testcontainers ile gerçek PostgreSQL'e karşı koşan 71 birim/entegrasyon testi

## Kullanılan Teknolojiler

- .NET 10, ASP.NET Core Web API
- Entity Framework Core + Npgsql (PostgreSQL)
- Swagger / Swashbuckle
- Microsoft.Extensions.Logging
- Docker, Docker Compose
- xUnit, Testcontainers

## Proje Yapısı

```
Controllers/    HTTP uçları (FilesController)
Services/       İş mantığı (tarama, sorgulama, arka plan servisi)
Models/         Veri modelleri ve DTO'lar
Data/           EF Core DbContext
Migrations/     Veritabanı şema geçmişi
Middleware/     Global hata yakalama
Tests/          xUnit test projesi
izlenen/        Taranacak örnek klasör
```

## Kurulum ve Çalıştırma

### Docker ile (önerilen)

Uygulama ve PostgreSQL'i tek komutla ayağa kaldırır, migration'ları otomatik uygular.

```bash
docker compose up --build
```

- API: http://localhost:8080
- Swagger: http://localhost:8080/swagger
- Taranan klasör: `./izlenen` (host'tan salt okunur olarak bağlanır)

Durdurmak için `docker compose down`; veritabanını da silmek için `docker compose down -v`.

### Yerel olarak

Gereksinim: yerel bir PostgreSQL sunucusu ve `appsettings.json`'daki `ConnectionStrings:DefaultConnection` değerinin o sunucuya işaret etmesi.

```bash
dotnet ef database update   # migration'ları uygula
dotnet run
```

## Ayarlar (appsettings.json)

```json
"WatchSettings": {
  "FolderPath": "izlenecek klasörün yolu",
  "ScanIntervalSeconds": 10
}
```

## API Uçları

| Metot | Yol | Açıklama |
|---|---|---|
| GET | `/api/files` | Sayfalı dosya listesi |
| GET | `/api/files/{id}` | Tek dosya, bulunamazsa 404 |
| GET | `/api/files/search?extension=` | Uzantıya göre arama |
| POST | `/api/files/scan` | Klasör taramasını manuel başlatır |
| GET | `/api/files/duplicates` | Hash'e göre gruplanmış yinelenen dosyalar |

**Önerilen kullanım sırası:** Uygulama açıldığında arka plan servisi klasörü zaten otomatik tarar (bkz. `ScanIntervalSeconds`), ama elle denerken önce **`POST /api/files/scan`** ile taramayı tetikleyip veritabanının dolmasını sağlamak, ardından listeleme/arama uçlarını denemek daha anlamlı sonuç verir — boş bir tabloda sayfalama veya arama denemenin gösterecek bir şeyi olmaz.

### GET /api/files — parametreler

| Parametre | Tip | Varsayılan | Açıklama |
|---|---|---|---|
| `page` | int | `1` | Kaçıncı sayfa isteniyor |
| `pageSize` | int | `10` | Sayfa başına kayıt sayısı (en fazla `100`, üzeri otomatik `100`'e sabitlenir) |
| `sortBy` | string | `id` | Sıralama alanı: `fileName`, `extension`, `sizeBytes`, `createdAt`, `modifiedAt`. Tanınmayan bir değer `id`'ye düşer |
| `sortOrder` | string | `asc` | `asc` (artan) veya `desc` (azalan) |

Örnekler:

```
GET /api/files
→ İlk 10 kayıt, Id'ye göre artan sırada (hiçbir parametre verilmese de çalışır)

GET /api/files?page=2&pageSize=20
→ 21-40 arası kayıtlar

GET /api/files?sortBy=sizeBytes&sortOrder=desc
→ En büyük dosya en üstte, 10'ar kayıtlık sayfalar hâlinde

GET /api/files?sortBy=modifiedAt&sortOrder=desc&pageSize=5
→ Son değiştirilen 5 dosya
```

### GET /api/files/search — parametreler

| Parametre | Tip | Zorunlu mu | Açıklama |
|---|---|---|---|
| `extension` | string | Evet | Aranacak uzantı. Nokta ile de (`.pdf`) noktasız da (`pdf`) yazılabilir, büyük/küçük harf önemsizdir |

Örnekler:

```
GET /api/files/search?extension=pdf     → .pdf dosyaları
GET /api/files/search?extension=.PDF    → aynı sonuç, büyük harf ve nokta fark etmez
GET /api/files/search                   → extension eksik, 400 Bad Request döner
```

## Swagger

`http://localhost:8080/swagger` — tüm endpoint'lerin listelendiği, doğrudan tarayıcıdan denenebildiği arayüz.

**Tüm endpoint'ler**

![Swagger ana sayfa](swaggerphotos/Screenshot%202026-08-19%20155344.png)

**`POST /api/files/scan` — manuel tarama çalıştırılmış hâli**

![POST scan çalıştırılmış](swaggerphotos/Screenshot%202026-08-19%20155446.png)

**`GET /api/files` — parametreler ve dönen sonuç**

![GET files parametreler](swaggerphotos/Screenshot%202026-08-19%20155930.png)
![GET files response](swaggerphotos/Screenshot%202026-08-19%20155949.png)

**`GET /api/files/search?extension=tXt` — büyük/küçük harf ve nokta yazımından bağımsız eşleşme**

![GET search büyük/küçük harf](swaggerphotos/Screenshot%202026-08-19%20160127.png)

## Arka Plan Servisi

`FileScanBackgroundService`, `ScanIntervalSeconds` ayarında belirtilen aralıkla klasörü kendiliğinden tarar — hiçbir API çağrısına gerek kalmadan. Aşağıdaki terminal çıktısı, container ayaktayken bu otomatik taramanın periyodik olarak çalıştığını ve her turda EF Core'un ürettiği SQL komutlarını gösteriyor:

![Arka plan servisi logları](swaggerphotos/Screenshot%202026-08-19%20160148.png)

## Testler

```bash
dotnet test
```

Testler gerçek bir PostgreSQL container'ı (Testcontainers ile, Docker gerektirir) üzerinde koşar; sahte (mock) veritabanı kullanılmaz.

## Mimari ve Teknik Kararlar

**Katmanlı yapı** — `Controllers` / `Services` / `Data` / `Models` ayrımı yapıldı. Controller'lar sadece HTTP'yi karşılayıp servise yönlendirir, iş mantığı servislerde toplanır; bu sayede iş mantığı HTTP'den bağımsız test edilebilir.

**Dependency Injection ve arayüzler üzerinden bağımlılık** — `FilesController`, somut `FileTrackingService` yerine `IFileTrackingService` arayüzüne bağımlı. DI kaydı `Program.cs`'te yapılır. Bu ayrım implementasyonu değiştirmeyi veya teste sahte bir uygulama vermeyi controller'a hiç dokunmadan mümkün kılar.

**Tekrar işleme kontrolü dosya yolu üzerinden** — bir dosyanın "daha önce görülüp görülmediği" `FilePath`'e göre belirleniyor (ilk sürümde dosya adı + değiştirilme tarihi kullanılmıştı, bu aynı dosyayı sürekli yeni kayıt gibi işleyip tekrar tekrar kaydediyordu — bkz. `YasananSorunlar.md`). İçerik değişip değişmediği ise SHA-256 hash ile ayrıca kontrol edilir: boyut veya tarih değişse bile hash aynıysa dosya yeniden hash'lenmez.

**SQLite yerine PostgreSQL** — proje SQLite ile başladı, sonradan PostgreSQL'e geçirildi. Gerekçe: PostgreSQL ayrı bir sunucu süreci olarak çalıştığı için Docker'da gerçek bir çok-servisli mimariyi (uygulama + veritabanı ayrı container) göstermeye ve production'a daha yakın bir kurulumu denemeye imkân veriyor. Geçişte iki önemli teknik detay öne çıktı: (1) PostgreSQL'in `timestamp with time zone` tipi yerel saatli `DateTime` kabul etmiyor, bu yüzden dosya tarihleri `CreationTimeUtc`/`LastWriteTimeUtc` ile UTC olarak saklanıyor; (2) SQLite'a özgü migration'lar (`INTEGER`/`TEXT` kolon tipleri) PostgreSQL'de anlamsız olduğu için migration'lar sıfırdan yeniden üretildi.

**Migration'ların uygulama açılışında otomatik çalıştırılması** — `Program.cs` içinde `dbContext.Database.Migrate()` her açılışta çağrılır. Container'ın runtime imajında (`aspnet:10.0`) EF Core araçları bulunmadığı için `dotnet ef database update` komutu container içinde çalıştırılamaz; bekleyen migration'ları uygulamanın kendisinin açılışta uygulaması tek pratik yol.

**Global exception middleware** — beklenmeyen hataların istemciye çıplak stack trace olarak sızmaması, bunun yerine sunucu tarafında loglanıp istemciye sade bir 500 + `traceId` dönmesi için pipeline'ın en dışına bir middleware eklendi.

**Sayfalama ve sıralama alan adı beyaz listesi ile sınırlı** — `SortBy` parametresi doğrudan sorguya gömülmez, sadece bilinen alanlara (`fileName`, `extension`, `sizeBytes`, `createdAt`, `modifiedAt`) eşlenir; tanınmayan bir değer `Id`'ye düşer. Amaç, istemciden gelen serbest metnin sorguya karışmaması.

**Background Service ile otomatik tarama, manuel tarama endpoint'inden ayrı** — `IFolderScannerService.ScanFolderAsync()` hem `POST /api/files/scan` tarafından hem de `FileScanBackgroundService` tarafından çağrılıyor. Tarama mantığı önce manuel endpoint ile doğru çalıştığı doğrulandıktan sonra otomatikleştirildi.

**Testler gerçek PostgreSQL'e karşı, sahte (mock) DbContext ile değil** — LINQ sorguları veritabanına göre farklı SQL'e çevrilir (ör. metin sıralaması PostgreSQL ile SQLite arasında farklı davranır). Testcontainers ile testler sırasında gerçek bir `postgres:17` container'ı açılıp gerçek migration'lar uygulanıyor; böylece "sorgu gerçekten çalışıyor mu" sorusu da test edilmiş oluyor. 71 test performans için tek bir container'ı paylaşıyor, izolasyon her testten önce tablo boşaltılarak sağlanıyor.

**Dockerfile iki aşamalı (multi-stage)** — derleme SDK imajıyla (~800 MB, derleyici dahil), çalıştırma ise sadece runtime imajıyla yapılıyor; son imaja SDK ve kaynak kod dahil olmuyor, imaj küçük kalıyor.

## Bilinen Sınırlamalar

- Diskten silinen dosyaların veritabanı kaydı otomatik temizlenmiyor (ek çalışma olarak değerlendirildi, teslim kapsamına alınmadı).
- "İşlenen dosyanın başka bir klasöre taşınması" seçeneği bilinçli olarak uygulanmadı: taranan klasör salt okunur (`:ro`) bağlanıyor, tarayıcının dosyaları taşıması/silmesi bu tasarımla çelişirdi.
