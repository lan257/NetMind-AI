import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

export default defineConfig({
  plugins: [vue()],
  server: {
    host: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5119',
        changeOrigin: true
      }
    }
  },
  preview: {
    host: true
  }
});
