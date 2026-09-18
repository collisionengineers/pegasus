// v28 self-check. The capture is checked with the proposals layer off; the
// proposals are then checked, per state, with it on. Prints RESULT {"fail":[...],"okCount":N} and exits non-zero on any failure.
//
//   node selfcheck.mjs                 offline checks only
//   LIVE=1 HOST_INFO=<host.json> node selfcheck.mjs
//                                       also compares every directly addressable state
//                                       with the running application, element for element
//
// Offline, per state file: loads with no console error or exception; carries the
// baseline banner and the shim; references no server-absolute stylesheet, script
// or image; every route it links to either resolves to a captured state or is
// counted as not captured (reported, not failed: the application has more pages
// than any capture).
//
// Live parity, per state captured by plain navigation: the structural signature
// (every element's tag and classes with its two ancestors) and every heading,
// label, column header, term and button text of the captured page must equal
// those of the live page. Digits are masked because clocks and ages move.
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { launch, sleep } from './cdp.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const current = resolve(here, '..');
const manifest = JSON.parse(readFileSync(join(here, 'manifest.json'), 'utf8'));
const captured = JSON.parse(readFileSync(join(here, 'captured.json'), 'utf8'));
const hostInfo = process.env.HOST_INFO ? JSON.parse(readFileSync(resolve(process.env.HOST_INFO), 'utf8')) : {};
const fill = (text) => text.replace(/\{\{(\w+)\}\}/g, (_, key) => hostInfo[key] ?? '');

const fail = [];
let okCount = 0;
const check = (ok, message) => { if (ok) okCount += 1; else fail.push(message); };

const SIGNATURE = `(async () => {
  for (const section of document.querySelectorAll('.record-section')) { section.scrollIntoView(); await new Promise((r) => setTimeout(r, 350)); }
  await new Promise((r) => setTimeout(r, 600));
  const root = document.body;
  const sig = (el) => el.tagName.toLowerCase() + [...el.classList].filter((c) => !/^is-|^has-/.test(c)).sort().map((c) => '.' + c).join('');
  const chains = new Set();
  const skip = (el) => el.tagName === 'SCRIPT' || el.hasAttribute('data-working-set') || el.closest('[data-working-set]') || el.closest('[data-toast-region]');
  const walk = (el, parents) => {
    if (skip(el)) return;
    const s = sig(el);
    chains.add([...parents.slice(-2), s].join(' > '));
    for (const child of el.children) walk(child, [...parents, s]);
  };
  for (const child of root.children) walk(child, []);
  const texts = new Set();
  root.querySelectorAll('h1,h2,h3,h4,label,th,dt,legend,button,summary,a.btn,option').forEach((el) => {
    if (skip(el)) return;
    const t = (el.textContent || '').replace(/\\s+/g, ' ').trim().replace(/[0-9]/g, '#');
    if (t) texts.add(el.tagName.toLowerCase() + ' | ' + t);
  });
  return JSON.stringify({ chains: [...chains].sort(), texts: [...texts].sort() });
})()`;

const page = await launch();
const routes = (() => {
  const source = readFileSync(join(current, 'assets', 'mock', 'routes.js'), 'utf8');
  return JSON.parse(source.slice(source.indexOf('{'), source.lastIndexOf('}') + 1));
})();
const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/g;
const keyOf = (pathAndQuery) => {
  const url = new URL(pathAndQuery.replace(/&amp;/g, '&'), 'https://x');
  const params = [...url.searchParams.entries()].map(([k, v]) => [k.toLowerCase(), v.toLowerCase()]).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0));
  const query = params.map(([k, v]) => `${k}=${v}`).join('&');
  return url.pathname.replace(/\/+$/, '').toLowerCase() + (query ? '?' + query : '');
};
const resolves = (live) => {
  const key = keyOf(live);
  const pathOnly = key.split('?')[0];
  return routes.exact[key] || routes.exact[pathOnly] || routes.shaped[pathOnly.replace(GUID, '{id}')] || null;
};

const uncaptured = new Map();
const files = readdirSync(join(current, 'states')).filter((name) => name.endsWith('.html'));
check(files.length === captured.length, `states folder has ${files.length} files but captured.json lists ${captured.length}`);

for (const state of captured) {
  const file = join(current, 'states', state.id + '.html');
  const html = readFileSync(file, 'utf8');
  check(html.includes(`v28 baseline state "${state.id}"`), `${state.id}: baseline banner missing`);
  check(html.includes('../assets/mock/shim.js'), `${state.id}: shim missing`);
  const absolute = [...html.matchAll(/<(?:link|script|img)\b[^>]*?\s(?:href|src)="(\/[^"]*)"/gi)].map((m) => m[1]);
  check(absolute.length === 0, `${state.id}: server-absolute asset left in page: ${absolute.slice(0, 3).join(', ')}`);
  check(!/Visual![0-9A-F]{8}/.test(html), `${state.id}: fixture credential present in page`);
  for (const match of html.matchAll(/<a\b[^>]*?\shref="(\/[^"#]*)[^"]*"/gi)) {
    if (/\/Download(\?|$)|^\/Received\/[^/]+\/(Source|Asset|Image)/i.test(match[1])) continue; // file responses, not pages
    if (!resolves(match[1])) {
      const shape = keyOf(match[1]).split('?')[0].replace(GUID, '{id}');
      uncaptured.set(shape, (uncaptured.get(shape) || 0) + 1);
    }
  }
  const before = page.errors.length;
  await page.goto(pathToFileURL(file).href + '?proposals=off', 700);
  const errors = page.errors.slice(before);
  check(errors.length === 0, `${state.id}: page error offline: ${errors[0]}`);
  const untouched = await page.eval(`!document.documentElement.hasAttribute('data-v28-proposals') && !document.querySelector('[data-v28-removed], [data-v28-proposal], [data-v28-live-tone]')`);
  check(untouched, `${state.id}: the baseline view carries proposal changes`);

  // The same state with the proposals layer on.
  const beforeLayer = page.errors.length;
  await page.goto(pathToFileURL(file).href + '?proposals=on', 700);
  const layerErrors = page.errors.slice(beforeLayer);
  check(layerErrors.length === 0, `${state.id}: page error with proposals on: ${layerErrors[0]}`);
  const facts = JSON.parse(await page.eval(`JSON.stringify((() => {
    const visible = (el) => !!el && !el.closest('[hidden]') && el.getClientRects().length > 0;
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    let provider = ''; let sept = ''; let node;
    while ((node = walker.nextNode())) {
      const tag = node.parentNode.nodeName;
      if (tag === 'SCRIPT' || tag === 'STYLE') continue;
      if (/provider/i.test(node.nodeValue)) provider = provider || node.nodeValue.trim().slice(0, 60);
      if (/\\bSept\\b/.test(node.nodeValue)) sept = sept || node.nodeValue.trim().slice(0, 60);
    }
    const chips = [...document.querySelectorAll('.status')].map((c) => [c.textContent.replace(/\\s+/g, ' ').trim().toLowerCase(), c.className]);
    return {
      on: document.documentElement.getAttribute('data-v28-proposals') === 'on',
      provider, sept,
      lockup: [...document.querySelectorAll('.brand img, .auth-brand img')].filter((i) => !/pegasus-mark-refined/.test(i.src)).length,
      stepper: [...document.querySelectorAll('.stepper')].filter(visible).length,
      greyCreated: chips.filter(([t, c]) => t === 'case created' && !/status--green/.test(c)).length,
      failedNotRed: chips.filter(([t, c]) => /failed|could not be read|not created/.test(t) && !/status--red/.test(c)).length,
      marks: document.querySelectorAll('#section-damage .damage-marks .dm-area').length,
      rows: document.querySelectorAll('#section-damage [data-damage-impact-list] [data-damage-row]').length,
      liveMarkers: [...document.querySelectorAll('#section-damage .damage-marker')].filter(visible).length,
      navless: !!document.querySelector('.external-shell .auth-card'),
      updatedOutsidePanels: [...document.querySelectorAll('[data-wc-freshness]')].filter((l) => visible(l) && !l.closest('.panel-head, .pane-head') && /^\\s*Updated/.test(l.textContent)).length,
      hubIconMismatch: (() => { const nav = {}; document.querySelectorAll('.admin-nav a[href]').forEach((a) => { const u = a.querySelector('use'); if (u) nav[a.getAttribute('href')] = u.getAttribute('href'); });
        return [...document.querySelectorAll('.admin-layout a[href]')].filter((a) => !a.closest('.admin-nav') && a.querySelector('use') && nav[a.getAttribute('href')] && a.querySelector('use').getAttribute('href') !== nav[a.getAttribute('href')]).length; })(),
    };
  })())`));
  check(facts.on, `${state.id}: proposals layer did not load`);
  check(!facts.provider, `${state.id}: P3 the word provider is still shown: "${facts.provider}"`);
  check(!facts.sept, `${state.id}: P6-G "Sept" is still shown: "${facts.sept}"`);
  check(facts.lockup === 0, `${state.id}: P1 a brand slot still shows the old lockup`);
  check(facts.stepper === 0, `${state.id}: P4 the lifecycle strip is still visible`);
  check(facts.greyCreated === 0, `${state.id}: P2 a Case created chip is not green`);
  check(facts.failedNotRed === 0, `${state.id}: P2 a failed outcome chip is not red`);
  check(facts.marks === facts.rows && facts.liveMarkers === 0, `${state.id}: P5 damage marks (${facts.marks}) and rows (${facts.rows}) disagree, or live markers show (${facts.liveMarkers})`);
  check(facts.updatedOutsidePanels === 0, `${state.id}: P6-E a second Updated line is still visible`);
  check(facts.hubIconMismatch === 0, `${state.id}: P6-H a hub icon differs from the nav's`);
  if (/^access-denied/.test(state.id)) check(facts.navless, `${state.id}: P6-D Access denied is not in the navless frame`);
  if (state.id === 'case-record' || state.id === 'case-record-editing') check(facts.marks >= 1, `${state.id}: P5 no damage area drawn`);
}

// Every preset must find its target in the state it names.
const presets = JSON.parse(readFileSync(join(here, 'shots.json'), 'utf8'));
for (const preset of presets) {
  const html = readFileSync(join(current, 'states', preset.state + '.html'), 'utf8');
  const params = new URLSearchParams(preset.query);
  if (params.get('dialog')) check(html.includes(`data-dialog-open="${params.get('dialog')}"`), `${preset.name}: dialog opener not in ${preset.state}`);
  if (params.get('scroll')) check(html.includes(`id="${params.get('scroll')}"`), `${preset.name}: scroll target not in ${preset.state}`);
}

// Every family file exists and names only captured states.
for (const family of manifest.families) {
  const frame = readFileSync(join(current, family.file), 'utf8');
  const ids = [...frame.matchAll(/<option value="([^"]+)" data-(?:live|query)/g)].map((m) => m[1]);
  check(ids.length > 0, `${family.file}: no states`);
  for (const id of new Set(ids)) check(files.includes(id + '.html'), `${family.file}: names missing state ${id}`);
}

let compared = 0;
if (process.env.LIVE) {
  for (const state of manifest.states) {
    if (state.steps || state.dom || state.base) continue; // reached by a post or a sign-in; not addressable by URL alone
    const liveUrl = manifest.bases.default + fill(state.path);
    await page.goto(liveUrl, 1300);
    const live = JSON.parse(await page.eval(SIGNATURE));
    await page.goto(pathToFileURL(join(current, 'states', state.id + '.html')).href + '?proposals=off', 900);
    const mock = JSON.parse(await page.eval(SIGNATURE));
    const diff = (a, b) => a.filter((item) => !b.includes(item));
    const missing = [...diff(live.chains, mock.chains), ...diff(live.texts, mock.texts)];
    const extra = [...diff(mock.chains, live.chains), ...diff(mock.texts, live.texts)];
    check(missing.length === 0, `${state.id}: ${missing.length} live items absent from capture, e.g. ${missing.slice(0, 3).join(' ;; ')}`);
    check(extra.length === 0, `${state.id}: ${extra.length} capture items absent from live, e.g. ${extra.slice(0, 3).join(' ;; ')}`);
    compared += 1;
  }
}
await page.close();

const report = {
  states: captured.length,
  presets: presets.length,
  liveCompared: compared,
  routesLinkedButNotCaptured: Object.fromEntries([...uncaptured.entries()].sort()),
};
writeFileSync(join(here, 'selfcheck-report.json'), JSON.stringify(report, null, 1) + '\n');
console.log(JSON.stringify(report, null, 1));
console.log('RESULT ' + JSON.stringify({ fail, okCount }));
process.exit(fail.length ? 1 : 0);
