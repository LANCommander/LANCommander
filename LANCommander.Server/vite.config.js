import { defineConfig } from 'vite';
import fs from 'node:fs';
import path from 'node:path';

// Vite doesn't have a first-class "CSS-only bundle" mode like webpack's
// MiniCssExtractPlugin. To produce wwwroot/css/app.css (matching the previous
// webpack output exactly) we build a tiny JS entry that only imports the SCSS,
// let Vite/Rollup extract the CSS asset with a fixed name, and then delete the
// otherwise-empty JS chunk that Vite is forced to also emit for a JS entry.
export default defineConfig({
    build: {
        outDir: 'wwwroot/css',
        emptyOutDir: false,
        cssCodeSplit: false,
        sourcemap: true,
        minify: true,
        rollupOptions: {
            input: {
                app: path.resolve(__dirname, 'Styles/app.entry.js'),
            },
            output: {
                entryFileNames: 'app.entry.js',
                assetFileNames: (assetInfo) => {
                    if (assetInfo.names?.some((n) => n.endsWith('.css'))) {
                        return 'app.css';
                    }
                    return '[name][extname]';
                },
            },
        },
    },
    plugins: [
        {
            // Remove the throwaway JS chunk (and its sourcemap) that Rollup must
            // still emit for a JS entry point, keeping only app.css/app.css.map.
            name: 'remove-js-entry-output',
            closeBundle() {
                const outDir = path.resolve(__dirname, 'wwwroot/css');
                for (const file of ['app.entry.js', 'app.entry.js.map']) {
                    const filePath = path.join(outDir, file);
                    if (fs.existsSync(filePath)) {
                        fs.unlinkSync(filePath);
                    }
                }
            },
        },
    ],
});
