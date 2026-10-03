import { defineConfig } from 'vite';

// base './' -> Electron dosyayi file:// ile acar; mutlak yollar orada calismaz.
export default defineConfig({
  base: './',
  build: {
    target: 'es2022',
    outDir: 'dist',
    emptyOutDir: true,
    chunkSizeWarningLimit: 6000,
  },
  server: { port: 5173 },
});
