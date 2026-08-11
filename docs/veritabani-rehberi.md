# Veritabanı Rehberi

Yolla verisine bakmak, sorgulamak ve sorun aramak için pratik başvuru.

---

## Bağlanma

### İnteraktif konsol

```bash
docker exec -it yolla-postgres psql -U yolla -d yolla
```

Konsol içinde işine yarayacaklar:

| Komut | Ne yapar |
|---|---|
| `\dt` | Tabloları listeler |
| `\d places` | Bir tablonun kolonlarını ve indekslerini gösterir |
| `\x` | Geniş satırları alt alta gösterir (uzun kayıtlar için şart) |
| `\q` | Çıkar |

### Tek seferlik sorgu

```bash
docker exec yolla-postgres psql -U yolla -d yolla -c "SELECT id, name FROM cities ORDER BY name;"
```

### Görsel araç tercih edersen

Bağlantı bilgileri: `localhost:5432`, veritabanı `yolla`, kullanıcı `yolla`, şifre `yolla_dev`.
DBeaver, pgAdmin veya JetBrains DataGrip ile bu bilgilerle bağlanabilirsin.

---

## Kimlik bilmeden sorgulamak

Şehir ve kategori kimliklerini ezberlemeye gerek yok; `slug` ve `key` alanları okunabilir
ve sabittir:

```sql
-- Bir şehrin yerleri
SELECT * FROM places
WHERE city_id = (SELECT id FROM cities WHERE slug = 'nevsehir');

-- Bir kategorideki yerler
SELECT * FROM places
WHERE category_id = (SELECT id FROM categories WHERE key = 'castle');

-- İkisi birden
SELECT p.name, p.quality_score
FROM places p
JOIN cities c   ON c.id = p.city_id
JOIN categories k ON k.id = p.category_id
WHERE c.slug = 'antalya' AND k.key = 'beach'
ORDER BY p.quality_score DESC;
```

---

## Tablolar

| Tablo | İçerik |
|---|---|
| `countries` | Ülkeler. Şu an yalnızca Türkiye (`iso2 = 'TR'`). |
| `cities` | 81 il. `slug` ile aranır, `boundary` sınır poligonu. |
| `districts` | 1.001 ilçe. `city_id` ile ile bağlı. |
| `categories` | 45 kategori. `key` kod içinde kullanılan sabit ad, `is_visible=false` olanlar kullanıcıya gösterilmez. |
| `places` | 54.385 turistik yer. Çekirdek tablo. |
| `devices` | Kayıt olmadan kullanan cihazlar. |
| `trips`, `trip_places` | Kullanıcı gezi planları. |
| `swipes` | Kart kaydırma kayıtları. |

### `places` kolonlarından önemli olanlar

| Kolon | Anlamı |
|---|---|
| `location` | `geography(Point,4326)` — koordinat. Metin değil, coğrafi tip. |
| `quality_score` | 0-100. 25 ve üstü kart destesine girer. |
| `photo_url` / `photo_author` / `photo_license` | Fotoğraf ve **zorunlu atıf** bilgisi. Üçü birden dolu değilse yer kart olarak gösterilmez. |
| `osm_type` + `osm_id` | OSM kimliği. Bu ikili benzersiz; veri toplayıcının idempotent çalışmasını sağlar. |
| `commons_ref` | OSM'deki `wikimedia_commons` etiketi. |
| `is_active` | Yayından kaldırılan kayıtlar için. |

---

## Sık kullanılan sorgular

### Genel durum

```sql
SELECT
  count(*)                                             AS toplam,
  count(*) FILTER (WHERE photo_url IS NOT NULL)        AS fotografli,
  count(*) FILTER (WHERE description_tr IS NOT NULL)   AS aciklamali,
  count(*) FILTER (WHERE quality_score >= 25)          AS feed_esigi_ustu
FROM places;
```

### Şehir bazında içerik durumu

```sql
SELECT
  c.name AS il,
  count(p.id)                                          AS toplam,
  count(p.id) FILTER (WHERE p.photo_url IS NOT NULL)   AS fotolu,
  count(p.id) FILTER (WHERE p.photo_url IS NOT NULL
                        AND p.description_tr IS NOT NULL) AS hazir_kart
FROM cities c
LEFT JOIN places p ON p.city_id = c.id
GROUP BY c.name
ORDER BY hazir_kart DESC;
```

### Bir şehrin kart destesi (API'nin gördüğü sıra)

```sql
SELECT p.name, k.name_tr AS kategori, p.quality_score, p.photo_author, p.photo_license
FROM places p
JOIN cities c     ON c.id = p.city_id
JOIN categories k ON k.id = p.category_id
WHERE c.slug = 'nevsehir'
  AND p.is_active AND k.is_visible
  AND p.photo_url IS NOT NULL
  AND p.photo_author IS NOT NULL AND p.photo_license IS NOT NULL
  AND p.quality_score >= 25
ORDER BY p.quality_score DESC, p.id
LIMIT 20;
```

### Kategori dağılımı

```sql
SELECT k.key, k.name_tr, count(p.id) AS adet,
       count(p.id) FILTER (WHERE p.photo_url IS NOT NULL) AS fotolu
FROM categories k
LEFT JOIN places p ON p.category_id = k.id
GROUP BY k.key, k.name_tr
ORDER BY adet DESC;
```

### Tek bir yerin tüm bilgisi

```sql
\x
SELECT * FROM places WHERE name ILIKE '%ayasofya%';
\x
```

### Coğrafi sorgular

```sql
-- Bir noktanın 5 km çevresindeki yerler (Sultanahmet civarı)
SELECT name, ST_Distance(location, ST_MakePoint(28.9770, 41.0055)::geography)::int AS metre
FROM places
WHERE ST_DWithin(location, ST_MakePoint(28.9770, 41.0055)::geography, 5000)
  AND photo_url IS NOT NULL
ORDER BY metre
LIMIT 20;

-- Koordinatı okunabilir biçimde görmek
-- (ST_X/ST_Y geography üzerinde çalışmaz, önce geometry'ye çevrilir)
SELECT name, ST_Y(location::geometry) AS enlem, ST_X(location::geometry) AS boylam
FROM places
LIMIT 5;
```

### Eksik veri arama

```sql
-- Feed eşiğini geçtiği halde fotoğrafı olmayanlar: elle içerik girilecek adaylar
SELECT c.name AS il, p.name, p.quality_score, p.wikidata_id
FROM places p
JOIN cities c ON c.id = p.city_id
WHERE p.quality_score >= 25 AND p.photo_url IS NULL
ORDER BY p.quality_score DESC
LIMIT 50;

-- Fotoğrafı olup atıf bilgisi eksik olanlar (kart olarak gösterilemezler)
SELECT id, name, photo_license, photo_author
FROM places
WHERE photo_url IS NOT NULL
  AND (photo_author IS NULL OR photo_license IS NULL);
```

---

## API üzerinden bakmak

Sorgu yazmadan veriye bakmanın en hızlı yolu Scalar arayüzü:

```bash
dotnet run --project src/Yolla.Api
```

`http://localhost:5088/scalar/v1` adresinde tüm uçları deneyebilirsin. Şehir kimlikleri
`GET /api/v1/geo/cities` yanıtında geliyor, yani elle kimlik aramaya gerek kalmıyor.

---

## Dikkat

- `docker compose down -v` komutundaki `-v` **tüm veritabanını siler**. `-v` olmadan güvenlidir.
- Windows konsolunda Türkçe karakterler bozuk görünürse psql öncesi `chcp 65001` çalıştır.
- `places` tablosunda doğrudan `UPDATE` yaparsan, veri toplayıcı bir daha çalıştığında bazı
  alanların üzerine yazılabilir. Kalıcı düzeltmeler için içerik katkı yapısı kullanılmalı.
