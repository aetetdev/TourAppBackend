import { useEffect, useState } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { jeton, yetkiDusunce } from './api/client';
import { Kabuk } from './Kabuk';
import { Giris } from './pages/Giris';
import { Yapim } from './pages/Yapim';

export default function App() {
  const [girisli, setGirisli] = useState(() => jeton.oku() !== null);

  // Herhangi bir uç 401/403 dönerse oturum düşüyor. Tek tek sayfalarda
  // kontrol etmek yerine tek yerden dinleniyor.
  useEffect(() => yetkiDusunce(() => setGirisli(false)), []);

  if (!girisli) {
    return <Giris girildi={() => setGirisli(true)} />;
  }

  return (
    <BrowserRouter basename="/admin">
      <Routes>
        <Route element={<Kabuk cikildi={() => setGirisli(false)} />}>
          <Route index element={<Yapim ad="Genel bakış" />} />
          <Route path="sehirler" element={<Yapim ad="Şehirler" />} />
          <Route path="yerler" element={<Yapim ad="Popüler yerler" />} />
          <Route path="rotalar" element={<Yapim ad="Rotalar" />} />
          <Route path="uyelik" element={<Yapim ad="Üyelik" />} />
          <Route
            path="moderasyon/fotograflar"
            element={<Yapim ad="Fotoğraf kuyruğu" />}
          />
          <Route
            path="moderasyon/yerler"
            element={<Yapim ad="Yer önerileri" />}
          />
          <Route
            path="icerik/fotograf-ekle"
            element={<Yapim ad="Fotoğraf ekle" />}
          />
          <Route path="icerik/yer-ekle" element={<Yapim ad="Yer ekle" />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
