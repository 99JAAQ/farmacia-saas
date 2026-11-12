/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The API runs on 5228 (dotnet run, "http" profile). The dev server proxies /api to it, so the browser sees
// a single origin: the SameSite=Strict refresh cookie works and no CORS configuration is needed.
const apiUrl = 'http://localhost:5228'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      '/api': apiUrl,
      '/health': apiUrl,
      // The real-time hub: WebSockets too.
      '/hubs': { target: apiUrl, ws: true },
    },
  },
  test: {
    // Same time zone on every machine (CI runs in UTC): date tests must fail if code mixes up local and UTC.
    env: { TZ: 'America/Bogota' },
  },
})
