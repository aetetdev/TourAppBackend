using System.Collections.Generic;
using System.Threading.Tasks;
using TourAppBackend.src.Domain.Entities;

namespace TourAppBackend.src.Data.Repositories.Interfaces
{
    public interface ITouristPlaceRepository
    {
        // Belirli bir CityId'ye ait turistik yerleri getirir
        Task<IEnumerable<TouristPlace>> GetByCityIdAsync(int cityId);

        // Belirli ID'lere göre koordinatları getirir
        Task<IEnumerable<string>> GetCoordinatesByIdsAsync(int cityId, List<int> placeIds);
    }
}
