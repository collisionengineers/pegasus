// Stage 1 only: five offline proposals for the Case page's Valuation section,
// today's section beside them, and their comparison page. The frame around
// the section is the Case page as the application rendered it (captured/),
// with the live site.css and case-workspace.css read from this checkout
// (origin/dev f5bc6e5f1). Run `node build-valuation-designs.mjs` before and
// after `python check-valuation-designs.py` so the comparison page embeds
// the default screenshots.
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { designs, presets, livePresets, options } from './lib/valuation-fixtures.mjs';

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
  .replace(/url\(\.\.\/fonts\/inter\/InterVariable\.[0-9a-z]+\.woff2\)/, `url(data:font/woff2;base64,${regular.toString('base64')})`)
  .replace('url(../fonts/inter/InterVariable-Italic.woff2)', `url(data:font/woff2;base64,${italic.toString('base64')})`);
const liveCss = `${site}\n${await text('src/Pegasus.Web/wwwroot/css/case-workspace.css')}`;
const designCss = await local('lib/valuation-designs.css');
const runtime = await local('lib/valuation-designs-runtime.js');

// The captured page as the mockup's frame: scripts out, the brand mark
// inlined, every section but Valuation collapsed to its head, and the
// Valuation section emptied to its head for a design to fill.
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

// Today's section as captured. The capture's fixture has no saved values
// and a 2031 test clock, so the build writes the three boxes' figures, the
// head figure a fresh page would show, this round's dates and a staff name.
// The operator's screenshot state keeps its "—" head: that page was not redrawn.
const figures = {
  'read-fetched': ['9064.00', '7347.00', '9064.00'], 'edit-fetched': ['9064.00', '7347.00', '9064.00'],
  'edit-pending': ['9064.00', '7347.00', '9064.00'],
  'read-applied': ['12500.00', '10250.00', '13425.00'], 'edit-applied': ['12500.00', '10250.00', '13425.00'],
};
const pounds = (value) => `&#xA3;${Number(value).toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
function today(fragment, name) {
  let html = fragment
    .replaceAll('2031-05', '2026-10').replaceAll('6 May 2031', '6 Oct 2026').replaceAll('May 2031', 'Oct 2026')
    .replaceAll('Unknown user', 'alex');
  const values = figures[name];
  if (!values) return html;
  for (const [index, kind] of ['retail', 'trade', 'engineer'].entries()) {
    html = html.replace('<div class="fv mono empty">Not recorded</div>', () => `<div class="fv mono">${pounds(values[index])}</div>`);
    html = html.replace(`data-valuation-value="${kind}" />`, () => `value="${values[index]}" data-valuation-value="${kind}" />`);
  }
  if (name !== 'edit-fetched') {
    html = html.replace('Engineer&#x27;s Value &#x2014;</span>', () => `Engineer&#x27;s Value ${pounds(values[2])}</span>`);
  }
  return html;
}

const frames = {
  edit: frame(await local('captured/frame-edit.html')),
  read: frame(await local('captured/frame-read.html')),
};
const captured = ['read-empty', 'read-fetched', 'edit-fetched', 'read-applied', 'edit-applied', 'edit-pending'];
const liveTemplates = (await Promise.all(captured.map(async (name) =>
  `<template id="tpl-live-${name}">${today(await local(`captured/valuation-${name}.html`), name)}</template>`))).join('\n');

const filename = (id) => `pegasus_valuation_${id}_v33.html`;
const all = [...designs, { id: 'live', name: 'Today', baseline: true, defaults: {} }];

for (const design of all) {
  const states = design.baseline ? livePresets : presets;
  const config = { id: design.id, name: design.name, presets: states, options: design.baseline ? [] : options, defaults: design.defaults };
  const nav = `<nav aria-label="Design alternatives">${designs.map((o) => `<a href="${filename(o.id)}"${o.id === design.id ? ' aria-current="page"' : ''}>${o.id.toUpperCase()}</a>`).join('')}<a href="${filename('live')}"${design.baseline ? ' aria-current="page"' : ''}>Today</a></nav>`;
  const optionPickers = config.options.map((o) => `<label>${esc(o.label)}<select data-option-picker="${o.key}">${o.values.map(([v, l]) => `<option value="${v}">${esc(l)}</option>`).join('')}</select></label>`).join('');
  const note = design.baseline
    ? 'Today\'s Valuation section as the application rendered it on 6 October 2026. It is a still: figures do not move here.'
    : 'An offline proposal using synthetic data. Changes save a moment after they are made, as on the live page, and change only this page.';
  const controls = `<details class="v33-mock" open><summary>Mockup controls · ${design.baseline ? 'Today' : `${design.id.toUpperCase()} · ${esc(design.name)}`}</summary><div class="v33-mock-body">${nav}<label>Page state<select data-state-picker>${states.map(([id, label]) => `<option value="${id}">${esc(label)}</option>`).join('')}</select></label>${optionPickers}<p>${note}</p><a class="plain" href="pegasus_valuation_designs_v33.html">Compare all five designs</a><a class="plain" href="${filename(design.id)}">Reset</a></div></details>`;
  const title = design.baseline ? 'Today · Valuation' : `${design.id.toUpperCase()} · ${design.name} · Valuation`;
  await writeFile(path.join(current, filename(design.id)), `<!doctype html><html lang="en-GB"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline'; script-src 'unsafe-inline'; form-action 'none'; base-uri 'none'"><title>${esc(title)}</title><style>${liveCss}\n${designCss}</style></head><body><div id="v33-frame" class="v33-frame"></div>
<template id="tpl-frame-edit">${frames.edit}</template>
<template id="tpl-frame-read">${frames.read}</template>
${design.baseline ? liveTemplates : ''}
<div id="v33-live" class="sr-only" role="status" aria-live="polite"></div>${controls}<script>window.valuationDesign=${JSON.stringify(config).replaceAll('<', '\\u003c')};window.mockupErrors=[];window.addEventListener('error',event=>window.mockupErrors.push(event.message));</script><script>${runtime}</script></body></html>`);
}

const cards = [];
for (const [index, design] of designs.entries()) {
  let preview;
  try {
    const image = await readFile(path.join(current, `v33-valuation-shots/0${index + 1}-${design.id}-fetched-1440.png`));
    preview = `<img src="data:image/png;base64,${image.toString('base64')}" alt="${esc(design.name)}: the Valuation section while editing">`;
  } catch (error) {
    if (error.code !== 'ENOENT') throw error;
    preview = `<span>Open the ${esc(design.name)} preview</span>`;
  }
  cards.push(`<article><a class="preview" href="${filename(design.id)}">${preview}</a><div class="option-heading"><span class="letter">${design.id.toUpperCase()}</span><h2>${esc(design.name)}</h2>${design.recommendation ? '<span class="recommendation">Recommended</span>' : ''}</div><p class="lead">${esc(design.subtitle)}</p><p>${esc(design.description)}</p><dl><dt>Why choose it</dt><dd>${esc(design.benefit)}</dd><dt>Tradeoff</dt><dd>${esc(design.tradeoff)}</dd></dl><div class="links"><a class="open-design" href="${filename(design.id)}">Explore design ${design.id.toUpperCase()} →</a><a href="${filename(design.id)}?state=recorded">Recorded</a><a href="${filename(design.id)}?state=recorded-read">Reading</a><a href="${filename(design.id)}?state=own">Own figure</a></div></article>`);
}
await writeFile(path.join(current, 'pegasus_valuation_designs_v33.html'), `<!doctype html><html lang="en-GB"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Five Valuation designs · Pegasus v33</title><style>${site}
body{background:var(--bg)}.review{max-width:1500px;margin:auto;padding:42px 32px}.review-header{border-bottom:1px solid var(--line);padding-bottom:28px}.review-kicker{font-size:10px;text-transform:uppercase;letter-spacing:.12em;color:var(--muted);margin-bottom:12px}.review h1{font-size:29px;letter-spacing:-.04em;font-weight:650;margin:0}.review-intro{max-width:860px;color:var(--muted);line-height:1.7;font-size:13px;margin:12px 0 0}.review-intro a,.review-note a{text-decoration:underline}.common{display:grid;grid-template-columns:repeat(3,1fr);gap:30px;padding:22px 0;border-bottom:1px solid var(--line)}.common h2{font-size:12px;margin:0 0 6px;font-weight:650}.common p{font-size:11.5px;line-height:1.7;color:var(--muted);margin:0}.alternatives{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:24px;margin-top:30px}.alternatives article{display:flex;flex-direction:column;min-width:0}.preview{display:grid;place-items:center;aspect-ratio:1.6;overflow:hidden;border:1px solid var(--line);border-radius:4px;background:var(--surface)}.preview img{width:100%;height:100%;object-fit:cover;object-position:top;display:block}.option-heading{display:flex;align-items:center;gap:9px;margin:22px 0 12px}.letter{display:grid;place-items:center;width:26px;height:26px;border:1px solid var(--line-strong);border-radius:3px;font-size:11px;font-weight:600}.option-heading h2{font-size:18px;font-weight:650;letter-spacing:-.025em;margin:0}.recommendation{margin-left:auto;font-size:10px;font-weight:650;color:var(--red-dark)}.alternatives p{font-size:12px;color:var(--muted);line-height:1.7;margin:0 0 8px}.alternatives .lead{font-size:13px;font-weight:600;color:var(--ink)}.alternatives dl{font-size:12px;line-height:1.7;margin:8px 0 20px}.alternatives dt{font-weight:650;margin-top:10px}.alternatives dd{color:var(--muted);margin:3px 0 0}.links{margin-top:auto;display:flex;flex-wrap:wrap;align-items:center;gap:12px;font-size:11px}.open-design{display:inline-flex;align-items:center;min-height:36px;background:var(--nav);color:#fff;padding:0 14px;border-radius:3px;font-size:12px;font-weight:600;text-decoration:none}.open-design:hover{background:var(--nav-2);color:#fff;text-decoration:none}.review-note{margin-top:30px;border-top:1px solid var(--line);padding-top:22px;font-size:12px;line-height:1.8;color:var(--muted)}.review-note strong{color:var(--ink)}@media(max-width:979px){.alternatives,.common{grid-template-columns:1fr}.review{padding:26px 20px}}
</style></head><body><main class="review"><header class="review-header"><p class="review-kicker">Pegasus · Valuation design review · 6 October 2026</p><h1>One Engineer's Value, beside the calculation that makes it.</h1><p class="review-intro">Five ways to lay out the Valuation section of the Case page, each drawn inside the Case page as the application renders it and from the same synthetic Case. Today the calculation sits at the far end of the section from the Engineer's Value box, the same figure appears again as "Proposed Engineer's Value", and "Applied Engineer's Value" reads "None yet" beside a filled box. <a href="${filename('live')}">Open today's section</a> to compare.</p></header><section class="common" aria-label="Shared by all five"><div><h2>One figure</h2><p>The Engineer's Value box is the only place the figure stands. The calculation fills it, as it does today; there is no second "Proposed" total and no "Applied" block that can disagree with it. The section head follows the box.</p></div><div><h2>A record only when there is one</h2><p>When a calculation has been recorded and the box still holds its figure, the page says so, either as the source's word on the Engineer's Value label or as one line of Basis, Applied by and Adjustments. It appears when the save lands, with no reload, and never reads "None yet".</p></div><div><h2>Nothing new to learn</h2><p>Every label is one the page already uses, every control today has is present, and the rules are today's: no Apply button, no Save on a source, Use this value chooses a source, and typing over the Engineer's Value makes the figure the Engineer's own.</p></div></section><div class="alternatives">${cards.join('')}</div><footer class="review-note"><strong>Recommended: C · Three columns.</strong> It keeps the three values where they were placed on 26 September, puts each one's origin directly beneath it, and is the shortest of the five. A is the smallest change. B reads most like the sum. D ties the value to its source. E puts the whole sum in one pane beside the sources.<br><strong>Try it:</strong> tick Add 20 % VAT and watch the Engineer's Value follow; type a figure over it; press Use this value on Glass's; press Done and read the same section greyed; switch the two strip choices. <a href="v33-notes.md">Read the notes and the lettered sign-off items</a>. These are offline design artifacts and do not establish how the application behaves.</footer></main></body></html>`);
console.log('Built five Valuation designs, today\'s section and the comparison page.');
