import { defineConfig } from "vite";
import { resolve } from "path";

export default defineConfig({
  root: __dirname,
  publicDir: false,
  build: {
    outDir: "wwwroot/css",
    assetsDir: "",
    emptyOutDir: false,
    sourcemap: true,
    cssCodeSplit: false,
    rollupOptions: {
      input: {
        app: resolve(__dirname, "Styles/app.scss"),
      },
      output: {
        assetFileNames: () => "app.css",
      },
    },
  },
});
