import type { ReactNode } from 'react';
import { sayi } from '../charts/palet';

export function SayfaBasligi({
  ad,
  aciklama,
}: {
  ad: string;
  aciklama: string;
}) {
  return (
    <div className="sayfa-basligi">
      <h1>{ad}</h1>
      <p>{aciklama}</p>
    </div>
  );
}

/**
 * Tek bir sayı.
 *
 * Grafiğe dönüştürülecek bir şey yok: tek değerin en okunaklı hali iri
 * yazılmış kendisi. Yanındaki alt satır sayıyı yorumluyor.
 */
export function Sayac({
  baslik,
  deger,
  alt,
  vurgu,
}: {
  baslik: string;
  deger: number | string;
  alt?: string;
  vurgu?: boolean;
}) {
  return (
    <div className="kart sayac">
      <div className="sayac-baslik">{baslik}</div>
      <div className={vurgu ? 'sayac-deger vurgulu' : 'sayac-deger'}>
        {typeof deger === 'number' ? sayi(deger) : deger}
      </div>
      {alt && <div className="sayac-alt">{alt}</div>}
    </div>
  );
}

export function Bolum({
  baslik,
  aciklama,
  children,
}: {
  baslik: string;
  aciklama?: string;
  children: ReactNode;
}) {
  return (
    <section className="kart bolum">
      <h2>{baslik}</h2>
      {aciklama && <p className="bolum-aciklama">{aciklama}</p>}
      {children}
    </section>
  );
}

/**
 * Tablo.
 *
 * Her grafiğin yanında bir tablo duruyor: grafik hızlı okumak, tablo kesin
 * değeri görmek için. Renk ayrımı zayıf kalan seriler de böyle okunabilir
 * kalıyor.
 */
export function Tablo<T>({
  satirlar,
  sutunlar,
  anahtar,
}: {
  satirlar: T[];
  sutunlar: { baslik: string; ciz: (satir: T) => ReactNode; sagda?: boolean }[];
  anahtar: (satir: T) => string | number;
}) {
  if (satirlar.length === 0) {
    return <p className="bos">Gösterilecek kayıt yok.</p>;
  }

  return (
    <div className="tablo-sar">
      <table className="tablo">
        <thead>
          <tr>
            {sutunlar.map((s) => (
              <th key={s.baslik} className={s.sagda ? 'sagda' : undefined}>
                {s.baslik}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {satirlar.map((satir) => (
            <tr key={anahtar(satir)}>
              {sutunlar.map((s) => (
                <td key={s.baslik} className={s.sagda ? 'sagda' : undefined}>
                  {s.ciz(satir)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
