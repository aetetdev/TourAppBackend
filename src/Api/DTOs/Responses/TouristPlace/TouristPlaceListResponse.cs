namespace TourAppBackend.src.Api.DTOs.Responses.TouristPlace
{
    public class TouristPlaceListResponse
    {
        public int Id { get; set; }
        public int CityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;    
        public string? Subdistrict { get; set; }
        public string? Photo { get; set; }
    }
}
