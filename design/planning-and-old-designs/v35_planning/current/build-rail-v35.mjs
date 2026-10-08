// Stage 1 only: the offline v35 proposals for the Case page's aside
// (Figures and Next action). The frame is the Case page as the application
// rendered it (captured/, from the v33/v34 rounds), with the live site.css and
// case-workspace.css read from this checkout; the runtime replaces the aside.
// Run `node build-rail-v35.mjs`, then `python check-rail-v35.py`.
import { readFile, writeFile } from 'node:fs/promises';
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
  // site.css names the font by its static-assets fingerprint.
  .replace(/url\(\.\.\/fonts\/inter\/InterVariable\.[0-9a-z]+\.woff2\)/, () => `url(data:font/woff2;base64,${regular.toString('base64')})`)
  .replace('url(../fonts/inter/InterVariable-Italic.woff2)', () => `url(data:font/woff2;base64,${italic.toString('base64')})`);
const liveCss = `${site}\n${await text('src/Pegasus.Web/wwwroot/css/case-workspace.css')}`;
const designCss = await local('lib/rail-v35.css');
const runtime = await local('lib/rail-v35-runtime.js');

// The captured page as the frame: scripts out and the brand mark inlined.
let body = await local('captured/frame-read.html');
body = body.slice(body.indexOf('<body>') + 6, body.lastIndexOf('</body>'));
body = body.replace(/<script\b[^>]*>[\s\S]*?<\/script>/g, '');
body = body.replace(/src="\/images\/pegasus-mark-refined-128\.[0-9a-z]+\.png"/, () => `src="data:image/png;base64,${mark.toString('base64')}"`);
if (!body.includes('data-case-aside')) throw new Error('The captured frame has no aside.');

const file = 'pegasus_case_rail_v35.html';
await writeFile(path.join(current, file), `<!doctype html><html lang="en-GB"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline'; script-src 'unsafe-inline'; form-action 'none'; base-uri 'none'"><title>v35 · Case aside: Next action</title><style>${liveCss}\n${designCss}</style></head><body><div id="v35-frame"></div>
<template id="tpl-frame">${body}</template>
<script>window.mockupErrors=[];window.addEventListener('error',event=>window.mockupErrors.push(event.message));</script><script>${runtime}</script></body></html>`);
console.log(`Built ${file}.`);
