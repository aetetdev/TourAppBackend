import { useState, type FormEvent } from 'react';
import { girisYap } from '../api/client';

/**
 * Panelin giriş ekranı.
 *
 * Panelin kendi hesap sistemi yok: uygulamanın hesap ucunu kullanıyor ve
 * yetkiyi jetondaki rol belirliyor. Rolü olmayan bir hesap giriş yapabilir
 * ama uçlardan 403 alır ve kabuk onu buraya geri düşürür.
 */
export function Giris({ girildi }: { girildi: () => void }) {
  const [eposta, setEposta] = useState('');
  const [sifre, setSifre] = useState('');
  const [hata, setHata] = useState<string | null>(null);
  const [bekliyor, setBekliyor] = useState(false);

  async function gonder(olay: FormEvent) {
    olay.preventDefault();
    setHata(null);
    setBekliyor(true);

    try {
      await girisYap(eposta, sifre);
      girildi();
    } catch (sorun) {
      setHata(sorun instanceof Error ? sorun.message : 'Giriş başarısız.');
    } finally {
      setBekliyor(false);
    }
  }

  return (
    <div className="giris-sayfa">
      <form className="giris-kart" onSubmit={gonder}>
        <div className="marka">Yolla</div>
        <p className="sonuk" style={{ marginTop: 0 }}>
          Yönetim paneli
        </p>

        <label htmlFor="eposta">E-posta</label>
        <input
          id="eposta"
          type="email"
          autoComplete="username"
          value={eposta}
          onChange={(o) => setEposta(o.target.value)}
          required
        />

        <label htmlFor="sifre">Parola</label>
        <input
          id="sifre"
          type="password"
          autoComplete="current-password"
          value={sifre}
          onChange={(o) => setSifre(o.target.value)}
          required
        />

        <div style={{ marginTop: 18 }}>
          <button className="tam" type="submit" disabled={bekliyor}>
            {bekliyor ? 'Giriliyor…' : 'Giriş yap'}
          </button>
        </div>

        {hata && <p className="hata">{hata}</p>}
      </form>
    </div>
  );
}
