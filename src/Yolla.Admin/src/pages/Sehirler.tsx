import type { SehirKullanim } from '../api/tipler';
import { YatayCubuk } from '../charts/grafikler';
import { sayi } from '../charts/palet';
import { Bolum, SayfaBasligi, Tablo } from '../components/parcalar';
import { Durumlu, useVeri } from '../components/veri';

export function Sehirler() {
  const durum = useVeri<SehirKullanim[]>('/admin/sehirler?take=20');

  return (
    <>
      <SayfaBasligi
        ad="Şehirler"
        aciklama="Hangi şehre ilgi var. Kullanıcının nerede olduğu değil, neyi gezdiği ölçülüyor — konum toplanmıyor, ilgi kaydırılan yerlerin şehrinden çıkarılıyor."
      />

      <Durumlu durum={durum}>
        {(sehirler) => (
          <>
            <Bolum
              baslik="Beğeniye göre şehirler"
              aciklama="Ham kaydırma 'kaç kart gördü'yü ölçüyor; beğeni 'neyi istedi'yi. Sıralama beğeniye göre."
            >
              <YatayCubuk
                birim="beğeni"
                veri={sehirler.slice(0, 12).map((s) => ({
                  ad: s.cityName,
                  deger: s.likes,
                }))}
              />
            </Bolum>

            <div style={{ marginTop: 16 }}>
              <Bolum baslik="Ayrıntı">
                <Tablo
                  satirlar={sehirler}
                  anahtar={(s) => s.cityId}
                  sutunlar={[
                    { baslik: 'Şehir', ciz: (s) => s.cityName },
                    {
                      baslik: 'Beğeni',
                      sagda: true,
                      ciz: (s) => sayi(s.likes),
                    },
                    {
                      baslik: 'Kaydırma',
                      sagda: true,
                      ciz: (s) => sayi(s.swipes),
                    },
                    {
                      baslik: 'Beğeni oranı',
                      sagda: true,
                      ciz: (s) =>
                        s.swipes
                          ? `%${Math.round((s.likes / s.swipes) * 100)}`
                          : '—',
                    },
                    { baslik: 'Plan', sagda: true, ciz: (s) => sayi(s.trips) },
                    {
                      baslik: 'Cihaz',
                      sagda: true,
                      ciz: (s) => sayi(s.devices),
                    },
                    {
                      baslik: 'Katalogdaki yer',
                      sagda: true,
                      ciz: (s) => sayi(s.places),
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
