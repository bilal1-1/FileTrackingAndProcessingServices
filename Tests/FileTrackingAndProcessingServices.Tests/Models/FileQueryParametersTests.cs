using FileTrackingAndProcessingServices.Models;

namespace FileTrackingAndProcessingServices.Tests.Models
{
    /// <summary>
    /// FileQueryParameters, istemciden gelen sayfalama değerlerini property
    /// setter'ları içinde sınırlıyor. Buradaki testler o sınırların gerçekten
    /// korunduğunu doğruluyor — servise geçersiz değer ulaşmamalı.
    /// </summary>
    public class FileQueryParametersTests
    {
        [Fact]
        public void Defaults_MatchExpectedValues()
        {
            var parameters = new FileQueryParameters();

            Assert.Equal(1, parameters.Page);
            Assert.Equal(10, parameters.PageSize);
            Assert.Equal("id", parameters.SortBy);
            Assert.Equal("asc", parameters.SortOrder);
        }

        [Theory]
        [InlineData(0)]      // sayfa numarası sıfır olamaz
        [InlineData(-1)]
        [InlineData(-999)]
        public void Page_BelowMinimum_ClampedToOne(int input)
        {
            var parameters = new FileQueryParameters { Page = input };

            Assert.Equal(1, parameters.Page);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(1000)]   // üst sınır yok, sadece alt sınır var
        public void Page_ValidValue_KeptAsIs(int input)
        {
            var parameters = new FileQueryParameters { Page = input };

            Assert.Equal(input, parameters.Page);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void PageSize_BelowMinimum_ClampedToOne(int input)
        {
            var parameters = new FileQueryParameters { PageSize = input };

            Assert.Equal(1, parameters.PageSize);
        }

        [Theory]
        [InlineData(101)]
        [InlineData(10_000)]   // istemci sunucuyu tek istekle zorlayamamalı
        public void PageSize_AboveMaximum_ClampedToHundred(int input)
        {
            var parameters = new FileQueryParameters { PageSize = input };

            Assert.Equal(100, parameters.PageSize);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(50)]
        [InlineData(100)]      // tam sınır değeri kırpılmamalı
        public void PageSize_WithinLimits_KeptAsIs(int input)
        {
            var parameters = new FileQueryParameters { PageSize = input };

            Assert.Equal(input, parameters.PageSize);
        }
    }
}
