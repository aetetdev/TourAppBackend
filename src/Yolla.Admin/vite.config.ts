import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Panel API'nin içinden servis ediliyor: derleme çıktısı doğrudan
// `Yolla.Api/wwwroot/admin` altına yazılıyor ve `/admin` adresinden açılıyor.
// Ayrı bir yayın süreci ve ikinci bir alan adı gerekmiyor.
export default defineConfig({
  plugins: [react()],
  base: '/admin/',
  build: {
    outDir: '../Yolla.Api/wwwroot/admin',
    emptyOutDir: true,
  },
  server: {
    // Geliştirmede panel 5173'te, API 5088'de. Vekil olmadan tarayıcı
    // çapraz kaynak engeline takılıyor; vekille aynı kaynaktan gelmiş
    // gibi davranıyor ve üretimdeki adresler birebir aynı kalıyor.
    proxy: {
      '/api': {
        target: 'http://localhost:5088',
        changeOrigin: true,
      },
      '/uploads': {
        target: 'http://localhost:5088',
        changeOrigin: true,
      },
    },
  },
});
