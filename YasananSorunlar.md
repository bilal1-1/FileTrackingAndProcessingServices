# Yaşanan Sorunlar ve Çözümleri

Bu dosya, proje geliştirme sürecinde karşılaşılan teknik sorunları, bu sorunların **neden** oluştuğunu ve **nasıl çözüldüğünü** kayıt altına almak için hazırlanmıştır.

---

## Sorun 1: Build Hatası — "The type or namespace name could not be found"

### Belirti

`dotnet ef migrations add InitialCreate` komutu çalıştırıldığında proje derlenemedi:

```
Build failed. Use dotnet build to see the errors.
```

`dotnet build` çalıştırıldığında gerçek hatalar ortaya çıktı:

```
error CS0246: The type or namespace name 'TrackedFile' could not be found
error CS0246: The type or namespace name 'AppDbContext' could not be found
error CS0246: The type or namespace name 'IFileTrackingService' could not be found
error CS1061: 'DbContextOptionsBuilder' does not contain a definition for 'UseSqlite'
```

### Kök Neden

Proje `Models`, `Data`, `Services`, `Controllers` gibi klasörlere bölündüğünde, C# her klasörü otomatik olarak **ayrı bir namespace (ad alanı)** olarak ele alıyor:

- `Models/` → `FileTrackingAndProcessingServices.Models`
- `Data/` → `FileTrackingAndProcessingServices.Data`
- `Services/` → `FileTrackingAndProcessingServices.Services`

Bir dosya, başka bir namespace'teki sınıfı kullanmak istediğinde bunu `using` ifadesiyle **açıkça belirtmesi** gerekiyor. `Services/` klasöründeki dosyalar `Models` ve `Data` namespace'lerindeki sınıflara (`TrackedFile`, `AppDbContext`) `using` olmadan erişmeye çalıştığı için derleyici bu sınıfları bulamadı. Aynı şekilde `UseSqlite()` metodu `Microsoft.EntityFrameworkCore` paketinden geldiği için o paketin de `using` ile eklenmesi gerekiyordu.

### Çözüm

Eksik olan `using` satırları ilgili dosyaların başına eklendi:

**`Services/IFileTrackingService.cs`:**
```csharp
using FileTrackingAndProcessingServices.Models;
```

**`Services/FileTrackingService.cs`:**
```csharp
using FileTrackingAndProcessingServices.Models;
using FileTrackingAndProcessingServices.Data;
using Microsoft.EntityFrameworkCore;
```

**`Controllers/FilesController.cs`:**
```csharp
using FileTrackingAndProcessingServices.Services;
```

**`Program.cs`:**
```csharp
using FileTrackingAndProcessingServices.Data;
using FileTrackingAndProcessingServices.Services;
using Microsoft.EntityFrameworkCore;
```

Bu düzeltmelerden sonra `dotnet build` "Build succeeded" verdi ve `dotnet ef migrations add InitialCreate` başarıyla çalıştı.

### Ders

Proje klasörlere (katmanlara) bölündüğünde, her klasörün kendi namespace'i oluşur. Bir katman başka bir katmandaki sınıfı kullanacaksa, o katmanın namespace'i için `using` eklenmesi gerekir. Bu, katmanlar arası bağımlılığın kodda **açıkça görünür** olmasını sağlar — gizli/örtük bağımlılık oluşmaz.

---

## Sorun 2: Swagger Arayüzü Açılmıyor / HTTP-HTTPS Yönlendirme Hatası

### Belirti

Ödev için görsel Swagger arayüzü isteniyordu. `Swashbuckle.AspNetCore` paketi eklendi, `Program.cs`'e `AddSwaggerGen()`, `UseSwagger()`, `UseSwaggerUI()` satırları eklendi. `dotnet run` çalıştırıldığında uygulama başarıyla başladı:

```
Now listening on: http://localhost:5158
Application started. Press Ctrl+C to shut down.
```

Ancak tarayıcıda `http://localhost:5158/swagger` adresine gidildiğinde sayfa **açılmadı** — bağlantı kurulamadı.

### Kök Neden

`Program.cs` içinde hâlâ şu satır duruyordu:

```csharp
app.UseHttpsRedirection();
```

Bu satır, sunucuya gelen her `http` isteğini otomatik olarak `https` adresine yönlendirmesini söylüyor. Fakat log çıktısında sadece `http://localhost:5158` dinleniyordu — **https için ayrı bir port/sertifika tanımlı değildi** (proje `launchSettings.json`'da sadece http profiliyle çalıştırılmıştı).

Sonuç: Tarayıcı `http://localhost:5158/swagger`'a istek attığında, sunucu bunu `https://localhost:5158/swagger`'a yönlendirmeye çalışıyor, ama o adreste dinleyen bir https sunucusu olmadığı için tarayıcı bağlantı kuramıyor. İstek `/swagger` sayfasına hiç ulaşamadan yönlendirmede başarısız oluyor.

### Çözüm

`Program.cs` içinden `app.UseHttpsRedirection();` satırı kaldırıldı. Yerel geliştirme ortamında sadece http profili kullanıldığı ve veri zaten dışarı çıkmadığı için https yönlendirmesine ihtiyaç yok.

**Doğrulama:** Uygulama yeniden başlatıldı ve test edildi:
- `http://localhost:5158/swagger/index.html` → HTTP 200 (Swagger arayüzü açılıyor)
- `http://localhost:5158/api/files` → `[]` (veritabanı bağlantısı çalışıyor, henüz kayıt yok)

Sorun çözüldü, iskelet uçtan uca çalışır durumda.

### Ders

`UseHttpsRedirection()` middleware'i, uygulamanın hem http hem https üzerinde dinlediği (özellikle production/canlı) senaryolar için tasarlanmıştır. Yerel geliştirmede sadece http profili kullanılıyorsa ve https listener tanımlı değilse, bu satır sonsuz/başarısız bir yönlendirmeye yol açar. Loglardaki "Now listening on" satırında hangi protokol(ler)in aktif olduğunu kontrol etmek, bu tür sorunları erken teşhis etmeye yardımcı olur.

---

## Sorun 3: Aynı Dosya Her Taramada Tekrar Tekrar Kaydediliyor

### Belirti

`POST /api/files/scan` endpoint'i eklendikten sonra tarama çalıştırıldı ve doğru sonuç verdi. Ancak endpoint ikinci, üçüncü kez çağrıldığında `0 yeni dosya işlendi.` beklenirken her seferinde yeni kayıtlar oluşmaya devam etti.

`GET /api/files` çıktısına bakıldığında, aynı dosyanın (`dosyatakip.db-wal`) boyutu sürekli artan onlarca kopyasının biriktiği görüldü:

```json
{ "id": 10, "fileName": "dosyatakip.db-wal", "sizeBytes": 8272,  "modifiedAt": "2026-08-12T14:29:28.8343411" },
{ "id": 11, "fileName": "dosyatakip.db-wal", "sizeBytes": 16512, "modifiedAt": "2026-08-12T14:32:10.5846883" },
{ "id": 12, "fileName": "dosyatakip.db-wal", "sizeBytes": 24752, "modifiedAt": "2026-08-12T14:32:13.7666503" },
{ "id": 13, "fileName": "dosyatakip.db-wal", "sizeBytes": 32992, "modifiedAt": "2026-08-12T14:32:14.7385806" }
```

Ödevin *"daha önce işlenen dosyalar tekrar işlenmemelidir"* gereksinimi ihlal ediliyordu.

### Kök Neden

İki ayrı problem üst üste binmişti.

**1. Tekrar kontrolü, değişebilen bir alanı kimliğin parçası yapıyordu.**

`FolderScannerService.ScanFolderAsync()` içindeki kontrol şöyleydi:

```csharp
bool alreadyExists = await _context.TrackedFiles
    .AnyAsync(f => f.FileName == file.Name && f.ModifiedAt == file.LastWriteTime);
```

Buradaki mantık "aynı isim **ve** aynı değiştirilme tarihi varsa bu dosyayı daha önce gördüm" diyor. Sorun şu: `ModifiedAt` dosyanın **kimliği değil, durumudur** — zamanla değişir. Dosya her değiştiğinde bu karşılaştırma başarısız oluyor ve kayıt "hiç görülmemiş yeni dosya" muamelesi görüp tekrar ekleniyor.

**2. Sürekli değişen bir dosya taranıyordu (`.db-wal`).**

SQLite, WAL (Write-Ahead Log) modunda çalışırken her yazma işlemini önce `dosyatakip.db-wal` dosyasına yazar. Taranan klasör, veritabanının bulunduğu klasörle aynı olduğu için şu döngü oluşuyordu:

1. Tarama çalışır, bulduğu yeni dosyaları `SaveChangesAsync()` ile veritabanına yazar
2. Bu yazma işlemi `dosyatakip.db-wal` dosyasını büyütür → dosyanın `ModifiedAt` ve boyutu değişir
3. Bir sonraki taramada `FileName + ModifiedAt` kontrolü bu dosyayı tanıyamaz → yeni kayıt olarak ekler
4. Bu ekleme yine WAL'a yazılır → adım 2'ye dön

Yani tarama, **kendi yazma işleminin yan etkisini yeni bir dosya değişikliği olarak algılıyordu.** Her tarama bir sonrakine yem üretiyordu.

Ayrıca kimlik olarak dosya adının kullanılması ayrı bir zayıflıktı: alt klasör taraması eklendiğinde `Belgeler/rapor.txt` ile `Arsiv/rapor.txt` aynı dosya sanılırdı.

### Çözüm

**1. Kimlik olarak tam dosya yolu kullanıldı.** `TrackedFile` modeline `FilePath` alanı eklendi:

```csharp
public string FilePath { get; set; }   // dosyanın tam yolu — tekrar kontrolünün anahtarı
```

Dosya adının aksine tam yol, dosya değişse bile sabit kalır ve farklı klasörlerdeki aynı isimli dosyaları birbirinden ayırır.

**2. Tekrar kontrolü yeniden yazıldı** — kayıt bulunduğunda atlamak yerine, mevcut kaydın değişebilen alanları yerinde güncelleniyor:

```csharp
var existing = await _context.TrackedFiles
    .FirstOrDefaultAsync(f => f.FilePath == file.FullName);

if (existing != null)
{
    // Zaten işlenmiş — tekrar İŞLENMEZ, yeni satır açılmaz.
    // Sadece diskteki güncel bilgisi tazelenir.
    existing.ModifiedAt = file.LastWriteTime;
    existing.SizeBytes = file.Length;

    _logger.LogInformation("Dosya zaten kayıtlı, bilgisi güncellendi: {FileName}", file.Name);
    continue;
}
```

Buradaki ayrım kritik: `FilePath` **kimlik** (sorgulanan), `ModifiedAt`/`SizeBytes` ise **veri** (güncellenen). Böylece dosya değişse bile ikinci bir satır açılmaz, `newFileCount` sayacına dahil edilmez, sadece bilgisi tazelenir.

**3. Migration ve temizlik:**

```
dotnet ef migrations add AddFilePathToTrackedFile
dotnet ef database drop --force    # birikmiş bozuk kayıtlar silindi
dotnet ef database update
```

**4. Yan düzeltmeler:** `appsettings.json`'daki anahtar `Watchsettings` → `WatchSettings` olarak düzeltildi (`Program.cs`'teki `GetSection("WatchSettings")` ile birebir aynı olması için — .NET config binding büyük/küçük harf duyarsız olduğundan çalışıyordu ama okuyan kişiyi yanıltıyordu). `.gitignore`'a `*.db-wal` ve `*.db-shm` eklendi.

**Bilinçli olarak yapılmayan:** `.db`, `.db-wal` gibi uzantıları taramadan hariç tutmak düşünüldü ancak tercih edilmedi. Uzantıya göre istisna koymak, mantığı belirli dosya tiplerine bağlı hale getirirdi; oysa `.db` dosyası da başka bir senaryoda meşru şekilde takip edilmek istenebilir. Tarama mantığının **dosya tipinden bağımsız ve her dosya için aynı** kalması tercih edildi. `.db-wal`'ın listede görünmesi bir kusur değil, tarayıcının veritabanıyla aynı klasöre yönlendirilmiş olmasının doğal sonucudur.

### Doğrulama

Veritabanı sıfırlandıktan sonra `POST /api/files/scan` arka arkaya 5 kez çalıştırıldı:

| Tarama | Sonuç | Açıklama |
|--------|-------|----------|
| 1. | `8 yeni dosya işlendi.` | Klasördeki mevcut dosyalar |
| 2. | `2 yeni dosya işlendi.` | `.db-wal` ve `.db-shm`, 1. taramanın yazma işlemi sırasında oluştu |
| 3. | `0 yeni dosya işlendi.` | ✔ |
| 4. | `0 yeni dosya işlendi.` | ✔ |
| 5. | `0 yeni dosya işlendi.` | ✔ |

`GET /api/files` çıktısı toplam **10 satırda sabit kaldı** (çoğalma yok). `dosyatakip.db-wal` kaydı `id=10` olarak yerinde durdu, yalnızca bilgisi tazelendi:

```
id=10  dosyatakip.db-wal  sizeBytes=20632  modifiedAt=2026-08-12T15:15:40.5691751
id=10  dosyatakip.db-wal  sizeBytes=24752  modifiedAt=2026-08-12T15:15:40.6162399
```

Aynı `id`, güncellenmiş boyut ve tarih — yeni satır açılmadığının, kaydın yerinde güncellendiğinin kanıtı.

### Ders

"Bu kaydı daha önce gördüm mü?" kontrolü, **zamanla değişmeyen bir kimlik** üzerinden yapılmalıdır. Boyut, değiştirilme tarihi gibi alanları kimliğin parçası yapmak, sürekli değişen kayıtlarda sonsuz kopya üretir. Doğru ayrım şudur: değişmeyen alan **kimliktir** (onunla ara), değişen alanlar **veridir** (onları güncelle).

İkinci ders: bir sistem kendi çıktısını girdi olarak okuyorsa (burada tarayıcının kendi veritabanı dosyasını taraması) geri besleme döngüsü oluşabilir. İzlenen alan ile sistemin kendi çalışma alanının ayrı tutulması, bu tür döngüleri baştan engeller.
