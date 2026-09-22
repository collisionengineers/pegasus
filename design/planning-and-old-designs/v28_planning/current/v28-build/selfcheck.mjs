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
      lifecyclePanel: [...document.querySelectorAll('[data-lifecycle-actions]')].filter(visible).length,
      notConnected: [...document.querySelectorAll('#section-valuation .valuation-card')].filter((c) => visible(c) && c.textContent.includes('is not connected')).length,
      getInHeading: document.querySelectorAll('#section-valuation .valuation-card h3 [data-valuation-source], #section-valuation [data-valuation-source].btn--ghost').length,
      entryCards: document.querySelectorAll('#section-valuation .valuation-card.entry').length,
      cazanaEntry: document.querySelectorAll('#section-valuation .valuation-card.entry[data-valuation-entry="cazana"]').length,
      estimatePdf: [...document.querySelectorAll('#section-estimate a, #section-estimate button')].filter((b) => b.textContent.includes('Estimate PDF')).length,
      printOutsideMore: [...document.querySelectorAll('#section-estimate a[data-document-preview]')].filter((a) => !a.closest('[data-estimate-more]')).length,
      printInMore: [...document.querySelectorAll('#section-estimate [data-estimate-more] a')].filter((a) => a.textContent.includes('Print Estimate')).length,
      compareInMore: document.querySelectorAll('#section-estimate [data-estimate-more] [data-estimate-compare]').length,
      printLinks: document.querySelectorAll('#section-estimate a[data-document-preview]').length,
      estimateTabs: document.querySelectorAll('#section-estimate [data-estimate-tab]').length,
      useChip: [...document.querySelectorAll('[data-estimate-use-condition]')].filter(visible).length,
      chasing: [...document.querySelectorAll('option')].filter((o) => !o.hidden && o.textContent.includes('chasing for update')).length,
      uploadLayout: document.querySelectorAll('.v28-upload .v28-upload__files, .v28-upload .v28-upload__decision, .v28-upload .v28-upload__discard').length,
      isUploadGroup: !!document.getElementById('group-decision-title'),
      isRecord: !!document.getElementById('section-overview'),
      recordEditing: !!document.querySelector('.case-record.is-editing'),
      made: [...new Set([...document.querySelectorAll('[data-v28-proposal]')].map((e) => e.getAttribute('data-v28-proposal')))],
      navLinks: [...document.querySelectorAll('[data-section-nav] [data-section-link]')].filter((a) => !a.hidden).map((a) => a.textContent.trim()),
      emptyComposed: [...document.querySelectorAll('[data-composed]')].filter((e) => visible(e) && !e.textContent.trim()).length,
      specTitle: (document.getElementById('section-estimate-title') || {}).textContent || '',
      notesShown: [...document.querySelectorAll('#section-estimate .fc')].filter((fc) => visible(fc) && /^Estimate notes/.test((fc.querySelector('label, .lbl') || {}).textContent || '')).length,
      nameOrDays: ['estimate-name', 'estimate-days'].filter((id) => visible(document.getElementById(id))).length,
      rateCells: [...document.querySelectorAll('#section-estimate .est-head .fc')].filter((fc) => visible(fc) && /rate/i.test((fc.querySelector('label') || {}).textContent || '')).length,
      basisRadios: [...document.querySelectorAll('#section-valuation .valuation-card input[type=radio]')].filter(visible).length,
      addLineRow: (() => { const add = [...document.querySelectorAll('#section-estimate button')].find((b) => b.textContent.trim() === 'Add line'); const del = document.querySelector('#section-estimate [data-est-delete-all]'); if (!add || !del) return 'n/a'; const a = add.getBoundingClientRect(), d = del.getBoundingClientRect(); return Math.abs(a.top - d.top) < 4 ? 'side-by-side' : 'stacked'; })(),
      vehicleHead: (() => { const h = document.querySelector('#section-vehicle > .panel-head'); return h ? Math.round(h.getBoundingClientRect().height) : 0; })(),
      useEstimateDisabled: [...document.querySelectorAll('#section-estimate button')].filter((b) => b.textContent.trim() === 'Use estimate' && b.disabled).length,
      decisionGroups: [...document.querySelectorAll('#section-settlement .v28-radiorow')].map((r) => ({
        role: r.getAttribute('role'),
        kids: [...new Set([...r.children].map((b) => b.getAttribute('role')))].join(','),
        checked: [...r.children].filter((b) => b.getAttribute('aria-checked') === 'true').length,
        stops: [...r.children].filter((b) => b.getAttribute('tabindex') === '0').length })),
      tickBoxes: document.querySelectorAll('#section-settlement .v28-tick, .v28-tickrow').length,
      damageEditing: document.querySelector('#section-damage[data-v28-damage="edit"]') ? 1 : 0,
      damageReset: [...document.querySelectorAll('#section-damage [data-damage-reset]')].filter(visible).length,
      updatedOutsidePanels: [...document.querySelectorAll('[data-wc-freshness]')].filter((l) => visible(l) && !l.closest('.panel-head, .pane-head') && /^\\s*Updated/.test(l.textContent)).length,
      floorPrice: (document.querySelector('[data-floor-price]') || {}).value || '',
      wbBoxes: document.querySelectorAll('.v28-wb').length,
      wbGrips: document.querySelectorAll('.v28-wb .v28-grip').length,
      tiles: [...document.querySelectorAll('#section-files .image-tile')].filter(visible).length,
      tileTools: [...document.querySelectorAll('#section-files .image-tile')].filter((t) => visible(t) && t.querySelector('.v28-img-tools [data-img-rotate]') && t.querySelector('[data-img-full]') && t.querySelector('[data-img-remove]') && t.querySelector('.v28-grip')).length,
      produce: document.querySelectorAll('[data-v28-produce]').length,
      previewKinds: (() => { const b = document.querySelector('[data-v28-produce]'); if (!b) return 0; b.click(); const d = document.getElementById('v28-preview-dialog'); const n = d ? d.querySelectorAll('[data-v28-kind]').length : 0; if (d) d.remove(); return n; })(),
      versionsButton: document.querySelectorAll('[data-v28-versions]').length,
      versionsSent: (() => { const b = document.querySelector('[data-v28-versions]'); if (!b) return -1; b.click(); const d = document.getElementById('v28-versions-dialog'); const n = d ? d.querySelectorAll('tbody .status--green').length : -1; if (d) d.remove(); return n; })(),
      originLine: document.querySelectorAll('.v28-origin').length,
      violRead: document.querySelectorAll('#section-estimate input.viol').length,
      subPanels: [...document.querySelectorAll('.record-section .sub-panel')].filter((p) => p.querySelector(':scope > h3')).length,
      subFolds: [...document.querySelectorAll('.record-section .sub-panel')].filter((p) => p.querySelector(':scope > h3 .v28-sub-collapse') && p.hasAttribute('data-collapse')).length,
      productType: document.querySelectorAll('#v28-product-type, [data-v28-proposal="P49"]').length,
      assignedCell: document.querySelectorAll('[data-v28-assigned]').length,
      imageNote: document.querySelectorAll('.v28-img-note').length,
      guidance: [...document.querySelectorAll('[data-v28-proposal] .muted, [data-v28-proposal].muted, [data-v28-proposal] [title]')].filter((e) => visible(e) && /\.\s+[A-Z]/.test((e.getAttribute('title') || e.textContent).trim())).length,
      breakdownAttach: document.querySelectorAll('[data-att="Breakdown"]').length,
      specAttach: document.querySelectorAll('[data-att="Repair Spec"]').length,
      reserveCell: [...document.querySelectorAll('[data-composed="reserve"]')].filter(visible).length,
      salvageNa: document.querySelectorAll('.v28-salvage-na').length,
      claimantVatOnClaim: document.querySelectorAll('#section-claim [data-field="settlement.claimant_vat_registered"]').length,
      unrelatedOnVehicle: [...document.querySelectorAll('#section-vehicle .sub-panel > h3')].filter((h) => /^Unrelated damage/.test(h.textContent.trim())).length,
      switchesOnValuation: document.querySelectorAll('#section-valuation [data-report-switches], #section-valuation [data-report-content]').length,
      signoffOnCase: document.querySelectorAll('#section-overview [data-v28-moved="P38"]').length,
      materialColumn: document.querySelectorAll('#section-estimate th[data-v28-proposal="P48"]').length,
      reportImagePanels: [...document.querySelectorAll('#section-report [data-report-images], #section-report [data-report-preparation]')].filter(visible).length,
      tileRoles: [...document.querySelectorAll('#section-files .image-tile')].filter((t) => visible(t) && t.querySelector('.v28-role')).length,
      filesTitle: (document.getElementById('section-files-title') || {}).textContent || '',
      originalReport: (() => { const s = document.getElementById('section-original-report'); return s ? (s.hidden ? 'hidden' : 'shown') : 'missing'; })(),
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
  check(facts.lifecyclePanel === 0, `${state.id}: P7 the Lifecycle actions container is still visible`);
  check(facts.notConnected === 0, `${state.id}: P8 a valuation card still says it is not connected`);
  check(facts.getInHeading === 0, `${state.id}: P8 Get valuation is still a ghost link in a card heading`);
  if (facts.entryCards > 0) check(facts.cazanaEntry === 1, `${state.id}: P8 Cazana has no entry card beside the others`);
  check(facts.estimatePdf === 0 && facts.printOutsideMore === 0, `${state.id}: P9 the estimate document button is not under More as Print Estimate`);
  if (facts.estimateTabs >= 2) check(facts.printInMore === facts.printLinks && facts.compareInMore === 1, `${state.id}: P9 More should hold Print Estimate and Compare (${facts.printInMore}, ${facts.compareInMore})`);
  check(facts.useChip === 0, `${state.id}: P10 the Use estimate lock chip is still visible`);
  check(facts.chasing === 0, `${state.id}: P3 a chasing-for-update category is still listed`);
  if (facts.isUploadGroup) check(facts.uploadLayout === 3, `${state.id}: P11 the reworked Upload received layout is incomplete (${facts.uploadLayout}/3)`);
  if (facts.isRecord) {
    check(facts.specTitle === 'Repair Spec', `${state.id}: P31 the section is titled "${facts.specTitle}"`);
    check(facts.notesShown === 0, `${state.id}: P32 Estimate notes is still shown`);
    check(facts.nameOrDays === 0, `${state.id}: P32 Estimate name or Repair days is still shown`);
    check(facts.basisRadios === 0, `${state.id}: P8 a Basis radio is still shown`);
    check(facts.vehicleHead < 70, `${state.id}: P6-I the Vehicle head is ${facts.vehicleHead}px tall`);
    check(facts.useEstimateDisabled === 0, `${state.id}: P10 Use estimate is still greyed out`);
    if (facts.recordEditing) {
      check(facts.rateCells <= 1, `${state.id}: P33 ${facts.rateCells} labour rate cells are shown`);
      check(facts.addLineRow !== 'stacked', `${state.id}: P16 Delete all lines is stacked above Add line`);
      check(facts.tickBoxes === 0, `${state.id}: P29 a tick box is still used for a single choice`);
      const badGroup = facts.decisionGroups.filter((g) => g.role !== 'radiogroup' || g.kids !== 'radio' || g.checked !== 1 || g.stops !== 1);
      check(facts.decisionGroups.length === 3 && badGroup.length === 0, `${state.id}: P29 ${facts.decisionGroups.length} decision groups, ${badGroup.length} not a single-choice radio group`);
      const spec = ['P34', 'P35', 'P36'].filter((id) => !facts.made.includes(id));
      check(spec.length === 0, `${state.id}: Repair Spec proposals made nothing: ${spec.join(', ')}`);
    }
    const want = ['P12', 'P24', 'P25', 'P26', 'P28', 'P30'].concat(facts.recordEditing ? ['P13', 'P14', 'P15', 'P17', 'P21', 'P22', 'P23', 'P29'] : []);
    const absent = want.filter((id) => !facts.made.includes(id));
    check(absent.length === 0, `${state.id}: record proposals made nothing: ${absent.join(', ')}`);
    check(facts.navLinks.join('|') === 'Case details|Claim|Inspection details|Vehicle|Repair Spec|Decisions|Report|Files|Notes', `${state.id}: P26 section row reads ${facts.navLinks.join(', ')}`);
    check(facts.emptyComposed === 0, `${state.id}: P12 a composed sentence is empty`);
    // Fifth pass (20 September): corrections, the dropped switches and the uninventoried features.
    check(facts.produce === 1 && facts.previewKinds === 3, `${state.id}: P42 Produce PDF or its three documents missing (${facts.produce}, ${facts.previewKinds})`);
    check(facts.versionsButton === 1 && facts.versionsSent === 1, `${state.id}: P43 Versions button or the Sent on report version missing (${facts.versionsButton}, ${facts.versionsSent})`);
    check(facts.originLine === 1, `${state.id}: P43 the origin line is missing`);
    check(facts.subPanels > 0 && facts.subFolds === facts.subPanels, `${state.id}: P45 ${facts.subFolds} of ${facts.subPanels} sub-cards fold`);
    check(facts.breakdownAttach === 0, `${state.id}: P22 Attach still says Breakdown`);
    check(facts.reserveCell === 1, `${state.id}: the computed reserve cell is missing`);
    check(facts.salvageNa === 1, `${state.id}: P29 the salvage not-applicable line is missing`);
    check(facts.claimantVatOnClaim === 1 && facts.unrelatedOnVehicle === 1 && facts.switchesOnValuation >= 1 && facts.signoffOnCase === 1, `${state.id}: P38 a placement did not move (vat ${facts.claimantVatOnClaim}, unrelated ${facts.unrelatedOnVehicle}, switches ${facts.switchesOnValuation}, sign-off ${facts.signoffOnCase})`);
    check(facts.originalReport === 'hidden', `${state.id}: P51 the Original report section is ${facts.originalReport} on a non-Audit Case`);
    check(facts.reportImagePanels === 0, `${state.id}: P50 the Report section still shows an images panel`);
    check(facts.tiles === 0 || facts.tileRoles === facts.tiles, `${state.id}: P50 ${facts.tileRoles} of ${facts.tiles} tiles carry their report role`);
    check(facts.productType === 0, `${state.id}: P49 was dropped on 20 September but ${facts.productType} product type control(s) remain`);
    check(facts.assignedCell === 0, `${state.id}: P38 repeats the assigned Engineer on Case details`);
    check(facts.imageNote === 0, `${state.id}: P41 the two-per-page note is guidance and was removed`);
    check(facts.guidance === 0, `${state.id}: ${facts.guidance} proposal element(s) carry a two-sentence tooltip or muted note`);
    check(facts.filesTitle.trim() === 'Files', `${state.id}: P26 the Files section is titled "${facts.filesTitle.trim()}"`);
    check(facts.tiles === 0 || facts.tiles === facts.tileTools || !facts.recordEditing, `${state.id}: P41 ${facts.tileTools} of ${facts.tiles} image tiles carry the working controls`);
    if (facts.recordEditing) {
      check(facts.floorPrice === '65', `${state.id}: P34 the price floor reads ${facts.floorPrice}, not 65`);
      check(facts.wbBoxes > 0 && facts.wbGrips === facts.wbBoxes, `${state.id}: P30 ${facts.wbGrips} of ${facts.wbBoxes} wording blocks carry a drag grip`);
      check(facts.specAttach === 1, `${state.id}: P22 Attach does not offer Repair Spec`);
      check(facts.materialColumn === 1, `${state.id}: P48 the Material column is missing`);
      const fifth = ['P37', 'P38', 'P39', 'P40', 'P41', 'P42', 'P43', 'P45', 'P48'].filter((id) => !facts.made.includes(id));
      check(fifth.length === 0, `${state.id}: fifth-pass proposals made nothing: ${fifth.join(', ')}`);
    } else {
      check(facts.violRead === 0, `${state.id}: P37 an off-pattern cell is marked in read mode`);
    }
  }
  if (facts.damageEditing) check(facts.damageReset === 1, `${state.id}: P5 the damage selector has no Reset button`);
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
