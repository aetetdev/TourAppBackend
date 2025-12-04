namespace TourAppBackend.src.Api.DTOs.Requests
{
    public class ShortestRouteRequest
    {
        public string StartCoordinates { get; set; } = string.Empty;
        public int CityId { get; set; }
        public List<int> PlaceIds { get; set; }
    }
}