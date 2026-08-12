# Yayına Alma

Yolla API'sini sunucuda çalıştırmak için gereken her şey.

---

## Ortam değişkenleri

Üretimde **zorunlu** olanlar. Uygulama bunlar olmadan açılmaz — bilinçli bir tercih;
eksik yapılandırmayla sessizce çalışan bir sistem, sonradan güvenlik açığına dönüşür.

| Değişken | Açıklama |
|---|---|
| `ConnectionStrings__Postgres` | Veritabanı bağlantısı. Geliştirme şifresi (`yolla_dev`) üretimde reddedilir. |
| `Jwt__Key` | Jeton imza anahtarı, en az 32 karakter. Sızarsa herkes istediği kullanıcı adına jeton üretebilir. |

Ek olarak:

| Değişken | Varsayılan | Açıklama |
|---|---|---|
| `ConnectionStrings__Redis` | — | Verilmezse önbellek devre dışı kalır |
| `Osrm__CarBaseUrl` | `http://localhost:5000` | Araç rota motoru |
| `Osrm__FootBaseUrl` | `http://localhost:5001` | Yürüme rota motoru |
| `Admin__ApiKey` | — | Verilmezse içerik yönetimi uçları tamamen kapalı |
| `Cors__AllowedOrigins__0` | — | Web istemcisinin adresi, örn. `https://yolla.travel`. **Verilmezse tarayıcıdan hiçbir istek geçmez**; başlangıçta uyarı loglanır. Birden fazla adres için `__1`, `__2`. Compose kullanılıyorsa `.env` içindeki `WEB_ORIGIN` |
| `ASPNETCORE_ENVIRONMENT` | `Production` | |

Anahtar üretmek için:

```bash
openssl rand -base64 48
```

---

## Görüntüyü derleme

Depo kökünden:

```bash
docker build -f src/Yolla.Api/Dockerfile -t yolla-api:latest .
```

Sonuç ~100 MB. Görüntü kök olmayan kullanıcıyla çalışır ve `/health` üzerinden kendi
sağlığını bildirir.

---

## Tek sunucuda çalıştırma

En basit kurulum: her şey aynı makinede, Docker Compose ile.

```bash
# .env dosyası (depoya girmez)
cat > .env <<'EOF'
POSTGRES_PASSWORD=uzun-ve-rastgele-bir-sifre
JWT_KEY=en-az-32-karakterlik-rastgele-anahtar
ADMIN_API_KEY=icerik-yonetimi-anahtari
WEB_ORIGIN=https://yolla.travel
EOF

docker compose --profile app up -d
```

`api` servisi `app` profilinde olduğu için geliştirmede kendiliğinden başlamaz.

### Sunucu boyutu

| Bileşen | Bellek |
|---|---|
| API | ~200 MB |
| PostgreSQL + PostGIS | ~1 GB |
| Redis | ~100 MB |
| OSRM (araç) | ~2 GB |
| OSRM (yürüme) | ~2,5 GB |

Rota motorlarıyla birlikte **8 GB bellek** rahat eder, 4 GB sıkışır. Disk: veritabanı
~2 GB, OSRM grafikleri ~9 GB.

> Bütçe sıkışıksa OSRM'i ayrı ve daha ucuz bir makineye almak yerine, rota motorlarını
> tek profille (yalnızca araç) çalıştırmak da mümkün; şehir içi yürüme rotaları o zaman
> araç profiliyle hesaplanır ve sonuç yanlış olur. Önerilmez.

---

## Veritabanı hazırlığı

Şema geçişleri uygulamayla birlikte **otomatik uygulanmaz** — bilinçli tercih: bir
şema değişikliğinin ne zaman uygulanacağı kontrol edilebilir olmalı.

```bash
dotnet ef database update \
  --project src/Yolla.Infrastructure \
  --startup-project src/Yolla.Api \
  --connection "Host=...;Database=yolla;Username=yolla;Password=..."
```

Veri (54 bin yer) ayrıca aktarılmalı. İki yol:

**Yerelden aktarım** (hızlı):
```bash
docker exec yolla-postgres pg_dump -U yolla -d yolla -Fc > yolla.dump
# sunucuda
pg_restore -U yolla -d yolla --no-owner yolla.dump
```

**Sıfırdan toplama** (sunucuda, uzun sürer):
```bash
./scripts/prepare-osm-data.ps1
dotnet run --project src/Yolla.Harvester -- import-boundaries
dotnet run --project src/Yolla.Harvester -- import-places
dotnet run --project src/Yolla.Harvester -- enrich
```

---

## Rota motoru

OSRM grafikleri sunucuda hazırlanmalı (yerelde üretilenler taşınabilir de, ~9 GB):

```bash
./scripts/prepare-osrm.ps1
docker compose up -d osrm-car osrm-foot
```

Hazırlık **en az 9 GB kullanılabilir bellek** ister. Yetersizse işlem hata vermeden
öldürülür; script bunu baştan kontrol edip uyarır.

---

## Sürekli tümleştirme

`.github/workflows/ci.yml` her push ve pull request'te:

1. Derleme
2. Birim testler (324)
3. Entegrasyon testleri (115) — Testcontainers ile gerçek PostGIS
4. Docker görüntüsünün derlenmesi

Kayıt defterine gönderim ve otomatik dağıtım, sunucu belirlendikten sonra eklenecek.

---

## Yayına almadan önce kontrol listesi

- [ ] `Jwt__Key` ve `POSTGRES_PASSWORD` rastgele üretildi, depoya girmiyor
- [ ] `Cors__AllowedOrigins` gerçek web adresiyle verildi — verilmezse web istemcisi çalışmaz
- [ ] HTTPS sonlandırma yapılandırıldı (ters vekil sunucu ya da yük dengeleyici)
- [ ] Veritabanı yedeği zamanlandı
- [ ] `/health` izleniyor
- [ ] KVKK aydınlatma metni ve gizlilik politikası yayında
- [ ] Hesap silme akışı istemcide görünür (uygulama mağazası şartı)

---

## Bilinen eksikler

Bunlar henüz yok; yayına çıkmadan tamamlanmalı:

- **Merkezi log ve hata izleme.** Loglar şu an yalnızca konteyner çıktısına gidiyor.
- **Sağlık kontrolü dar.** Yalnızca veritabanını görüyor; Redis veya OSRM çökse
  `/health` hâlâ "sağlıklı" der.
- **Hız sınırı bellekte.** Birden fazla API örneği çalıştırılırsa sınır örnek başına
  uygulanır, yani toplam sınır katlanır. Redis'e taşınmalı.
- **Yedekleme ve geri yükleme denenmedi.**
