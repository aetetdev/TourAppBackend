import { useState } from 'react';
import { api, ApiError } from '../api/client';
import type { FotoGonderi } from '../api/tipler';
import { SayfaBasligi } from '../components/parcalar';
import { Durumlu, useVeri } from '../components/veri';

export function FotografKuyrugu() {
  const durum = useVeri<FotoGonderi[]>('/moderation/bekleyenler?take=100');
  const [islenen, setIslenen] = useState<number[]>([]);

  return (
    <>
      <SayfaBasligi
        ad="Fotoğraf kuyruğu"
        aciklama="Kullanıcıların gönderdiği fotoğraflar. Onaylanan fotoğraf yerin kartına giriyor ve gönderene coin yazılıyor."
      />

      <Durumlu durum={durum}>
        {(liste) => {
          const kalan = liste.filter((g) => !islenen.includes(g.id));

          if (kalan.length === 0) {
            return <div className="kart bos">Bekleyen gönderi yok.</div>;
          }

          return (
            <div className="izgara" style={{ gap: 16 }}>
              {kalan.map((gonderi) => (
                <FotoKart
                  key={gonderi.id}
                  gonderi={gonderi}
                  bitti={() => setIslenen((o) => [...o, gonderi.id])}
                />
              ))}
            </div>
          );
        }}
      </Durumlu>
    </>
  );
}

function FotoKart({
  gonderi,
  bitti,
}: {
  gonderi: FotoGonderi;
  bitti: () => void;
}) {
  const [bekliyor, setBekliyor] = useState(false);
  const [hata, setHata] = useState<string | null>(null);

  // Aynı kişi sürekli reddediliyorsa daha dikkatli bakmak gerekiyor.
  const supheli =
    gonderi.userRejectedCount > gonderi.userApprovedCount &&
    gonderi.userRejectedCount >= 3;

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
      api.post(`/moderation/${gonderi.id}/reddet`, { reason: sebep }),
    );
  };

  return (
    <div className="kart kuyruk-kart">
      <a href={gonderi.url} target="_blank" rel="noopener">
        <img src={gonderi.url} alt="Gönderilen fotoğraf" loading="lazy" />
      </a>

      <div>
        <h2>{gonderi.placeName}</h2>
        <p className="satir">
          {gonderi.cityName ?? ''} · yer #{gonderi.placeId}
        </p>
        <p className="satir">
          {gonderi.width}×{gonderi.height} ·{' '}
          {(gonderi.sizeBytes / 1024).toFixed(0)} KB
        </p>
        {/* Gönderenin kimliği değil yalnızca numarası ve geçmişi
            gösteriliyor: karar için gereken şey bu. */}
        <p className={supheli ? 'satir uyari' : 'satir'}>
          Gönderen #{gonderi.userId} · {gonderi.userApprovedCount} onay ·{' '}
          {gonderi.userRejectedCount} red
        </p>
        <p className="satir">
          {new Date(gonderi.createdAt).toLocaleString('tr-TR')}
        </p>

        {gonderi.existingPhotoUrl ? (
          <div className="mevcut">
            <span className="satir">Yerin şu anki fotoğrafı:</span>
            <br />
            <img src={gonderi.existingPhotoUrl} alt="Mevcut fotoğraf" />
          </div>
        ) : (
          <p className="satir">Yerin fotoğrafı yok.</p>
        )}

        <div className="eylem">
          <button
            className="onay"
            disabled={bekliyor}
            onClick={() =>
              calistir(() => api.post(`/moderation/${gonderi.id}/onayla`))
            }
          >
            Onayla (+10 coin)
          </button>
          <button className="red" disabled={bekliyor} onClick={reddet}>
            Reddet
          </button>
        </div>

        {hata && <p className="hata">{hata}</p>}
      </div>
    </div>
  );
}
