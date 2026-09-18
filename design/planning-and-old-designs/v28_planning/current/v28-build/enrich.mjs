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
  window.v28set = (name, value) => {
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
  const editing = await page.eval(`!!document.querySelector('input[name="editLeaseToken"]')`);
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
    console.log(await page.eval(`window.v28set(${JSON.stringify(name)}, ${JSON.stringify(value)})`));
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

// Estimate: one hand-entered estimate through the live New estimate editor.
await openEdit();
const hasEstimate = await page.eval(`!document.querySelector('#section-estimate .estimate-empty')`);
if (!hasEstimate) {
  await page.goto(`${base}/Cases/${info.caseId}?section=estimate&estimate=new`, 3000);
  console.log('estimate:', await page.eval(`(() => {
    const section = document.getElementById('section-estimate');
    const set = (name, value, index) => {
      const all = section.querySelectorAll('[name="' + name + '"]');
      const el = all[index === undefined ? 0 : index];
      if (!el) return;
      el.value = value;
      el.dispatchEvent(new Event('input', { bubbles: true }));
      el.dispatchEvent(new Event('change', { bubbles: true }));
    };
    set('estimateName', 'Example Bodyshop estimate');
    set('estimateRepairDays', '4');
    set('estimateLabourRate', '48.00');
    set('estimatePaintMaterials', '185.00');
    const last = section.querySelectorAll('[name="lineDescription"]').length - 1;
    set('lineOperation', 'Replace', last);
    set('lineDescription', 'Rear bumper cover', last);
    set('linePartNumber', 'EX-1001', last);
    set('lineQuantity', '1', last);
    set('linePartPounds', '412.50', last);
    set('lineLabourHours', '2.5', last);
    set('linePaintHours', '3.0', last);
    return 'entered in row ' + last;
  })()`));
  try { await page.eval(`document.querySelector('[data-estimate-save]').click()`); } catch { /* navigates */ }
  await sleep(4000);
  console.log('  ', await page.eval(`[...document.querySelectorAll('.notice, .field-error, .validation-summary')].map((e) => e.textContent.replace(/\s+/g, ' ').trim()).filter(Boolean).slice(0, 4).join(' || ').slice(0, 300)`));
}

// Leave the record out of edit mode so the capture browser can claim it.
await page.goto(`${base}/Cases/${info.caseId}`);
try { await page.eval(`document.getElementById('case-finish-editing-form') && document.getElementById('case-finish-editing-form').requestSubmit()`); } catch { /* navigates */ }
await sleep(2500);
console.log('editing after release:', await page.eval(`!!document.querySelector('input[name="editLeaseToken"]')`));
await page.close();
