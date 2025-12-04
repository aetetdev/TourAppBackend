using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using TourAppBackend.src.Business.Interfaces;
using TourAppBackend.src.Business.Services;

namespace TourAppBackend.src.Api.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddBusinessLayer(this IServiceCollection services)
        {
            // Business service'leri kaydet
            services.AddScoped<ITouristPlaceService, TouristPlaceService>();

            // AutoMapper profile kaydı
            services.AddAutoMapper(typeof(TourAppBackend.src.Business.Mappers.MappingProfile));

            return services;
        }
    }
}
