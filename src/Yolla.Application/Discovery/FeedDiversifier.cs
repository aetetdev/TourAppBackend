namespace Yolla.Application.Discovery;

/// <summary>
/// Kart sırasını çeşitlendirir.
/// </summary>
/// <remarks>
/// Kalite puanına göre sıralanmış liste ham haliyle kullanılamaz: Türkiye verisinde
/// camiler ve manzara noktaları sayıca baskın olduğu için deste "cami, cami, cami..."
/// diye gidiyor ve kullanıcı ilgisini kaybediyor. Bu sınıf sırayı bozmadan araya farklı
/// kategoriler serpiştirir - puan sıralaması korunur, yalnızca eşit değerdeki kartlar
/// öne alınır.
/// </remarks>
public static class FeedDiversifier
{
    /// <summary>Aynı kategoriden art arda gösterilebilecek en fazla kart sayısı.</summary>
    public const int MaxConsecutiveSameCategory = 2;

    public static IReadOnlyList<PlaceCardDto> Diversify(IReadOnlyList<PlaceCardDto> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        if (cards.Count <= MaxConsecutiveSameCategory)
        {
            return cards;
        }

        var remaining = new LinkedList<PlaceCardDto>(cards);
        var result = new List<PlaceCardDto>(cards.Count);

        string? lastCategory = null;
        var consecutive = 0;

        while (remaining.First is not null)
        {
            var node = SelectNext(remaining, lastCategory, consecutive);
            var card = node.Value;

            remaining.Remove(node);
            result.Add(card);

            if (card.CategoryKey == lastCategory)
            {
                consecutive++;
            }
            else
            {
                lastCategory = card.CategoryKey;
                consecutive = 1;
            }
        }

        return result;
    }

    private static LinkedListNode<PlaceCardDto> SelectNext(
        LinkedList<PlaceCardDto> remaining,
        string? lastCategory,
        int consecutive)
    {
        var first = remaining.First!;

        // Sınır dolmadıysa sıradaki kart olduğu gibi alınır
        if (lastCategory is null || consecutive < MaxConsecutiveSameCategory)
        {
            return first;
        }

        // Sınır doldu: listede farklı kategoriden ilk kartı öne al
        for (var node = first; node is not null; node = node.Next)
        {
            if (node.Value.CategoryKey != lastCategory)
            {
                return node;
            }
        }

        // Hepsi aynı kategoriden: yapılacak bir şey yok
        return first;
    }
}
