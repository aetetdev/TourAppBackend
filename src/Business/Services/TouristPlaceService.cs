using AutoMapper;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Globalization;
using TourAppBackend.src.Api.DTOs.Requests;
using TourAppBackend.src.Api.DTOs.Responses;
using TourAppBackend.src.Api.DTOs.Responses.TouristPlace;
using TourAppBackend.src.Business.Interfaces;
using TourAppBackend.src.Data.Repositories.Interfaces;
using System.Text.Json.Serialization;

namespace TourAppBackend.src.Business.Services
{
    public class TouristPlaceService : ITouristPlaceService
    {
        private readonly ITouristPlaceRepository _repository;
        private readonly IMapper _mapper;

        public TouristPlaceService(ITouristPlaceRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<TouristPlaceListResponse>> GetByCityAsync(int cityId)
        {
            var entities = await _repository.GetByCityIdAsync(cityId);
            return _mapper.Map<IEnumerable<TouristPlaceListResponse>>(entities);
        }

        public async Task<ShortestRouteResponse> GetShortestRouteAsync(ShortestRouteRequest request)
        {
            var startParts = request.StartCoordinates.Split(',');

            double startLatitude = double.Parse(startParts[0],CultureInfo.InvariantCulture);

            double startLongitude = double.Parse(startParts[1],CultureInfo.InvariantCulture);

            var places = await _repository.GetCoordinatesByIdsAsync(request.CityId, request.PlaceIds);

            var parsedPlaces = places.Select(coordinate =>
            {
                var parts = coordinate.Split(',');

                return (
                    Latitude: double.Parse(parts[0], CultureInfo.InvariantCulture),
                    Longitude: double.Parse(parts[1], CultureInfo.InvariantCulture)
                );
            }).ToList();

            // OSRM formatı: longitude,latitude
            var coordinates = string.Join(";",
                parsedPlaces.Select(p =>
                    p.Longitude.ToString(CultureInfo.InvariantCulture) + "," +
                    p.Latitude.ToString(CultureInfo.InvariantCulture)
                )
            );

            var osrmUrl = $"http://router.project-osrm.org/route/v1/driving/" +
                          $"{startLongitude.ToString(CultureInfo.InvariantCulture)}," +
                          $"{startLatitude.ToString(CultureInfo.InvariantCulture)};" +
                          $"{coordinates}?overview=full";

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(osrmUrl);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var osrmResponse = JsonSerializer.Deserialize<OSRMResponse>(jsonResponse);

            return new ShortestRouteResponse
            {
                Distance = osrmResponse!.Routes.First().Distance,
                Duration = osrmResponse.Routes.First().Duration,
                Geometry = osrmResponse.Routes.First().Geometry
            };
        }
    }
}
