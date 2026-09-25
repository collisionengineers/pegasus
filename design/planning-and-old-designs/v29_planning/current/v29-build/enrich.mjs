// Works the seeded Case through the application's own edit session so the
// record shows a worked Case rather than an empty one. Everything is entered
// through the live form controls and saved by the live Save button; nothing is
// written to the database directly. Run once against the local fixture host,
// before capture.mjs.
//
//   HOST_INFO=<host.json> node enrich.mjs
import { readFileSync } from 'node:fs';
import { launch, sleep } from './cdp.mjs';

const info = JSON.parse(readFileSync(process.env.HOST_INFO, 'utf8'));
const base = info.url;
const page = await launch();

const helpers = `
  window.v29set = (name, value) => {
    const el = document.querySelector('[name="' + name + '"]');
    if (!el) return 'MISSING ' + name;
    if (el.tagName === 'SELECT') {
      const options = [...el.options].filter((o) => o.value !== '');
      const pick = value === 'FIRST' ? options[0] : value === 'LAST' ? options[options.length - 1]
        : options.find((o) => o.value === value || o.textContent.trim() === value);
      if (!pick) return 'NO OPTION ' + name + ' in [' + options.map((o) => o.value).join(',') + ']';
      el.value = pick.value;
    } else if (el.type === 'checkbox') {
      el.checked = !!value;
    } else {
      el.value = value;
    }
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
    return 'ok ' + name + '=' + el.value;
  };
`;

async function openEdit() {
  await page.goto(`${base}/Cases/${info.caseId}`);
  const editing = await page.eval(`[...document.querySelectorAll('input[name="editLeaseToken"]')].some((i) => i.value)`);
  if (!editing) {
    try { await page.eval(`document.querySelector('form[action*="handler=ClaimLease"] button').click()`); } catch { /* navigates */ }
    await sleep(3000);
  }
  // Mount every deferred section so its controls exist.
  await page.eval(`(async () => { for (const s of document.querySelectorAll('.record-section')) { s.scrollIntoView(); await new Promise((r) => setTimeout(r, 500)); } })()`);
  await sleep(1500);
  await page.eval(helpers);
}

const fields = JSON.parse(readFileSync(new URL(process.env.FIELDS || './enrich-fields.json', import.meta.url), 'utf8'));

// Saved three fields at a time: one save that changes many fields overflows
// CaseHistory.Reason on dev at 904903fd1 and the whole save is refused.
const entries = Object.entries(fields);
for (let i = 0; i < entries.length; i += 3) {
  await openEdit();
  for (const [name, value] of entries.slice(i, i + 3)) {
    console.log(await page.eval(`window.v29set(${JSON.stringify(name)}, ${JSON.stringify(value)})`));
  }
  try {
    await page.eval(`[...document.querySelectorAll('button')].find((b) => b.hasAttribute('data-case-save')).click()`);
  } catch { /* navigates */ }
  await sleep(3500);
  console.log('  saved:', await page.eval(`[...document.querySelectorAll('.notice')].map((e) => e.textContent.replace(/\s+/g, ' ').trim()).filter((t) => /saved|not applied/i.test(t)).join(' || ')`));
}
// Damage: the plan records a zone when the operator clicks it; a second click
// steps the severity. Saved by the same ribbon Save.
await openEdit();
console.log('damage zones clicked:', await page.eval(`(() => {
  const zones = [...document.querySelectorAll('[data-damage-zone]')];
  const pick = (code) => zones.find((z) => z.getAttribute('data-damage-zone') === code) || null;
  const clicked = [];
  if (zones.some((z) => z.hasAttribute('data-sev'))) return 'already recorded';
  for (const [code, times] of [['rear_centre', 2], ['tailgate', 1], ['rear_left_corner', 1]]) {
    const zone = pick(code);
    if (!zone) continue;
    for (let i = 0; i < times; i++) zone.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    clicked.push(code);
  }
  return clicked.join(',') + ' of ' + zones.map((z) => z.getAttribute('data-damage-zone')).join(' ');
})()`));
try { await page.eval(`[...document.querySelectorAll('button')].find((b) => b.hasAttribute('data-case-save')).click()`); } catch { /* navigates */ }
await sleep(3500);

// Valuation: one guide entered by hand in the Glass's card and saved by that card.
await openEdit();
console.log('valuation:', await page.eval(`(() => {
  const save = document.querySelector('[data-valuation-save="glasses"]');
  if (!save) return 'no card';
  if (document.querySelector('#section-valuation [data-valuation-recorded], #section-valuation .guide-card .mono')) return 'already recorded';
  const form = save.closest('form');
  form.querySelector('[name="retailValue"]').value = '9200.00';
  form.querySelector('[name="tradeValue"]').value = '8100.00';
  return 'entered';
})()`));
try { await page.eval(`document.querySelector('[data-valuation-save="glasses"]').click()`); } catch { /* navigates */ }
await sleep(3500);

// Engineer's Value: applied from the recorded guide by the live Apply button,
// so Settlement, the Figures aside and the proposals that read it have a figure.
await openEdit();
console.log('apply:', await page.eval(`(() => {
  const section = document.getElementById('section-valuation');
  const applied = [...section.querySelectorAll('.lbl, dt, span')].some((e) => /Applied Engineer/i.test(e.textContent) && /\\u00a3[0-9]/.test((e.parentElement || e).textContent));
  if (applied) return 'already applied';
  const button = [...section.querySelectorAll('button')].find((b) => /Apply as Engineer/.test(b.textContent));
  if (!button) return 'no Apply button';
  button.click();
  return 'pressed';
})()`));
await sleep(4000);

// Estimates: two hand-entered estimates through the live New estimate editor.
// The application offers Compare only once a Case holds two.
const ESTIMATES = [
  { name: 'Example Bodyshop estimate', days: '4', rate: '48.00', paint: '185.00',
    line: { op: 'Replace', text: 'Rear bumper cover', part: 'EX-1001', qty: '1', pounds: '412.50', labour: '2.5', paintHours: '3.0' } },
  { name: 'Example Bodyshop supplementary', days: '5', rate: '48.00', paint: '210.00',
    line: { op: 'Replace', text: 'Rear bumper cover and reinforcement', part: 'EX-1002', qty: '1', pounds: '538.00', labour: '3.5', paintHours: '3.0' } },
];
for (const wanted of ESTIMATES) {
  await openEdit();
  const names = await page.eval(`[...document.querySelectorAll('#section-estimate [data-estimate-tab]')].map((t) => t.textContent.replace(/[ \\t\\n\\r]+/g, ' ').trim()).join(' | ')`);
  if (names.includes(wanted.name)) { console.log('estimate already there:', wanted.name); continue; }
  await page.goto(`${base}/Cases/${info.caseId}?section=estimate&estimate=new`, 3000);
  console.log('estimate:', await page.eval(`((wanted) => {
    const section = document.getElementById('section-estimate');
    const set = (name, value, index) => {
      const all = section.querySelectorAll('[name="' + name + '"]');
      const el = all[index === undefined ? 0 : index];
      if (!el) return;
      el.value = value;
      el.dispatchEvent(new Event('input', { bubbles: true }));
      el.dispatchEvent(new Event('change', { bubbles: true }));
    };
    set('estimateName', wanted.name);
    set('estimateRepairDays', wanted.days);
    set('estimateLabourRate', wanted.rate);
    set('estimatePaintMaterials', wanted.paint);
    const last = section.querySelectorAll('[name="lineDescription"]').length - 1;
    set('lineOperation', wanted.line.op, last);
    set('lineDescription', wanted.line.text, last);
    set('linePartNumber', wanted.line.part, last);
    set('lineQuantity', wanted.line.qty, last);
    set('linePartPounds', wanted.line.pounds, last);
    set('lineLabourHours', wanted.line.labour, last);
    set('linePaintHours', wanted.line.paintHours, last);
    return wanted.name + ' entered in row ' + last;
  })(${JSON.stringify(wanted)})`));
  try { await page.eval(`document.querySelector('[data-estimate-save]').click()`); } catch { /* navigates */ }
  await sleep(4000);
  console.log('  ', await page.eval(`[...document.querySelectorAll('.notice')].map((e) => e.textContent.replace(/[ \\t\\n\\r]+/g, ' ').trim()).filter((t) => /estimate/i.test(t)).slice(0, 2).join(' || ').slice(0, 200)`));
}

// Images: the fixture host seeds four retained images (fifth pass); two are
// tagged through the live tag picker so the report strip has a Close-up and an
// Overview and the record can show image ordering and the two-per-page sheet.
for (const [index, tag] of [[0, 'Close-up'], [1, 'Overview']]) {
  await openEdit();
  await page.goto(`${base}/Cases/${info.caseId}?section=files`, 2500);
  const outcome = await page.eval(`(() => {
    const tab = [...document.querySelectorAll('#section-files [role="tab"]')].find((t) => /^Images/.test(t.textContent.trim()));
    if (tab) tab.click();
    const tiles = [...document.querySelectorAll('#section-files .image-tile')];
    const tile = tiles[${index}]; if (!tile) return 'no tile ' + ${index} + ' of ' + tiles.length;
    if (tile.querySelector('.tag-option[aria-pressed="true"]')) return 'already tagged';
    const option = [...tile.querySelectorAll('.tag-option')].find((b) => b.textContent.trim() === ${JSON.stringify(tag)});
    if (!option) return 'no option ' + ${JSON.stringify(tag)};
    option.click();
    return 'clicked ' + ${JSON.stringify(tag)} + ' on tile ' + ${index} + ' of ' + tiles.length;
  })()`);
  console.log('image tag:', outcome);
  await sleep(3500);
}

// Two images go into the report through the live viewer's "Include in report"
// tick (staged as Supporting and saved by the ribbon Save). Only then does the
// Report section render the preparation cards whose role select sets the
// Close-up and the Overview, saved by a second Save.
await openEdit();
await page.goto(`${base}/Cases/${info.caseId}?section=files`, 2500);
console.log('include in report:', await page.eval(`(async () => {
  const tab = [...document.querySelectorAll('#section-files [role="tab"]')].find((t) => /^Images/.test(t.textContent.trim()));
  if (tab) tab.click();
  const tiles = [...document.querySelectorAll('#section-files .image-tile a[data-evidence-item]')];
  if (tiles.length < 2) return 'only ' + tiles.length + ' tiles';
  if ([...document.querySelectorAll('#section-files .image-tile')].filter((t) => t.getAttribute('data-preparation-role') !== 'NotUsed').length >= 2) return 'already included';
  const done = [];
  for (const tile of tiles.slice(0, 2)) {
    tile.click();
    await new Promise((r) => setTimeout(r, 800));
    const box = document.querySelector('[data-viewer-in-report]');
    if (box && !box.checked) { box.checked = true; box.dispatchEvent(new Event('change', { bubbles: true })); done.push(tile.getAttribute('data-file-name')); }
    const close = document.querySelector('[data-viewer-close]'); if (close) close.click();
    await new Promise((r) => setTimeout(r, 400));
  }
  return 'ticked ' + done.join(', ');
})()`));
try { await page.eval(`[...document.querySelectorAll('button')].find((b) => b.hasAttribute('data-case-save')).click()`); } catch { /* navigates */ }
await sleep(3500);

// Report roles: the first image becomes the Close-up and the second the
// Overview through the live preparation cards on the Report section, staged
// into the Case form and saved by the ribbon Save, so two images are in the
// report and the readiness list loses those two items.
await openEdit();
console.log('report roles:', await page.eval(`(() => {
  const cards = [...document.querySelectorAll('#section-report [data-preparation-card]')];
  if (cards.length < 2) return 'only ' + cards.length + ' preparation cards';
  // The role follows the tile's tag, so the tag and the report role agree.
  const byTag = (tag) => cards.find((card) => { const tile = document.querySelector('#section-files .image-tile[data-image-tile="' + card.getAttribute('data-preparation-occurrence') + '"] [data-tag]'); return tile && tile.getAttribute('data-tag') === tag; });
  const closeCard = byTag('Close-up') || cards[0], overviewCard = byTag('Overview') || cards[1];
  const set = (card, wanted) => {
    const select = card.querySelector('[data-preparation-role-select]'); if (!select) return 'no select';
    const option = [...select.options].find((o) => wanted.test(o.textContent) || wanted.test(o.value));
    if (!option) return 'no option in ' + [...select.options].map((o) => o.value).join(',');
    if (select.value === option.value) return 'already ' + option.value;
    select.value = option.value; select.dispatchEvent(new Event('change', { bubbles: true })); return option.value;
  };
  return set(closeCard, /close/i) + ' / ' + set(overviewCard, /overview/i);
})()`));
try { await page.eval(`[...document.querySelectorAll('button')].find((b) => b.hasAttribute('data-case-save')).click()`); } catch { /* navigates */ }
await sleep(3500);

// Leave the record out of edit mode so the capture browser can claim it.
await page.goto(`${base}/Cases/${info.caseId}`);
try { await page.eval(`document.getElementById('case-finish-editing-form') && document.getElementById('case-finish-editing-form').submit()`); } catch { /* navigates */ }
await sleep(2500);
console.log('editing after release:', await page.eval(`[...document.querySelectorAll('input[name="editLeaseToken"]')].some((i) => i.value)`));
await page.close();
