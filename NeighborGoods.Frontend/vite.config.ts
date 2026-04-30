import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    tsconfigPaths: true,
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('@microsoft/signalr')) {
            return 'signalr'
          }

          if (id.includes('framer-motion')) {
            return 'motion'
          }

          if (id.includes('@lottiefiles/dotlottie-react')) {
            return 'lottie'
          }

          if (id.includes('node_modules')) {
            return 'vendor'
          }

          return undefined
        },
      },
    },
  },
})
