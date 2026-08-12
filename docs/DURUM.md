# Yolla — Proje Durumu

Son güncelleme: 2026-08-12

Bu belge backend'in nerede olduğunu ve sırada ne olduğunu tutar. Frontend tarafı buna
bakarak neyin hazır olduğunu görebilir.

---

## Özet

| | |
|---|---|
| Veri | 54.385 turistik yer, 81 il, 1.001 ilçe |
| Kart olarak gösterilebilir | 2.898 yer (fotoğraf + açıklama + atıf tam) |
| Fotoğraflı kayıt | 3.149 |
| Çalışan API ucu | 30 |
| Test | 324 birim + 115 entegrasyon, tamamı geçiyor |
| Rota motoru | OSRM, araç ve yürüme profilleri hazır (Türkiye) |

**Mobil uygulama yazmaya bugün başlanabilir.** Kart destesi, kaydırma, yer detayı,
gezi planı ve her iki rota modu uçtan uca çalışıyor.

---

## Biten işler

### Veri katmanı
- OpenStreetMap Türkiye verisi işlendi (osmium tabanlı, tekrar çalıştırılabilir)
- Ham OSM etiketleri 45 Yolla kategorisine normalize edildi; otel/pansiyon/turizm bürosu
  gizli kategorilere ayrıldı
- Kalite puanı (0-100): fotoğraf, Wikipedia, Wikidata, ad ayırt ediciliği
- İl/ilçe ataması PostGIS ile (metin karşılaştırması değil)
- Wikimedia zenginleştirmesi: fotoğraf + **fotoğrafçı adı ve lisans** + Wikipedia özeti
- Fotoğraf kaynak zinciri: Wikidata P18 → OSM commons etiketi → Wikipedia görseli →
  (opsiyonel) koordinat araması

### API
- Cihaz oturumu (hesapsız kullanım), JWT
- Şehir listesi ve arama
- Kart destesi: kalite sıralı, çeşitlilik kurallı, imleç sayfalamalı
- Kaydırma kaydı, geri alma, beğenilenler
- Görseller üç boyutta: orijinal + 500 px + 960 px (küçültme sunucuda, Wikimedia'nın
  standart genişliklerine yuvarlanarak)
- Yer detayı, kısa adla erişim, yakındakiler
- Rota optimizasyonu (gezgin satıcı) ve yol koridoru keşfi
- Gezi planları (oluştur/düzenle/sil, durak yönetimi, rota hesaplama)
- İçerik girişi (elle fotoğraf/açıklama ekleme)

### Altyapı
- PostgreSQL 17 + PostGIS 3.5, EF Core 10
- OSRM (araç + yürüme), Docker Compose ile
- Hız sınırlama, RFC 7807 hata biçimi
- CORS: geliştirmede tüm yerel adresler açık, üretimde yalnızca yapılandırılanlar
  (eksikse hiçbiri — eskiden herkese açılıyordu)
- Scalar API dokümantasyonu

---

## Sırada (öncelik sırasıyla)

### 1. Kullanıcı hesabı ✅ bitti
- Kayıt, giriş, hesap bilgisi, şifre değiştirme
- Cihazı hesaba bağlama: anonim planlar ve kaydırmalar korunuyor
- Hesap silme (App Store zorunluluğu); tüm kişisel veri birlikte siliniyor
- Kalan: şifre sıfırlama (e-posta gönderimi altyapısı gerekiyor)

### 2. Deploy altyapısı ✅ bitti
- Çok aşamalı Dockerfile (~102 MB, kök olmayan kullanıcı)
- docker-compose'a `app` profilinde api servisi
- GitHub Actions: derleme + 439 test + görüntü derleme
- `docs/deploy.md`: ortam değişkenleri, sunucu boyutu, veri aktarımı, kontrol listesi
- Kalan: gerçek sunucu ve otomatik dağıtım

### 3. Redis cache ✅ bitti
- Şehir listesi ve yer detayları önbellekte (ölçüm: 681 ms → 20 ms)
- İçerik girildiğinde ilgili anahtarlar temizleniyor
- Hız sınırı Redis'e taşındı: birden fazla sunucu çalıştığında sınır artık doğru uygulanıyor
- Redis erişilemezse ürün çalışmaya devam ediyor, yalnızca yavaşlıyor

### 4. İzleme  ← şu an burada
- ✅ Sağlık kontrolü genişletildi: `/health` (yük dengeleyici için) ve `/health/detay`
  (veritabanı, rota motoru, önbellek ayrı ayrı)
- Kalan: merkezi log toplama ve hata takibi (Sentry benzeri)
- Kalan: temel ölçümler (istek sayısı, yanıt süreleri)

### Sonraya bırakılanlar
- Yönetim CRUD'u (yer ekleme/düzenleme/gizleme)
- Öneri kişiselleştirme (kaydırma verisi toplanıyor ama kullanılmıyor)
- Genel arama ucu (yer araması)
- Çok günlük plan bölme (`dayIndex` alanı hazır, mantık yok)
- Plan paylaşma / GPX dışa aktarma
- Veri tazeleme işleri (OSM verisi zamanla eskir)
- Çoklu dil içeriği (`place_translations` tablosu boş)
- KVKK metinleri ve veri saklama politikası

---

## Bilinen sınırlar

**İçerik kapsaması dengesiz.** Kart olarak gösterilebilir yer sayısı illere göre çok
değişiyor: İstanbul 832, Nevşehir 41, Kırıkkale 0. Bunun sebebi OSM katkıcılarının
yoğunlaştığı bölgeler. Şehir içi mod 30 ilde güçlü, 35 ilde zayıf. Rota modu bundan az
etkileniyor çünkü koridor boyunca toplam sayı önemli.

**Fotoğrafsız 51.236 kayıt var.** Bunların 47 bini zaten feed eşiğinin altında (mahalle
camileri, isimsiz tepeler). Gerçek boşluk: feed'e girmeye layık ama fotoğrafsız 4.003
kayıt. Otomatik kaynaklar tükendi; kalanı elle doldurulacak (`/content` uçları bunun için).

**Koordinat araması kapalı.** Commons'ta koordinat bazlı fotoğraf araması denendi, isabet
oranı %20 çıktı (bir camiye kedi fotoğrafı eşleşti). İsim benzerliği filtresiyle %70'e
çıkarıldı ama varsayılan olarak kapalı bırakıldı.

**Rota süreleri gerçekçi ama pratik olmayabilir.** Kapadokya'da 4 durak yürüyerek 8 saat
çıkıyor; duraklar birbirinden uzak. İleride mesafeye göre "araç önerilir" uyarısı eklenebilir.
