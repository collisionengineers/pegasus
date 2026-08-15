// Builds dist/index.js (ESM) with esbuild and copies the application's real
// stylesheet to dist/styles.css. The stylesheet is never authored here: the
// design system ships src/Pegasus.Web/wwwroot/css/site.css byte-for-byte.
import { build } from 'esbuild';
import { copyFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const pkg = resolve(here, '..');
const repo = resolve(pkg, '../..');

mkdirSync(resolve(pkg, 'dist'), { recursive: true });
await build({
  entryPoints: [resolve(pkg, 'src/index.ts')],
  outfile: resolve(pkg, 'dist/index.js'),
  bundle: true,
  format: 'esm',
  platform: 'browser',
  target: 'es2020',
  jsx: 'automatic',
  external: ['react', 'react-dom', 'react/jsx-runtime'],
  loader: { '.png': 'dataurl' },
  sourcemap: false,
  logLevel: 'info',
});
copyFileSync(resolve(repo, 'src/Pegasus.Web/wwwroot/css/site.css'), resolve(pkg, 'dist/styles.css'));
console.log('copied site.css -> dist/styles.css');
