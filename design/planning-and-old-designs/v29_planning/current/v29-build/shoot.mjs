// Screenshots of the captured states at the three review breakpoints.
//
//   node shoot.mjs            every state in captured.json plus shots.json extras
//   ONLY=id1,id2 node shoot.mjs
//   WIDTHS=1440 node shoot.mjs
//
// shots.json adds presets over a state: [{ "name", "state", "query" }] where
// query is a shim preset such as "dialog=account-dialog" or "scroll=section-damage".
// Any console error or uncaught exception in a state page fails the run.
import { readFileSync, mkdirSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { launch, sleep } from './cdp.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const current = resolve(here, '..');
const outDir = join(current, 'v29-shots');
mkdirSync(outDir, { recursive: true });

const SIZES = { 1580: 1000, 1440: 900, 760: 1000 };
const widths = (process.env.WIDTHS || '1580,1440,760').split(',').map(Number);
const only = process.env.ONLY ? new Set(process.env.ONLY.split(',')) : null;

const captured = JSON.parse(readFileSync(join(here, 'captured.json'), 'utf8'));
const extras = existsSync(join(here, 'shots.json')) ? JSON.parse(readFileSync(join(here, 'shots.json'), 'utf8')) : [];
// Baseline shots are taken with the proposals layer off. proposal-shots.json
// lists the states that show a proposal; those are taken with it on and are
// named p<nn>-... so the two sets never overwrite each other.
const proposalShots = existsSync(join(here, 'proposal-shots.json')) ? JSON.parse(readFileSync(join(here, 'proposal-shots.json'), 'utf8')) : [];
const which = process.env.SET || 'all'; // all | baseline | proposals
// A shot's number is its place in its own full list, so a filtered run writes
// the same file names as a full one.
const baselineShots = [...captured.map((state) => ({ name: state.id, state: state.id, query: '' })), ...extras]
  .map((shot, index) => ({ ...shot, number: index + 1 }));
const shots = [
  ...(which === 'proposals' ? [] : baselineShots),
  ...(which === 'baseline' ? [] : proposalShots.map((shot, index) => ({ ...shot, proposal: true, number: index + 1 }))),
].filter((shot) => !only || only.has(shot.name));

const page = await launch();
const problems = [];
for (const shot of shots) {
  const number = shot.number;
  const proposalNumber = shot.number;
  const layer = shot.proposal ? 'proposals=on' : 'proposals=off';
  const url = pathToFileURL(join(current, 'states', shot.state + '.html')).href + '?' + layer + (shot.query ? '&' + shot.query : '');
  for (const width of widths) {
    await page.viewport(width, SIZES[width] || 900);
    const before = page.errors.length;
    // Each shot starts from an empty open-records strip, as a fresh browser would.
    await page.goto(url, 300);
    await page.eval(`try { window.localStorage.clear(); window.sessionStorage.clear(); } catch (e) {}`);
    await page.goto(url, 1100);
    const name = shot.proposal
      ? `p${String(proposalNumber).padStart(2, '0')}-${shot.name}-${width}.png`
      : `s${String(number).padStart(2, '0')}-${shot.name}-${width}.png`;
    writeFileSync(join(outDir, name), await page.screenshot());
    for (const error of page.errors.slice(before)) problems.push(`${shot.name} @${width}: ${error}`);
  }
  console.log('shot', shot.name);
}
await page.close();
console.log(`\n${shots.length} shots x ${widths.length} widths, ${problems.length} page errors`);
for (const problem of [...new Set(problems)]) console.log('  ERROR', problem);
process.exit(problems.length ? 1 : 0);
