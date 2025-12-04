using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TourAppBackend.src.Data.Context;
using TourAppBackend.src.Data.Repositories.Interfaces;
using TourAppBackend.src.Domain.Entities;

namespace TourAppBackend.src.Data.Repositories
{
    public class TouristPlaceRepository : ITouristPlaceRepository
    {
        private readonly TourAppDbContext _context;

        public TouristPlaceRepository(TourAppDbContext context)
        {
            _context = context;
        }

        // CityId ile filtreleyip ilgili kayitlari dondurur
        public async Task<IEnumerable<TouristPlace>> GetByCityIdAsync(int cityId)
        {
            return await _context.TouristPlaces
                .Where(tp => tp.CityId == cityId)
                .ToListAsync();
        }

        // Belirli ID'lere göre koordinatları getirir
        public async Task<IEnumerable<string>> GetCoordinatesByIdsAsync(int cityId, List<int> placeIds)
        {
            return await _context.TouristPlaces
                .Where(tp => tp.CityId == cityId && placeIds.Contains(tp.Id))
                .Select(tp => tp.Coordinates)
                .ToListAsync();
        }
    }
}
