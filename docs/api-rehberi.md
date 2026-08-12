# Yolla API — İstemci Rehberi

Mobil ve web istemcileri için pratik başvuru. Tüm uçların ayrıntılı açıklaması ve
deneme arayüzü için: `http://localhost:5088/scalar/v1`

---

## Temel bilgiler

**Adres:** `http://localhost:5088/api/v1` (geliştirme)

**Yanıt biçimi.** Başarılı yanıtlar ortak bir zarf içinde gelir:

```json
{
  "data": { },
  "attributions": ["© OpenStreetMap katkıcıları", "Fotoğraflar: Wikimedia Commons"]
}
```

`attributions` alanı **ekranda gösterilmek zorundadır** — harita ve fotoğraf verisinin
lisans şartı. Kart üzerindeki `photoAttribution` alanı da aynı şekilde zorunludur.

**Hatalar** RFC 7807 biçiminde döner, zarf kullanılmaz:

```json
{
  "status": 404,
  "title": "Kayıt bulunamadı",
  "detail": "Şehir bulunamadı: 999",
  "instance": "/api/v1/geo/cities/999",
  "traceId": "0HN7A2B3C4D5E"
}
```

Doğrulama hatalarında ek olarak `errors` alanı bulunur:

```json
{
  "status": 400,
  "title": "İstek doğrulanamadı",
  "errors": { "Platform": ["Platform şunlardan biri olmalı: ios, android, web."] }
}
```

**Enum'lar metin olarak taşınır:** `"Like"`, `"Pass"`, `"City"`, `"Foot"` — sayı değil.

**Hız sınırı:** dakikada 120 istek; rota uçları için ayrıca dakikada 20. Aşılırsa `429` döner.

**Web istemcisi (CORS).** Geliştirmede tüm yerel adreslere (localhost / 127.0.0.1, port
farkı gözetmeden) izin verilir; `flutter run -d chrome` rastgele port seçtiği için ek
ayar gerekmez. Üretimde izin verilen adres `Cors__AllowedOrigins__0` ortam değişkeniyle
verilir — tanımlı değilse tarayıcıdan **hiçbir istek geçmez** (bkz. `docs/deploy.md`).

---

## 1. Açılış: cihaz kaydı

İstemci ilk açılışta bir UUID üretir, **kalıcı olarak saklar** ve bu uca gönderir.
Kullanıcının hesap açmasına gerek yoktur.

```http
POST /api/v1/devices/register
Content-Type: application/json

{
  "deviceUuid": "8f14e45f-ea4a-4f6b-9d3c-2a1b7c9e5d20",
  "platform": "ios",
  "appVersion": "1.0.0",
  "language": "tr"
}
```

```json
{
  "data": {
    "deviceId": 42,
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "expiresAt": "2026-11-09T13:12:01+00:00",
    "userId": null
  }
}
```

Jeton **90 gün** geçerlidir. Sonraki tüm isteklerde gönderilir:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

> Aynı UUID ile tekrar çağırmak güvenlidir: yeni kayıt açılmaz, oturum tazelenir.
> Uygulama her açılışta çağırabilir.

---

## 2. Şehir seçimi

```http
GET /api/v1/geo/cities?country=TR&onlyWithContent=true
```

```json
{
  "data": [
    { "id": 180, "name": "İstanbul", "slug": "istanbul",
      "latitude": 41.01, "longitude": 28.97, "readyPlaceCount": 832 }
  ]
}
```

`readyPlaceCount` o şehirde kart olarak gösterilebilecek yer sayısıdır. **Sıfırsa o şehir
için deste boş gelir** — istemci bunu kullanıcıya bildirmeli veya şehri gizlemeli.
`onlyWithContent=true` bu şehirleri baştan eler.

Arama: `?search=nev` (Türkçe karakter farkı gözetilmez, "izmir" → "İzmir" bulur).

---

## 3. Kart destesi

```http
GET /api/v1/discovery/city/106/feed?take=20
Authorization: Bearer ...
```

```json
{
  "data": {
    "items": [
      {
        "id": 93,
        "name": "Ortahisar Kalesi",
        "slug": "ortahisar-kalesi",
        "categoryKey": "castle",
        "categoryName": "Kale",
        "categoryIcon": "castle",
        "photoUrl": "https://upload.wikimedia.org/wikipedia/commons/1/1c/Cappadocia.jpg",
        "photoThumbUrl": "https://upload.wikimedia.org/wikipedia/commons/thumb/1/1c/Cappadocia.jpg/500px-Cappadocia.jpg",
        "photoLargeUrl": "https://upload.wikimedia.org/wikipedia/commons/thumb/1/1c/Cappadocia.jpg/960px-Cappadocia.jpg",
        "photoAttribution": "Fotoğraf: Brocken Inaglory (CC BY-SA 3.0)",
        "photoSource": "https://commons.wikimedia.org/wiki/File:Cappadocia.jpg",
        "description": "Ürgüp'te kale",
        "cityName": "Nevşehir",
        "districtName": "Ürgüp",
        "latitude": 38.619809,
        "longitude": 34.8644308,
        "averageVisitMinutes": null,
        "qualityScore": 92,
        "routeProgress": null,
        "detourMeters": null
      }
    ],
    "nextCursor": "OTI6OTM=",
    "hasMore": true
  },
  "attributions": ["© OpenStreetMap katkıcıları", "Fotoğraflar: Wikimedia Commons"]
}
```

**Sayfalama imleçle yapılır**, sayfa numarasıyla değil:

```http
GET /api/v1/discovery/city/106/feed?take=20&cursor=OTI6OTM=
```

`hasMore` false olduğunda deste bitmiştir. Bozuk imleç gönderilirse hata dönmez, ilk
sayfa gelir.

**Filtreler:** `?categories=castle,museum,beach` · `?language=en`

> Jeton gönderildiğinde daha önce kaydırılmış yerler otomatik olarak elenir.

### Görsel adresleri

Her kart üç adres taşır. **`photoUrl` doğrudan gösterilmemeli** — Commons orijinalidir,
ortalama ~1 MB.

| Alan | Genişlik | Ölçüm (652 KB'lık örnek) | Nerede |
|---|---|---|---|
| `photoThumbUrl` | 500 px | 131 KB | Liste, önizleme, düşük yoğunluklu ekran |
| `photoLargeUrl` | 960 px | 385 KB | Tam ekran kart, yüksek yoğunluklu ekran |
| `photoUrl` | orijinal | 652 KB | Yalnızca indirme/paylaşma |

Küçültme sunucuda yapılıyor çünkü Wikimedia yalnızca **standart genişliklere** izin
veriyor (20, 40, 60, 120, 250, 330, 500, 960, 1280, 1920, 3840); listede olmayan bir
genişlik `400 Bad Request` döner. Liste değişirse tek yerden düzeltilir, istemciler
aynı mantığı ayrı ayrı taşımaz.

Adres Wikimedia deseniyle uyuşmuyorsa (elle girilmiş fotoğraf, SVG) küçültülmüş alanlar
orijinalin aynısını taşır; **hiçbir durumda boş gelmez.**

Aynı alanlar yer detayında (`photoThumbUrl`, `photoLargeUrl`), yakındakiler listesinde
(`photoThumbUrl`) ve plan listesinde (`coverPhotoThumbUrl`) da bulunur.

---

## 4. Kaydırma

```http
POST /api/v1/discovery/swipes
Authorization: Bearer ...

{
  "swipes": [
    { "placeId": 93, "direction": "Like",  "context": "City" },
    { "placeId": 94, "direction": "Pass",  "context": "City" },
    { "placeId": 95, "direction": "Later", "context": "City" }
  ]
}
```

```json
{ "data": { "recorded": 3, "totalLiked": 12 } }
```

- Yönler: `Like` (plana ekle), `Pass` (ilgilenmiyorum), `Later` (sonra bakarım)
- Tek istekte en fazla **200** kaydırma → çevrimdışı biriktirip toplu gönderilebilir
- Aynı yer tekrar gönderilirse yön güncellenir, yeni kayıt açılmaz

Beğenilenler: `GET /api/v1/discovery/swipes/liked`

### Geri alma

Kullanıcı yanlışlıkla kaydırdığında:

```http
DELETE /api/v1/discovery/swipes/93
Authorization: Bearer ...
```

```json
{ "data": { "removed": true, "totalLiked": 11 } }
```

Kayıt silinir; yer **kart destesine geri döner**, beğeniyse beğeni listesinden çıkar.

Kayıt bulunamazsa hata dönmez, `removed: false` ile başarılı yanıt verilir. Kaydırmalar
toplu gönderildiği için istemci, henüz gönderilmemiş bir kaydırma için de bu ucu
çağırabilir — gönderilip gönderilmediğini takip etmesine gerek yok.

> Yalnızca **yön değiştirmek** için bu uca gerek yok: aynı yeri farklı yönle tekrar
> göndermek kaydı günceller. Geri alma, yerin desteye dönmesi gerektiğinde kullanılır.

---

## 4b. Kullanıcı hesabı (isteğe bağlı)

Hesap **zorunlu değildir**; uygulama cihaz oturumuyla tam çalışır. Hesabın tek işlevi
verileri cihazlar arasında taşımak.

```http
POST /api/v1/account/register
{
  "email": "gezgin@example.com",
  "password": "en-az-8-karakter",
  "displayName": "Eren",
  "deviceUuid": "8f14e45f-ea4a-4f6b-9d3c-2a1b7c9e5d20"
}
```

**`deviceUuid` göndermek önemli:** o cihazda anonim olarak oluşturulmuş planlar ve
kaydırmalar hesaba bağlanır, kullanıcı hiçbir şey kaybetmez. Dönen jeton cihaz
jetonunun yerini alır ve aynı biçimde kullanılır.

| İşlem | Uç |
|---|---|
| Giriş | `POST /api/v1/account/login` (aynı şekilde `deviceUuid` alır) |
| Hesap bilgisi | `GET /api/v1/account/me` |
| Şifre değiştir | `POST /api/v1/account/change-password` |
| **Hesap sil** | `POST /api/v1/account/delete` → `{ "password": "..." }` |

> **Hesap silme ekranı zorunlu.** Uygulama mağazaları, hesap açtıran uygulamaların
> silme seçeneğini de sunmasını şart koşuyor. İşlem geri alınamaz: planlar,
> kaydırmalar ve cihaz kayıtları birlikte silinir.

Şifre kuralı: en az 8 karakter. Karmaşıklık zorunluluğu yok.

Kimlik doğrulama uçlarında hız sınırı daha dardır: **5 dakikada 10 istek**.

---

## 5. Yer detayı

```http
GET /api/v1/places/93
```

Kart alanlarına ek olarak: `nameEn`, `address`, `website`, `openingHours`,
`wikipediaUrl`, `directionsUrl`, `citySlug` ve `nearby` (yakındaki 6 yer).

Görsel alanları kartla aynıdır: başlık görseli için `photoLargeUrl`, `nearby`
satırları için `photoThumbUrl`. Kartın aksine burada hepsi null olabilir — detay
sayfası fotoğrafsız yerler için de açılır.

`directionsUrl` harita uygulamasında yol tarifi açar — kullanıcı güncel yorumlar ve
çalışma saatleri için oraya yönlendirilebilir.

Web sayfaları için kısa adla: `GET /api/v1/places/by-slug/nevsehir/uchisar-kalesi`

Yakındakiler: `GET /api/v1/places/nearby?latitude=41.0&longitude=28.9&radiusMeters=3000`

---

## 6. Gezi planları

Tüm uçlar jeton gerektirir; her cihaz yalnızca kendi planlarını görür.

```http
POST /api/v1/trips
{
  "name": "Kapadokya Hafta Sonu",
  "mode": "City",
  "travelMode": "Foot",
  "cityId": 106,
  "placeIds": [93, 104, 87]
}
```

Şehirlerarası planda `cityId` yerine:

```json
{
  "mode": "Route",
  "travelMode": "Car",
  "startPoint": { "latitude": 41.0082, "longitude": 28.9784 },
  "endPoint":   { "latitude": 36.8969, "longitude": 30.7133 }
}
```

| İşlem | Uç |
|---|---|
| Planlarım | `GET /api/v1/trips` |
| Plan detayı | `GET /api/v1/trips/{id}` |
| Adı değiştir | `PATCH /api/v1/trips/{id}` → `{ "name": "..." }` |
| Sil | `DELETE /api/v1/trips/{id}` |
| Durak ekle/çıkar | `POST /api/v1/trips/{id}/places` → `{ "add": [93], "remove": [87] }` |
| **Rotayı hesapla** | `POST /api/v1/trips/{id}/optimize` |

> Duraklar veya ulaşım tipi değişince hesaplanmış rota **temizlenir**; `distanceMeters`
> ve `routeGeometry` null olur. Yeniden `optimize` çağrılmalıdır.

`optimize` sonrası plan şunları taşır:

```json
{
  "distanceMeters": 4258,
  "durationSeconds": 3060,
  "visitDurationMinutes": 180,
  "routeGeometry": "kodlanmış polyline",
  "places": [ { "order": 1, "dayIndex": 0, "isVisited": false, "place": { } } ]
}
```

`routeGeometry` **kodlanmış polyline**'dır (precision 5). MapLibre'de çizmek için
`flutter_polyline_points` (Dart) veya `@mapbox/polyline` (JS) ile çözülür.

---

## 7. Rota uçları

### Şehir içi: seçilenleri en kısa sıraya diz

```http
POST /api/v1/routes/optimize
{
  "placeIds": [93, 104, 87, 112],
  "startPoint": { "latitude": 38.6431, "longitude": 34.8286 },
  "travelMode": "Foot",
  "roundTrip": false
}
```

Gezgin satıcı problemini çözer; `stops` listesi uğrama sırasındadır.

### Şehirlerarası: yol üstünde ne var

```http
POST /api/v1/routes/corridor
{
  "start": { "latitude": 41.0082, "longitude": 28.9784 },
  "end":   { "latitude": 36.8969, "longitude": 30.7133 },
  "bufferKm": 15,
  "excludeEndpointsKm": 20,
  "take": 20
}
```

```json
{
  "data": {
    "cards": { "items": [ ], "nextCursor": "...", "hasMore": true },
    "routeDistanceMeters": 675500,
    "routeDurationSeconds": 31680,
    "routeGeometry": "kodlanmış polyline"
  }
}
```

Koridor kartlarında iki ek alan dolu gelir:

- `routeProgress` — yolun neresinde (0-1). Kartlar bu sıraya göre gelir, yani kullanıcı
  önce yolun başındaki yerleri görür.
- `detourMeters` — ana yoldan sapma mesafesi. "5 km sapma" gibi gösterilebilir.

`excludeEndpointsKm` başlangıç ve varış çevresini eler; kullanıcı zaten bulunduğu şehri
biliyor. Varsayılan 20 km.

---

## Tipik akışlar

**Şehir içi gezi**
```
devices/register → geo/cities → discovery/city/{id}/feed
   → discovery/swipes (kaydır)
   → trips (plan oluştur, beğenilenlerle)
   → trips/{id}/optimize (rotayı hesapla)
   → haritada routeGeometry'yi çiz
```

**Şehirlerarası yolculuk**
```
devices/register → routes/corridor (yol üstü kartlar)
   → discovery/swipes (kaydır)
   → trips (Route modunda plan)
   → trips/{id}/optimize
```

---

## İstemci tarafında dikkat edilecekler

**Atıf zorunlu.** Her fotoğrafın altında `photoAttribution` gösterilmeli, haritada
`attributions` listesi bulunmalı. Bu hukuki yükümlülük, tasarım tercihi değil.

**Jeton saklama.** `accessToken` güvenli depoda tutulmalı (`flutter_secure_storage`,
web'de httpOnly cookie veya bellek). Süresi dolunca `devices/register` yeniden çağrılır.

**Çevrimdışı kaydırma.** Kaydırmalar yerelde biriktirilip bağlantı gelince toplu
gönderilebilir (200'lük gruplar).

**Boş şehir durumu.** `readyPlaceCount` sıfır olan şehirlerde deste boş gelir; kullanıcıya
"bu şehir için henüz içerik yok" denmeli.

**Rota süresi uyarısı.** `optimize` sonucu uzun çıkabilir (Kapadokya'da 4 durak yürüyerek
8 saat). İstemci mesafeye göre "araç önerilir" uyarısı gösterebilir.
