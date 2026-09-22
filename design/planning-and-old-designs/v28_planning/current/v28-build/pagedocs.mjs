// Writes the README.md of every page folder under ../../pages from the capture
// itself (captured.json, shots.json, the v28-shots listing), so a page's list of
// states, routes and screenshots cannot disagree with what was captured.
// how-it-works.md files are written by hand from the live source and are left alone.
//   node pagedocs.mjs
import { readFileSync, writeFileSync, readdirSync, existsSync } from 'node:fs';
import { dirname, resolve, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const current = resolve(here, '..');
const pagesRoot = resolve(current, '..', 'pages');
const manifest = JSON.parse(readFileSync(join(here, 'manifest.json'), 'utf8'));
const captured = JSON.parse(readFileSync(join(here, 'captured.json'), 'utf8'));
const presets = JSON.parse(readFileSync(join(here, 'shots.json'), 'utf8'));
const pages = JSON.parse(readFileSync(join(here, 'pages.json'), 'utf8'));
const shots = readdirSync(join(current, 'v28-shots'));
const familyFile = Object.fromEntries(manifest.families.map((family) => [family.key, family.file]));
const byId = Object.fromEntries(captured.map((state) => [state.id, state]));

const shotLinks = (name, up) => [1580, 1440, 760].map((width) => {
  const file = shots.find((shot) => new RegExp(`^s\\d+-${name}-${width}\\.png$`).test(shot));
  return file ? `[${width}](${up}current/v28-shots/${file})` : `${width} missing`;
}).join(' · ');

for (const page of pages) {
  const folder = join(pagesRoot, page.folder);
  const up = relative(folder, resolve(current, '..')).replace(/\\/g, '/') + '/';
  const lines = [`# ${page.title}`, ''];
  if (page.parent) lines.push(`- **Parent:** [${page.parent.title}](${page.parent.link})`);
  lines.push(`- **Live source:** ${page.source.map((file) => '`' + file + '`').join(', ')}`);
  if (existsSync(join(folder, 'how-it-works.md'))) lines.push('- [**How it works**](how-it-works.md)');
  for (const child of page.children || []) lines.push(`- [${child.title}](${child.link})`);
  lines.push('');
  if (page.intro) lines.push(page.intro, '');

  const rows = [];
  for (const id of page.states || []) {
    const state = byId[id];
    if (!state) throw new Error(`${page.folder}: state ${id} was not captured`);
    const frame = familyFile[state.family];
    rows.push(`| ${state.label} | \`${state.live}\` | [frame](${up}current/${frame}#${id}) · [page](${up}current/states/${id}.html) | ${shotLinks(id, up)} |`);
  }
  for (const name of page.presets || []) {
    const preset = presets.find((item) => item.name === name);
    if (!preset) throw new Error(`${page.folder}: preset ${name} is not in shots.json`);
    const state = byId[preset.state];
    const frame = familyFile[state.family];
    rows.push(`| ${preset.label} (on "${state.label}") | \`${state.live}\` | [frame](${up}current/${frame}#${preset.state}?${preset.query}) | ${shotLinks(name, up)} |`);
  }
  if (rows.length) {
    lines.push('## Captured states', '',
      'Each state is the running application\'s own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.', '',
      '| State | Live route | Open | Screenshots |', '| --- | --- | --- | --- |', ...rows, '');
  } else {
    lines.push('## Captured states', '', 'None. See below.', '');
  }
  if (page.notCaptured && page.notCaptured.length) {
    lines.push('## Not captured', '', ...page.notCaptured.map((item) => `- ${item}`), '');
  }
  writeFileSync(join(folder, 'README.md'), lines.join('\n'));
  console.log('page', page.folder, rows.length, 'rows');
}
