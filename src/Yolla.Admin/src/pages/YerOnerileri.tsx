import { useState } from 'react';
import { api, ApiError } from '../api/client';
import type { YerOnerisi } from '../api/tipler';
import { SayfaBasligi } from '../components/parcalar';
import { Durumlu, useVeri } from '../components/veri';

export function YerOnerileri() {
  const durum = useVeri<YerOnerisi[]>('/moderation/yer-onerileri?take=100');
  const [islenen, setIslenen] = useState<number[]>([]);

  return (
    <>
      <SayfaBasligi
        ad="Yer önerileri"
        aciklama="Kullanıcıların önerdiği, katalogda olmayan yerler. Onaylanan öneri gerçek bir yer olarak yaratılıyor ve önerene coin yazılıyor."
      />

      <Durumlu durum={durum}>
        {(liste) => {
          const kalan = liste.filter((o) => !islenen.includes(o.id));

          if (kalan.length === 0) {
            return <div className="kart bos">Bekleyen öneri yok.</div>;
          }

          return (
            <div className="izgara" style={{ gap: 16 }}>
              {kalan.map((oneri) => (
                <OneriKart
                  key={oneri.id}
                  oneri={oneri}
                  bitti={() => setIslenen((o) => [...o, oneri.id])}
                />
              ))}
            </div>
          );
        }}
      </Durumlu>
    </>
  );
}

function OneriKart({
  oneri,
  bitti,
}: {
  oneri: YerOnerisi;
  bitti: () => void;
}) {
  const [bekliyor, setBekliyor] = useState(false);
  const [hata, setHata] = useState<string | null>(null);

  async function calistir(is: () => Promise<unknown>) {
    setBekliyor(true);
    setHata(null);
    try {
      await is();
      bitti();
    } catch (sorun) {
      setHata(sorun instanceof ApiError ? sorun.message : 'İşlem başarısız.');
      setBekliyor(false);
    }
  }

  const reddet = () => {
    const sebep = prompt('Red sebebi (kullanıcıya gösterilecek):');
    if (!sebep) return;

    return calistir(() =>
      api.post(`/moderation/yer-onerileri/${oneri.id}/reddet`, {
        reason: sebep,
      }),
    );
  };

  // Onaylamadan önce oraya gerçekten bakmak gerekiyor; harita bağlantısı
  // bunu tek tıka indiriyor.
  const harita = `https://www.openstreetmap.org/?mlat=${oneri.latitude}&mlon=${oneri.longitude}#map=17/${oneri.latitude}/${oneri.longitude}`;

  return (
    <div className="kart">
      <h2>{oneri.name}</h2>
      <p className="satir">
        {oneri.categoryName} · {oneri.cityName}
        {oneri.districtName ? ` / ${oneri.districtName}` : ''}
      </p>
      <p className="satir">
        <a href={harita} target="_blank" rel="noopener">
          Haritada aç ({oneri.latitude.toFixed(5)}, {oneri.longitude.toFixed(5)})
        </a>
      </p>
      {oneri.address && <p className="satir">Adres: {oneri.address}</p>}
      {oneri.description && <p className="aciklama">{oneri.description}</p>}
      <p className="satir">
        Öneren #{oneri.userId} · {oneri.userApprovedCount} onaylı önerisi var
      </p>
      <p className="satir">
        {new Date(oneri.createdAt).toLocaleString('tr-TR')}
      </p>

      {/* Kuyruğun büyük kısmı tekrar öneri; karar bu listeye bakılarak
          veriliyor, ayrı bir ekrana gitmeden. */}
      <div className="yakin">
        <span className="satir">Çevresindeki kayıtlı yerler:</span>
        {oneri.nearby.length === 0 ? (
          <p className="satir">1 km çevresinde kayıtlı yer yok.</p>
        ) : (
          <ul>
            {oneri.nearby.map((y) => (
              <li key={y.id}>
                {y.name} · {y.categoryName} · {y.distanceMeters} m
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="eylem">
        <button
          className="onay"
          disabled={bekliyor}
          onClick={() =>
            calistir(() =>
              api.post(`/moderation/yer-onerileri/${oneri.id}/onayla`),
            )
          }
        >
          Kataloğa al (+25 coin)
        </button>
        <button className="red" disabled={bekliyor} onClick={reddet}>
          Reddet
        </button>
      </div>

      {hata && <p className="hata">{hata}</p>}
    </div>
  );
}
