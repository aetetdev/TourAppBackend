using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TourAppBackend.src.Business.Interfaces;
using TourAppBackend.src.Api.DTOs.Requests;

namespace TourAppBackend.src.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TouristPlacesController : ControllerBase
    {
        private readonly ITouristPlaceService _service;

        public TouristPlacesController(ITouristPlaceService service)
        {
            _service = service;
        }

        [HttpGet("city/{cityId:int}")]
        public async Task<IActionResult> GetByCity(int cityId)
        {
            var result = await _service.GetByCityAsync(cityId);
            return Ok(result);
        }

        [HttpPost("shortest-route")]
        public async Task<IActionResult> GetShortestRoute([FromBody] ShortestRouteRequest request)
        {
            var result = await _service.GetShortestRouteAsync(request);
            return Ok(result);
        }
    }
}
