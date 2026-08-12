using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Yolla.Application.Common;
using Yolla.Application.Discovery;
using Yolla.Application.Routing;
using Yolla.Application.Trips;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="ITripService"/>
public sealed class TripService(YollaDbContext context, IRoutingClient routingClient) : ITripService
{
    /// <summary>Bir plana eklenebilecek en fazla durak sayısı.</summary>
    public const int MaxPlacesPerTrip = 40;

    /// <summary>Gezme süresi bilinmeyen yerler için varsayılan (dakika).</summary>
    private const int DefaultVisitMinutes = 45;

    private const int MaxNameLength = 200;

    public async Task<TripDetailDto> CreateAsync(
        int deviceId,
        CreateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureDeviceExistsAsync(deviceId, cancellationToken);

        City? city = null;

        if (request.CityId is { } cityId)
        {
            city = await context.Cities.FirstOrDefaultAsync(x => x.Id == cityId, cancellationToken)
                   ?? throw new NotFoundException("Şehir", cityId);
        }

        if (request.Mode == TripMode.City && city is null)
        {
            throw RequestValidationException.Single(
                nameof(request.CityId), "Şehir içi planda şehir seçilmeli.");
        }

        if (request.Mode == TripMode.Route && (request.StartPoint is null || request.EndPoint is null))
        {
            throw RequestValidationException.Single(
                nameof(request.StartPoint), "Rota planında başlangıç ve varış noktası gerekli.");
        }

        var trip = new Trip
        {
            DeviceId = deviceId,
            Mode = request.Mode,
            TravelMode = request.TravelMode,
            CityId = city?.Id,
            Name = BuildName(request.Name, city?.Name),
            StartPoint = ToPoint(request.StartPoint),
            EndPoint = ToPoint(request.EndPoint)
        };

        context.Trips.Add(trip);
        await context.SaveChangesAsync(cancellationToken);

        if (request.PlaceIds is { Count: > 0 })
        {
            await AddPlacesAsync(trip, request.PlaceIds, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(deviceId, trip.Id, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<TripSummaryDto>> GetAllAsync(
        int deviceId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.Trips
            .AsNoTracking()
            .Where(x => x.DeviceId == deviceId)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Mode,
                x.TravelMode,
                CityName = x.City != null ? x.City.Name : null,
                PlaceCount = x.Places.Count,
                x.TotalDistanceMeters,
                CoverPhotoUrl = x.Places
                    .OrderBy(p => p.OrderIndex)
                    .Select(p => p.Place.PhotoUrl)
                    .FirstOrDefault(),
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new TripSummaryDto
        {
            Id = x.Id,
            Name = x.Name ?? "Gezi planı",
            Mode = x.Mode,
            TravelMode = x.TravelMode,
            CityName = x.CityName,
            PlaceCount = x.PlaceCount,
            DistanceMeters = x.TotalDistanceMeters,
            CoverPhotoUrl = x.CoverPhotoUrl,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        }).ToList();
    }

    public async Task<TripDetailDto> GetAsync(
        int deviceId,
        int tripId,
        string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var trip = await LoadTripAsync(deviceId, tripId, tracking: false, cancellationToken);

        return BuildDetail(trip, IsEnglish(language));
    }

    public async Task<TripDetailDto> UpdateAsync(
        int deviceId,
        int tripId,
        UpdateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trip = await LoadTripAsync(deviceId, tripId, tracking: true, cancellationToken);

        if (request.Name is not null)
        {
            var name = request.Name.Trim();

            if (name.Length == 0 || name.Length > MaxNameLength)
            {
                throw RequestValidationException.Single(
                    nameof(request.Name), $"Plan adı 1-{MaxNameLength} karakter olmalı.");
            }

            trip.Name = name;
        }

        if (request.TravelMode is { } travelMode && travelMode != trip.TravelMode)
        {
            trip.TravelMode = travelMode;

            // Ulaşım tipi değişti; hesaplanmış rota artık geçerli değil
            ClearRoute(trip);
        }

        await context.SaveChangesAsync(cancellationToken);

        return await GetAsync(deviceId, tripId, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(int deviceId, int tripId, CancellationToken cancellationToken = default)
    {
        var trip = await LoadTripAsync(deviceId, tripId, tracking: true, cancellationToken);

        context.Trips.Remove(trip);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TripDetailDto> ModifyPlacesAsync(
        int deviceId,
        int tripId,
        ModifyTripPlacesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trip = await LoadTripAsync(deviceId, tripId, tracking: true, cancellationToken);

        var changed = false;

        if (request.Remove is { Count: > 0 })
        {
            var toRemove = trip.Places.Where(x => request.Remove.Contains(x.PlaceId)).ToList();

            foreach (var tripPlace in toRemove)
            {
                trip.Places.Remove(tripPlace);
                context.TripPlaces.Remove(tripPlace);
            }

            changed |= toRemove.Count > 0;
        }

        if (request.Add is { Count: > 0 })
        {
            changed |= await AddPlacesAsync(trip, request.Add, cancellationToken);
        }

        if (changed)
        {
            // Duraklar değişti; eski rota artık doğru değil
            ClearRoute(trip);
            Renumber(trip);

            await context.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(deviceId, tripId, cancellationToken: cancellationToken);
    }

    public async Task<TripDetailDto> OptimizeAsync(
        int deviceId,
        int tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await LoadTripAsync(deviceId, tripId, tracking: true, cancellationToken);

        if (trip.Places.Count == 0)
        {
            throw RequestValidationException.Single("places", "Planda hiç durak yok.");
        }

        var ordered = trip.Places.OrderBy(x => x.OrderIndex).ToList();

        var points = new List<GeoPoint>(ordered.Count + 1);
        var hasStartPoint = trip.StartPoint is not null;

        if (trip.StartPoint is { } start)
        {
            points.Add(new GeoPoint(start.Y, start.X));
        }

        points.AddRange(ordered.Select(x => new GeoPoint(x.Place.Location.Y, x.Place.Location.X)));

        if (points.Count < 2)
        {
            throw RequestValidationException.Single(
                "places",
                "Rota için en az iki nokta gerekli. Tek durak varsa plana başlangıç noktası ekleyin.");
        }

        var route = await routingClient.OptimizeTripAsync(
            points, trip.TravelMode, roundTrip: false, cancellationToken);

        ApplyOrder(ordered, route.WaypointOrder, hasStartPoint);

        trip.TotalDistanceMeters = route.DistanceMeters;
        trip.TotalDurationSeconds = route.DurationSeconds;
        trip.RouteGeometry = route.Geometry;

        await context.SaveChangesAsync(cancellationToken);

        return await GetAsync(deviceId, tripId, cancellationToken: cancellationToken);
    }

    private async Task<bool> AddPlacesAsync(
        Trip trip,
        IReadOnlyList<int> placeIds,
        CancellationToken cancellationToken)
    {
        var existing = trip.Places.Select(x => x.PlaceId).ToHashSet();

        var toAdd = placeIds.Distinct().Where(id => !existing.Contains(id)).ToList();

        if (toAdd.Count == 0)
        {
            return false;
        }

        if (trip.Places.Count + toAdd.Count > MaxPlacesPerTrip)
        {
            throw RequestValidationException.Single(
                "add", $"Bir plana en fazla {MaxPlacesPerTrip} durak eklenebilir.");
        }

        var known = await context.Places
            .Where(x => toAdd.Contains(x.Id) && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (known.Count == 0)
        {
            throw new NotFoundException("Yer", string.Join(", ", toAdd));
        }

        var order = trip.Places.Count == 0 ? 0 : trip.Places.Max(x => x.OrderIndex) + 1;

        foreach (var placeId in toAdd.Where(known.Contains))
        {
            var tripPlace = new TripPlace
            {
                TripId = trip.Id,
                PlaceId = placeId,
                OrderIndex = order++
            };

            trip.Places.Add(tripPlace);
            context.TripPlaces.Add(tripPlace);
        }

        return true;
    }

    /// <summary>Rota motorundan gelen sırayı duraklara yazar.</summary>
    private static void ApplyOrder(
        List<TripPlace> ordered,
        IReadOnlyList<int> waypointOrder,
        bool hasStartPoint)
    {
        if (waypointOrder.Count == 0)
        {
            return;
        }

        var order = 0;

        foreach (var index in waypointOrder)
        {
            // Başlangıç noktası bir durak değil
            var placeIndex = hasStartPoint ? index - 1 : index;

            if (placeIndex >= 0 && placeIndex < ordered.Count)
            {
                ordered[placeIndex].OrderIndex = order++;
            }
        }
    }

    private static void Renumber(Trip trip)
    {
        var order = 0;

        foreach (var tripPlace in trip.Places.OrderBy(x => x.OrderIndex))
        {
            tripPlace.OrderIndex = order++;
        }
    }

    private static void ClearRoute(Trip trip)
    {
        trip.TotalDistanceMeters = null;
        trip.TotalDurationSeconds = null;
        trip.RouteGeometry = null;
    }

    private async Task<Trip> LoadTripAsync(
        int deviceId,
        int tripId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = context.Trips
            .Include(x => x.City)
            .Include(x => x.Places)
                .ThenInclude(x => x.Place)
                    .ThenInclude(x => x.Category)
            .Include(x => x.Places)
                .ThenInclude(x => x.Place)
                    .ThenInclude(x => x.City)
            .Include(x => x.Places)
                .ThenInclude(x => x.Place)
                    .ThenInclude(x => x.District)
            .AsQueryable();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        // Sahiplik kontrolü sorgunun içinde: başka cihazın planı hiç yüklenmez,
        // dolayısıyla "var mı yok mu" bilgisi de sızmaz
        var trip = await query.FirstOrDefaultAsync(
            x => x.Id == tripId && x.DeviceId == deviceId, cancellationToken);

        return trip ?? throw new NotFoundException("Gezi planı", tripId);
    }

    private static TripDetailDto BuildDetail(Trip trip, bool isEnglish)
    {
        var places = trip.Places
            .OrderBy(x => x.OrderIndex)
            .Select((x, index) => new TripPlaceDto
            {
                Order = index + 1,
                DayIndex = x.DayIndex,
                IsVisited = x.IsVisited,
                Note = x.Note,
                Place = ToCard(x.Place, isEnglish)
            })
            .ToList();

        return new TripDetailDto
        {
            Id = trip.Id,
            Name = trip.Name ?? "Gezi planı",
            Mode = trip.Mode,
            TravelMode = trip.TravelMode,
            CityId = trip.CityId,
            CityName = trip.City?.Name,
            StartPoint = ToGeoPoint(trip.StartPoint),
            EndPoint = ToGeoPoint(trip.EndPoint),
            Places = places,
            DistanceMeters = trip.TotalDistanceMeters,
            DurationSeconds = trip.TotalDurationSeconds,
            VisitDurationMinutes = places.Sum(x => x.Place.AverageVisitMinutes ?? DefaultVisitMinutes),
            RouteGeometry = trip.RouteGeometry,
            CreatedAt = trip.CreatedAt
        };
    }

    private static PlaceCardDto ToCard(Place place, bool isEnglish) => new()
    {
        Id = place.Id,
        Name = place.Name,
        Slug = place.Slug,
        CategoryKey = place.Category.Key,
        CategoryName = isEnglish ? place.Category.NameEn : place.Category.NameTr,
        CategoryIcon = place.Category.Icon,
        PhotoUrl = place.PhotoUrl ?? string.Empty,
        PhotoAttribution = Attribution.ForPhoto(place.PhotoAuthor, place.PhotoLicense) ?? string.Empty,
        PhotoSource = place.PhotoSource,
        Description = isEnglish ? place.DescriptionEn ?? place.DescriptionTr : place.DescriptionTr,
        CityName = place.City.Name,
        DistrictName = place.District?.Name,
        Latitude = place.Location.Y,
        Longitude = place.Location.X,
        AverageVisitMinutes = place.AvgVisitMinutes,
        QualityScore = place.QualityScore
    };

    private async Task EnsureDeviceExistsAsync(int deviceId, CancellationToken cancellationToken)
    {
        if (!await context.Devices.AnyAsync(x => x.Id == deviceId, cancellationToken))
        {
            throw new NotFoundException("Cihaz", deviceId);
        }
    }

    private static string BuildName(string? requested, string? cityName)
    {
        var name = requested?.Trim();

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
        }

        return string.IsNullOrWhiteSpace(cityName) ? "Gezi planı" : $"{cityName} gezisi";
    }

    private static Point? ToPoint(GeoPoint? point) =>
        point is { } value ? new Point(value.Longitude, value.Latitude) { SRID = 4326 } : null;

    private static GeoPoint? ToGeoPoint(Point? point) =>
        point is null ? null : new GeoPoint(point.Y, point.X);

    private static bool IsEnglish(string language) =>
        language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
}
