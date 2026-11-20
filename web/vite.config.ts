import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'

// The ASP.NET Core API in development (docker compose exposes it on 8080).
const backend = process.env.VEHICLEGO_API_URL ?? 'http://localhost:8080'

export default defineConfig({
  plugins: [
    vue(),
    // Registers only the Element Plus components that are used; the generated d.ts lets vue-tsc see their types.
    // Function APIs (ElMessage, ElMessageBox) are imported manually together with their styles.
    Components({ dirs: [], resolvers: [ElementPlusResolver()], dts: 'src/components.d.ts' }),
  ],
  server: {
    port: 5173,
    strictPort: true,
    // No changeOrigin: the Host header must stay localhost:5173, otherwise the OIDC redirect URI
    // built by the backend would not match the one registered at Google.
    proxy: {
      '/api': { target: backend },
      '/signin-oidc': { target: backend },
    },
  },
  build: {
    // web/dist is git-ignored; the Dockerfile copies it into the Web project's wwwroot.
    outDir: 'dist',
    emptyOutDir: true,
  },
  test: {
    include: ['src/**/*.spec.ts'],
    environment: 'node',
  },
})
