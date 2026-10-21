import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  base: "/app/",
  server: {
    host: "127.0.0.1",
    strictPort: true,
    hmr: {
      path: "/app/hmr",
      clientPort: Number(process.env.VITE_BACKEND_PORT || 5000),
    },
  },
  build: { outDir: "dist", emptyOutDir: true },
});
