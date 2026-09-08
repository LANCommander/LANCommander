import { defineConfig } from 'vite';
import path from 'node:path';
import { viteStaticCopy } from 'vite-plugin-static-copy';

// Library build producing an ES module bundle (wwwroot/bundle.js) plus a
// single CSS bundle (wwwroot/ui.css), matching the previous webpack output
// (module output + MiniCssExtractPlugin) exactly so Blazor's wwwroot
// references keep working unchanged.
export default defineConfig({
    build: {
        outDir: 'wwwroot',
        emptyOutDir: false,
        cssCodeSplit: false,
        sourcemap: true,
        minify: true,
        lib: {
            entry: path.resolve(__dirname, '_Imports.razor.ts'),
            formats: ['es'],
            fileName: () => 'bundle.js',
            cssFileName: 'ui',
        },
        rollupOptions: {
            output: {
                // Keep a single, non-hashed JS output file.
                entryFileNames: 'bundle.js',
            },
        },
    },
    plugins: [
        viteStaticCopy({
            targets: [
                {
                    src: path.resolve(__dirname, 'node_modules/bootstrap-icons/bootstrap-icons.svg').replace(/\\/g, '/'),
                    dest: '.',
                },
            ],
        }),
    ],
});
