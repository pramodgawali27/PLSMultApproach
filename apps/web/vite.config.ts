import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";


export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    allowedHosts: [
      'pls-web.happydune-0689c206.eastus.azurecontainerapps.io'
    ]
  }
});
