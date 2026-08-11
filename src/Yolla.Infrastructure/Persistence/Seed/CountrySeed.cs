using Microsoft.EntityFrameworkCore;
using Yolla.Domain.Entities;

namespace Yolla.Infrastructure.Persistence.Seed;

// Ülkeler. Veri toplama Türkiye ile başlıyor; diğerleri harvester çalıştıkça IsActive olur.
public static class CountrySeed
{
    private static readonly DateTimeOffset SeedDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Apply(ModelBuilder builder)
    {
        builder.Entity<Country>().HasData(new Country
        {
            Id = 1,
            Iso2 = "TR",
            NameTr = "Türkiye",
            NameEn = "Türkiye",
            OsmRelationId = 174737,
            IsActive = true,
            CreatedAt = SeedDate
        });
    }
}
