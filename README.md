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
- Her push ve pull request'te derleme + testleri koşan GitHub Actions CI
- Testcontainers ile gerçek PostgreSQL'e karşı koşan 91 birim/entegrasyon testi

## Kullanılan Teknolojiler

- .NET 10, ASP.NET Core Web API
- Entity Framework Core + Npgsql (PostgreSQL)
- Swagger / Swashbuckle
- Microsoft.Extensions.Logging
- Docker, Docker Compose
- xUnit, Testcontainers

## Proje Yapısı

```
src/
├── Domain/                     Entity'ler. Hiçbir projeye ve pakete bağlı değil.
│   └── Entities/               TrackedFile
│
├── Application/                İş katmanı. Yalnızca Domain'e bağlı, NuGet paketi yok.
│   ├── Interfaces/             IRepository<T>, IFileRepository, IUnitOfWork,
│   │                           IFolderScannerService, IFileTrackingService
│   ├── Services/               FileTrackingService
│   ├── DTOs/                   TrackedFileDto, DuplicateGroupDto
│   ├── Models/                 FileQueryParameters, PagedResult, FolderWatchSettings
│   └── Mapping/                Entity -> DTO çevirisi
│
├── Infrastructure/             Teknolojiyle temas eden katman. EF Core ve Npgsql
│   │                           SADECE burada.
│   ├── Persistence/            AppDbContext, Migrations
│   │   └── Repositories/       Repository<T>, FileRepository, UnitOfWork
│   ├── FileSystem/             FolderScannerService (diski tarar)
│   └── DependencyInjection.cs  AddInfrastructure — arayüz/sınıf bağlamaları
│
└── WebApi/                     Sunum katmanı. Çalıştırılabilir olan tek proje.
    ├── Controllers/            FilesController
    ├── Middleware/             Global hata yakalama
    ├── BackgroundServices/     FileScanBackgroundService
    └── Program.cs              Composition root

Tests/          xUnit test projesi (dört katmana da bağlı)
  Models/         FileQueryParameters, PagedResult — saf mantık, veritabanısız
  Services/       FileTrackingService, FolderScannerService — gerçek PostgreSQL'e karşı
  Repositories/   Update davranışı ve FilePath benzersizliği
  Api/            HTTP uçları — WebApplicationFactory ile gerçek boru hattı
  TestHelpers/    Container, geçici klasör, test veritabanı, ApiFactory

watched/        Taranacak örnek klasör
.github/        CI workflow (derleme + testler)
```

**Bağımlılık kuralı — oklar hep içeri bakar:**

```
WebApi ──> Application ──> Domain
   │            ▲
   └────> Infrastructure ─┘
```

`Application` katmanı `Infrastructure`'ı tanımaz; yalnızca `Interfaces/` altında
"böyle bir repository olacak" der. O arayüzleri `Infrastructure` uygular ve ikisi
`Program.cs`'te birbirine bağlanır. Bu sayede veritabanı teknolojisi değişse
`Application` ve `Domain` hiç değişmez.

## Kurulum ve Çalıştırma

### Docker ile (önerilen)

Uygulama ve PostgreSQL'i tek komutla ayağa kaldırır, migration'ları otomatik uygular.

```bash
docker compose up --build
```

- API: http://localhost:8080
- Swagger: http://localhost:8080/swagger
- Taranan klasör: `./watched` (host'tan salt okunur olarak bağlanır)

Durdurmak için `docker compose down`; veritabanını da silmek için `docker compose down -v`.

### Yerel olarak

Gereksinim: yerel bir PostgreSQL sunucusu ve `src/WebApi/appsettings.json`'daki `ConnectionStrings:DefaultConnection` değerinin o sunucuya işaret etmesi.

```bash
dotnet run --project src/WebApi
```

Migration'lar açılışta otomatik uygulandığı için ayrıca komut çalıştırmak gerekmez.
Elle uygulamak ya da yeni migration üretmek istersen, DbContext Infrastructure'da
ve çalıştırılabilir proje WebApi olduğu için iki proje de belirtilmeli:

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/WebApi
dotnet ef migrations add MigrationAdi --project src/Infrastructure --startup-project src/WebApi
```

## Ayarlar (src/WebApi/appsettings.json)

```json
"WatchSettings": {
  "FolderPath": "watched",
  "ScanIntervalSeconds": 10
}
```

`FolderPath` göreli yazılabilir; bu durumda **çalışma dizinine göre değil, uygulamanın içerik köküne göre** çözülür (`Program.cs`). Böylece uygulamanın nereden başlatıldığı sonucu değiştirmez. Mutlak yol verilirse olduğu gibi kullanılır.

- Yerelde `appsettings.Development.json` bu değeri `../../watched` yapar — yani depo kökündeki `watched/` klasörü taranır.
- Docker'da `WatchSettings__FolderPath=/data/watch` ortam değişkeni devreye girer ve JSON'daki değeri ezer.

Başka bir klasörü taratmak için ayarı değiştirmek yerine ortam değişkeni vermek yeterli:

```bash
WatchSettings__FolderPath=/istediginiz/klasor dotnet run --project src/WebApi
```

### Bağlantı bilgileri hakkında

Depodaki PostgreSQL kullanıcı adı ve şifresi (`postgres` / `postgres`) **bilinçli olarak açıkta**: bu bir demo projesi ve `docker compose up` komutunun ek bir kuruluma gerek kalmadan çalışması isteniyor. Veritabanı yalnızca compose ağında ayakta, dışarıya port açmıyor.

Gerçek bir ortamda bu değerler depoya girmez; bağlantı dizesi ortam değişkeni (`ConnectionStrings__DefaultConnection`) ya da .NET user secrets üzerinden verilir. Uygulama zaten ortam değişkenini JSON'a tercih ettiği için kodda hiçbir değişiklik gerekmez.

## API Uçları

| Metot | Yol | Açıklama |
|---|---|---|
| GET | `/api/files` | Sayfalı dosya listesi |
| GET | `/api/files/{id}` | Tek dosya, bulunamazsa 404 |
| GET | `/api/files/search?extension=` | Uzantıya göre arama (sayfalı) |
| POST | `/api/files/scan` | Klasör taramasını manuel başlatır |
| GET | `/api/files/duplicates` | Hash'e göre gruplanmış yinelenen dosyalar (sayfalı) |

Liste dönen üç uç da aynı zarfı döner: `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage`. Sayfalama parametreleri (`page`, `pageSize`, `sortBy`, `sortOrder`) üçünde de geçerlidir.

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

Ayrıca `GET /api/files` ile aynı sayfalama ve sıralama parametrelerini kabul eder. `totalCount`, uzantı filtresi uygulandıktan sonraki toplam sayıdır — yani "kaç `.pdf` var".

Örnekler:

```
GET /api/files/search?extension=pdf                  → ilk 10 .pdf dosyası
GET /api/files/search?extension=.PDF                 → aynı sonuç, büyük harf ve nokta fark etmez
GET /api/files/search?extension=pdf&page=2           → sonraki 10 kayıt
GET /api/files/search?extension=pdf&sortBy=sizeBytes&sortOrder=desc
                                                     → en büyük .pdf dosyaları önce
GET /api/files/search                                → extension eksik, 400 Bad Request döner
```

### GET /api/files/duplicates — parametreler

`page` ve `pageSize` ile sayfalanır. Gruplar **israf edilen alana göre azalan** sırada gelir; bu sıralama veritabanında yapılır, yani `page=1` her zaman en çok yer kaplayan grupları verir. `totalCount` toplam yinelenen grup sayısıdır (dosya sayısı değil). `sortBy`/`sortOrder` bu uçta yok sayılır — grup sıralaması sabittir.

```
GET /api/files/duplicates              → en çok yer israf eden ilk 10 grup
GET /api/files/duplicates?pageSize=3   → ilk 3 grup
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

Aynı komutlar her push ve pull request'te GitHub Actions üzerinde de koşuyor (`.github/workflows/ci.yml`): `dotnet restore`, `dotnet build -warnaserror`, `dotnet test`. Uyarılar hata sayılıyor — proje şu anda 0 uyarı ile derleniyor ve bu eşiğin sessizce kayması istenmiyor.

## Mimari ve Teknik Kararlar

**Clean Architecture katmanları** — proje tek bir web projesi olarak başladı (`Controllers` / `Services` / `Data` / `Models` klasörleri), sonradan dört ayrı projeye bölündü: `Domain`, `Application`, `Infrastructure`, `WebApi`. Bölünmenin klasör ayrımından farkı, **derleyicinin kuralı zorlamasıdır**: `Application` projesinin `Infrastructure`'a referansı olmadığı için oradaki bir sınıfı yanlışlıkla kullanmak derleme hatası verir. Klasör ayrımında bunu engelleyen hiçbir şey yoktu.

**Entity yerine DTO dönülmesi** — servisler `TrackedFile` entity'sini değil `TrackedFileDto` döner. Veritabanı tablosunu doğrudan dışarı vermek, tablo şemasını API sözleşmesi haline getirir; tabloya bir kolon eklendiği anda API cevabı da istemsizce değişirdi. Çeviri tek yerde (`Application/Mapping`) toplandı.

**Repository ve Unit of Work** — `DbContext` yalnızca `Infrastructure` içinde kullanılır; servisler `IFileRepository` ve `IUnitOfWork` arayüzlerini görür. Ortak CRUD işlemleri generic `Repository<T>`'de bir kez yazıldı, `FileRepository` yalnızca TrackedFile'a özel sorguları ekler. Repository metotları `SaveChanges` çağırmaz — kaydetme anını çağıran taraf belirler, böylece tarama döngüsünde biriken tüm ekleme ve güncellemeler tek transaction'da yazılır. Güncelleme `Update` ile açıkça bildirilir: EF Core takip ettiği kayıttaki değişikliği kendiliğinden fark ederdi, ama o zaman kodda güncellemenin yapıldığını söyleyen hiçbir ifade olmaz ve davranış sorgunun `AsNoTracking` olmamasına sessizce bağlı kalırdı.

**Eşzamanlı tarama iki katmanda engelleniyor** — tarama iki yerden tetiklenebilir: `POST /api/files/scan` ve arka plan servisi. İkisi çakışırsa her ikisi de "bu dosya henüz kayıtlı değil" görüp aynı satırı ekleyebilir. Önlem iki katmanlı: (1) `FolderScannerService` içinde süreç genelinde bir kilit (`static SemaphoreSlim`) — ikinci çağıran bekler, paralel koşmaz; (2) veritabanında `FilePath` üzerinde **benzersiz index**. Kilit tek süreç içindir; uygulama birden çok container olarak çoğaltılırsa her sürecin kendi kilidi olacağı için asıl güvence index'tir. Benzersiz olmayan `Hash` index'iyle karıştırılmamalı: aynı hash'in birden çok satırda olması zaten aranan durumdur, aynı yolun iki kez olması ise her zaman hatadır.

**İptal edilebilirlik (CancellationToken)** — tüm async veri erişimi ve servis metotları `CancellationToken` alır (varsayılanı `default`, yani mevcut çağıranlar etkilenmez). Controller'lar `HttpContext.RequestAborted`, arka plan servisi `stoppingToken` geçirir. Böylece istemci bağlantıyı kapattığında ya da uygulama kapanırken süren tarama ve sorgular bırakılabiliyor; taramanın dosya döngüsü her turda iptal kontrolü yapar ve yarım kalan değişiklikler kaydedilmez. Döngüdeki genel `catch (Exception)` bloğu iptali yutmasın diye `OperationCanceledException` ayrıca yakalanıp yukarı bırakılıyor.

**Dependency Injection ve arayüzler üzerinden bağımlılık** — `FilesController`, somut `FileTrackingService` yerine `IFileTrackingService` arayüzüne bağımlı. Somut sınıfların arayüzlere bağlandığı tek yer `Program.cs` (composition root); Infrastructure kendi kayıtlarını `AddInfrastructure` uzantısıyla kendisi yapar. Bu ayrım implementasyonu değiştirmeyi veya teste sahte bir uygulama vermeyi controller'a hiç dokunmadan mümkün kılar.

**Tekrar işleme kontrolü dosya yolu üzerinden** — bir dosyanın "daha önce görülüp görülmediği" `FilePath`'e göre belirleniyor (ilk sürümde dosya adı + değiştirilme tarihi kullanılmıştı, bu aynı dosyayı sürekli yeni kayıt gibi işleyip tekrar tekrar kaydediyordu — bkz. `YasananSorunlar.md`). İçerik değişip değişmediği ise SHA-256 hash ile ayrıca kontrol edilir: boyut veya tarih değişse bile hash aynıysa dosya yeniden hash'lenmez.

**SQLite yerine PostgreSQL** — proje SQLite ile başladı, sonradan PostgreSQL'e geçirildi. Gerekçe: PostgreSQL ayrı bir sunucu süreci olarak çalıştığı için Docker'da gerçek bir çok-servisli mimariyi (uygulama + veritabanı ayrı container) göstermeye ve production'a daha yakın bir kurulumu denemeye imkân veriyor. Geçişte iki önemli teknik detay öne çıktı: (1) PostgreSQL'in `timestamp with time zone` tipi yerel saatli `DateTime` kabul etmiyor, bu yüzden dosya tarihleri `CreationTimeUtc`/`LastWriteTimeUtc` ile UTC olarak saklanıyor; (2) SQLite'a özgü migration'lar (`INTEGER`/`TEXT` kolon tipleri) PostgreSQL'de anlamsız olduğu için migration'lar sıfırdan yeniden üretildi.

**Migration'ların uygulama açılışında otomatik çalıştırılması** — `Program.cs` açılışta `app.Services.ApplyMigrations()` çağırır; bu uzantı `Infrastructure/DependencyInjection.cs` içinde `dbContext.Database.Migrate()` çalıştırır (WebApi'nin `AppDbContext`'i tanımak zorunda kalmaması için uzantıya alındı). Container'ın runtime imajında (`aspnet:10.0`) EF Core araçları bulunmadığı için `dotnet ef database update` komutu container içinde çalıştırılamaz; bekleyen migration'ları uygulamanın kendisinin açılışta uygulaması tek pratik yol.

**Global exception middleware** — beklenmeyen hataların istemciye çıplak stack trace olarak sızmaması, bunun yerine sunucu tarafında loglanıp istemciye sade bir 500 + `traceId` dönmesi için pipeline'ın en dışına bir middleware eklendi.

**Sayfalama ve sıralama alan adı beyaz listesi ile sınırlı** — `SortBy` parametresi doğrudan sorguya gömülmez, sadece bilinen alanlara (`fileName`, `extension`, `sizeBytes`, `createdAt`, `modifiedAt`) eşlenir; tanınmayan bir değer `Id`'ye düşer. Amaç, istemciden gelen serbest metnin sorguya karışmaması.

**Background Service ile otomatik tarama, manuel tarama endpoint'inden ayrı** — `IFolderScannerService.ScanFolderAsync()` hem `POST /api/files/scan` tarafından hem de `FileScanBackgroundService` tarafından çağrılıyor. Tarama mantığı önce manuel endpoint ile doğru çalıştığı doğrulandıktan sonra otomatikleştirildi.

**HTTP uçları ayrıca uçtan uca sınanıyor** — servis testleri "sorgu doğru sonucu veriyor mu" sorusunu cevaplıyor; ama bulunamayan kaydın 404'e çevrilmesi, eksik parametrenin 400 dönmesi, `duplicates` rotasının `{id}` kalıbından önce eşleşmesi ve hata middleware'inin 500 + `traceId` üretmesi controller sınıfının içinde değil boru hattında yaşıyor. Bunlar `WebApplicationFactory` ile uygulama bellek içinde gerçek boru hattıyla ayağa kaldırılarak test ediliyor. Üretim yapılandırmasından yalnızca iki şey değişiyor: bağlantı dizesi test container'ına yönlendiriliyor ve arka plan tarama servisi kaldırılıyor (kalsaydı testin kurduğu veriye kendiliğinden kayıt ekler, testler ara ara düşerdi).

**Testler gerçek PostgreSQL'e karşı, sahte (mock) DbContext ile değil** — LINQ sorguları veritabanına göre farklı SQL'e çevrilir (ör. metin sıralaması PostgreSQL ile SQLite arasında farklı davranır). Testcontainers ile testler sırasında gerçek bir `postgres:17` container'ı açılıp gerçek migration'lar uygulanıyor; böylece "sorgu gerçekten çalışıyor mu" sorusu da test edilmiş oluyor. 91 test performans için tek bir container'ı paylaşıyor, izolasyon her testten önce tablo boşaltılarak sağlanıyor.

**Dockerfile iki aşamalı (multi-stage)** — derleme SDK imajıyla (~800 MB, derleyici dahil), çalıştırma ise sadece runtime imajıyla yapılıyor; son imaja SDK ve kaynak kod dahil olmuyor, imaj küçük kalıyor.

## Bilinen Sınırlamalar

- Diskten silinen dosyaların veritabanı kaydı otomatik temizlenmiyor (ek çalışma olarak değerlendirildi, teslim kapsamına alınmadı).
- "İşlenen dosyanın başka bir klasöre taşınması" seçeneği bilinçli olarak uygulanmadı: taranan klasör salt okunur (`:ro`) bağlanıyor, tarayıcının dosyaları taşıması/silmesi bu tasarımla çelişirdi.
