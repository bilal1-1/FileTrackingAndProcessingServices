using FileTrackingAndProcessingServices.Models;
using FileTrackingAndProcessingServices.Services;
using FileTrackingAndProcessingServices.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FileTrackingAndProcessingServices.Tests.Services
{
    /// <summary>
    /// FolderScannerService'in tarama davranışı: yeni dosya algılama, tekrar
    /// kontrolü ve hash karşılaştırması. Gerçek bir geçici klasör ve bellek içi
    /// bir veritabanı kullanılıyor.
    /// </summary>
    public class FolderScannerServiceTests : IDisposable
    {
        private readonly VeritabaniOrtami _ortam;
        private readonly GeciciKlasor _klasor;

        public FolderScannerServiceTests()
        {
            _ortam = new VeritabaniOrtami();
            _klasor = new GeciciKlasor();
        }

        public void Dispose()
        {
            _ortam.Dispose();
            _klasor.Dispose();
        }

        private FolderScannerService Tarayici(string? klasorYolu = null)
        {
            var ayarlar = Options.Create(new FolderWatchSettings
            {
                FolderPath = klasorYolu ?? _klasor.Yol,
                ScanIntervalSeconds = 60
            });

            return new FolderScannerService(
                _ortam.Context,
                ayarlar,
                NullLogger<FolderScannerService>.Instance);
        }

        // ---------- Yeni dosya algılama ----------

        [Fact]
        public async Task Tarama_YeniDosya_KaydedilirVeAlanlariDoldurulur()
        {
            _klasor.DosyaYaz("rapor.txt", "merhaba dunya");

            int yeniSayisi = await Tarayici().ScanFolderAsync();

            Assert.Equal(1, yeniSayisi);

            var kayit = await _ortam.Context.TrackedFiles.SingleAsync();
            Assert.Equal("rapor.txt", kayit.FileName);
            Assert.Equal(".txt", kayit.Extension);
            Assert.Equal(Path.Combine(_klasor.Yol, "rapor.txt"), kayit.FilePath);
            Assert.Equal(13, kayit.SizeBytes);              // "merhaba dunya" 13 bayt
            Assert.Equal(64, kayit.Hash.Length);            // SHA-256 hex karşılığı
        }

        [Fact]
        public async Task Tarama_BosKlasor_SifirDoner()
        {
            int yeniSayisi = await Tarayici().ScanFolderAsync();

            Assert.Equal(0, yeniSayisi);
            Assert.Empty(_ortam.Context.TrackedFiles);
        }

        [Fact]
        public async Task Tarama_KlasorYoksa_SifirDonerVeCokmez()
        {
            var olmayanYol = Path.Combine(Path.GetTempPath(), "boyle-bir-klasor-yok-" + Guid.NewGuid());

            int yeniSayisi = await Tarayici(olmayanYol).ScanFolderAsync();

            Assert.Equal(0, yeniSayisi);
            Assert.Empty(_ortam.Context.TrackedFiles);
        }

        [Fact]
        public async Task Tarama_AltKlasordekiDosya_TaranmazSuAn()
        {
            // GetFiles() alt klasörlere inmiyor. Bu test mevcut davranışı
            // kayıt altına alıyor; alt klasör desteği eklenirse burası da
            // değişmeli.
            var altKlasor = _klasor.AltKlasorOlustur("arsiv");
            File.WriteAllText(Path.Combine(altKlasor, "gizli.txt"), "icerik");
            _klasor.DosyaYaz("gorunur.txt", "icerik");

            int yeniSayisi = await Tarayici().ScanFolderAsync();

            Assert.Equal(1, yeniSayisi);
            var kayit = await _ortam.Context.TrackedFiles.SingleAsync();
            Assert.Equal("gorunur.txt", kayit.FileName);
        }

        // ---------- Tekrar kontrolü ----------

        [Fact]
        public async Task Tarama_AyniDosyaIkinciKez_YeniKayitAcilmaz()
        {
            _klasor.DosyaYaz("rapor.txt", "icerik");

            int ilkTarama = await Tarayici().ScanFolderAsync();
            int ikinciTarama = await Tarayici().ScanFolderAsync();

            Assert.Equal(1, ilkTarama);
            Assert.Equal(0, ikinciTarama);      // tekrar işlenmemeli
            Assert.Equal(1, await _ortam.Context.TrackedFiles.CountAsync());
        }

        [Fact]
        public async Task Tarama_UcKezUstUste_KayitSayisiSabitKalir()
        {
            _klasor.DosyaYaz("a.txt", "aaa");
            _klasor.DosyaYaz("b.txt", "bbb");

            await Tarayici().ScanFolderAsync();
            await Tarayici().ScanFolderAsync();
            await Tarayici().ScanFolderAsync();

            Assert.Equal(2, await _ortam.Context.TrackedFiles.CountAsync());
        }

        // ---------- Hash davranışı ----------

        [Fact]
        public async Task Tarama_HesaplananHash_BilinenSha256DegeriyleAyni()
        {
            // Dışarıdan doğrulama: "hello" metninin SHA-256 karşılığı bilinen
            // sabit bir değer. Servisin ürettiği hash buna eşit değilse
            // implementasyon yanlıştır.
            _klasor.DosyaYaz("hello.txt", "hello");

            await Tarayici().ScanFolderAsync();

            var kayit = await _ortam.Context.TrackedFiles.SingleAsync();
            Assert.Equal(
                "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
                kayit.Hash);
        }

        [Fact]
        public async Task Tarama_AyniIcerikliFarkliDosyalar_AyniHashAlir()
        {
            _klasor.DosyaYaz("kopya-a.txt", "birebir ayni icerik");
            _klasor.DosyaYaz("kopya-b.txt", "birebir ayni icerik");

            await Tarayici().ScanFolderAsync();

            var kayitlar = await _ortam.Context.TrackedFiles.ToListAsync();
            Assert.Equal(2, kayitlar.Count);
            Assert.Equal(kayitlar[0].Hash, kayitlar[1].Hash);
        }

        [Fact]
        public async Task Tarama_FarkliIcerik_FarkliHashAlir()
        {
            _klasor.DosyaYaz("a.txt", "birinci icerik");
            _klasor.DosyaYaz("b.txt", "ikinci icerik");

            await Tarayici().ScanFolderAsync();

            var kayitlar = await _ortam.Context.TrackedFiles.ToListAsync();
            Assert.NotEqual(kayitlar[0].Hash, kayitlar[1].Hash);
        }

        [Fact]
        public async Task Tarama_IcerikDegisti_HashYenilenirYeniSatirAcilmaz()
        {
            var dosyaYolu = _klasor.DosyaYaz("rapor.txt", "eski icerik");
            await Tarayici().ScanFolderAsync();

            var eskiKayit = await _ortam.Context.TrackedFiles.SingleAsync();
            var eskiHash = eskiKayit.Hash;
            var eskiId = eskiKayit.Id;
            _ortam.Context.ChangeTracker.Clear();

            File.WriteAllText(dosyaYolu, "tamamen farkli yeni icerik");
            int yeniSayisi = await Tarayici().ScanFolderAsync();

            Assert.Equal(0, yeniSayisi);   // yeni dosya değil, güncelleme
            var guncelKayit = await _ortam.Context.TrackedFiles.SingleAsync();
            Assert.Equal(eskiId, guncelKayit.Id);            // aynı satır
            Assert.NotEqual(eskiHash, guncelKayit.Hash);     // hash yenilendi
            Assert.Equal(26, guncelKayit.SizeBytes);         // boyut da tazelendi
        }

        [Fact]
        public async Task Tarama_SadeceTarihDegisti_HashAyniKalir()
        {
            // Yedekten geri yükleme / dosyanın değiştirilmeden kaydedilmesi
            // senaryosu: tarih değişir, içerik aynı kalır. Hash yeniden
            // hesaplanır ama sonuç değişmemelidir.
            var dosyaYolu = _klasor.DosyaYaz("rapor.txt", "degismeyen icerik");
            await Tarayici().ScanFolderAsync();

            var eskiHash = (await _ortam.Context.TrackedFiles.SingleAsync()).Hash;
            _ortam.Context.ChangeTracker.Clear();

            var yeniTarih = File.GetLastWriteTime(dosyaYolu).AddHours(5);
            File.SetLastWriteTime(dosyaYolu, yeniTarih);

            await Tarayici().ScanFolderAsync();

            var guncelKayit = await _ortam.Context.TrackedFiles.SingleAsync();
            Assert.Equal(eskiHash, guncelKayit.Hash);        // içerik aynı → hash aynı
            Assert.Equal(yeniTarih, guncelKayit.ModifiedAt); // tarih tazelendi
        }

        [Fact]
        public async Task Tarama_HashiBosOlanEskiKayit_Doldurulur()
        {
            // Hash alanı eklenmeden önce oluşmuş kayıtlar geri doldurulmalı;
            // veritabanını sıfırlamaya gerek kalmamalı.
            var dosyaYolu = _klasor.DosyaYaz("eski.txt", "icerik");
            var dosyaBilgisi = new FileInfo(dosyaYolu);

            _ortam.Ekle(new TrackedFile
            {
                FileName = dosyaBilgisi.Name,
                FilePath = dosyaBilgisi.FullName,
                Extension = dosyaBilgisi.Extension,
                Hash = "",                                  // hash'siz eski kayıt
                SizeBytes = dosyaBilgisi.Length,
                CreatedAt = dosyaBilgisi.CreationTime,
                ModifiedAt = dosyaBilgisi.LastWriteTime
            });

            int yeniSayisi = await Tarayici().ScanFolderAsync();

            Assert.Equal(0, yeniSayisi);                    // yeni kayıt açılmadı
            var kayit = await _ortam.Context.TrackedFiles.SingleAsync();
            Assert.Equal(64, kayit.Hash.Length);            // hash dolduruldu
        }

        // ---------- Uçtan uca: tarama + yinelenen tespiti ----------

        [Fact]
        public async Task TaramaSonrasi_YinelenenTespiti_KopyalariBulur()
        {
            _klasor.DosyaYaz("kopya-a.txt", "ayni icerik");
            _klasor.DosyaYaz("kopya-b.txt", "ayni icerik");
            _klasor.DosyaYaz("tekil-c.txt", "farkli icerik");

            await Tarayici().ScanFolderAsync();

            var trackingService = new FileTrackingService(_ortam.Context);
            var gruplar = await trackingService.GetDuplicatesAsync();

            var grup = Assert.Single(gruplar);
            Assert.Equal(2, grup.Count);
            Assert.DoesNotContain(grup.Files, f => f.FileName == "tekil-c.txt");
        }
    }
}
