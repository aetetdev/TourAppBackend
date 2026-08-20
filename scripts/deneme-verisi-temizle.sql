-- Yönetim panosunu dolu görmek için üretilen deneme verisini siler.
--
-- Panoyu boş bir veritabanında değerlendirmek mümkün değil: kaç şehir öne
-- çıkıyor, koridorlar nasıl duruyor, coin ekonomisi kendi kendine dönüyor mu
-- — hiçbiri sıfır satırla görülmüyor. Bu yüzden gerçekçi bir örnek küme
-- üretildi ve tamamı işaretlendi.
--
-- İşaret: kullanıcı e-postası '@yolla.test' ile bitiyor. Bu kullanıcılara
-- bağlı her şey (cihaz, kaydırma, plan, katkı, coin, premium) buradan
-- siliniyor. Gerçek kullanıcıların verisine dokunulmuyor.
--
-- Çalıştırma:
--   docker exec -i yolla-postgres psql -U yolla -d yolla \
--     < scripts/deneme-verisi-temizle.sql

BEGIN;

CREATE TEMP TABLE deneme_kullanici ON COMMIT DROP AS
SELECT id FROM users WHERE email LIKE '%@yolla.test';

CREATE TEMP TABLE deneme_cihaz ON COMMIT DROP AS
SELECT id FROM devices WHERE user_id IN (SELECT id FROM deneme_kullanici);

-- Coin defteri ve premium hakları.
DELETE FROM coin_entries WHERE user_id IN (SELECT id FROM deneme_kullanici);
DELETE FROM premium_grants WHERE user_id IN (SELECT id FROM deneme_kullanici);

-- Katkılar ve öneriler. Onaylanmış önerinin yarattığı yer de gidiyor:
-- kullanıcı önerisiyle gelen yerlerin OSM kimliği negatif.
DELETE FROM photo_submissions WHERE user_id IN (SELECT id FROM deneme_kullanici);
DELETE FROM place_suggestions WHERE user_id IN (SELECT id FROM deneme_kullanici);
DELETE FROM places WHERE osm_id < -1000000;

-- Planlar ve kaydırmalar.
DELETE FROM trip_places
WHERE trip_id IN (SELECT id FROM trips WHERE device_id IN (SELECT id FROM deneme_cihaz));
DELETE FROM trips WHERE device_id IN (SELECT id FROM deneme_cihaz);
DELETE FROM swipes WHERE device_id IN (SELECT id FROM deneme_cihaz);

-- Cihazlar ve kullanıcılar.
DELETE FROM devices WHERE id IN (SELECT id FROM deneme_cihaz);
DELETE FROM user_roles WHERE user_id IN (SELECT id FROM deneme_kullanici);
DELETE FROM users WHERE id IN (SELECT id FROM deneme_kullanici);

COMMIT;

-- Yüklenen deneme görselleri dosya sisteminde:
--   src/Yolla.Api/bin/Debug/net10.0/uploads/deneme/
