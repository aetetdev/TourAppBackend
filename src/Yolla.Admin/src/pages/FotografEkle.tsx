import { useState, type FormEvent } from 'react';
import { api } from '../api/client';
import type { FotografsizYer, SehirKullanim } from '../api/tipler';
import { sayi } from '../charts/palet';
import { Bolum, SayfaBasligi, Tablo } from '../components/parcalar';
import { Durumlu, useVeri } from '../components/veri';

/**
 * İçerik ekibinin fotoğrafsız yerlere doğrudan fotoğraf eklediği ekran.
 *
 * Kullanıcı katkısından farkı moderasyondan geçmemesi: ekip zaten
 * moderasyonun kendisi. Eklenen fotoğraf anında yayına giriyor ve yerin
 * kalite puanı yeniden hesaplanıyor — puan eşiği geçerse yer kart destesine
 * de giriyor.
 */
export function FotografEkle() {
  const [sehir, setSehir] = useState('');
  const [arama, setArama] = useState('');
  const [secili, setSecili] = useState<FotografsizYer | null>(null);

  const sehirler = useVeri<SehirKullanim[]>('/admin/sehirler?take=40');
  const adres =
    `/admin/fotografsiz-yerler?take=40` +
    (sehir ? `&cityId=${sehir}` : '') +
    (arama ? `&search=${encodeURIComponent(arama)}` : '');
  const yerler = useVeri<FotografsizYer[]>(adres);

  return (
    <>
      <SayfaBasligi
        ad="Fotoğraf ekle"
        aciklama="Fotoğrafı olmayan yerlere ekip doğrudan fotoğraf ekliyor. Moderasyondan geçmiyor, anında yayına giriyor."
      />

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

        <label htmlFor="arama">Ara</label>
        <input
          id="arama"
          value={arama}
          placeholder="Yer adı"
          onChange={(o) => setArama(o.target.value)}
          style={{ width: 220 }}
        />
      </div>

      {secili && (
        <div style={{ marginBottom: 16 }}>
          <YuklemeFormu
            yer={secili}
            kapat={() => setSecili(null)}
            yuklendi={() => {
              setSecili(null);
              yerler.yenile();
            }}
          />
        </div>
      )}

      <Durumlu durum={yerler}>
        {(liste) => (
          <Bolum
            baslik="Fotoğraf bekleyen yerler"
            aciklama="Kaliteli olanlar önce: bir fotoğraf eklendiğinde doğrudan kart destesine giren kayıtlar."
          >
            <Tablo
              satirlar={liste}
              anahtar={(y) => y.placeId}
              sutunlar={[
                { baslik: 'Yer', ciz: (y) => y.name },
                {
                  baslik: 'Şehir',
                  ciz: (y) =>
                    y.districtName
                      ? `${y.cityName} / ${y.districtName}`
                      : y.cityName,
                },
                { baslik: 'Kategori', ciz: (y) => y.categoryName },
                {
                  baslik: 'Puan',
                  sagda: true,
                  ciz: (y) => sayi(y.qualityScore),
                },
                {
                  baslik: 'Açıklama',
                  ciz: (y) =>
                    y.hasDescription ? (
                      'var'
                    ) : (
                      <span className="etiket eksik">yok</span>
                    ),
                },
                {
                  baslik: 'Konum',
                  ciz: (y) => (
                    <a
                      href={`https://www.openstreetmap.org/?mlat=${y.latitude}&mlon=${y.longitude}#map=17/${y.latitude}/${y.longitude}`}
                      target="_blank"
                      rel="noopener"
                    >
                      haritada
                    </a>
                  ),
                },
                {
                  baslik: '',
                  ciz: (y) => (
                    <button className="ikincil" onClick={() => setSecili(y)}>
                      Fotoğraf ekle
                    </button>
                  ),
                },
              ]}
            />
          </Bolum>
        )}
      </Durumlu>
    </>
  );
}

function YuklemeFormu({
  yer,
  kapat,
  yuklendi,
}: {
  yer: FotografsizYer;
  kapat: () => void;
  yuklendi: () => void;
}) {
  const [dosya, setDosya] = useState<File | null>(null);
  const [fotografci, setFotografci] = useState('');
  const [lisans, setLisans] = useState('');
  const [kaynak, setKaynak] = useState('');
  const [bekliyor, setBekliyor] = useState(false);
  const [hata, setHata] = useState<string | null>(null);

  async function gonder(olay: FormEvent) {
    olay.preventDefault();
    if (!dosya) return;

    setBekliyor(true);
    setHata(null);

    const form = new FormData();
    form.append('photo', dosya);
    form.append('photographerName', fotografci.trim());
    if (lisans.trim()) form.append('license', lisans.trim());
    if (kaynak.trim()) form.append('sourceUrl', kaynak.trim());

    try {
      // `Content-Type` elle verilmiyor: çok parçalı gövdenin sınırını
      // tarayıcı üretiyor, elle yazılan başlık onu bozuyor.
      await api.postForm(`/admin/yerler/${yer.placeId}/fotograf`, form);
      yuklendi();
    } catch (sorun) {
      setHata(sorun instanceof Error ? sorun.message : 'Yükleme başarısız.');
    } finally {
      setBekliyor(false);
    }
  }

  return (
    <form className="kart" onSubmit={gonder}>
      <h2>{yer.name}</h2>
      <p className="satir">
        {yer.cityName} · {yer.categoryName} · yer #{yer.placeId}
      </p>

      <label htmlFor="dosya">Fotoğraf</label>
      <input
        id="dosya"
        type="file"
        accept="image/jpeg,image/png"
        onChange={(o) => setDosya(o.target.files?.[0] ?? null)}
        required
      />

      <label htmlFor="fotografci">Fotoğrafı çeken</label>
      <input
        id="fotografci"
        value={fotografci}
        onChange={(o) => setFotografci(o.target.value)}
        placeholder="Ad Soyad"
        required
      />

      <label htmlFor="lisans">Lisans (boş bırakılırsa "Yolla")</label>
      <input
        id="lisans"
        value={lisans}
        onChange={(o) => setLisans(o.target.value)}
        placeholder="CC BY-SA 4.0"
      />

      <label htmlFor="kaynak">Kaynak adresi (varsa)</label>
      <input
        id="kaynak"
        value={kaynak}
        onChange={(o) => setKaynak(o.target.value)}
        placeholder="https://…"
      />

      <p className="satir" style={{ marginTop: 12 }}>
        Fotoğraf sunucuda JPEG'e çevriliyor, 1920 piksele indiriliyor ve
        EXIF'i (konum dahil) siliniyor.
      </p>

      <div className="eylem">
        <button type="submit" disabled={bekliyor || !dosya || !fotografci.trim()}>
          {bekliyor ? 'Yükleniyor…' : 'Yükle ve yayına al'}
        </button>
        <button type="button" className="ikincil" onClick={kapat}>
          Vazgeç
        </button>
      </div>

      {hata && <p className="hata">{hata}</p>}
    </form>
  );
}
