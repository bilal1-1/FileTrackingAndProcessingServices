using FileTrackingAndProcessingServices.Models;
using FileTrackingAndProcessingServices.Services;
using FileTrackingAndProcessingServices.Tests.TestHelpers;

namespace FileTrackingAndProcessingServices.Tests.Services
{
    /// <summary>
    /// FileTrackingService'in sorgulama davranışı. Her test kendi veritabanıyla
    /// başlar (xUnit her test için sınıfı yeniden oluşturur), bu yüzden testler
    /// birbirinin verisini görmez.
    /// </summary>
    public class FileTrackingServiceTests : IDisposable
    {
        private readonly VeritabaniOrtami _ortam;
        private readonly FileTrackingService _service;

        public FileTrackingServiceTests()
        {
            _ortam = new VeritabaniOrtami();
            _service = new FileTrackingService(_ortam.Context);
        }

        public void Dispose() => _ortam.Dispose();

        private void YirmiBesKayitEkle()
        {
            var kayitlar = Enumerable.Range(1, 25)
                .Select(i => VeritabaniOrtami.Dosya($"dosya-{i:00}.txt", sizeBytes: i * 10))
                .ToArray();

            _ortam.Ekle(kayitlar);
        }

        // ---------- GetAllFilesAsync: sayfalama ----------

        [Fact]
        public async Task GetAllFiles_VarsayilanParametreler_IlkOnKaydiDoner()
        {
            YirmiBesKayitEkle();

            var sonuc = await _service.GetAllFilesAsync(new FileQueryParameters());

            Assert.Equal(10, sonuc.Items.Count);
            Assert.Equal(25, sonuc.TotalCount);
            Assert.Equal(1, sonuc.Page);
            Assert.Equal(3, sonuc.TotalPages);
            Assert.False(sonuc.HasPreviousPage);
            Assert.True(sonuc.HasNextPage);
        }

        [Fact]
        public async Task GetAllFiles_IkinciSayfa_SonrakiOnKaydiDoner()
        {
            YirmiBesKayitEkle();

            var sonuc = await _service.GetAllFilesAsync(
                new FileQueryParameters { Page = 2, PageSize = 10 });

            Assert.Equal(10, sonuc.Items.Count);
            Assert.Equal("dosya-11.txt", sonuc.Items.First().FileName);
            Assert.Equal("dosya-20.txt", sonuc.Items.Last().FileName);
            Assert.True(sonuc.HasPreviousPage);
        }

        [Fact]
        public async Task GetAllFiles_SonSayfa_KalanKayitlariDoner()
        {
            YirmiBesKayitEkle();

            var sonuc = await _service.GetAllFilesAsync(
                new FileQueryParameters { Page = 3, PageSize = 10 });

            Assert.Equal(5, sonuc.Items.Count);   // 25 kayıttan geriye kalan
            Assert.False(sonuc.HasNextPage);
        }

        [Fact]
        public async Task GetAllFiles_VarOlmayanSayfa_BosListeDonerAmaToplamDogru()
        {
            YirmiBesKayitEkle();

            var sonuc = await _service.GetAllFilesAsync(
                new FileQueryParameters { Page = 99, PageSize = 10 });

            Assert.Empty(sonuc.Items);
            Assert.Equal(25, sonuc.TotalCount);   // toplam bilgisi kaybolmamalı
        }

        [Fact]
        public async Task GetAllFiles_TumSayfalarBirlestirildiginde_HicbirKayitTekrarlamaz()
        {
            // Sıralamada eşitlik varsa aynı kayıt iki sayfada görünebilir.
            // Servis bunu ThenBy(Id) ile engelliyor; burada aynı FileName'e sahip
            // kayıtlarla o güvence sınanıyor.
            var kayitlar = Enumerable.Range(1, 15)
                .Select(_ => VeritabaniOrtami.Dosya("ayni-isim.txt"))
                .ToArray();
            _ortam.Ekle(kayitlar);

            var toplananIdler = new List<int>();
            for (int sayfa = 1; sayfa <= 3; sayfa++)
            {
                var sonuc = await _service.GetAllFilesAsync(new FileQueryParameters
                {
                    Page = sayfa,
                    PageSize = 5,
                    SortBy = "fileName"
                });
                toplananIdler.AddRange(sonuc.Items.Select(f => f.Id));
            }

            Assert.Equal(15, toplananIdler.Count);
            Assert.Equal(15, toplananIdler.Distinct().Count());
        }

        [Fact]
        public async Task GetAllFiles_KayitYoksa_BosSonucDoner()
        {
            var sonuc = await _service.GetAllFilesAsync(new FileQueryParameters());

            Assert.Empty(sonuc.Items);
            Assert.Equal(0, sonuc.TotalCount);
            Assert.Equal(0, sonuc.TotalPages);
        }

        // ---------- GetAllFilesAsync: sıralama ----------

        [Fact]
        public async Task GetAllFiles_IsmeGoreArtan_AlfabetikSiralar()
        {
            _ortam.Ekle(
                VeritabaniOrtami.Dosya("cccc.txt"),
                VeritabaniOrtami.Dosya("aaaa.txt"),
                VeritabaniOrtami.Dosya("bbbb.txt"));

            var sonuc = await _service.GetAllFilesAsync(
                new FileQueryParameters { SortBy = "fileName", SortOrder = "asc" });

            Assert.Equal(
                new[] { "aaaa.txt", "bbbb.txt", "cccc.txt" },
                sonuc.Items.Select(f => f.FileName));
        }

        [Fact]
        public async Task GetAllFiles_BoyutaGoreAzalan_BuyuktenKucugeSiralar()
        {
            _ortam.Ekle(
                VeritabaniOrtami.Dosya("kucuk.txt", sizeBytes: 10),
                VeritabaniOrtami.Dosya("buyuk.txt", sizeBytes: 5000),
                VeritabaniOrtami.Dosya("orta.txt", sizeBytes: 300));

            var sonuc = await _service.GetAllFilesAsync(
                new FileQueryParameters { SortBy = "sizeBytes", SortOrder = "desc" });

            Assert.Equal(
                new long[] { 5000, 300, 10 },
                sonuc.Items.Select(f => f.SizeBytes));
        }

        [Theory]
        [InlineData("FILENAME")]   // büyük harf de kabul edilmeli
        [InlineData("fileName")]
        [InlineData("filename")]
        public async Task GetAllFiles_SortByBuyukKucukHarfDuyarsiz(string sortBy)
        {
            _ortam.Ekle(
                VeritabaniOrtami.Dosya("bbbb.txt"),
                VeritabaniOrtami.Dosya("aaaa.txt"));

            var sonuc = await _service.GetAllFilesAsync(
                new FileQueryParameters { SortBy = sortBy });

            Assert.Equal("aaaa.txt", sonuc.Items.First().FileName);
        }

        [Theory]
        [InlineData("bilinmeyenAlan")]
        [InlineData("")]
        [InlineData("id")]
        public async Task GetAllFiles_TaninmayanSortBy_IdSiralamasinaDuser(string sortBy)
        {
            // Beyaz liste dışındaki değerler sorguya konmaz, Id'ye düşer.
            _ortam.Ekle(
                VeritabaniOrtami.Dosya("cccc.txt"),
                VeritabaniOrtami.Dosya("aaaa.txt"),
                VeritabaniOrtami.Dosya("bbbb.txt"));

            var sonuc = await _service.GetAllFilesAsync(
                new FileQueryParameters { SortBy = sortBy });

            // Ekleme sırası korunur (Id artan)
            Assert.Equal(
                new[] { "cccc.txt", "aaaa.txt", "bbbb.txt" },
                sonuc.Items.Select(f => f.FileName));
        }

        // ---------- GetByIdAsync ----------

        [Fact]
        public async Task GetById_KayitVarsa_DonerVeAlanlariDogrudur()
        {
            _ortam.Ekle(VeritabaniOrtami.Dosya("rapor.pdf", extension: ".pdf", sizeBytes: 2048));
            var eklenen = _ortam.Context.TrackedFiles.First();

            var sonuc = await _service.GetByIdAsync(eklenen.Id);

            Assert.NotNull(sonuc);
            Assert.Equal("rapor.pdf", sonuc!.FileName);
            Assert.Equal(".pdf", sonuc.Extension);
            Assert.Equal(2048, sonuc.SizeBytes);
        }

        [Fact]
        public async Task GetById_KayitYoksa_NullDoner()
        {
            _ortam.Ekle(VeritabaniOrtami.Dosya("var.txt"));

            var sonuc = await _service.GetByIdAsync(99999);

            Assert.Null(sonuc);
        }

        // ---------- SearchByExtensionAsync ----------

        [Fact]
        public async Task Search_EslesenUzanti_SadeceOnlariDoner()
        {
            _ortam.Ekle(
                VeritabaniOrtami.Dosya("a.txt", extension: ".txt"),
                VeritabaniOrtami.Dosya("b.pdf", extension: ".pdf"),
                VeritabaniOrtami.Dosya("c.txt", extension: ".txt"));

            var sonuc = await _service.SearchByExtensionAsync(".txt");

            Assert.Equal(2, sonuc.Count);
            Assert.All(sonuc, f => Assert.Equal(".txt", f.Extension));
        }

        [Fact]
        public async Task Search_EslesmeYoksa_BosListeDoner()
        {
            _ortam.Ekle(VeritabaniOrtami.Dosya("a.txt", extension: ".txt"));

            var sonuc = await _service.SearchByExtensionAsync(".docx");

            Assert.Empty(sonuc);   // null değil, boş liste
        }

        [Theory]
        [InlineData(".TXT", ".txt")]   // diskte büyük, aranan küçük
        [InlineData(".txt", ".TXT")]   // diskte küçük, aranan büyük
        [InlineData(".TxT", ".tXt")]   // ikisi de karışık
        public async Task Search_BuyukKucukHarfFarki_YineDeEslesir(
            string kayitliUzanti, string aranan)
        {
            // Tarayıcı uzantıyı diskteki haliyle kaydediyor: "BELGE.TXT" -> ".TXT".
            // Kullanıcının aramada aynı harf düzenini tutturmak zorunda olmaması
            // gerekir.
            _ortam.Ekle(VeritabaniOrtami.Dosya("belge", extension: kayitliUzanti));

            var sonuc = await _service.SearchByExtensionAsync(aranan);

            Assert.Single(sonuc);
        }

        [Theory]
        [InlineData(".TIF", ".tif")]
        [InlineData(".tif", ".TIF")]
        public async Task Search_IHarfiIcerenUzanti_MakineKulturundenEtkilenmez(
            string kayitliUzanti, string aranan)
        {
            // Türkçe kültürde ToLower() 'I' harfini 'ı'ya çevirir; bu da
            // veritabanındaki lower() sonucuyla ('i') uyuşmaz. Servis bu yüzden
            // aranan değeri ToLowerInvariant() ile küçültüyor. Bu test, makinenin
            // kültür ayarı ne olursa olsun eşleşmenin bozulmadığını güvenceye alır.
            _ortam.Ekle(VeritabaniOrtami.Dosya("resim", extension: kayitliUzanti));

            var sonuc = await _service.SearchByExtensionAsync(aranan);

            Assert.Single(sonuc);
        }

        [Fact]
        public async Task Search_HarfDuyarsizlik_YanlisUzantilariGetirmez()
        {
            // Duyarsızlık, alakasız uzantıların da gelmesi anlamına gelmemeli.
            _ortam.Ekle(
                VeritabaniOrtami.Dosya("a", extension: ".TXT"),
                VeritabaniOrtami.Dosya("b", extension: ".txt"),
                VeritabaniOrtami.Dosya("c", extension: ".pdf"),
                VeritabaniOrtami.Dosya("d", extension: ".txtx"));

            var sonuc = await _service.SearchByExtensionAsync(".txt");

            Assert.Equal(2, sonuc.Count);
            Assert.All(sonuc, f => Assert.Equal(".txt", f.Extension.ToLowerInvariant()));
        }

        // ---------- GetDuplicatesAsync ----------

        [Fact]
        public async Task GetDuplicates_AyniHashliIkiKayit_TekGrupOlarakDoner()
        {
            _ortam.Ekle(
                VeritabaniOrtami.Dosya("kopya-a.txt", hash: "aaa", sizeBytes: 24),
                VeritabaniOrtami.Dosya("kopya-b.txt", hash: "aaa", sizeBytes: 24));

            var sonuc = await _service.GetDuplicatesAsync();

            var grup = Assert.Single(sonuc);
            Assert.Equal("aaa", grup.Hash);
            Assert.Equal(2, grup.Count);
            Assert.Equal(24, grup.SizeBytes);
            Assert.Equal(24, grup.WastedBytes);   // tek kopya bırakılsa 24 bayt kazanılır
            Assert.Equal(2, grup.Files.Count);
        }

        [Fact]
        public async Task GetDuplicates_TekilKayit_GrubaGirmez()
        {
            _ortam.Ekle(
                VeritabaniOrtami.Dosya("kopya-a.txt", hash: "aaa"),
                VeritabaniOrtami.Dosya("kopya-b.txt", hash: "aaa"),
                VeritabaniOrtami.Dosya("tekil-c.txt", hash: "ccc"));

            var sonuc = await _service.GetDuplicatesAsync();

            var grup = Assert.Single(sonuc);
            Assert.DoesNotContain(grup.Files, f => f.FileName == "tekil-c.txt");
        }

        [Fact]
        public async Task GetDuplicates_UcKopya_IkiKopyalikIsrafHesaplar()
        {
            _ortam.Ekle(
                VeritabaniOrtami.Dosya("a.txt", hash: "aaa", sizeBytes: 500),
                VeritabaniOrtami.Dosya("b.txt", hash: "aaa", sizeBytes: 500),
                VeritabaniOrtami.Dosya("c.txt", hash: "aaa", sizeBytes: 500));

            var sonuc = await _service.GetDuplicatesAsync();

            var grup = Assert.Single(sonuc);
            Assert.Equal(3, grup.Count);
            Assert.Equal(1000, grup.WastedBytes);   // 500 * (3 - 1)
        }

        [Fact]
        public async Task GetDuplicates_HashiBosOlanlar_KopyaSayilmaz()
        {
            // Boş hash "henüz hesaplanmadı" demek; "içerikleri aynı" demek değil.
            _ortam.Ekle(
                VeritabaniOrtami.Dosya("a.txt", hash: ""),
                VeritabaniOrtami.Dosya("b.txt", hash: ""),
                VeritabaniOrtami.Dosya("c.txt", hash: ""));

            var sonuc = await _service.GetDuplicatesAsync();

            Assert.Empty(sonuc);
        }

        [Fact]
        public async Task GetDuplicates_BirdenFazlaGrup_EnCokIsrafEdenBastaGelir()
        {
            _ortam.Ekle(
                // küçük grup: 10 bayt israf
                VeritabaniOrtami.Dosya("kucuk-1.txt", hash: "kkk", sizeBytes: 10),
                VeritabaniOrtami.Dosya("kucuk-2.txt", hash: "kkk", sizeBytes: 10),
                // büyük grup: 9000 bayt israf
                VeritabaniOrtami.Dosya("buyuk-1.bin", hash: "bbb", sizeBytes: 9000),
                VeritabaniOrtami.Dosya("buyuk-2.bin", hash: "bbb", sizeBytes: 9000));

            var sonuc = await _service.GetDuplicatesAsync();

            Assert.Equal(2, sonuc.Count);
            Assert.Equal("bbb", sonuc[0].Hash);
            Assert.Equal(9000, sonuc[0].WastedBytes);
            Assert.Equal(10, sonuc[1].WastedBytes);
        }

        [Fact]
        public async Task GetDuplicates_HicKopyaYoksa_BosListeDoner()
        {
            _ortam.Ekle(
                VeritabaniOrtami.Dosya("a.txt", hash: "aaa"),
                VeritabaniOrtami.Dosya("b.txt", hash: "bbb"));

            var sonuc = await _service.GetDuplicatesAsync();

            Assert.Empty(sonuc);
        }

        [Fact]
        public async Task GetDuplicates_VeritabaniBossa_BosListeDoner()
        {
            var sonuc = await _service.GetDuplicatesAsync();

            Assert.Empty(sonuc);
        }
    }
}
