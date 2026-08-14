using FileTrackingAndProcessingServices.Models;

namespace FileTrackingAndProcessingServices.Tests.Models
{
    /// <summary>
    /// PagedResult'ın TotalPages, HasPreviousPage ve HasNextPage değerleri
    /// hesaplanmış property'ler. İstemci "kaç sayfa var, ileri gidebilir miyim"
    /// sorusunu bunlara bakarak cevaplıyor; yanlış hesap istemciyi yanıltır.
    /// </summary>
    public class PagedResultTests
    {
        private static PagedResult<TrackedFile> Olustur(int page, int pageSize, int totalCount)
        {
            return new PagedResult<TrackedFile>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        [Theory]
        [InlineData(10, 25, 3)]   // 25 kayıt / 10'luk sayfa = 3 sayfa (son sayfa yarım)
        [InlineData(10, 20, 2)]   // tam bölünüyor, artık sayfa yok
        [InlineData(10, 1, 1)]    // tek kayıt da bir sayfa
        [InlineData(10, 0, 0)]    // hiç kayıt yoksa sayfa da yok
        [InlineData(100, 250, 3)]
        public void TotalPages_YukariYuvarlanarakHesaplanir(
            int pageSize, int totalCount, int beklenenSayfaSayisi)
        {
            var result = Olustur(page: 1, pageSize: pageSize, totalCount: totalCount);

            Assert.Equal(beklenenSayfaSayisi, result.TotalPages);
        }

        [Fact]
        public void HasPreviousPage_IlkSayfada_False()
        {
            var result = Olustur(page: 1, pageSize: 10, totalCount: 50);

            Assert.False(result.HasPreviousPage);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(5)]
        public void HasPreviousPage_IlkSayfaDisinda_True(int page)
        {
            var result = Olustur(page: page, pageSize: 10, totalCount: 50);

            Assert.True(result.HasPreviousPage);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void HasNextPage_SonSayfadanOncekilerde_True(int page)
        {
            // 25 kayıt, 10'luk sayfa => 3 sayfa
            var result = Olustur(page: page, pageSize: 10, totalCount: 25);

            Assert.True(result.HasNextPage);
        }

        [Fact]
        public void HasNextPage_SonSayfada_False()
        {
            var result = Olustur(page: 3, pageSize: 10, totalCount: 25);

            Assert.False(result.HasNextPage);
        }

        [Fact]
        public void HasNextPage_SonSayfaninOtesindeIstenirse_False()
        {
            // İstemci var olmayan bir sayfayı isterse "daha var" denmemeli.
            var result = Olustur(page: 99, pageSize: 10, totalCount: 25);

            Assert.False(result.HasNextPage);
        }

        [Fact]
        public void HicKayitYoksa_IleriGeriYok()
        {
            var result = Olustur(page: 1, pageSize: 10, totalCount: 0);

            Assert.Equal(0, result.TotalPages);
            Assert.False(result.HasPreviousPage);
            Assert.False(result.HasNextPage);
            Assert.Empty(result.Items);
        }
    }
}
