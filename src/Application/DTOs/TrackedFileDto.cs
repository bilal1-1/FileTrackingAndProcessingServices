namespace FileTrackingAndProcessingServices.Application.DTOs
{
    /// <summary>
    /// Bir dosyanın API cevabında görünen hali.
    ///
    /// Neden entity (TrackedFile) yerine bu var: veritabanı tablosunu doğrudan
    /// dışarı vermek, tablo şemasını API sözleşmesi haline getirir. Tabloya bir
    /// kolon eklendiği anda API cevabı da istemsizce değişir. Bu sınıf araya
    /// girerek ikisini birbirinden ayırıyor: tablo değişse de dışarıya verilen
    /// alanlar burada, tek yerde ve bilerek belirleniyor.
    ///
    /// Hash alanı bilerek dışarıda bırakıldı: 64 karakterlik parmak izi
    /// yinelenen tespiti için tutulan bir iç detay, dosya listesinde bir anlam
    /// taşımıyor. Grup seviyesinde anlamlı olduğu yerde
    /// <see cref="DuplicateGroupDto"/> içinde veriliyor.
    /// </summary>
    public class TrackedFileDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
    }
}
