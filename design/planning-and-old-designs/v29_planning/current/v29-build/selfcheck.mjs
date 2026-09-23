// v29 self-check. The capture is checked with the proposals layer off; each
// proposal preset (proposal-shots.json) is then checked with it on. Prints
// RESULT {"fail":[...],"okCount":N} and exits non-zero on any failure.
//
//   node selfcheck.mjs                 offline checks only
//   LIVE=1 HOST_INFO=<host.json> node selfcheck.mjs
//                                       also compares every directly addressable state
//                                       with the running application, element for element
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { launch } from './cdp.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const current = resolve(here, '..');
const manifest = JSON.parse(readFileSync(join(here, 'manifest.json'), 'utf8'));
const captured = JSON.parse(readFileSync(join(here, 'captured.json'), 'utf8'));
const presets = JSON.parse(readFileSync(join(here, 'proposal-shots.json'), 'utf8'));
const hostInfo = process.env.HOST_INFO ? JSON.parse(readFileSync(resolve(process.env.HOST_INFO), 'utf8')) : {};
const fill = (text) => text.replace(/\{\{(\w+)\}\}/g, (_, key) => hostInfo[key] ?? '');

const fail = [];
let okCount = 0;
const check = (ok, message) => { if (ok) okCount += 1; else fail.push(message); };

// ---- baseline ---------------------------------------------------------------
const page = await launch({ width: 1580, height: 1000 });
const files = readdirSync(join(current, 'states')).filter((name) => name.endsWith('.html'));
check(files.length === captured.length, `states folder has ${files.length} files but captured.json lists ${captured.length}`);

for (const state of captured) {
  const file = join(current, 'states', state.id + '.html');
  const html = readFileSync(file, 'utf8');
  check(html.includes(`v29 baseline state "${state.id}"`), `${state.id}: baseline banner missing`);
  check(html.includes('../assets/mock/shim.js'), `${state.id}: shim missing`);
  const absolute = [...html.matchAll(/<(?:link|script|img)\b[^>]*?\s(?:href|src)="(\/[^"]*)"/gi)].map((m) => m[1]);
  check(absolute.length === 0, `${state.id}: server-absolute asset left in page: ${absolute.slice(0, 3).join(', ')}`);
  const before = page.errors.length;
  await page.goto(pathToFileURL(file).href + '?proposals=off', 700);
  check(page.errors.length === before, `${state.id}: page error offline: ${page.errors[before]}`);
  const untouched = await page.eval(`!document.documentElement.hasAttribute('data-v29-proposals') && !document.querySelector('[data-v29-removed], [data-v29-proposal]')`);
  check(untouched, `${state.id}: the baseline view carries proposal changes`);
}

// ---- proposals --------------------------------------------------------------
// One fact-gathering pass per preset; the expectations below read these facts.
const FACTS = `JSON.stringify((() => {
  const q = (s) => document.querySelector(s);
  const qa = (s) => [...document.querySelectorAll(s)];
  const visible = (el) => !!el && !el.closest('[hidden]') && el.getClientRects().length > 0;
  const text = (el) => el ? el.textContent.replace(/\\s+/g, ' ').trim() : '';
  const menu = q('[data-case-ribbon-actions] details.menu');
  return {
    on: document.documentElement.getAttribute('data-v29-proposals') === 'on',
    made: [...new Set(qa('[data-v29-proposal]').map((e) => e.getAttribute('data-v29-proposal')))].sort(),
    layoutButtons: qa('[data-case-layout]').filter(visible).length,
    viewTabs: qa('[data-v29-view-tabs] .workspace-tab').filter(visible).map((t) => text(t.querySelector('.ref')) + ' ' + text(t.querySelector('.reg')) + (t.classList.contains('is-active') ? '*' : '')),
    workingSet: visible(q('[data-working-set]')),
    triageFiles: visible(q('#section-files[data-v29-proposal="P7"]')),
    searchLinks: qa('.pane table tbody tr').filter(visible).map((r) => { const a = r.querySelector('td .table-row-link'); return a ? text(a) + ' -> ' + a.getAttribute('href').replace(/[0-9a-f-]{36}/, '{id}') : ''; }),
    heading: text(q('.ribbon-ref .ribbon-value')),
    auditReference: text(q('[data-v29-audit-reference] .ribbon-value')),
    typeChip: text(q('[data-case-type-chip]')),
    editCase: qa('[data-case-edit], [data-v29-triage-case] .ribbon-actions > form button').filter(visible).map(text),
    sectionEdits: qa('[data-section-edit]').filter(visible).length,
    gatedLabels: qa('.panel-actions .gated[data-v29-proposal]').filter(visible).map(text),
    reportStatus: text(q('[data-report-preview-card] [data-report-status]')),
    inspectionReport: text(q('[data-v29-inspection-report]')),
    notReady: visible(q('[data-report-not-ready]')),
    auditFolder: text(q('[data-v29-audit-folder]')),
    createAuditItem: qa('[data-create-audit]').filter(visible).length,
    menuOpen: !!(menu && menu.open),
    dialog: (() => { const d = qa('.dialog-backdrop').filter((b) => !b.hidden)[0]; return d ? { title: text(d.querySelector('h2')), facts: qa('.dialog-backdrop:not([hidden]) dt').map(text), values: qa('.dialog-backdrop:not([hidden]) dd').map(text), labels: qa('.dialog-backdrop:not([hidden]) label').map(text) } : null; })(),
    originalReport: visible(q('#section-original-report')),
    triageFrame: !!q('[data-v29-triage-case]'),
    triageSections: qa('[data-v29-triage-case] .section-nav .section-link').map(text),
    setPrincipal: qa('[data-dialog-open="triage-principal-dialog"]').filter(visible).length,
    pageHeader: visible(q('.page-header')),
    oldTriageRef: /\\bT-\\d{5}\\b/.test((document.body || {}).innerText || ""),
    triageRef: ((document.body || {}).innerText || "").includes('t.QDOS31003'),
    triageLinks: qa('a[href^="/Triage/"]').length,
    metrics: qa('.wc-metrics .metric .metric-label').map(text),
    stripClass: (q('.wc-metrics') || { className: '' }).className,
    workflowGroup: (() => { const t = q('button.scope-button[value="triage"]'); const qy = q('button.scope-button[value="query"]'); return !!(t && qy && qy.nextElementSibling === t); })(),
    caseTypes: qa('#CaseType option').map(text),
    shownFields: qa('main form .field').filter(visible).map((f) => text(f.querySelector('label'))),
    openTriage: qa('[data-unidentified-action="triage"]').filter(visible).length,
    searchRows: qa('.pane table tbody tr').filter(visible).map((r) => text(r.querySelector('td'))),
    horizontalSpill: document.documentElement.scrollWidth > window.innerWidth + 1
  };
})())`;

const expectations = {
  'audit-view': (f) => [
    [f.viewTabs.join(',') === 'Inspection QDOS31001,Audit a.QDOS31001*', `view tabs ${f.viewTabs.join(',')}`],
    [!f.workingSet, 'the working-set strip is still shown'],
    [f.layoutButtons === 2, `Scroll/Tabs should stay; ${f.layoutButtons} buttons`],
    [f.heading === 'QDOS31001', `heading ${f.heading}`],
    [f.auditReference === '', 'the ribbon names the Audit reference by default'],
    [f.typeChip === 'Inspection + Audit', `type chip ${f.typeChip}`],
    [f.reportStatus.startsWith('a.QDOS31001'), `audit report card ${f.reportStatus}`],
    [/QDOS31001 · Sent/.test(f.inspectionReport) && /Inspection view/.test(f.inspectionReport), `inspection report line ${f.inspectionReport}`],
    [f.auditFolder === 'Box audit folder: preparing', 'audit folder chip missing'],
    [f.editCase.length === 1, 'Edit Case missing from the Audit view'],
    [f.sectionEdits > 0, 'section Edit missing from the Audit view']
  ],
  'audit-view-report': (f) => [
    [f.reportStatus.startsWith('a.QDOS31001'), `audit report card ${f.reportStatus}`],
    [/QDOS31001 · Sent/.test(f.inspectionReport), `inspection report line ${f.inspectionReport}`]
  ],
  'inspection-view': (f) => [
    [f.viewTabs.join(',') === 'Inspection QDOS31001*,Audit a.QDOS31001', `view tabs ${f.viewTabs.join(',')}`],
    [f.editCase.length === 0, 'Edit Case offered in the Inspection view'],
    [f.sectionEdits === 0, `${f.sectionEdits} section Edit buttons in the Inspection view`],
    [f.gatedLabels.length > 0 && f.gatedLabels.every((t) => t === 'Read-only · Audit created'), `labels ${f.gatedLabels.join('|')}`],
    [f.reportStatus === 'QDOS31001 · Sent 06 May 2031 11:30', `report status ${f.reportStatus}`],
    [!f.notReady, 'Report not ready shown on a sent report'],
    [f.layoutButtons === 2, `Scroll/Tabs should stay; ${f.layoutButtons} buttons`]
  ],
  'audit-view-files': (f) => [
    [f.auditFolder === 'Box audit folder: preparing', 'audit folder chip missing']
  ],
  'inspection-view-report': (f) => [
    [f.reportStatus === 'QDOS31001 · Sent 06 May 2031 11:30', `report status ${f.reportStatus}`],
    [!f.notReady, 'Report not ready shown on a sent report']
  ],
  'audit-view-ribbon-ref': (f) => [
    [f.auditReference === 'a.QDOS31001', `audit reference ${f.auditReference}`]
  ],
  'audit-editing': (f) => [
    [f.viewTabs.join(',') === 'Inspection QDOS31001,Audit a.QDOS31001*', `view tabs ${f.viewTabs.join(',')}`],
    [f.createAuditItem === 0, 'Create audit offered after the Audit exists']
  ],
  'sent-read': (f) => [
    [f.viewTabs.length === 0 && !f.workingSet, 'a strip is shown before the Audit exists'],
    [f.layoutButtons === 2, `Scroll/Tabs should stay; ${f.layoutButtons} buttons`],
    [f.reportStatus === 'QDOS31001 · Sent 06 May 2031 11:30', `report status ${f.reportStatus}`]
  ],
  'sent-read-single-tab': (f) => [
    [f.viewTabs.join(',') === 'Inspection QDOS31001*', `view tabs ${f.viewTabs.join(',')}`]
  ],
  'sent-actions': (f) => [
    [f.menuOpen, 'Actions menu not open'],
    [f.createAuditItem === 1, 'Create audit not in the Actions menu']
  ],
  'create-audit-dialog': (f) => [
    [!!f.dialog && f.dialog.title === 'Create audit', 'Create audit dialog not open'],
    [!!f.dialog && f.dialog.facts.join(',') === 'Case,Audit reference,Engineer', `dialog facts ${f.dialog && f.dialog.facts.join(',')}`],
    [!!f.dialog && f.dialog.values[0] === 'QDOS31001' && f.dialog.values[1] === 'a.QDOS31001', `dialog values ${f.dialog && f.dialog.values.join(',')}`]
  ],
  'standalone-audit': (f) => [
    [f.viewTabs.length === 0 && !f.workingSet, 'a strip is shown on a Case with one view'],
    [f.layoutButtons === 2, `Scroll/Tabs should stay; ${f.layoutButtons} buttons`],
    [f.originalReport, 'Original report section missing'],
    [f.heading === 'a.QDOS31002', `heading ${f.heading}`]
  ],
  'triage-case': (f) => [
    [!f.workingSet && f.viewTabs.length === 0, 'a strip is shown on the Triage Case'],
    [f.triageFiles, 'the Case Files section is missing'],
    [!f.triageFrame, 'the Triage page was restructured'],
    [f.pageHeader, 'the live page header was removed'],
    [f.setPrincipal === 1, 'the live Set principal was removed'],
    [!f.oldTriageRef && f.triageRef, 'a T- reference remains']
  ],
  'work-centre': (f) => [
    [f.metrics.join(',') === 'Not ready,Review,Held,Unidentified,Triages', `metrics ${f.metrics.join(',')}`],
    [/metric-strip--5/.test(f.stripClass), 'metric strip not five wide'],
    [!f.oldTriageRef && f.triageRef, 'Needs attention still names T-'],
    [f.triageLinks === 0, 'a /Triage/ link remains'],
    [!f.workingSet, 'the working-set strip is still shown']
  ],
  'work-centre-metric-after-held': (f) => [
    [f.metrics.join(',') === 'Not ready,Review,Held,Triages,Unidentified', `metrics ${f.metrics.join(',')}`]
  ],
  'cases-triage': (f) => [
    [f.workflowGroup, 'Triage not in the Workflow group after Query'],
    [!f.oldTriageRef && f.triageRef, 'the list still names T-'],
    [f.triageLinks === 0, 'a /Triage/ link remains']
  ],
  'create-triage': (f) => [
    [f.caseTypes.join(',') === 'Inspection,Inspection and Audit,Triage', `case types ${f.caseTypes.join(',')}`],
    [f.shownFields.join(',') === 'Principal code,Case type,Registration', `fields ${f.shownFields.join(',')}`]
  ],
  'open-triage': (f) => [
    [f.openTriage === 1, 'Open the Triage not offered'],
    [!!f.dialog && f.dialog.title === 'Open the Triage', 'dialog not open'],
    [!!f.dialog && f.dialog.labels.join(',') === 'Vehicle registration', `dialog labels ${f.dialog && f.dialog.labels.join(',')}`]
  ],
  'open-triage-principal': (f) => [
    [!!f.dialog && f.dialog.labels.join(',') === 'Principal,Vehicle registration', `dialog labels ${f.dialog && f.dialog.labels.join(',')}`]
  ],
  'search-audit-entry': (f) => [
    [f.searchLinks.join(' | ') === 'a.QDOS31002 -> /Cases/{id} | QDOS31001 -> /Cases/{id}?view=inspection | a.QDOS31001 -> /Cases/{id}?view=audit', `search entries ${f.searchLinks.join(' | ')}`]
  ],
  'search-triage': (f) => [
    [f.searchRows.join(',') === 't.QDOS31003', `search rows ${f.searchRows.join(',')}`]
  ]
};

for (const preset of presets) {
  const expect = expectations[preset.name];
  check(!!expect, `${preset.name}: no expectations written`);
  if (!expect) continue;
  const url = pathToFileURL(join(current, 'states', preset.state + '.html')).href + '?' + (preset.query ? preset.query + '&' : '') + 'proposals=on';
  for (const width of [1580, 1440]) {
    await page.viewport(width, width === 1580 ? 1000 : 900);
    const before = page.errors.length;
    await page.goto(url, 300);
    await page.eval(`try { window.localStorage.clear(); window.sessionStorage.clear(); } catch (e) {}`);
    await page.goto(url, 1100);
    check(page.errors.length === before, `${preset.name} @${width}: page error: ${page.errors[before]}`);
    const facts = JSON.parse(await page.eval(FACTS));
    check(facts.on, `${preset.name} @${width}: proposals layer not on`);
    check(!facts.horizontalSpill, `${preset.name} @${width}: horizontal spill`);
    for (const [ok, message] of expect(facts)) check(ok, `${preset.name} @${width}: ${message}`);
  }
}

// ---- family frames ------------------------------------------------------------
for (const family of manifest.families) {
  const frame = readFileSync(join(current, family.file), 'utf8');
  const ids = [...frame.matchAll(/<option value="([^"]+)" data-(?:live|query)/g)].map((m) => m[1]);
  check(ids.length > 0, `${family.file}: no states`);
  for (const id of new Set(ids)) check(files.includes(id + '.html'), `${family.file}: names missing state ${id}`);
}

// ---- live parity ---------------------------------------------------------------
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
let compared = 0;
if (process.env.LIVE) {
  await page.viewport(1440, 900);
  for (const state of manifest.states) {
    if (state.steps || state.dom) continue; // reached by a post; not addressable by URL alone
    await page.goto(manifest.bases.default + fill(state.path), 1300);
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

const report = { states: captured.length, presets: presets.length, liveCompared: compared };
writeFileSync(join(here, 'selfcheck-report.json'), JSON.stringify(report, null, 1) + '\n');
console.log(JSON.stringify(report, null, 1));
console.log('RESULT ' + JSON.stringify({ fail, okCount }));
process.exit(fail.length ? 1 : 0);
