// Stage 1 only: the offline v34 proposal for the Case page's Valuation
// section as guide cards. The frame around the section is the Case page as
// the application rendered it (captured/, from the v33 round), with the live
// site.css and case-workspace.css read from this checkout. Run
// `node build-valuation-v34.mjs`, then `python check-valuation-v34.py`.
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { presets, options, defaults } from './lib/valuation-v34-fixtures.mjs';

const current = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(current, '../../../../');
const text = (relative) => readFile(path.join(repo, relative), 'utf8');
const bytes = (relative) => readFile(path.join(repo, relative));
const local = (relative) => readFile(path.join(current, relative), 'utf8');
const esc = (value) => String(value)
  .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');

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
const designCss = await local('lib/valuation-v34.css');
const runtime = await local('lib/valuation-v34-runtime.js');

// The captured page as the mockup's frame: scripts out, the brand mark
// inlined, every section but Valuation collapsed to its head, and the
// Valuation section emptied to its head for the design to fill.
function frame(html) {
  let body = html.slice(html.indexOf('<body>') + 6, html.lastIndexOf('</body>'));
  body = body.replace(/<script\b[^>]*>[\s\S]*?<\/script>/g, '');
  body = body.replace(/src="\/images\/pegasus-mark-refined-128\.[0-9a-z]+\.png"/, () => `src="data:image/png;base64,${mark.toString('base64')}"`);
  body = body.replace(/<section class="(record-section[^"]*)" id="section-(?!valuation")/g, '<section class="$1 is-collapsed" id="section-');
  const start = body.lastIndexOf('<section', body.indexOf('id="section-valuation"'));
  const end = body.lastIndexOf('<section', body.indexOf('id="section-estimate"'));
  const section = body.slice(start, end);
  const opening = '<div class="panel-body stack">';
  const head = section.slice(0, section.indexOf(opening) + opening.length);
  return `${body.slice(0, start)}${head}</div>\n</section>\n${body.slice(end)}`;
}

const frames = {
  edit: frame(await local('captured/frame-edit.html')),
  read: frame(await local('captured/frame-read.html')),
};

const config = { presets, options, defaults };
const file = 'pegasus_case_valuation_v34.html';
const optionPickers = options.map((o) => `<label>${esc(o.label)}<select data-option-picker="${o.key}">${o.values.map(([v, l]) => `<option value="${v}">${esc(l)}</option>`).join('')}</select></label>`).join('');
const controls = `<details class="v34-mock" open><summary>Mockup controls · v34 Valuation</summary><div class="v34-mock-body"><label>Page state<select data-state-picker>${presets.map(([id, label]) => `<option value="${id}">${esc(label)}</option>`).join('')}</select></label>${optionPickers}<p>An offline proposal using synthetic data. Changes save a moment after they are made, as on the live page, and change only this page.</p><a class="plain" href="${file}" data-strip-reset>Reset</a></div></details>`;
await writeFile(path.join(current, file), `<!doctype html><html lang="en-GB"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline'; script-src 'unsafe-inline'; form-action 'none'; base-uri 'none'"><title>v34 · Valuation as guide cards</title><style>${liveCss}\n${designCss}</style></head><body><div id="v34-frame" class="v34-frame"></div>
<template id="tpl-frame-edit">${frames.edit}</template>
<template id="tpl-frame-read">${frames.read}</template>
<div id="v34-live" class="sr-only" role="status" aria-live="polite"></div>${controls}<script>window.valuationDesign=${JSON.stringify(config).replaceAll('<', '\\u003c')};window.mockupErrors=[];window.addEventListener('error',event=>window.mockupErrors.push(event.message));</script><script>${runtime}</script></body></html>`);
console.log(`Built ${file}.`);
