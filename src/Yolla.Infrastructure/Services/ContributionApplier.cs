using System.Globalization;
using Yolla.Application.Places;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;

namespace Yolla.Infrastructure.Services;

/// <summary>
/// Yayına alınan bir katkıyı yerin gösterilen alanlarına işler.
/// </summary>
/// <remarks>
/// Katkı satırı tek başına hiçbir yerde okunmuyor: kart destesi ve yer
/// detayı <see cref="Place"/> üzerindeki alanlara bakıyor. Katkı yazılıp bu
/// adım atlanırsa fotoğraf hiçbir yerde görünmüyor.
///
/// Üç yerden çağrılıyor — elle içerik girişi, kullanıcı gönderisinin onayı
/// ve içerik ekibinin doğrudan eklemesi. Ortak bir yerde durmasının sebebi
/// bu: üçünden birinde unutulduğunda katkı sessizce kayboluyor.
/// </remarks>
public static class ContributionApplier
{
    /// <summary>Kendi ürettiğimiz içeriğin atfı.</summary>
    public const string OwnLicense = "Yolla";

    public static void Apply(Place place, PlaceContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(contribution);

        switch (contribution.Type)
        {
            case ContributionType.Photo:
                place.PhotoUrl = contribution.Value;
                place.PhotoAuthor = contribution.Author ?? OwnLicense;
                place.PhotoLicense = contribution.License ?? OwnLicense;
                place.PhotoSource = contribution.SourceUrl;
                break;

            case ContributionType.Description:
                if (contribution.Language == "en")
                {
                    place.DescriptionEn = contribution.Value;
                }
                else
                {
                    place.DescriptionTr = contribution.Value;
                }

                break;

            case ContributionType.VisitDuration:
                if (short.TryParse(contribution.Value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var minutes))
                {
                    place.AvgVisitMinutes = minutes;
                }

                break;
        }

        Rescore(place);
    }

    /// <summary>
    /// Yayından kaldırılan katkının izini yerden siler.
    /// </summary>
    /// <remarks>
    /// Yalnızca hâlâ o katkıya ait olan değer temizleniyor: aynı alanı
    /// sonradan başka bir katkı doldurmuşsa onu silmek yanlış olur.
    /// </remarks>
    public static void Revert(Place place, PlaceContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(contribution);

        switch (contribution.Type)
        {
            case ContributionType.Photo when place.PhotoUrl == contribution.Value:
                place.PhotoUrl = null;
                place.PhotoAuthor = null;
                place.PhotoLicense = null;
                place.PhotoSource = null;
                break;

            case ContributionType.Description when contribution.Language == "en":
                place.DescriptionEn = null;
                break;

            case ContributionType.Description:
                place.DescriptionTr = null;
                break;

            case ContributionType.VisitDuration:
                place.AvgVisitMinutes = null;
                break;
        }

        Rescore(place);
    }

    /// <summary>
    /// Kalite puanını yeniden hesaplar.
    /// </summary>
    /// <remarks>
    /// Fotoğraf eklenince puan yükseliyor ve yer kart destesinin eşiğini
    /// geçebiliyor; katkının asıl karşılığı bu.
    /// </remarks>
    public static void Rescore(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);

        place.QualityScore = PlaceQualityScorer.Score(new PlaceQualityInput
        {
            Name = place.Name,
            HasWikidata = place.WikidataId is not null,
            HasPhoto = place.PhotoUrl is not null,
            HasWikipedia = place.WikipediaTitle is not null,
            HasDescription = place.DescriptionTr is not null,
            HasNameEn = place.NameEn is not null,
            HasWebsite = place.Website is not null,
            HasOpeningHours = place.OpeningHours is not null,
            CategoryWeight = place.Category?.Weight ?? 0
        });
    }
}
