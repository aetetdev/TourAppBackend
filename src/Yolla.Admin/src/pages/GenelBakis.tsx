import type { Ozet } from '../api/tipler';
import { Egri, YatayCubuk } from '../charts/grafikler';
import { sayi } from '../charts/palet';
import { Bolum, Sayac, SayfaBasligi, Tablo } from '../components/parcalar';
import { Durumlu, useVeri } from '../components/veri';

export function GenelBakis() {
  const durum = useVeri<Ozet>('/admin/ozet');

  return (
    <>
      <SayfaBasligi
        ad="Genel bakış"
        aciklama="Kullanım, içerik ve moderasyon kuyruğu tek ekranda. Bütün sayılar toplam; kişisel veri içermiyor."
      />

      <Durumlu durum={durum}>
        {(o) => {
          const begeniOrani = o.swipeCount
            ? Math.round((o.likeCount / o.swipeCount) * 100)
            : 0;

          return (
            <>
              <div className="izgara dortlu">
                <Sayac
                  baslik="Kullanıcı"
                  deger={o.userCount}
                  alt={`${sayi(o.premiumUserCount)} premium`}
                />
                <Sayac
                  baslik="Cihaz"
                  deger={o.deviceCount}
                  alt={`son 7 günde ${sayi(o.activeDevices7)} aktif`}
                />
                <Sayac
                  baslik="Plan"
                  deger={o.tripCount}
                  alt={`son 30 günde ${sayi(o.tripsLast30Days)}`}
                />
                <Sayac
                  baslik="Beğeni oranı"
                  deger={`%${begeniOrani}`}
                  alt={`${sayi(o.swipeCount)} kaydırmanın ${sayi(o.likeCount)} tanesi`}
                />
              </div>

              <div className="izgara dortlu" style={{ marginTop: 16 }}>
                <Sayac
                  baslik="Bekleyen fotoğraf"
                  deger={o.pendingPhotoCount}
                  alt="moderasyon kuyruğunda"
                  vurgu={o.pendingPhotoCount > 0}
                />
                <Sayac
                  baslik="Bekleyen yer önerisi"
                  deger={o.pendingSuggestionCount}
                  alt="moderasyon kuyruğunda"
                  vurgu={o.pendingSuggestionCount > 0}
                />
                <Sayac
                  baslik="Fotoğrafsız yer"
                  deger={o.placesWithoutPhoto}
                  alt={`${sayi(o.placeCount)} yerin içinde`}
                />
                <Sayac baslik="Şehir" deger={o.cityCount} alt="katalogda" />
              </div>

              <div style={{ marginTop: 16 }}>
                <Bolum
                  baslik="Son 30 günde kurulan planlar"
                  aciklama="Günlük plan sayısı. Kesin değer için eğrinin üstüne gel."
                >
                  <Egri
                    birim="plan"
                    veri={o.tripsByDay.map((g) => ({
                      // Eksende yalnızca gün ve ay: otuz tam tarih sığmıyor.
                      ad: new Date(g.day).toLocaleDateString('tr-TR', {
                        day: '2-digit',
                        month: '2-digit',
                      }),
                      deger: g.count,
                    }))}
                  />
                </Bolum>
              </div>

              <div className="izgara ikili" style={{ marginTop: 16 }}>
                <Bolum baslik="Cihaz platformları">
                  <YatayCubuk
                    birim="cihaz"
                    yukseklik={o.platforms.length * 40 + 30}
                    veri={o.platforms.map((p) => ({
                      ad: p.name,
                      deger: p.count,
                    }))}
                  />
                </Bolum>

                <Bolum
                  baslik="İçerik durumu"
                  aciklama="Fotoğrafsız yerler kart destesine giremiyor; içerik ekibinin iş listesi bu."
                >
                  <Tablo
                    satirlar={[
                      {
                        ad: 'Toplam yer',
                        deger: o.placeCount,
                      },
                      {
                        ad: 'Fotoğrafı olan',
                        deger: o.placeCount - o.placesWithoutPhoto,
                      },
                      { ad: 'Fotoğrafsız', deger: o.placesWithoutPhoto },
                    ]}
                    anahtar={(s) => s.ad}
                    sutunlar={[
                      { baslik: 'Ölçü', ciz: (s) => s.ad },
                      { baslik: 'Adet', sagda: true, ciz: (s) => sayi(s.deger) },
                    ]}
                  />
                </Bolum>
              </div>
            </>
          );
        }}
      </Durumlu>
    </>
  );
}
