import { useState } from 'react';
import type { PopulerYer, SehirKullanim } from '../api/tipler';
import { YatayCubuk } from '../charts/grafikler';
import { sayi } from '../charts/palet';
import { Bolum, SayfaBasligi, Tablo } from '../components/parcalar';
import { Durumlu, useVeri } from '../components/veri';

export function Yerler() {
  const [sehir, setSehir] = useState('');

  const sehirler = useVeri<SehirKullanim[]>('/admin/sehirler?take=40');
  const yerler = useVeri<PopulerYer[]>(
    `/admin/yerler?take=25${sehir ? `&cityId=${sehir}` : ''}`,
  );

  return (
    <>
      <SayfaBasligi
        ad="Popüler yerler"
        aciklama="En çok ilgi gören turistik yerler. Sıralamada plana eklenmek beğeniden ağır basıyor: biri niyet, diğeri ilgi."
      />

      {/* Süzgeç grafiklerin üstünde tek satır: neyin daraltıldığı en başta
          görünmeli. */}
      <div className="suzgec">
        <label htmlFor="sehir">Şehir</label>
        <select
          id="sehir"
          value={sehir}
          onChange={(o) => setSehir(o.target.value)}
        >
          <option value="">Bütün şehirler</option>
          {(sehirler.veri ?? []).map((s) => (
            <option key={s.cityId} value={s.cityId}>
              {s.cityName}
            </option>
          ))}
        </select>
      </div>

      <Durumlu durum={yerler}>
        {(liste) => (
          <>
            <Bolum
              baslik="Plana en çok eklenen yerler"
              aciklama="Beğeni 'hoşuma gitti', plana eklemek 'gitmeyi düşünüyorum' demek."
            >
              <YatayCubuk
                birim="plan"
                veri={liste
                  .filter((y) => y.tripAdds > 0)
                  .slice(0, 12)
                  .map((y) => ({ ad: y.name, deger: y.tripAdds }))}
              />
            </Bolum>

            <div style={{ marginTop: 16 }}>
              <Bolum baslik="Ayrıntı">
                <Tablo
                  satirlar={liste}
                  anahtar={(y) => y.placeId}
                  sutunlar={[
                    { baslik: 'Yer', ciz: (y) => y.name },
                    { baslik: 'Şehir', ciz: (y) => y.cityName },
                    { baslik: 'Kategori', ciz: (y) => y.categoryName },
                    {
                      baslik: 'Plana eklenme',
                      sagda: true,
                      ciz: (y) => sayi(y.tripAdds),
                    },
                    {
                      baslik: 'Beğeni',
                      sagda: true,
                      ciz: (y) => sayi(y.likes),
                    },
                    {
                      baslik: 'Fotoğraf',
                      ciz: (y) =>
                        y.hasPhoto ? (
                          'var'
                        ) : (
                          <span className="etiket eksik">yok</span>
                        ),
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
