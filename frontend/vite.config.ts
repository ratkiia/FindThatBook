import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// The backend URL can be overridden with VITE_API_TARGET (defaults to the API's
// local HTTP port). All /api requests are proxied there in dev so the browser
// never has cross-origin issues.
const apiTarget = process.env.VITE_API_TARGET ?? 'http://localhost:5099';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
});
