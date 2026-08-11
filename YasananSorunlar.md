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
