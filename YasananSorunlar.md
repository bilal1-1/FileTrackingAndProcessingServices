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

## Açık Konu: Silinen Dosyalar Veritabanında Kalıyor

**Durum: çözüm aranacak. Henüz uygulanmadı.**

### Belirti

Diskten silinen bir dosyanın `TrackedFiles` kaydı veritabanında sonsuza kadar duruyor. `FolderScannerService` yalnızca *ekleme* ve *güncelleme* yapıyor; hiçbir yerde silme yok. Sonuç olarak `GET /api/files` çıktısı, artık var olmayan dosyaları da listeliyor.

Sorun 4'ün doğrulaması sırasında somut olarak görüldü: test için oluşturulan üç dosya (`kopya-a.txt`, `kopya-b.txt`, `tekil-c.txt`) diskten silindiği halde kayıtları veritabanında kaldı.

### Neden Önemli

Yinelenen tespiti bu boşluktan doğrudan etkileniyor. Silinmiş bir dosyanın kaydı durduğu sürece `GET /api/files/duplicates`, artık var olmayan dosyalar için "şu kadar yer israf ediliyor" diyebilir — yani **yanlış bilgi** üretir.

### Tespit Nasıl Yapılabilir

Ek bir tarama ya da ek sorgu gerekmiyor. `ScanFolderAsync` zaten taramanın başında tüm kayıtları tek sorguda `existingFiles` sözlüğüne alıyor. Döngüde diskte karşılaşılan her kayıt işaretlenirse, döngü bittiğinde sözlükte işaretsiz kalanlar tam olarak "diskte artık yok" olan kayıtlardır.

### Düşünülen Yollar

| Yol | Nasıl | Bedeli |
|-----|-------|--------|
| Kaydı sil (hard delete) | Satır veritabanından uçar | En basit, mevcut endpoint'ler etkilenmez. Bilgi kalıcı kaybolur |
| Silinmiş işaretle (soft delete) | `IsDeleted` + `DeletedAt` alanları | Tarihçe korunur, geri dönüşü var. Tüm sorgulara filtre eklemek gerekir |
| Sayaçlı silme | Üst üste N taramada görülmezse sil | Geçici erişilemezliğe dayanıklı. Ek alan ve karmaşıklık |

### Uygularken Dikkat Edilecek Üç Nokta

**1. Silme kapsamı taranan klasörle sınırlanmalı.** Tarama tek bir klasörü, alt klasörler hariç yapıyor (`GetFiles()`); ama `existingFiles` veritabanındaki *tüm* kayıtları çekiyor. `appsettings.json`'daki `FolderPath` bir gün değişirse, eski klasörün kayıtları "diskte yok" görünür ve haksız yere silinir.

**2. Klasör erişilemezken silme çalışmamalı.** Bu koruma şu an zaten var: `Directory.Exists` başarısızsa metot en başta `return 0` yapıyor, silme koduna hiç ulaşılmaz. Eklenecek kod bu davranışı bozmamalı.

**3. `duplicates` sorgusu silinmişleri saymamalı.** Soft delete seçilirse filtre şart; yoksa yukarıdaki yanlış bilgi üretilmeye devam eder.

### Kapsam Dışı Sayılan

Dosya *taşındığında* bu mekanizma onu "silinmiş + yeni eklenmiş" olarak görür. Hash aynı kaldığı için aslında taşındığı çıkarılabilir, ama bu ayrı bir özellik — burada ele alınmayacak.


