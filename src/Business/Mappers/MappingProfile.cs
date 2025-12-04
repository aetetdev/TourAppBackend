using AutoMapper;
using TourAppBackend.src.Api.DTOs.Responses.TouristPlace;
using TourAppBackend.src.Domain.Entities;

namespace TourAppBackend.src.Business.Mappers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Entity -> DTO mapping
            CreateMap<TouristPlace, TouristPlaceListResponse>();
        }
    }
}
