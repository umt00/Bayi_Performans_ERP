import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'https://localhost:7000',
        changeOrigin: true,
        secure: false,
      },
      '/auth': {
        target: 'https://localhost:7000',
        changeOrigin: true,
        secure: false,
      },
      '/bayi': {
        target: 'https://localhost:7000',
        changeOrigin: true,
        secure: false,
      },
      '/bayiler': {
        target: 'https://localhost:7000',
        changeOrigin: true,
        secure: false,
      },
      '/stok': {
        target: 'https://localhost:7000',
        changeOrigin: true,
        secure: false,
      },
      '/teklifler': {
        target: 'http://localhost:8000',
        changeOrigin: true,
        secure: false,
      },
      '/urunler': {
        target: 'https://localhost:7000',
        changeOrigin: true,
        secure: false,
      },
      '/depolar': {
        target: 'https://localhost:7000',
        changeOrigin: true,
        secure: false,
      },
      '/tanimlar': {
        target: 'https://localhost:7000',
        changeOrigin: true,
        secure: false,
      },
      '/jobs': {
        target: 'https://localhost:7000',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
