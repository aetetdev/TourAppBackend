using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TourAppBackend.src.Data.Context;
using TourAppBackend.src.Data.Repositories;
using TourAppBackend.src.Data.Repositories.Interfaces;

namespace TourAppBackend.src.Data
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDataLayer(this IServiceCollection services, string connectionString)
        {
            // DbContext kaydý — connection string API'den gelir
            services.AddDbContext<TourAppDbContext>(options =>
                options.UseSqlServer(connectionString));

            // Repository kayýtlarý (örnek)
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<ITouristPlaceRepository, TouristPlaceRepository>();

            return services;
        }
    }
}