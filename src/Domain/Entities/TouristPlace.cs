namespace TourAppBackend.src.Domain.Entities
{
    using TourAppBackend.src.Domain.Common;

    // TourAppDB'deki TouristPlaces tablosunu temsil eden entity
    public class TouristPlace : BaseEntity
    {
        // CityId kolonunu takip edeceğiz
        public int CityId { get; set; }

        // Turistik yerin adı
        public string Name { get; set; } = string.Empty;

        // Şehir adı (veritabanında varsa)
        public string City { get; set; } = string.Empty;

        // İlçe bilgisi
        public string District { get; set; } = string.Empty;

        // Altbölge bilgisi (opsiyonel)
        public string? Subdistrict { get; set; }

        // Fotoğraf linki (opsiyonel)
        public string? Photo { get; set; }

        // Added Coordinates property to represent the coordinate string from the database
        public string Coordinates { get; set; } = string.Empty;
    }
}
