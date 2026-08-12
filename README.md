# Yolla — Backend

Swipe kartlarıyla turistik yer keşfi ve en kısa gezi rotası çıkaran gezi rehberi uygulamasının sunucu tarafı.

İki çalışma modu var:

| Mod | Ne yapar |
|---|---|
| **Şehir içi** | Seçilen şehrin turistik yerlerini kart olarak sunar, beğenilenler için yürüme rotası çıkarır |
| **Şehirlerarası rota** | İki şehir arası yol koridorundaki yerleri yol sırasına göre sunar, duraklarla birlikte rotayı hesaplar |

Veri kapsamı Türkiye ile başlıyor; şema ve kod ilk günden çok ülkeli tasarlandı.

---

## API uçları

| Uç | İşlev |
|---|---|
| `POST /api/v1/devices/register` | Cihaz oturumu ve jeton (hesap gerekmez) |
| `GET /api/v1/geo/countries` · `/cities` | Ülke ve şehir listesi, arama |
| `GET /api/v1/discovery/city/{id}/feed` | Kart destesi |
| `POST /api/v1/discovery/swipes` | Kart kaydırma kaydı |
| `DELETE /api/v1/discovery/swipes/{placeId}` | Kaydırmayı geri al (yer desteye döner) |
| `GET /api/v1/discovery/swipes/liked` | Beğenilen yerler |
| `GET /api/v1/places/{id}` · `/by-slug/{sehir}/{yer}` | Yer detayı |
| `GET /api/v1/places/nearby` | Yakındaki yerler |
| `POST /api/v1/routes/optimize` | En kısa gezi rotası (gezgin satıcı) |
| `POST /api/v1/routes/corridor` | İki şehir arası yol üstü keşif |
| `GET/POST/PATCH/DELETE /api/v1/trips` | Gezi planları |
| `POST /api/v1/trips/{id}/optimize` | Planın rotasını hesapla ve kaydet |
| `GET/POST /api/v1/content/...` | İçerik girişi (yönetim anahtarı gerekli) |

Tüm uçlar `http://localhost:5088/scalar/v1` adresinde açıklama ve örneklerle listelenir.

## Mimari

```
Yolla.slnx
├── src/
│   ├── Yolla.Domain          Entity ve enum'lar. Hiçbir katmana bağımlı değil.
│   ├── Yolla.Application     İş kuralları: kategori eşleme, kalite skoru, metin normalizasyonu
│   ├── Yolla.Infrastructure  EF Core + PostGIS, Identity, Redis
│   ├── Yolla.Api             REST API, OpenAPI/Scalar, health check
│   └── Yolla.Harvester       OpenStreetMap veri toplama aracı
└── tests/
    ├── Yolla.Application.Tests      Birim testler
    └── Yolla.Api.IntegrationTests   Gerçek PostGIS ile uçtan uca testler
```

Bağımlılık yönü tek yönlü: `Api → Infrastructure → Application → Domain`.

### Neden PostGIS

Ürünün çekirdek sorusu coğrafi: *"Bu rotanın 15 km çevresinde hangi turistik yerler var?"*
Bu, `ST_DWithin(location, route_line, 15000)` ile GIST indeksi üzerinden milisaniyelerde cevaplanır.
Koordinatlar bu yüzden metin değil `geography(Point, 4326)` olarak tutulur.

### Neden kendi OSRM'imiz

`router.project-osrm.org` demo sunucusunun kullanım şartları üretim kullanımını yasaklıyor ve
hız sınırı uyguluyor. Rota motoru Docker'da kendi altyapımızda çalışır — hem şehir içi yürüme
(`foot`) hem şehirlerarası araç (`car`) profiliyle.

Şehir içi rota `/trip` servisiyle hesaplanır (gezgin satıcı problemi çözülür, yani duraklar
**en kısa sırayla** dizilir); `/route` kullanılmaz çünkü o noktaları verilen sırayla bağlar.

---

## Kurulum

### Gereksinimler

- .NET SDK 10
- Docker Desktop

### 1. Altyapıyı başlat

```bash
docker compose up -d postgres redis
```

Ayağa kalkanlar: PostgreSQL 17 + PostGIS 3.5 (`:5432`), Redis 7 (`:6379`).

### 2. Veritabanı şifresini tanımla

Şifre repoda tutulmaz:

```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=yolla;Username=yolla;Password=yolla_dev" --project src/Yolla.Api
```

### 3. Şemayı uygula

```bash
dotnet ef database update --project src/Yolla.Infrastructure --startup-project src/Yolla.Api
```

### 4. API'yi çalıştır

```bash
dotnet run --project src/Yolla.Api
```

- API dokümantasyonu: `http://localhost:5088/scalar/v1`
- Sağlık kontrolü: `http://localhost:5088/health`

> API'nin `Development` ortamında çalışması gerekir; aksi halde user-secrets yüklenmez.
> `launchSettings.json` bunu zaten ayarlar.

---

## Veri toplama

Turistik yer verisi OpenStreetMap'ten gelir. İki aşamalı:

### 1. Ham veriyi hazırla

```bash
./scripts/prepare-osm-data.ps1
```

Türkiye extract'ini (~500 MB) indirir ve osmium ile üç dosya üretir:

| Dosya | İçerik |
|---|---|
| `data/poi.geojsonl` | Turistik yer adayları |
| `data/provinces.geojsonl` | İl sınırları (`admin_level=4`) |
| `data/districts.geojsonl` | İlçe sınırları (`admin_level=6`) |

Aynı `.pbf` dosyası OSRM rota motoru tarafından da kullanılır, ikinci kez indirilmez.

### 2. Veriyi incele

Veritabanına yazmadan önce ne geldiğini gör:

```bash
dotnet run --project src/Yolla.Harvester -- inspect data/poi.geojsonl
```

Kaç kayıt kullanılabilir, hangi kategorilere dağılıyor, kaçı adsız olduğu için elenecek ve
kategori haritasında hangi OSM etiketleri eksik kalmış — hepsini raporlar.

### Ham OSM verisi neden doğrudan kullanılamaz

`tourism=*` etiketinin altında oteller, pansiyonlar ve turizm büroları da bulunur. Ayrıca bir
kale `historic=castle`, bir şelale `waterway=waterfall`, bir cami
`amenity=place_of_worship + religion=muslim` ile işaretlenir — tek tip bir "turistik yer"
etiketi yoktur.

[`OsmCategoryMapper`](src/Yolla.Application/Osm/OsmCategoryMapper.cs) bu dağınıklığı 40'a yakın
Yolla kategorisine normalize eder; konaklama ve hizmet noktaları gizli kategorilere düşer
(veride kalır, kullanıcıya gösterilmez).

[`PlaceQualityScorer`](src/Yolla.Application/Places/PlaceQualityScorer.cs) her yere 0-100
arası puan verir: Wikidata kaydı, fotoğrafı ve Wikipedia makalesi olanlar öne çıkar; "Merkez
Camii" gibi ayırt edici olmayan adlar cezalandırılır. Feed eşiğinin altında kalanlar veride
durur ama kart olarak gösterilmez.

---

## Testler

```bash
dotnet test
```

Birim testler bağımlılık gerektirmez. Entegrasyon testleri Testcontainers ile gerçek bir
PostGIS örneği başlatır, bu yüzden Docker'ın çalışıyor olması gerekir.

---

## Lisans ve atıf yükümlülüğü

- **Yer verisi:** OpenStreetMap, [ODbL](https://opendatacommons.org/licenses/odbl/) —
  her haritada "© OpenStreetMap contributors" gösterilmesi zorunlu.
- **Fotoğraflar:** Wikimedia Commons, çoğunlukla CC BY-SA — **fotoğrafçı adı ve lisans
  gösterilmeden kullanılamaz**. Bu yüzden `places` tablosunda `photo_author`,
  `photo_license` ve `photo_source` kolonları var.
