import { defineConfig } from "vite";
import { resolve } from "path";
import { viteStaticCopy } from "vite-plugin-static-copy";

export default defineConfig({
  root: __dirname,
  publicDir: false,
  build: {
    outDir: "wwwroot",
    assetsDir: "",
    sourcemap: true,
    emptyOutDir: false,
    cssCodeSplit: false,
    lib: {
      entry: resolve(__dirname, "Main.ts"),
      formats: ["iife"],
      name: "LANCommanderUI",
      fileName: () => "bundle.js",
    },
    rollupOptions: {
      output: {
        assetFileNames: (assetInfo) => {
          if (assetInfo.type === "asset" && assetInfo.name?.endsWith(".css")) {
            return "ui.css";
          }

          return "[name][extname]";
        },
      },
    },
  },
  plugins: [
    viteStaticCopy({
      targets: [
        {
          src: resolve(
            __dirname,
            "node_modules/bootstrap-icons/bootstrap-icons.svg"
          ),
          dest: ".",
        },
      ],
    }),
  ],
});
