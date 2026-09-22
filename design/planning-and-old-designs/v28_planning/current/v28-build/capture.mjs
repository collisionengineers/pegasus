// v28 capture: saves real server-rendered Pegasus pages as offline state files.
//
//   node capture.mjs [manifest.json]
//
// For every state in the manifest the script drives a running Pegasus.Web host
// (the synthetic, locally seeded one; never production), performs the listed
// steps, and then saves the server's own HTML for the page the browser ended
// on. Nothing is transcribed by hand: markup, wording, gating and formats are
// whatever the application rendered. Static assets (CSS, JS, fonts, images) are
// downloaded once into ../assets and referenced relatively, so the result works
// offline from the file system. The only addition to each page is one script,
// ../assets/mock/shim.js, which routes clicks and form posts between captured
// states because there is no server behind the files.
import { mkdirSync, writeFileSync, readFileSync, existsSync, rmSync } from 'node:fs';
import { dirname, resolve, join, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { launch, sleep } from './cdp.mjs';

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'; // loopback host with a development certificate

const here = dirname(fileURLToPath(import.meta.url));
const current = resolve(here, '..');
const manifestPath = resolve(process.argv[2] || join(here, 'manifest.json'));
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
// HOST_INFO: the JSON the local fixture host writes (ids of the seeded records).
const hostInfo = process.env.HOST_INFO ? JSON.parse(readFileSync(resolve(process.env.HOST_INFO), 'utf8')) : {};
// AUTH_INFO: the JSON the local sign-in fixture writes (a throwaway user's credentials).
if (process.env.AUTH_INFO) {
  const auth = JSON.parse(readFileSync(resolve(process.env.AUTH_INFO), 'utf8'));
  hostInfo.authUserName = auth.credentials.userName;
  hostInfo.authPassword = auth.credentials.password;
}
const only = process.env.ONLY ? new Set(process.env.ONLY.split(',')) : null;

const statesDir = join(current, 'states');
const assetsDir = join(current, 'assets');
if (!only) { rmSync(statesDir, { recursive: true, force: true }); }
mkdirSync(statesDir, { recursive: true });
mkdirSync(join(assetsDir, 'mock'), { recursive: true });

const fill = (text) => text.replace(/\{\{(\w+)\}\}/g, (_, key) => {
  if (!(key in hostInfo)) throw new Error(`manifest placeholder {{${key}}} is not in host info`);
  return hostInfo[key];
});

// ---- assets ---------------------------------------------------------------
const assetMap = new Map(); // live url path -> relative path from states/
const FINGERPRINT = /\.[a-z0-9]{10}(\.[a-z0-9]+)$/i;

async function saveAsset(base, urlPath) {
  if (assetMap.has(urlPath)) return assetMap.get(urlPath);
  const clean = urlPath.split('?')[0];
  const isStatic = /^\/(css|js|fonts|images|favicon)/.test(clean);
  let relative;
  const response = await fetch(base + urlPath.replace(/&amp;/g, '&'));
  if (!response.ok) { assetMap.set(urlPath, null); return null; }
  const bytes = Buffer.from(await response.arrayBuffer());
  if (isStatic) {
    relative = 'assets' + clean.replace(FINGERPRINT, '$1');
  } else {
    // Application-served media (case images, thumbnails, documents).
    const type = response.headers.get('content-type') || '';
    const ext = type.includes('png') ? '.png' : type.includes('jpeg') ? '.jpg' : type.includes('pdf') ? '.pdf'
      : type.includes('webp') ? '.webp' : type.includes('svg') ? '.svg' : (extname(clean) || '.bin');
    relative = 'assets/media/' + createHash('sha256').update(bytes).digest('hex').slice(0, 16) + ext;
  }
  const target = join(current, relative);
  mkdirSync(dirname(target), { recursive: true });
  let output = bytes;
  if (relative.endsWith('.css')) {
    // Pull in what the stylesheet itself references (fonts, images).
    const css = bytes.toString('utf8');
    for (const match of css.matchAll(/url\(\s*['"]?([^'")]+)['"]?\s*\)/g)) {
      const ref = match[1];
      if (ref.startsWith('data:') || ref.startsWith('#')) continue;
      const absolute = new URL(ref, base + clean).pathname;
      await saveAsset(base, absolute);
    }
  }
  writeFileSync(target, output);
  assetMap.set(urlPath, '../' + relative);
  return '../' + relative;
}

async function rewriteAssets(base, html) {
  const attr = /(<(?:link|script|img|source|video|iframe|embed|object)\b[^>]*?\s(?:href|src|data-src|poster)=")(\/[^"]*)(")/gi;
  const found = new Set();
  for (const match of html.matchAll(attr)) found.add(match[2]);
  // Image tiles and viewers also carry application URLs in data attributes.
  const dataAttr = /\s(data-(?:full|src|image|preview|thumb|original|viewer-src|evidence-src|href-image)[a-z-]*)="(\/[^"]*)"/gi;
  for (const match of html.matchAll(dataAttr)) found.add(match[2]);
  for (const urlPath of found) await saveAsset(base, urlPath);
  const swap = (value) => assetMap.get(value) || value;
  html = html.replace(attr, (_, head, value, tail) => head + swap(value) + tail);
  html = html.replace(dataAttr, (_, name, value) => ` ${name}="${swap(value)}"`);
  return html;
}

// ---- capture --------------------------------------------------------------
const routeKey = (pathAndQuery) => {
  const url = new URL(pathAndQuery, 'https://x');
  const params = [...url.searchParams.entries()].sort(([a], [b]) => a.localeCompare(b));
  const query = params.map(([k, v]) => `${k.toLowerCase()}=${v.toLowerCase()}`).join('&');
  return url.pathname.replace(/\/+$/, '').toLowerCase() + (query ? '?' + query : '');
};

const page = await launch();
const captured = [];
const failures = [];

for (const state of manifest.states) {
  if (only && !only.has(state.id)) continue;
  const base = manifest.bases[state.base || 'default'];
  try {
    await page.goto(base + fill(state.path));
    for (const step of state.steps || []) {
      try { await page.eval(fill(step)); } catch (error) {
        // A step that navigates tears down the execution context; that is expected.
        if (!/context|navigat|destroyed|Cannot find/i.test(String(error))) throw error;
      }
      await sleep(state.settle || 1500);
    }
    const location = await page.eval('location.pathname + location.search');
    let html;
    if (state.dom) {
      html = '<!DOCTYPE html>\n' + await page.eval('document.documentElement.outerHTML');
    } else {
      html = await page.eval(`fetch(location.href, { credentials: 'include', headers: { Accept: 'text/html' } }).then((r) => r.text())`);
      // Mount deferred record sections exactly as case-workspace.js would.
      const lazy = [...html.matchAll(/<section\b[^>]*\bdata-lazy="([^"]+)"[^>]*>\s*<\/section>/g)];
      for (const match of lazy) {
        const fragment = await page.eval(`(async () => {
          const lease = document.querySelector('input[name="editLeaseToken"]');
          const headers = { Accept: 'text/html' };
          if (lease && lease.value) headers['X-Pegasus-Edit-Lease'] = lease.value;
          const response = await fetch(location.pathname.replace(/\\/+$/, '') + '/Section?section=${match[1]}', { credentials: 'same-origin', headers });
          return response.ok ? response.text() : '';
        })()`);
        const section = fragment && fragment.match(/<section\b[\s\S]*<\/section>/);
        if (!section) { failures.push(`${state.id}: deferred section ${match[1]} did not load`); continue; }
        html = html.replace(match[0], () => section[0]);
      }
    }
    if (state.expect && !html.includes(fill(state.expect))) {
      failures.push(`${state.id}: expected text not present: ${fill(state.expect)}`);
    }
    if (hostInfo.authPassword) html = html.split(hostInfo.authPassword).join('');
    html = await rewriteAssets(base, html);
    const banner = `<!--\n  v28 baseline state "${state.id}". Captured from a running Pegasus.Web at ${manifest.source}\n`
      + `  (synthetic local fixture data). This is the server's own HTML for ${location.replace(/--/g, '- -')}.\n`
      + `  It is a design review artifact, not application code and not design authority.\n-->\n`;
    html = html.replace(/<head>/i, () => `<head>\n<script>window.V28_STATE=${JSON.stringify({ id: state.id, family: state.family, live: location })};</script>\n`
      + `<script src="../assets/mock/routes.js"></script>\n<script src="../assets/mock/shim.js"></script>`);
    html = html.replace(/^<!DOCTYPE html>\s*/i, '<!DOCTYPE html>\n' + banner);
    writeFileSync(join(statesDir, state.id + '.html'), html);
    for (const step of state.after || []) {
      try { await page.eval(fill(step)); } catch { /* navigation */ }
      await sleep(1500);
    }
    captured.push({ ...state, live: location, key: routeKey(location) });
    console.log('captured', state.id, '<-', location);
  } catch (error) {
    failures.push(`${state.id}: ${error.message}`);
    console.log('FAILED  ', state.id, error.message);
  }
}
await page.close();

// ---- routes ---------------------------------------------------------------
if (!only) {
  const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/g;
  const exact = {};
  const shaped = {};
  for (const state of captured) {
    if (state.route === false) continue;
    if (!(state.key in exact)) exact[state.key] = state.id;
    const pathOnly = state.key.split('?')[0];
    if (!(pathOnly in exact) && state.primary) exact[pathOnly] = state.id;
    const shape = pathOnly.replace(GUID, '{id}');
    if (!(shape in shaped) || state.primary) shaped[shape] = state.id;
  }
  const transitions = {};
  for (const state of captured) {
    if (state.transitions) transitions[state.id] = state.transitions;
  }
  const families = manifest.families.map((family) => ({
    ...family,
    states: captured.filter((s) => s.family === family.key).map((s) => ({ id: s.id, label: s.label, live: s.live, note: s.note || '' })),
  }));
  writeFileSync(join(assetsDir, 'mock', 'routes.js'),
    '// Generated by v28-build/capture.mjs. Live route -> captured state.\n'
    + 'window.V28_ROUTES = ' + JSON.stringify({ exact, shaped, transitions, families }, null, 1) + ';\n');
  writeFileSync(join(here, 'captured.json'), JSON.stringify(captured.map(({ id, family, label, live, note }) => ({ id, family, label, live, note })), null, 1) + '\n');
}

console.log(`\n${captured.length} states captured, ${failures.length} problems`);
for (const failure of failures) console.log('  PROBLEM', failure);
process.exit(failures.length ? 1 : 0);
