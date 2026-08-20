import type { Rotalar as RotaVeri } from '../api/tipler';
import { YatayCubuk } from '../charts/grafikler';
import { sayi } from '../charts/palet';
import { Bolum, Sayac, SayfaBasligi, Tablo } from '../components/parcalar';
import { Durumlu, useVeri } from '../components/veri';

export function Rotalar() {
  const durum = useVeri<RotaVeri>('/admin/rotalar?take=20');

  return (
    <>
      <SayfaBasligi
        ad="Rotalar"
        aciklama="Kullanıcılar nereyi geziyor, nereden nereye gidiyor. Şehir içi planlar ve şehirlerarası koridorlar ayrı ölçülüyor."
      />

      <Durumlu durum={durum}>
        {(r) => (
          <>
            <div className="izgara ucul">
              {r.travelModes.map((u) => (
                <Sayac key={u.name} baslik={`${u.name} planı`} deger={u.count} />
              ))}
              <Sayac
                baslik="Şehirlerarası"
                deger={r.corridors.reduce((t, k) => t + k.trips, 0)}
                alt="koridor planı"
              />
            </div>

            <div style={{ marginTop: 16 }}>
              <Bolum
                baslik="Şehir içi planlar"
                aciklama="Hangi şehirde kaç plan kuruldu."
              >
                <YatayCubuk
                  birim="plan"
                  veri={r.cityTrips
                    .slice(0, 12)
                    .map((c) => ({ ad: c.cityName, deger: c.trips }))}
                />
              </Bolum>
            </div>

            <div className="izgara ikili" style={{ marginTop: 16 }}>
              <Bolum
                baslik="En çok tercih edilen koridorlar"
                aciklama="Şehirlerarası planların uç şehirleri."
              >
                <Tablo
                  satirlar={r.corridors}
                  anahtar={(k) => `${k.fromCityName}-${k.toCityName}`}
                  sutunlar={[
                    {
                      baslik: 'Rota',
                      ciz: (k) => `${k.fromCityName} → ${k.toCityName}`,
                    },
                    { baslik: 'Plan', sagda: true, ciz: (k) => sayi(k.trips) },
                  ]}
                />
              </Bolum>

              <Bolum
                baslik="Şehir içi planların şekli"
                aciklama="Ortalama durak sayısı ve mesafe."
              >
                <Tablo
                  satirlar={r.cityTrips}
                  anahtar={(c) => c.cityName}
                  sutunlar={[
                    { baslik: 'Şehir', ciz: (c) => c.cityName },
                    { baslik: 'Plan', sagda: true, ciz: (c) => sayi(c.trips) },
                    {
                      baslik: 'Ort. durak',
                      sagda: true,
                      ciz: (c) => c.averageStops.toLocaleString('tr-TR'),
                    },
                    {
                      baslik: 'Ort. mesafe',
                      sagda: true,
                      ciz: (c) =>
                        `${c.averageDistanceKm.toLocaleString('tr-TR')} km`,
                    },
                  ]}
                />
              </Bolum>
            </div>
          </>
        )}
      </Durumlu>
    </>
  );
}
