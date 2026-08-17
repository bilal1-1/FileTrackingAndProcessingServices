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

---

## Sorun 4: SHA-256 Hash Hesaplanıyordu Ama Hiçbir Kararı Etkilemiyordu

### Belirti

`TrackedFile` modeline `Hash` alanı eklendi, her dosya için SHA-256 hesaplanıp veritabanına yazıldı. Ancak "bu hash ne işe yarıyor?" sorusu sorulduğunda somut bir cevap çıkmadı.

Projede `Hash` geçen yerler arandığında durum netleşti:

```
Models/TrackedFile.cs          → alanın tanımı
Services/FolderScannerService  → hesaplama ve yazma
Migrations/...                 → kolon tanımı
```

Hiçbir controller, hiçbir servis metodu bu alanı **okumuyordu**. Hash yazılıyor ama kimse sormuyordu — yani maliyeti olan (her dosyanın baştan sona okunması) ama karşılığı olmayan bir alandı.

### Kök Neden

İki ayrı eksiklik vardı.

**1. Hash karşılaştırılmıyor, sadece üzerine yazılıyordu.**

`FolderScannerService` içindeki kod şöyleydi:

```csharp
bool hashGerekli = string.IsNullOrEmpty(existing.Hash)
    || existing.SizeBytes != file.Length
    || existing.ModifiedAt != file.LastWriteTime;

if (hashGerekli)
{
    existing.Hash = await ComputeHashAsync(file);   // eski değere hiç bakılmıyor
    _logger.LogDebug("Dosya değişmiş, hash yeniden hesaplandı: {FileName}", file.Name);
}
```

Buradaki mantık ters dönmüş durumdaydı: "değişti mi?" kararını **boyut ve tarih** veriyor, hash ise karar verildikten *sonra* hesaplanıp saklanıyordu. Eski hash ile yeni hash hiçbir zaman karşılaştırılmadığı için, içeriğin gerçekten değişip değişmediği bilinmiyordu.

Somut sonucu: bir dosya yedekten geri yüklendiğinde veya açılıp değiştirilmeden kaydedildiğinde `ModifiedAt` değişir ama içerik aynı kalır. Bu durumda log `"Dosya değişmiş"` diyordu — **yanlış bir ifade**. Servisin elinde doğruyu söylemesini sağlayacak veri (yeni hash) vardı, sadece eskisiyle kıyaslamıyordu.

**2. Hash'i tüketen hiçbir özellik yoktu.**

Bir hash'in bir izleme servisinde yapabileceği iki iş vardır: *aynı içerikli dosyaları eşleştirmek* (yinelenen tespiti) ve *içeriğin değişip değişmediğini kesin söylemek* (bütünlük). İkisi de yazılmamıştı.

### Çözüm

**1. Hash, üzerine yazılmadan önce eskisiyle karşılaştırılıyor.** Üç durum ayrıştırıldı:

```csharp
if (hashGerekli)
{
    var yeniHash = await ComputeHashAsync(file);

    if (string.IsNullOrEmpty(existing.Hash))
        _logger.LogDebug("Hash'i olmayan kayıt dolduruldu: {FileName}", file.Name);
    else if (existing.Hash != yeniHash)
        _logger.LogInformation("Dosya içeriği değişti: {FileName}", file.Name);
    else
        _logger.LogDebug("Değiştirilme tarihi değişti ama içerik aynı: {FileName}", file.Name);

    existing.Hash = yeniHash;
}
```

Üçüncü daldaki çıkarım şuna dayanıyor: boyut değişseydi hash de değişirdi. Hash aynı çıktıysa boyut da aynıdır, dolayısıyla `hashGerekli`'yi tetikleyen tek şey tarih olabilir.

**2. Yinelenen dosya tespiti eklendi.** `GET /api/files/duplicates` endpoint'i, aynı hash'e sahip kayıtları gruplayıp döner:

```csharp
var duplicateHashes = await _context.TrackedFiles
    .Where(f => f.Hash != "")
    .GroupBy(f => f.Hash)
    .Where(g => g.Count() > 1)
    .Select(g => g.Key)
    .ToListAsync();
```

Gruplama ve sayma veritabanında yapılır (`GROUP BY ... HAVING COUNT(*) > 1`); belleğe yalnızca yinelenen kayıtlar çekilir, tüm tablo değil. Hash'i boş olan kayıtlar dışarıda bırakılır — henüz hesaplanmamış olmaları onları birbirinin kopyası yapmaz.

Yanıt, her grup için boşa giden alanı (`WastedBytes = SizeBytes * (Count - 1)`) da içerir ve gruplar bu değere göre azalan sıralanır; listeye bakan kişi için en işe yarar sıralama budur.

**3. `Hash` kolonuna index eklendi** (`AddHashIndex` migration'ı). Gruplama sorgusu index olmadan her çağrıda tüm tabloyu tarardı. Index **benzersiz değil** — aynı hash'in birden fazla satırda bulunması zaten aradığımız durum.

### Bilinçli Olarak Kapsam Dışı Bırakılan

**Boyutu ve tarihi aynı kalıp içeriği değişen dosyalar tespit edilmiyor.**

Aynı uzunlukta bir düzenleme yapılır ve `LastWriteTime` da korunursa (bazı yedekleme/eşitleme araçları bunu yapar), `hashGerekli` koşulu `false` kalır, hash hiç hesaplanmaz ve değişiklik kaçar. Üstelik veritabanındaki hash sessizce yanlış hale gelir.

Bunu yakalamanın tek yolu hash'i **koşulsuz**, yani her taramada her dosya için hesaplamaktır. Bu da her taramanın klasördeki tüm baytları baştan sona okuması demektir — 500 GB'lık bir klasörde her tarama 500 GB okuma anlamına gelir. Ödev ölçeğinde bu maliyet, kapattığı riskle orantısız bulundu.

Boyut + tarih ön kontrolü, gerçek dosya sistemlerinin ezici çoğunluğunda doğru çalışan pratik bir sezgiseldir; bilinçli olarak korundu. Gerçekten gerekseydi çözüm, ayrı bir "derin doğrulama" modu (ör. gecelik tam tarama) olurdu — sürekli çalışan tarama döngüsüne yüklenmezdi.

### Doğrulama

İçeriği birebir aynı iki dosya (`kopya-a.txt`, `kopya-b.txt`) ve farklı içerikli bir üçüncü dosya (`tekil-c.txt`) oluşturuldu.

| Adım | Beklenen | Sonuç |
|------|----------|-------|
| `sha256sum` ile dış doğrulama | a ve b aynı, c farklı | ✔ `5fe8d301…` / `5fe8d301…` / `a12cbe81…` |
| `GET /api/files/duplicates` | a+b tek grupta, c yok | ✔ `count: 2`, `wastedBytes: 24`, c listede değil |
| Servisin ürettiği hash | `sha256sum` çıktısıyla birebir aynı | ✔ `5fe8d301…` |
| `kopya-a.txt` içeriği değiştirildi, tekrar tarandı | grup dağılır, liste boşalır | ✔ `[]` |
| `kopya-b.txt` içeriği değiştirildi | log: içerik değişti | ✔ `Dosya içeriği değişti: kopya-b.txt` |

Servisin hesapladığı hash'in bağımsız bir araçla (`sha256sum`) birebir eşleşmesi, implementasyonun doğruluğunu dışarıdan kanıtlıyor.

Test dosyaları doğrulamadan sonra silindi.

### Ders

Bir alanı hesaplamak, o alanın işe yaradığı anlamına gelmez. `Hash` kolonu vardı, doğru hesaplanıyordu, migration'ı bile yazılmıştı — ama hiçbir sorgu onu okumadığı için sistemin davranışına hiçbir katkısı yoktu. **Bir verinin değeri, onu tüketen bir karar ya da özellik olduğunda doğar.**

İkinci ders: bir kontrolün *maliyetini* ödeyip *faydasını* almamak mümkündür. Hash her değişiklikte hesaplanıyor (maliyet) ama karşılaştırılmıyordu (fayda alınmıyor). Bu tür durumlar, "şu alan hangi kararı etkiliyor?" diye sorarak ortaya çıkar.

---

## Sorun 5: Uzantı Araması Büyük/Küçük Harfe Duyarlıydı

### Belirti

Bu hata bir kullanıcı şikâyetiyle değil, **birim testleri yazılırken** ortaya çıktı.

Diskte `BELGE.TXT` adında bir dosya varken `GET /api/files/search?extension=.txt` çağrısı **boş liste** dönüyordu. Ne hata mesajı ne exception vardı — endpoint 200 OK ve `[]` döndürüyordu. Yani hata, "sonuç yok" görüntüsünün arkasına saklanıyordu.

Şüpheyi ölçmek için yazılan geçici test:

```csharp
ortam.Ekle(VeritabaniOrtami.Dosya("BELGE.TXT", extension: ".TXT"));

var sonuc = await service.SearchByExtensionAsync(".txt");

Assert.Single(sonuc);
```

Sonuç:

```
Assert.Single() Failure: The collection was empty
```

### Kök Neden

Tek başına yanlış olmayan iki davranış üst üste bindi.

**1. Tarayıcı uzantıyı diskteki haliyle kaydediyor.** `FolderScannerService`, `file.Extension` değerini olduğu gibi yazıyor. Windows dosya adlarında büyük/küçük harfi koruduğu için `BELGE.TXT` dosyası veritabanına `.TXT` olarak giriyor. Bu doğru davranış — tarayıcının veriyi değiştirmemesi beklenir.

**2. Karşılaştırma harfe duyarlı yapılıyordu.** Servisteki sorgu şöyleydi:

```csharp
return await _context.TrackedFiles
    .Where(f => f.Extension == extension)
    .ToListAsync();
```

Bu `==`, SQLite tarafında varsayılan (BINARY) karşılaştırmaya çevrilir ve `.TXT` ile `.txt` farklı sayılır.

İkisi birleşince kullanıcı, aramada **diskteki harf düzenini tahmin etmek zorunda** kalıyordu. Bir kullanıcının bunu bilmesi beklenemez; üstelik aynı klasörde `rapor.txt` ve `BELGE.TXT` varsa tek bir arama ikisini birden getiremezdi.

### Çözüm

Karşılaştırmanın iki tarafı da küçük harfe indiriliyor — ama **bilerek farklı metotlarla**:

```csharp
var aranan = extension.ToLowerInvariant();

return await _context.TrackedFiles
    .Where(f => f.Extension.ToLower() == aranan)
    .ToListAsync();
```

**Neden iki farklı metot?** İşin ince yeri burası.

- **Aranan değer (C# tarafı) `ToLowerInvariant()` ile küçültülür.** `ToLower()` kullanılsaydı makinenin kültür ayarı devreye girerdi. Türkçe kültürde `"I"` harfinin küçüğü `"ı"`dır; yani `".TIF"` → `".tıf"` olurdu. Veritabanındaki `lower()` ise kültür tanımaz, `".tif"` üretir. Noktalı ı ile noktasız i eşleşmez ve arama yine sessizce boş dönerdi — üstelik bu hata **sadece Türkçe makinelerde** görülürdü.
- **Kolon tarafındaki `ToLower()` SQL'e `lower()` olarak çevrilir.** Karşılaştırma veritabanında yapılır, tüm tablo belleğe çekilmez.

**Elenen alternatifler:**

| Yol | Neden seçilmedi |
|-----|-----------------|
| `EF.Functions.Like(...)` | SQLite'ta LIKE zaten harfe duyarsız, ama `%` ve `_` karakterlerini joker sayar. Kullanıcı `.t_t` ararsa beklenmedik sonuç döner |
| Kolona `NOCASE` collation | Migration gerektirir ve o kolon üzerindeki *tüm* karşılaştırmaları etkiler — yalnızca aramayı ilgilendiren bir kararın şemaya yazılması |

### Doğrulama

Davranış altı testle güvenceye alındı:

| Test | Kapsadığı durum |
|------|-----------------|
| `Search_BuyukKucukHarfFarki_YineDeEslesir` | `.TXT`↔`.txt`, `.txt`↔`.TXT`, `.TxT`↔`.tXt` |
| `Search_IHarfiIcerenUzanti_MakineKulturundenEtkilenmez` | `.TIF`↔`.tif` — yukarıdaki kültür tuzağı |
| `Search_HarfDuyarsizlik_YanlisUzantilariGetirmez` | Duyarsızlık, `.pdf` ve `.txtx` gibi alakasız uzantıları getirmemeli |

Testler önce **eski koda karşı** çalıştırıldı:

```
Failed: 6, Passed: 2     ← eski kod (f.Extension == extension)
Passed: 71, Failed: 0    ← düzeltilmiş kod
```

Bu, testlerin gerçekten bu düzeltmeyi koruduğunu gösteriyor: kod eski haline dönerse testler kırmızı yanar.

### Ders

**Testin değeri sadece ileride bozulmayı yakalamak değil; yazılırken hâlihazırda var olan hatayı ortaya çıkarmaktır.** Bu hata aylardır koddaydı ve Swagger üzerinden elle defalarca test edilmişti. Görülmemesinin sebebi şu: elle test eden kişi kendi oluşturduğu dosyayı arar, dolayısıyla harf düzenini zaten bilir. Test yazmak ise "ya kullanıcı farklı yazsaydı?" sorusunu sormaya zorluyor.

İkinci ders: **sessiz hatalar en tehlikelileridir.** Exception fırlatan bir hata kendini duyurur. Boş liste dönen bir hata ise "aradığın şey yok" gibi görünür — kullanıcı sonucun yanlış olduğunu anlamaz, aramayı bırakır.

Üçüncü ders: **kültüre bağlı metotlar sessiz taşıyıcıdır.** `ToLower()` çoğu makinede doğru çalışır, Türkçe kültürde `I/ı` yüzünden bozulur. Karşılaştırma ve normalleştirme işlerinde `ToLowerInvariant()` / `OrdinalIgnoreCase` tercih edilmeli; kültüre duyarlı olanlar yalnızca kullanıcıya *gösterilecek* metinler içindir.

---

## Sorun 6: SQLite'tan PostgreSQL'e Geçişte Ortaya Çıkan Beş Sorun

### Belirti

Geçiş, başlarken "bağlantı dizesini değiştir, bitti" gibi görünüyordu. Gerçekte
tek satırlık bir iş değildi: `Microsoft.EntityFrameworkCore.Sqlite` yerine
`Npgsql.EntityFrameworkCore.PostgreSQL` konup `UseSqlite` → `UseNpgsql`
yapıldığında art arda beş ayrı sorun çıktı. Hepsinin ortak kökeni aynı:
**SQLite ile PostgreSQL yalnızca "aynı işi yapan iki veritabanı" değil, farklı
mimarilere sahip iki ayrı sistem.** SQLite uygulamanın içinde çalışan bir
kütüphane ve tek bir dosya; PostgreSQL ise ayrı bir sunucu süreci.

Not: geçiş bir performans darboğazı için yapılmadı — bu ölçekte SQLite
fazlasıyla yeterliydi. Gerekçe gerçekçilik ve öğrenmeydi; bu bilinçli olarak
kabul edildi.

---

### 6.1 — Npgsql yerel saatli tarihleri reddediyor

**Belirti.** `FolderScannerService` diske ilk kaydı yazmaya çalıştığında
PostgreSQL tarafı değeri kabul etmiyor.

**Kök neden.** Tarayıcı tarihleri şöyle okuyordu:

```csharp
CreatedAt = file.CreationTime,
ModifiedAt = file.LastWriteTime
```

Bu özellikler **yerel saatli** (`Kind = Local`) bir `DateTime` döndürür. Npgsql
`DateTime` tipini PostgreSQL'in `timestamp with time zone` kolonuna eşler ve bu
kolona yerel saatli bir değer yazılmasına izin vermez. Dahası `Kind` değeri
`Unspecified` olan bir değer de reddedilir — yani testlerdeki
`new DateTime(2026, 1, 1)` bile geçersizdir.

SQLite bu sorunu hiç göstermiyordu çünkü tarihi metin olarak saklıyor ve ne
yazıldığını sorgulamıyor. Hata veritabanının değişmesiyle ortaya çıktı; kod
zaten baştan beri saat dilimi bilgisi olmayan bir değer yazıyordu.

**Çözüm.** Tarihler UTC olarak saklanıyor:

```csharp
CreatedAt = file.CreationTimeUtc,
ModifiedAt = file.LastWriteTimeUtc
```

Karşılaştırma satırı da (`existing.ModifiedAt != file.LastWriteTimeUtc`) aynı uca
çevrildi — biri UTC diğeri yerel kalsaydı her taramada saat farkı kadar sahte
"değişmiş" tespiti üretirdi.

**Elenen alternatif:** kolonu `timestamp without time zone` yapmak. Kod hiç
değişmezdi ve yerel saatler aynen saklanırdı, ama saat dilimi bilgisi kaybolur.
UTC saklamak tercih edildi: sunucunun saat dilimi değişse ya da uygulama başka
bir bölgede çalışsa bile kayıtlı an aynı kalır.

**Bedeli bilinçli kabul edildi:** API artık yerel saate göre 3 saat geride
görünen tarihler döndürüyor.

---

### 6.2 — Migration'lar sağlayıcıya özeldir

**Belirti.** Mevcut dört migration (`InitialCreate`,
`AddFilePathToTrackedFile`, `AddHashToTrackedFile`, `AddHashIndex`) PostgreSQL
üzerinde çalışmıyor.

**Kök neden.** Migration dosyaları veritabanından bağımsız görünse de,
uygulanırken **sağlayıcıya özel SQL üretirler**. SQLite için üretilmiş kod
(`Sqlite:Autoincrement` gibi ek bilgiler dahil) PostgreSQL'de geçerli değildir.

**Çözüm.** Dört migration ve model anlık görüntüsü silinip tek bir
`InitialCreate` yeniden üretildi. Veri taşıma yapılmadı — ödev ölçeğinde sıfırdan
başlamak yeterli. Üretilen şema PostgreSQL tiplerini kullanıyor:

| Kolon | SQLite | PostgreSQL |
|---|---|---|
| `Id` | `INTEGER` + AUTOINCREMENT | `integer` + identity |
| metin alanları | `TEXT` | `text` |
| `SizeBytes` | `INTEGER` | `bigint` |
| `CreatedAt` / `ModifiedAt` | `TEXT` | `timestamp with time zone` |

Son satır, 6.1'deki sorunun neden kaçınılmaz olduğunu da açıklıyor.

---

### 6.3 — Testler artık çalıştırılmayan bir veritabanını doğruluyordu

**Belirti.** Uygulama PostgreSQL'e geçtikten sonra 71 test yeşil yanmaya devam
etti. Sorun tam da buydu: testler hâlâ bellek içi **SQLite**'a karşı koşuyordu.

**Kök neden.** Aynı LINQ sorgusu her veritabanı için farklı SQL'e çevrilir.
SQLite'a karşı geçen bir test, üretimde çalışan veritabanı hakkında hiçbir şey
söylemez. Fark teorik de değildi — geçiş sırasında canlı olarak görüldü.
`sortBy=fileName&sortOrder=desc` PostgreSQL'de şunu döndürdü:

```
YasananSorunlar.md, Program.cs, .gitignore, FileTrackingAndProcessingServices.csproj
```

`.gitignore` neden `F`'den önce geliyor? Çünkü PostgreSQL'in varsayılan
collation'ı metni **dil kurallarına göre** sıralar: baştaki noktayı yok sayar ve
büyük/küçük harfi birincil düzeyde ayırmaz, yani `gitignore` olarak değerlendirir
(`Y > P > g > F`). SQLite ise metni **bayt değerine** göre sıralar ve `.gitignore`
bambaşka bir yere düşerdi. Sıralama iddiası eden bir test, ikisinde farklı sonuç
verebilir.

**Çözüm.** Testler `Testcontainers.PostgreSql` ile gerçek PostgreSQL'e taşındı.
Üç tasarım kararı:

1. **Tek container, tüm testler için.** Container açmak saniyeler sürer; 71 test
   için ayrı ayrı açmak süreyi dakikalara çıkarırdı. `PostgreSqlSunucusu` bir
   koleksiyon fixture'ı olarak container'ı bir kez açar.
2. **İzolasyon `TRUNCATE TABLE "TrackedFiles" RESTART IDENTITY` ile.**
   Milisaniyeler sürer. `RESTART IDENTITY` olmadan `Id` sayacı testler arasında
   büyümeye devam eder ve "ilk kaydın Id'si 1" gibi beklentiler kırılırdı.
3. **Şema `Database.Migrate()` ile kuruluyor, `EnsureCreated()` ile değil.**
   Bu, `Program.cs`'in açılışta yaptığının aynısı; böylece migration'ın
   çalışabilir bir şema ürettiği de her test koşusunda doğrulanmış olur.
   (SQLite döneminde `EnsureCreated()` kullanılıyordu; gerekçe "testin amacı
   migration geçmişi değil" idi. Gerçek veritabanına geçilince migration'ı da
   doğrulamak bedava geldiği için bu karar değişti.)

Test sınıfları tek bir koleksiyonda toplandı. Bu hem container'ı paylaştırıyor
hem de **paralel koşmayı engelliyor** — paralel koşsalardı ortak tabloyu
birbirlerinden silerlerdi.

**Bedeli bilinçli kabul edildi:** `dotnet test` artık Docker'ın çalışıyor
olmasını gerektiriyor ve süre ~1 saniyeden ~5 saniyeye çıktı.

---

### 6.4 — Gizlenmiş EF Core sürüm çakışması (CS1705)

**Belirti.** SQLite paketleri test projesinden kaldırılınca derleme patladı:

```
error CS1705: Assembly 'FileTrackingAndProcessingServices' uses
'Microsoft.EntityFrameworkCore, Version=10.0.10.0' which has a higher version
than referenced assembly 'Microsoft.EntityFrameworkCore, Version=10.0.4.0'
```

**Kök neden.** Web projesi `Microsoft.EntityFrameworkCore.Design` 10.0.10
kullandığı için EF Core **10.0.10**'a karşı derleniyor. Npgsql ise EF Core
**10.0.4** getiriyor. Test projesi, `Design` paketinin `PrivateAssets=all`
olması nedeniyle 10.0.10'u miras almıyor ve 10.0.4'te kalıyor — yani daha eski
bir derlemeye karşı, daha yeni bir derlemeyle derlenmiş projeyi kullanmaya
çalışıyor.

Bu çakışma aslında baştan beri vardı; görünmemesinin sebebi test projesindeki
`Microsoft.EntityFrameworkCore.Sqlite` 10.0.10 paketinin sürümü yukarı
çekmesiydi. Paket kaldırılınca dayanak da kalktı.

**Çözüm.** Test projesine `Microsoft.EntityFrameworkCore.Relational` 10.0.10
açıkça eklendi (EF Core 10.0.10'u da beraberinde getirir).

---

### 6.5 — Container kurulumu tek servisten iki servise çıktı

**Belirti.** SQLite döneminde tek container yetiyordu. PostgreSQL ayrı bir sunucu
süreci olduğu için artık iki container gerekiyor ve aralarında bir **zamanlama
sorunu** var: uygulama `Program.cs` içinde açılışta `Database.Migrate()`
çağırıyor, o an veritabanı hazır değilse uygulama patlayarak kapanır.

**Kök neden.** Bir PostgreSQL container'ının "başlamış" olması "bağlantı kabul
ediyor" demek değildir; arada veri klasörünü hazırladığı birkaç saniye vardır.
Docker'ın `depends_on` ifadesi yalnızca **başlatma sırasını** garanti eder,
hazır olmayı değil.

**Çözüm.** `docker-compose.yml`'de `db` servisine `pg_isready` tabanlı bir
sağlık kontrolü tanımlandı, `api` servisi de `condition: service_healthy` ile
bekletildi. Ek olarak `UseNpgsql(..., npgsql => npgsql.EnableRetryOnFailure())`
ile geçici bağlantı hatalarında sorgular kendiliğinden yeniden deneniyor.

Yan düzenlemeler:

- Bağlantı dizesi **bilinçli olarak Dockerfile'a yazılmadı** — şifre imaja
  gömülmesin ve `Host=db` bilgisini veritabanının servis adını bilen taraf
  (compose) versin diye. `localhost` yazmak container'ın kendi içini işaret eder
  ve geçişte en sık yapılan hatadır.
- SQLite dosyası için açılan `/app/data` klasörü kaldırıldı; veri artık `pgdata`
  isimli volume'de.
- İzlenen klasör `:ro` (salt okunur) bağlanıyor — tarayıcı dosyaları yalnızca
  okuyup hash'liyor.

**Ek olarak çıkan küçük bir gürültü.** Uygulama açılışında log'a şu düşüyordu:

```
Cannot load library libgssapi_krb5.so.2
Error: libgssapi_krb5.so.2: cannot open shared object file
```

Npgsql açılışta Kerberos/GSSAPI desteğini yokluyor; `aspnet:10.0` slim imajında
o kitaplık yok. **Ölümcül değil** — bağlantı şifreyle kurulur ve uygulama
sorunsuz çalışır — ama her açılışta hata gibi görünen bir satır bırakıyordu.
Runtime aşamasına `libgssapi-krb5-2` eklenerek kaynağında giderildi.

---

### Doğrulama

Geçiş, gerçek bir kurulum üzerinde uçtan uca doğrulandı:

| Kontrol | Sonuç |
|---|---|
| Sağlık kontrolü kapısı | `db Waiting → Healthy → api Starting` ✔ |
| Migration açılışta uygulanıyor | ✔ |
| Tarama (salt okunur mount) | 4 dosya ✔ |
| `search?extension=.txt` | `BELGE.TXT` dahil 3 kayıt ✔ |
| `duplicates` | 2 dosya, farklı isim, aynı hash ✔ |
| Swagger | HTTP 200 ✔ |
| `down` + `up` sonrası veri | Id'ler korundu, satır çoğalmadı ✔ |
| Uygulama log'unda hata/uyarı | yok ✔ |
| İmaj boyutu | 411 MB → 365 MB |
| Birim testler (gerçek PostgreSQL) | 71/71 ✔ |

### Ders

**Bir bağımlılığı değiştirmek, onu kullanan kodun varsayımlarını da açığa
çıkarır.** Kod baştan beri saat dilimi olmayan tarihler yazıyordu (6.1) ve EF
Core sürümleri baştan beri çakışıyordu (6.4); ikisi de SQLite ortamı hoşgörülü
olduğu için görünmüyordu. Yeni sistem daha katı olduğu için hatayı o yaratmadı,
sadece görünür kıldı.

**İkinci ders — testler, test ettikleri şeyle aynı ortamda koşmalıdır.** 71 test
geçiyor olması, uygulama PostgreSQL'de koşarken testler SQLite'ta koştuğu sürece
bir şey ifade etmiyordu. Yeşil testler yanlış bir güven duygusu verebilir:
önemli olan kaç testin geçtiği değil, **neyi doğruladıkları**.

**Üçüncü ders — "başladı" ile "hazır" aynı şey değildir.** Dağıtık kurulumlarda
bir servisin ayakta olması iş görmeye hazır olduğu anlamına gelmez (6.5); sıra
garantisi yeterli değildir, hazır olma açıkça ölçülmelidir.

---

## Açık Konu: Silinen Dosyalar Veritabanında Kalıyor

**Durum: yöntem kararlaştırıldı, henüz uygulanmadı.**
**Seçilen yol: sayaçlı silme (hard delete).** Gerekçeler ve uygulama planı aşağıda.

### Belirti

Diskten silinen bir dosyanın `TrackedFiles` kaydı veritabanında sonsuza kadar duruyor. `FolderScannerService` yalnızca *ekleme* ve *güncelleme* yapıyor; hiçbir yerde silme yok. Sonuç olarak `GET /api/files` çıktısı, artık var olmayan dosyaları da listeliyor.

Sorun 4'ün doğrulaması sırasında somut olarak görüldü: test için oluşturulan üç dosya (`kopya-a.txt`, `kopya-b.txt`, `tekil-c.txt`) diskten silindiği halde kayıtları veritabanında kaldı.

### Neden Önemli

Yinelenen tespiti bu boşluktan doğrudan etkileniyor. Silinmiş bir dosyanın kaydı durduğu sürece `GET /api/files/duplicates`, artık var olmayan dosyalar için "şu kadar yer israf ediliyor" diyebilir — yani **yanlış bilgi** üretir.

### Tespit Nasıl Yapılabilir

Ek bir tarama ya da ek sorgu gerekmiyor. `ScanFolderAsync` zaten taramanın başında tüm kayıtları tek sorguda `existingFiles` sözlüğüne alıyor. Döngüde diskte karşılaşılan her kayıt işaretlenirse, döngü bittiğinde sözlükte işaretsiz kalanlar tam olarak "diskte artık yok" olan kayıtlardır.

### Düşünülen Yollar ve Neden Sayaçlı Silme Seçildi

| Yol | Nasıl | Değerlendirme |
|-----|-------|---------------|
| Kaydı sil (hard delete) | Satır veritabanından uçar | En basit, mevcut endpoint'ler etkilenmez. **Ama tek bir başarısız taramada geri dönüşü olmayan toplu silme riski var** |
| Silinmiş işaretle (soft delete) | `IsDeleted` + `DeletedAt` alanları | Tarihçe korunur, geri dönüşü var. **ELENDİ** — aşağıdaki maliyetleri bu ödev için orantısız bulundu |
| **Sayaçlı silme** ✔ | Üst üste N taramada görülmezse sil | **SEÇİLDİ.** Geçici erişilemezliğe dayanıklı, otomatik, tarihçe tutmuyor |

**Soft delete neden elendi.** İki gizli maliyeti var:

1. *Her sorguya filtre eklemek gerekir.* Birini unutmak sessiz hata üretir — en tehlikelisi `duplicates`, çünkü silinmiş dosyaları saymaya devam edip "şu kadar yer israf ediliyor" diye **yanlış bilgi** üretir. Çözümü EF Core'un global query filter'ı (`HasQueryFilter(f => !f.IsDeleted)`) ile tek satıra inebilirdi.
2. *Ama o zaman tarayıcı da silinmiş kayıtları göremez.* Silinen bir dosya geri konulursa tarayıcı onu sözlükte bulamaz, "yeni dosya" sanıp aynı `FilePath` ile ikinci satır açar — yani Sorun 3'te çözülen hata geri gelir. Tarayıcının `IgnoreQueryFilters()` kullanması ve kaydı *diriltmesi* (`IsDeleted = false`) gerekirdi.

Bu proje bir klasörü izliyor ve "silinmiş dosyanın tarihçesi" bir gereksinim değil; iki ek mekanizmayı taşımaya değmedi.

**Sayaçlı silmenin asıl kazancı.** "Bir taramada görünmedi → sil" ile "üst üste N taramada görünmedi → sil" arasındaki fark kritik:

```
Ağ sürücüsü 30 saniye kopar (3 tarama kaçar)

Tek taramada sil:   1. taramada bütün kayıtlar uçar. Geri dönüş yok.
N taramada sil:     sayaç 1→2→3 olur, sürücü geri gelir,
                    sayaç sıfırlanır, hiçbir şey silinmez. ✔
```

Aynı koruma tek dosya için de geçerli: bir dosya tarama anında başka bir program tarafından kilitliyse ya da yazılıyorsa, tek seferlik aksaklık kaydı uçurmaz. Mantık "acele etme, emin ol" — dosya gerçekten silinmişse kaydının ~100 saniye sonra düşmesi kimseyi rahatsız etmez, ama yanlışlıkla silinen kayıtlar geri gelmez.

### Uygulama Planı

**1. Model.** `TrackedFile`'a bir sayaç alanı eklenir (ör. `GorulmemeSayaci`, `int`, varsayılan 0). Migration gerekir.

Alan `[JsonIgnore]` ile işaretlenmeli: `GET /api/files` doğrudan `TrackedFile` döndürdüğü için, aksi halde API çıktısına yeni bir alan sızar. Silme mekanizmasının API yüzeyinde **hiç görünmemesi** bilinçli bir tercih.

**2. Ayar.** `appsettings.json` → `WatchSettings` altına eşik: `"SilmeIcinKacTarama": 10`. `ScanIntervalSeconds: 10` ile birlikte, dosya silindikten ~100 saniye sonra kaydı düşer. İkisi de ayardan değiştirilebilir.

**3. Tarama döngüsü.**

| Durum | Sayaç |
|---|---|
| Dosya diskte görüldü | `0`'a **sıfırlanır** |
| Kayıt var, dosya diskte yok | `+1` |
| Sayaç eşiğe ulaştı | Kayıt silinir (`RemoveRange`) |

**4. Tespit mekanizması — ek sorgu gerekmiyor.** `ScanFolderAsync` zaten taramanın başında tüm kayıtları tek sorguda `existingFiles` sözlüğüne alıyor. Döngüde diskte görülen her yol bir `HashSet`'e eklenirse, döngü bittiğinde sözlükte kalan işaretsiz kayıtlar tam olarak "diskte yok" olanlardır.

### Uygularken Dikkat Edilecek Dört Nokta

**1. "Görüldü" işareti `try` bloğunun DIŞINDA ve önünde konmalı.** Döngüdeki `try/catch`, hash hesaplanırken oluşan hataları yutuyor. İşaretleme `try` içinde olursa, geçici bir okuma hatası alan dosya "görülmedi" sayılır ve sayacı artmaya başlar — oysa dosya yerinde duruyordur. Dosyanın *var olduğu* ile *işlenebildiği* ayrı şeylerdir.

**2. Silme kapsamı taranan klasörle sınırlanmalı.** `existingFiles` veritabanındaki *tüm* kayıtları çekiyor, oysa tarama yalnızca `FolderPath`'i tarıyor. `FolderPath` değişirse — ki container'a geçerken tam olarak bu oldu, `C:\Users\...` → `/data/watch` — eski klasörün kayıtları sayacı doldurup haksız yere silinir. Sayaç bunu **çözmez, sadece geciktirir.**

İşi kolaylaştıran bir durum var: tarama alt klasörlere inmiyor (`GetFiles()`), dolayısıyla taranan klasördeki her dosyanın dizini `FolderPath`'e **tam eşittir**. Kontrol bu yüzden kesin olabilir:

```csharp
Path.GetDirectoryName(kayit.FilePath) == normalizeEdilmisFolderPath
```

Yol karşılaştırması işletim sistemine göre seçilmeli: Windows'ta büyük/küçük harf ayrımı yok, Linux'ta var. Uygulama Linux container'da, testler Windows'ta koşuyor. **Alt klasör taraması eklenirse bu kontrol de değişmeli.**

**3. Klasör erişilemezken silme çalışmamalı.** "Klasör boş" ile "klasör okunamıyor" birbirine benzer (ikisinde de sıfır dosya) ama anlamları zıttır: birincide kayıtlar gerçekten silinmeli, ikincide hiçbir şey yapılmamalı. Koruma şu an zaten var — `Directory.Exists` başarısızsa metot en başta `return 0` yapıyor, silme koduna hiç ulaşılmaz. **Eklenecek kod bu davranışı bozmamalı**; silme mantığı bu kontrolün üstüne taşınmamalı, kontrol "uyar ama devam et" haline getirilmemeli. Bir testle sabitlenmeli: *"klasör yokken tarama çalışır, hiçbir kayıt silinmez"*.

**4. Sayaç veritabanında tutulmalı, bellekte değil.** Bellekte tutmak migration'dan kurtarırdı, ama `FolderScannerService` scoped olarak kayıtlı (her taramada yeniden oluşuyor), dolayısıyla sayaç ayrı bir singleton'da yaşamak zorunda kalırdı. Daha önemlisi: uygulama her yeniden başladığında sayaçlar sıfırlanır. Container sık restart alırsa eşiğe hiç ulaşılmaz ve silme **hiçbir zaman gerçekleşmez.** Kolon daha dürüst.

### Kapsam Dışı Sayılan

**Taşınan dosyalar ayrıca ele alınmayacak** — çünkü gerek yok. Sık yapılan bir varsayımı düzeltmek gerekiyor: **dosya taşındığında hash DEĞİŞMEZ.** Hash içerikten hesaplanır, taşımak içeriği değiştirmez; değişen tek şey `FilePath`'tir.

Bu mekanizma taşımayı "silinmiş + yeni eklenmiş" olarak görür, ve sonuç doğrudur:

```
/data/watch/a.txt (hash H)  →  b.txt olarak yeniden adlandırıldı
Tarama: b.txt sözlükte yok   →  yeni satır açılır, hash yine H
        a.txt görülmedi      →  sayacı dolar, kaydı silinir
Sonuç:  tek satır, yeni yol, aynı hash — veritabanı gerçeği yansıtıyor ✔
```

Dosya klasörün *dışına* taşınırsa kaydı silinir; zaten tek bir klasör izleniyor, dışarısı kapsam dışı.

Tek kusuru raporlamada: yeniden adlandırma `newFileCount`'a 1 olarak yansır, yani `POST /api/files/scan` bunu "1 yeni dosya işlendi" diye bildirir. Taşımayı ayrı bir olay olarak tanımak (aynı hash'in kaybolan ve beliren iki yolda görülmesi) ayrı bir özelliktir, burada ele alınmayacak.


