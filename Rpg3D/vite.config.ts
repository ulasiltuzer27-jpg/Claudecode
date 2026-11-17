import { defineConfig } from 'vite';

export default defineConfig({
  // Göreli yollar: oyun hangi alt klasörden servis edilirse edilsin çalışsın
  // (yerel `vite preview`, statik barındırma, Artifact).
  base: './',
  build: {
    target: 'es2022',
    // three.js tek başına ~650 KB; uyarıyı anlamlı bir eşiğe çek.
    chunkSizeWarningLimit: 1200,
  },
  server: { host: '127.0.0.1', port: 5173 },
  preview: { host: '127.0.0.1', port: 4173 },
});
