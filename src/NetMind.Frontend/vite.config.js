import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

export default defineConfig({
  plugins: [vue()],
  server: {
    host: true,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET || 'http://127.0.0.1:5120',
        changeOrigin: true
      }
    },
    allowedHosts: [
      'unexalting-maniacal-ayleen.ngrok-free.dev'
    ]
  },
  preview: {
    host: true
  }
});
