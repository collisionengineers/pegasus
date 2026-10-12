// Stage 1 only: the offline v38 mockup of the Repair Spec section, today and
// as proposed, inside the Case page. The page frames are the v36 round's
// captures of the running synthetic host (captured/), restyled with the live
// site.css and case-workspace.css from this checkout; the Repair Spec
// section is rendered from the fixture by lib/repair-spec-v38-render.js.
// Run `node build-repair-spec-v38.mjs`, then `python check-repair-spec-v38.py`.
import { readFile, writeFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const current = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(current, '../../../../');
const text = (relative) => readFile(path.join(repo, relative), 'utf8');
const bytes = (relative) => readFile(path.join(repo, relative));
const local = (relative) => readFile(path.join(current, relative), 'utf8');

const [regular, mark] = await Promise.all([
  bytes('src/Pegasus.Web/wwwroot/fonts/inter/InterVariable.woff2'),
  bytes('src/Pegasus.Web/wwwroot/images/pegasus-mark-refined-128.png'),
]);
const site = (await text('src/Pegasus.Web/wwwroot/css/site.css'))
  .replace(/url\(\.\.\/fonts\/inter\/InterVariable\.[0-9a-z]+\.woff2\)/, () => `url(data:font/woff2;base64,${regular.toString('base64')})`);
const liveCss = [site, await text('src/Pegasus.Web/wwwroot/css/case-workspace.css')].join('\n');
const designCss = await local('lib/repair-spec-v38.css');
const model = (await local('lib/repair-spec-v38-model.mjs')).replace(/export default v38Model;\s*$/, 'window.v38Model = v38Model;\n');
const render = await local('lib/repair-spec-v38-render.js');
const runtime = await local('lib/repair-spec-v38-runtime.js');

const tiles = [];
for (const name of (await readdir(path.join(current, 'captured/images'))).sort()) {
  tiles.push((await readFile(path.join(current, 'captured/images', name))).toString('base64'));
}

function body(html) {
  let b = html.slice(html.indexOf('<body'), html.lastIndexOf('</body>'));
  b = b.slice(b.indexOf('>') + 1);
  b = b.replace(/<script\b[^>]*>[\s\S]*?<\/script>/g, '');
  b = b.replace(/src="\/images\/pegasus-mark-refined-128\.[0-9a-z]+\.png"/g, () => `src="data:image/png;base64,${mark.toString('base64')}"`);
  let n = 0;
  b = b.replace(/src="\/Cases\/[^"]*Download[^"]*"/g, () => {
    const data = tiles[n % Math.max(tiles.length, 1)] ?? '';
    n += 1;
    return `src="data:image/png;base64,${data}"`;
  });
  b = b.replace(/\shref="\/[^"]*"/g, ' href="#"');
  b = b.replace(/\saction="[^"]*"/g, ' action="#"');
  return b;
}

const templates = [];
for (const mode of ['read', 'edit']) {
  const html = await local(`captured/frame-engineer-${mode}.html`);
  if (!html.includes('data-case-record')) throw new Error(`frame-engineer-${mode}.html has no Case record.`);
  templates.push(`<template id="frame-engineer-${mode}">${body(html)}</template>`);
}

const file = 'pegasus_repair_spec_v38.html';
await writeFile(path.join(current, file), `<!doctype html><html lang="en-GB"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline'; script-src 'unsafe-inline'; form-action 'none'; base-uri 'none'"><title>v38 · Repair Spec</title><style>${liveCss}\n${designCss}</style></head><body><div id="v38-frame"></div>
${templates.join('\n')}
<script>window.mockupErrors=[];window.addEventListener('error',event=>window.mockupErrors.push(event.message));</script><script>${model}</script><script>${render}</script><script>${runtime}</script></body></html>`);
console.log(`Built ${file} with ${templates.length} frames and ${tiles.length} tile images.`);
