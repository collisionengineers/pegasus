// Ends any edit session the fixture user holds on the seeded Case, and reports
// what the record holds. Run after enrich.mjs when a run was interrupted.
//   HOST_INFO=<host.json> node release.mjs
import { readFileSync } from 'node:fs';
import { launch, sleep } from './cdp.mjs';

const info = JSON.parse(readFileSync(process.env.HOST_INFO, 'utf8'));
const page = await launch();
await page.goto(`${info.url}/Cases/${info.caseId}`, 2000);
const state = () => page.eval(`(() => ({
  editing: !!document.querySelector('input[name="editLeaseToken"]'),
  takeOver: [...document.querySelectorAll('button')].some((b) => /Take over|Recover/.test(b.textContent)),
}))()`);
let now = await state();
if (now.takeOver) {
  try { await page.eval(`[...document.querySelectorAll('button')].find((b) => /Take over|Recover/.test(b.textContent)).click()`); } catch { /* navigates */ }
  await sleep(3000);
  now = await state();
}
if (now.editing) {
  try { await page.eval(`document.getElementById('case-finish-editing-form').submit()`); } catch { /* navigates */ }
  await sleep(3000);
}
await page.goto(`${info.url}/Cases/${info.caseId}?section=damage`, 2500);
console.log(JSON.stringify(await state()), 'zones:', await page.eval(`(document.querySelector('[data-damage-count]') || {}).textContent`));
await page.close();
