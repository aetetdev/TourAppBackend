import { useEffect, useState } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { jeton, yetkiDusunce } from './api/client';
import { Kabuk } from './Kabuk';
import { GenelBakis } from './pages/GenelBakis';
import { Giris } from './pages/Giris';
import { Rotalar } from './pages/Rotalar';
import { Sehirler } from './pages/Sehirler';
import { Uyelik } from './pages/Uyelik';
import { Yapim } from './pages/Yapim';
import { Yerler } from './pages/Yerler';

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
          <Route index element={<GenelBakis />} />
          <Route path="sehirler" element={<Sehirler />} />
          <Route path="yerler" element={<Yerler />} />
          <Route path="rotalar" element={<Rotalar />} />
          <Route path="uyelik" element={<Uyelik />} />
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
