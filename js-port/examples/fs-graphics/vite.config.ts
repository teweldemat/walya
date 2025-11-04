import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174
  },
  optimizeDeps: {
    include: ['@tewelde/funcscript', '@tewelde/funcscript/browser', '@tewelde/funcscript/parser']
  },
  build: {
    commonjsOptions: {
      include: [/funcscript-js/, /node_modules/]
    }
  }
});
