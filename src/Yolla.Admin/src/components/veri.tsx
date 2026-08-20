import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { api } from '../api/client';

interface Durum<T> {
  veri: T | null;
  hata: string | null;
  yukleniyor: boolean;
  yenile: () => void;
}

/**
 * Bir uçtan veri çeker; yükleme ve hata durumunu birlikte taşır.
 *
 * Her sayfada aynı üç durumu (yükleniyor / hata / veri) elle yazmak yerine
 * tek yerde: ekranlar yalnızca veriyi çizmekle uğraşıyor.
 */
export function useVeri<T>(yol: string): Durum<T> {
  const [veri, setVeri] = useState<T | null>(null);
  const [hata, setHata] = useState<string | null>(null);
  const [yukleniyor, setYukleniyor] = useState(true);

  const cek = useCallback(() => {
    let birakildi = false;
    setYukleniyor(true);
    setHata(null);

    api
      .get<T>(yol)
      .then((sonuc) => {
        // Adres değişince eski istek geç dönüp yeni veriyi ezmemeli.
        if (!birakildi) setVeri(sonuc);
      })
      .catch((sorun) => {
        if (!birakildi) {
          setHata(sorun instanceof Error ? sorun.message : 'Veri alınamadı.');
        }
      })
      .finally(() => {
        if (!birakildi) setYukleniyor(false);
      });

    return () => {
      birakildi = true;
    };
  }, [yol]);

  const [tetik, setTetik] = useState(0);
  useEffect(() => cek(), [cek, tetik]);

  return { veri, hata, yukleniyor, yenile: () => setTetik((t) => t + 1) };
}

/** Yükleme ve hata durumlarını tek yerden çizer. */
export function Durumlu<T>({
  durum,
  children,
}: {
  durum: Durum<T>;
  children: (veri: T) => ReactNode;
}) {
  if (durum.yukleniyor && durum.veri === null) {
    return <div className="kart bos">Yükleniyor…</div>;
  }

  if (durum.hata) {
    return (
      <div className="kart">
        <p className="hata" style={{ marginTop: 0 }}>
          {durum.hata}
        </p>
        <button className="ikincil" onClick={durum.yenile}>
          Tekrar dene
        </button>
      </div>
    );
  }

  return durum.veri === null ? null : <>{children(durum.veri)}</>;
}
