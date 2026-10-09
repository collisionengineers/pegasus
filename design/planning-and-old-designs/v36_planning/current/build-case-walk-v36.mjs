// Stage 1 only: the offline v36 mockup of the whole Case page, today and as
// proposed. The frames are the Case page as the running synthetic host
// rendered it on 9 October 2026 with its scripts run (captured/), restyled
// with the live site.css and case-workspace.css from this checkout; the
// runtime draws the proposal over them. Run `node build-case-walk-v36.mjs`,
// then `python check-case-walk-v36.py`.
import { readFile, writeFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const current = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(current, '../../../../');
const text = (relative) => readFile(path.join(repo, relative), 'utf8');
const bytes = (relative) => readFile(path.join(repo, relative));
const local = (relative) => readFile(path.join(current, relative), 'utf8');

const [regular, italic, mark] = await Promise.all([
  bytes('src/Pegasus.Web/wwwroot/fonts/inter/InterVariable.woff2'),
  bytes('src/Pegasus.Web/wwwroot/fonts/inter/InterVariable-Italic.woff2'),
  bytes('src/Pegasus.Web/wwwroot/images/pegasus-mark-refined-128.png'),
]);
const site = (await text('src/Pegasus.Web/wwwroot/css/site.css'))
  .replace(/url\(\.\.\/fonts\/inter\/InterVariable\.[0-9a-z]+\.woff2\)/, () => `url(data:font/woff2;base64,${regular.toString('base64')})`)
  .replace('url(../fonts/inter/InterVariable-Italic.woff2)', () => `url(data:font/woff2;base64,${italic.toString('base64')})`);
const liveCss = [
  site,
  await text('src/Pegasus.Web/wwwroot/css/case-workspace.css'),
  await text('src/Pegasus.Web/wwwroot/css/work-centre.css'),
  await text('src/Pegasus.Web/wwwroot/css/cases-index.css'),
].join('\n');
const designCss = await local('lib/case-walk-v36.css');
const findings = await local('lib/findings-v36.js');
const runtime = await local('lib/case-walk-v36-runtime.js');

// The tile images the host served, inlined in order of appearance.
const tiles = [];
for (const name of (await readdir(path.join(current, 'captured/images'))).sort()) {
  tiles.push((await readFile(path.join(current, 'captured/images', name))).toString('base64'));
}

// Frames: state → mode → captured file.
const FRAMES = {
  engineer: { read: 'frame-engineer-read.html', edit: 'frame-engineer-edit.html' },
  review: { read: 'frame-review-read.html' },
  held: { read: 'frame-held-read.html' },
  colleague: { read: 'frame-colleague-read.html' },
  notready: { read: 'frame-notready-read.html' },
  views: { read: 'frame-views-read.html', edit: 'frame-views-edit.html' },
  viewsinsp: { read: 'frame-viewsinsp-read.html' },
};
const PAGES = { 'work-centre': 'frame-work-centre.html', cases: 'frame-cases-list.html' };

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
  // Links and forms go nowhere offline.
  b = b.replace(/\shref="\/[^"]*"/g, ' href="#"');
  b = b.replace(/\saction="[^"]*"/g, ' action="#"');
  return b;
}

const templates = [];
for (const [state, modes] of Object.entries(FRAMES)) {
  for (const [mode, file] of Object.entries(modes)) {
    const html = await local(`captured/${file}`);
    if (!html.includes('data-case-record')) throw new Error(`${file} has no Case record.`);
    templates.push(`<template id="frame-${state}-${mode}">${body(html)}</template>`);
  }
}
for (const [page, file] of Object.entries(PAGES)) {
  templates.push(`<template id="page-${page}">${body(await local(`captured/${file}`))}</template>`);
}

const file = 'pegasus_case_walk_v36.html';
await writeFile(path.join(current, file), `<!doctype html><html lang="en-GB"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline'; script-src 'unsafe-inline'; form-action 'none'; base-uri 'none'"><title>v36 · Case page walk</title><style>${liveCss}\n${designCss}</style></head><body><div id="v36-frame"></div>
${templates.join('\n')}
<script>window.mockupErrors=[];window.addEventListener('error',event=>window.mockupErrors.push(event.message));</script><script>${findings}</script><script>${runtime}</script></body></html>`);
console.log(`Built ${file} with ${templates.length} frames and ${tiles.length} tile images.`);
