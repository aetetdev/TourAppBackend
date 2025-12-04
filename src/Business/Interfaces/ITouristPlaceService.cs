using System.Collections.Generic;
using System.Threading.Tasks;
using TourAppBackend.src.Api.DTOs.Requests;
using TourAppBackend.src.Api.DTOs.Responses;
using TourAppBackend.src.Api.DTOs.Responses.TouristPlace;

namespace TourAppBackend.src.Business.Interfaces
{
    public interface ITouristPlaceService
    {
        // Belirli bir şehir id'sine göre turistik yerlerin listesi
        Task<IEnumerable<TouristPlaceListResponse>> GetByCityAsync(int cityId);

        // En kısa rotayı hesaplamak için eklenen yöntem
        Task<ShortestRouteResponse> GetShortestRouteAsync(ShortestRouteRequest request);
    }
}
