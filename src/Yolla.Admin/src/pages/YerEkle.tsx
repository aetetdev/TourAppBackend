import { useState, type FormEvent } from 'react';
import { api } from '../api/client';
import type { Kategori } from '../api/tipler';
import { SayfaBasligi } from '../components/parcalar';
import { Durumlu, useVeri } from '../components/veri';

interface Sonuc {
  placeId: number;
  name: string;
  cityName: string;
  slug: string;
}

/**
 * İçerik ekibinin kataloğa doğrudan yer eklediği ekran.
 *
 * Kullanıcı önerisinden farkı kuyruğa düşmemesi ve kalite puanının daha
 * yüksek başlaması: ekip kaydı doğrulayarak giriyor.
 *
 * Şehir ve ilçe koordinattan bulunuyor; ekip il seçmiyor. Sınırlar zaten
 * veritabanında ve elle seçim yanlış girildiğinde kaydı bozuyor.
 */
export function YerEkle() {
  const kategoriler = useVeri<Kategori[]>('/places/oneriler/kategoriler');

  return (
    <>
      <SayfaBasligi
        ad="Yer ekle"
        aciklama="Katalogda olmayan bir yeri doğrudan ekle. Moderasyondan geçmiyor; şehir ve ilçe koordinattan bulunuyor."
      />

      <Durumlu durum={kategoriler}>{(liste) => <Form kategoriler={liste} />}</Durumlu>
    </>
  );
}

function Form({ kategoriler }: { kategoriler: Kategori[] }) {
  const [ad, setAd] = useState('');
  const [kategori, setKategori] = useState('');
  const [enlem, setEnlem] = useState('');
  const [boylam, setBoylam] = useState('');
  const [aciklama, setAciklama] = useState('');
  const [adres, setAdres] = useState('');

  const [bekliyor, setBekliyor] = useState(false);
  const [hata, setHata] = useState<string | null>(null);
  const [sonuc, setSonuc] = useState<Sonuc | null>(null);

  const enlemSayi = Number(enlem.replace(',', '.'));
  const boylamSayi = Number(boylam.replace(',', '.'));
  const konumGecerli =
    enlem !== '' &&
    boylam !== '' &&
    Number.isFinite(enlemSayi) &&
    Number.isFinite(boylamSayi);

  async function gonder(olay: FormEvent) {
    olay.preventDefault();
    setBekliyor(true);
    setHata(null);
    setSonuc(null);

    try {
      const eklenen = await api.post<Sonuc>('/admin/yerler', {
        name: ad.trim(),
        categoryKey: kategori,
        latitude: enlemSayi,
        longitude: boylamSayi,
        description: aciklama.trim() || undefined,
        address: adres.trim() || undefined,
      });

      setSonuc(eklenen);
      setAd('');
      setEnlem('');
      setBoylam('');
      setAciklama('');
      setAdres('');
    } catch (sorun) {
      setHata(sorun instanceof Error ? sorun.message : 'Kayıt başarısız.');
    } finally {
      setBekliyor(false);
    }
  }

  return (
    <>
      {sonuc && (
        <div className="kart basari">
          <strong>{sonuc.name}</strong> kataloğa eklendi — {sonuc.cityName} ·
          yer #{sonuc.placeId}
          <p className="satir" style={{ marginTop: 6 }}>
            Fotoğrafı yok; "Fotoğraf ekle" ekranından tamamlanabilir.
          </p>
        </div>
      )}

      <form className="kart" onSubmit={gonder} style={{ maxWidth: 620 }}>
        <label htmlFor="ad">Yerin adı</label>
        <input
          id="ad"
          value={ad}
          onChange={(o) => setAd(o.target.value)}
          placeholder="Kuşcenneti Seyir Terası"
          maxLength={250}
          required
        />

        <label htmlFor="kategori">Kategori</label>
        <select
          id="kategori"
          value={kategori}
          onChange={(o) => setKategori(o.target.value)}
          required
        >
          <option value="">Seç…</option>
          {kategoriler.map((k) => (
            <option key={k.key} value={k.key}>
              {k.name}
            </option>
          ))}
        </select>

        <div className="ikili-alan">
          <div>
            <label htmlFor="enlem">Enlem</label>
            <input
              id="enlem"
              value={enlem}
              onChange={(o) => setEnlem(o.target.value)}
              placeholder="40.18260"
              required
            />
          </div>
          <div>
            <label htmlFor="boylam">Boylam</label>
            <input
              id="boylam"
              value={boylam}
              onChange={(o) => setBoylam(o.target.value)}
              placeholder="29.06650"
              required
            />
          </div>
        </div>

        {konumGecerli && (
          <p className="satir" style={{ marginTop: 6 }}>
            <a
              href={`https://www.openstreetmap.org/?mlat=${enlemSayi}&mlon=${boylamSayi}#map=17/${enlemSayi}/${boylamSayi}`}
              target="_blank"
              rel="noopener"
            >
              Girilen konumu haritada doğrula
            </a>
          </p>
        )}

        <label htmlFor="adres">Adres (isteğe bağlı)</label>
        <input
          id="adres"
          value={adres}
          onChange={(o) => setAdres(o.target.value)}
          maxLength={400}
        />

        <label htmlFor="aciklama">Açıklama (isteğe bağlı)</label>
        <textarea
          id="aciklama"
          value={aciklama}
          onChange={(o) => setAciklama(o.target.value)}
          rows={4}
          maxLength={1000}
        />

        <div className="eylem">
          <button
            type="submit"
            disabled={bekliyor || ad.trim().length < 3 || !kategori || !konumGecerli}
          >
            {bekliyor ? 'Ekleniyor…' : 'Kataloğa ekle'}
          </button>
        </div>

        {hata && <p className="hata">{hata}</p>}
      </form>
    </>
  );
}
