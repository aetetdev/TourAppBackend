import { NavLink, Outlet } from 'react-router-dom';
import { jeton } from './api/client';

interface Baglanti {
  yol: string;
  ad: string;
  simge: string;
}

const PANO: Baglanti[] = [
  { yol: '/', ad: 'Genel bakış', simge: '◴' },
  { yol: '/sehirler', ad: 'Şehirler', simge: '⌘' },
  { yol: '/yerler', ad: 'Popüler yerler', simge: '★' },
  { yol: '/rotalar', ad: 'Rotalar', simge: '↝' },
  { yol: '/uyelik', ad: 'Üyelik', simge: '◆' },
];

const ISLEM: Baglanti[] = [
  { yol: '/moderasyon/fotograflar', ad: 'Fotoğraf kuyruğu', simge: '▣' },
  { yol: '/moderasyon/yerler', ad: 'Yer önerileri', simge: '⚑' },
  { yol: '/icerik/fotograf-ekle', ad: 'Fotoğraf ekle', simge: '＋' },
  { yol: '/icerik/yer-ekle', ad: 'Yer ekle', simge: '⊕' },
];

export function Kabuk({ cikildi }: { cikildi: () => void }) {
  function cikis() {
    jeton.sil();
    cikildi();
  }

  return (
    <div className="kabuk">
      <nav className="yan">
        <div className="marka">Yolla</div>

        <div className="baslik">Pano</div>
        {PANO.map((b) => (
          <Menu key={b.yol} baglanti={b} />
        ))}

        <div className="baslik">İşlemler</div>
        {ISLEM.map((b) => (
          <Menu key={b.yol} baglanti={b} />
        ))}

        <div style={{ marginTop: 'auto', paddingTop: 18 }}>
          <button className="ikincil tam" onClick={cikis}>
            Çıkış
          </button>
        </div>
      </nav>

      <main className="icerik">
        <Outlet />
      </main>
    </div>
  );
}

function Menu({ baglanti }: { baglanti: Baglanti }) {
  return (
    <NavLink
      to={baglanti.yol}
      // Kök yol her adresin ön eki olduğu için yalnızca birebir eşleşmede
      // etkin sayılıyor; yoksa bütün sayfalarda "Genel bakış" da yanıyor.
      end={baglanti.yol === '/'}
      className={({ isActive }) => (isActive ? 'etkin' : '')}
    >
      <span aria-hidden="true">{baglanti.simge}</span>
      {baglanti.ad}
    </NavLink>
  );
}
