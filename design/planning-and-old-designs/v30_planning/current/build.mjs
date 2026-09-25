import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../../');
const capturedShell = await readFile(path.join(repo, 'design/planning-and-old-designs/v28_planning/current/states/upload-group-status.html'), 'utf8');
const brand = await readFile(path.join(repo, 'design/planning-and-old-designs/v28_planning/current/assets/images/marks/pegasus-lockup.png'));
const regularFont = await readFile(path.join(repo, 'src/Pegasus.Web/wwwroot/fonts/inter/InterVariable.woff2'));
const italicFont = await readFile(path.join(repo, 'src/Pegasus.Web/wwwroot/fonts/inter/InterVariable-Italic.woff2'));
const liveCss = (await readFile(path.join(repo, 'src/Pegasus.Web/wwwroot/css/site.css'), 'utf8'))
  .replace('url(../fonts/inter/InterVariable.woff2)', `url(data:font/woff2;base64,${regularFont.toString('base64')})`)
  .replace('url(../fonts/inter/InterVariable-Italic.woff2)', `url(data:font/woff2;base64,${italicFont.toString('base64')})`);
const shellPrefix = capturedShell
  .slice(capturedShell.indexOf('<body>') + '<body>'.length, capturedShell.indexOf('<header class="page-header">'))
  .replace('src="../assets/images/marks/pegasus-lockup.png"', `src="data:image/png;base64,${brand.toString('base64')}"`);
const options = [
  ['a', 'Decision beside files', 'A compact result and decision pane sits beside a stable file ledger.'],
  ['b', 'Guided sequence', 'A single column leads from selection through processing to the destination choice.'],
  ['c', 'Destination first', 'The destination decision is the main work area; files remain in a slim evidence index.'],
  ['d', 'Operations table', 'A full-width file ledger sits beneath a compact decision workspace.'],
  ['e', 'Evidence gallery', 'A small image contact sheet gives visual context while a concise result card owns the decision.'],
];
const states = ['select', 'chosen', 'storing', 'processing', 'decision', 'registered', 'mixed', 'single', 'failed', 'discarded', 'no-match', 'multiple', 'attached'];
const fileNames = [
  'WhatsApp Image 2026-09-17 at 12.57.40 PM.jpeg',
  'WhatsApp Image 2026-09-17 at 12.57.41 PM (1).jpeg',
  'WhatsApp Image 2026-09-17 at 12.57.41 PM (2).jpeg',
  'WhatsApp Image 2026-09-17 at 12.57.41 PM (3).jpeg',
  'WhatsApp Image 2026-09-17 at 12.57.41 PM (4).jpeg',
  'WhatsApp Image 2026-09-17 at 12.57.41 PM (5).jpeg',
  'WhatsApp Image 2026-09-17 at 12.57.42 PM.jpeg',
  'WhatsApp Image 2026-09-17 at 12.57.42 PM (1).jpeg',
  'WhatsApp Image 2026-09-17 at 12.57.42 PM (2).jpeg',
  'WhatsApp Image 2026-09-17 at 12.57.42 PM (3).jpeg',
  'WhatsApp Image 2026-09-17 at 12.57.42 PM (4).jpeg',
];
const esc = value => String(value).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const link = (label, state, cls = '') => `<a class="btn ${cls}" href="?state=${state}">${label}</a>`;

function statusFor(state, index) {
  if (state === 'storing') return '<span class="status status--blue">Uploading</span>';
  if (state === 'processing') return `<span class="status status--blue">${index < 3 ? 'Processing' : 'Received'}</span>`;
  if (state === 'mixed') return `<span class="status ${index === 3 ? 'status--red' : 'status--green'}">${index === 3 ? 'Could not be read' : 'Ready'}</span>`;
  if (state === 'failed') return `<span class="status ${index === 2 ? 'status--red' : 'status--neutral'}">${index === 2 ? 'Failed' : 'Not uploaded'}</span>`;
  if (state === 'attached') return '<span class="status status--green">Added to Case</span>';
  if (state === 'decision') return '<span class="status status--green">Ready</span>';
  if (state === 'single') return '<span class="status status--green">Vehicle images</span>';
  if (state === 'registered') return '<span class="status status--green">Vehicle images</span>';
  if (state === 'discarded') return '<span class="status status--neutral">Discarded</span>';
  return '<span class="status status--neutral">Selected</span>';
}
function fileRows(state, mode = 'rows') {
  const show = state === 'select' ? 0 : state === 'single' ? 1 : fileNames.length;
  if (!show) return '<li class="empty">No files selected.</li>';
  return fileNames.slice(0, show).map((name, i) => {
    const glyph = '<svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8" cy="8" r="1.5"/><path d="m3 17 5-5 4 4 4-6 5 7"/></svg>';
    const icon = mode === 'gallery' ? `<span class="tile-thumb" aria-hidden="true">${glyph}<span>JPEG</span></span>` : `<span class="file-glyph" aria-hidden="true">${glyph}</span>`;
    const detail = `<small>${(0.23 + i * 0.01).toFixed(2)} MB</small>`;
    return `<li class="file-row" data-file-row>${icon}<span class="file-name" title="${esc(name)}"><strong>${esc(name)}</strong>${detail}</span>${statusFor(state, i)}</li>`;
  }).join('');
}
function summary(state) {
  switch(state) {
    case 'storing': return '<strong>Uploading 11 files</strong>';
    case 'processing': return '<strong>11 stored · 3 processing</strong>';
    case 'mixed': return '<strong>1 file could not be read</strong>';
    case 'failed': return '<strong>1 upload failed</strong>';
    case 'discarded': return '<strong>Source and processing record retained</strong>';
    default: return '<strong>11 files selected</strong>';
  }
}
function decision(state) {
  if (state === 'select' || state === 'chosen') return `<section class="decision-panel"><h2>Select files</h2><div class="droparea"><svg class="icon drop-glyph" aria-hidden="true"><use href="#icon-upload" /></svg><strong>Drag files here or choose files</strong><span>EML, MSG, PDF, DOC, DOCX, JPG, PNG, MP4 or MOV · 100 MB each · 200 MB total · 20 files</span><label class="btn" for="file-input">Choose files</label><input id="file-input" type="file" multiple aria-label="Choose files" /></div><div class="action-row">${state === 'chosen' ? link('Upload 11 files','storing','btn--primary') : ''}${state === 'chosen' ? link('Clear','select') : ''}</div></section>`;
  if (state === 'storing' || state === 'processing') return `<section class="decision-panel"><h2>${summary(state)}</h2>${state === 'storing' ? '<div class="progress" role="progressbar" aria-label="Uploading files"><i style="width:100%"></i></div>' : link('Refresh status','decision')}</section>`;
  if (state === 'failed') return `<section class="decision-panel"><h2>1 upload failed</h2><div class="action-row">${link('Choose files again','chosen','btn--primary')}${link('New upload','select')}</div></section>`;
  if (state === 'discarded') return `<section class="decision-panel"><h2>Submission discarded</h2><p>Source and processing record retained.</p>${link('New upload','select','btn--primary')}</section>`;
  if (state === 'attached') return `<section class="decision-panel"><h2>Added to Case QDOS26010</h2><p>BH17RZV · J. Morgan · Review</p><p>11 files</p>${link('New upload','select')}</section>`;
  const noMatch = state === 'no-match';
  const multiple = state === 'multiple';
  const candidate = (ref, claimant, stage) => `<div class="case-candidate"><div><strong>${ref}</strong><span>${stage}</span></div><p>BH17RZV · ${claimant}</p><button type="button" class="btn ${multiple ? '' : 'btn--primary'}" data-propose="${ref}" data-claimant="${claimant}" data-stage="${stage}">Review and add to ${ref}</button></div>`;
  return `<section class="decision-panel" id="decision"><div class="case-primary"><h2>${noMatch ? 'No matching Case' : multiple ? 'Possible Cases' : 'Proposed Case'}</h2>${state === 'mixed' ? `<div class="state-summary">${summary(state)}</div>` : ''}${noMatch ? '' : candidate('QDOS26010','J. Morgan','Review')}${multiple ? candidate('QDOS25984','A. Taylor','With Engineer') : ''}<details class="attach" ${noMatch ? 'open' : ''}><summary>${noMatch ? 'Find a Case' : 'Find another Case'}</summary><div class="field"><label for="case-reference">Case/PO</label><input id="case-reference" type="text" placeholder="Case reference" required /></div><div class="action-row"><button type="button" class="btn" data-confirm>Review Case</button></div></details></div><div class="decision-secondary"><div class="image-record"><span>Vehicle images</span><a class="record-link" href="?state=registered">BH17RZV-01 →</a><small>Awaiting instruction</small></div><div class="action-row">${link('Leave undecided','select')}</div><details class="discard"><summary>Discard submission</summary><button type="button" class="btn btn--danger" data-discard>Discard ${state === 'single' ? 'file' : '11 files'}</button></details></div></section>`;

}
function filePanel(state, variant) {
  const gallery = variant === 'e';
  return `<section class="files-panel" aria-labelledby="files-title"><div class="panel-head"><h2 id="files-title">${state === 'single' ? 'Uploaded file' : 'Files'}</h2><span>${state === 'single' ? '1 file' : state === 'select' ? '0 files' : '11 files'}</span></div><ul class="file-list ${gallery ? 'file-list--gallery' : ''}">${fileRows(state, gallery ? 'gallery' : 'rows')}</ul></section>`;
}
function main(state, variant) {
  const files = filePanel(state, variant);
  const decide = decision(state);
  if (variant === 'a') return `<div class="layout layout--split">${files}${decide}</div>`;
  if (variant === 'b') return `<div class="steps"><span class="step ${state === 'select' || state === 'chosen' ? 'is-current' : ''}">1 · Select</span><span class="step ${state === 'storing' || state === 'processing' ? 'is-current' : ''}">2 · Process</span><span class="step ${['decision','registered','mixed','single','no-match','multiple','attached'].includes(state) ? 'is-current' : ''}">3 · Decide</span></div><div class="layout layout--single">${decide}${files}</div>`;
  if (variant === 'c') return `<div class="layout layout--destination">${decide}<aside class="evidence-index">${files}</aside></div>`;
  if (variant === 'd') return `<div class="layout layout--table">${files}<div class="table-footer">${decide}</div></div>`;
  return `<div class="layout layout--gallery">${files}${decide}</div>`;
}
const proposalCss = `
.upload-proposal .layout{display:grid;gap:12px;align-items:start}
.upload-proposal .layout--split{grid-template-columns:minmax(0,1.35fr) minmax(340px,1fr)}
.upload-proposal .layout--single{grid-template-columns:1fr;max-width:940px;margin:auto}
.upload-proposal .layout--destination{grid-template-columns:minmax(0,1fr) 340px}
.upload-proposal .layout--table{grid-template-columns:1fr}
.upload-proposal .layout--gallery{grid-template-columns:minmax(0,1.2fr) minmax(340px,1fr)}
.upload-proposal .files-panel,.upload-proposal .decision-panel{background:#fff;border:1px solid var(--line);border-radius:3px;min-width:0;padding:12px}
.upload-proposal .panel-head{display:flex;justify-content:space-between;align-items:center;padding-bottom:8px;border-bottom:1px solid var(--line)}
.upload-proposal .panel-head span,.upload-proposal .quiet{color:var(--muted)}
.upload-proposal .file-list{list-style:none;margin:0;padding:0}
.upload-proposal .file-row{display:grid;grid-template-columns:22px minmax(0,1fr) auto;gap:10px;align-items:center;min-height:40px;padding:5px 2px;border-bottom:1px solid var(--line);font-size:12px}
.upload-proposal .file-row:last-child{border-bottom:0}
.upload-proposal .file-glyph{color:var(--muted);font-size:17px}
.upload-proposal .file-name{min-width:0}.upload-proposal .file-name strong{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.upload-proposal .file-name small{display:block;color:var(--muted);font-size:10px}
.upload-proposal .status{border:1px solid currentColor;border-radius:12px;padding:2px 7px;font-size:10px;font-weight:700;white-space:nowrap}
.upload-proposal .status--blue{color:var(--blue);background:var(--blue-bg)}.upload-proposal .status--green{color:var(--green);background:var(--green-bg)}.upload-proposal .status--red{color:var(--danger);background:var(--danger-bg)}.upload-proposal .status--neutral{color:var(--muted);background:var(--surface-2)}
.upload-proposal .decision-panel h2{font-size:16px;margin:0 0 10px}.upload-proposal .decision-panel .field{display:grid;gap:5px;max-width:440px;margin:10px 0}.upload-proposal .decision-panel input{height:36px;border:1px solid var(--line-strong);padding:6px 9px}
.upload-proposal .action-row{display:flex;gap:8px;flex-wrap:wrap;margin:12px 0}.upload-proposal .suggestion{display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin:10px 0}.upload-proposal details{border-top:1px solid var(--line);margin-top:12px;padding-top:10px}.upload-proposal details summary{cursor:pointer;font-weight:700}.upload-proposal details[open] summary{margin-bottom:10px}.upload-proposal .discard summary{color:var(--danger)}
.upload-proposal .state-summary{background:var(--navy-bg);border-left:3px solid var(--navy);padding:8px 10px;margin:10px 0}.upload-proposal .record-link{display:inline-block;font-weight:700;margin:5px 0}.upload-proposal .progress{height:7px;background:var(--surface-3);margin:12px 0}.upload-proposal .progress i{display:block;height:100%;background:var(--blue)}
.upload-proposal .droparea{border:2px dashed var(--line-strong);padding:18px;display:flex;flex-direction:column;align-items:center;gap:7px;text-align:center;min-height:180px}.upload-proposal .droparea input{position:absolute;left:-9999px}.upload-proposal .drop-glyph{font-size:26px;color:var(--red)}
.upload-proposal .steps{display:flex;gap:8px;max-width:940px;margin:0 auto 12px}.upload-proposal .step{flex:1;background:var(--surface-3);padding:7px;text-align:center;font-weight:700}.upload-proposal .step.is-current{background:#fff;border-bottom:3px solid var(--red)}
.upload-proposal .layout--destination .evidence-index{grid-column:2}.upload-proposal .layout--destination .file-name small,.upload-proposal .layout--destination .status{display:none}.upload-proposal .layout--destination .file-row{grid-template-columns:20px minmax(0,1fr)}
.upload-proposal .layout--table .table-footer{grid-row:1}.upload-proposal .layout--table .files-panel{grid-row:2}.upload-proposal .layout--table .decision-panel{display:grid;grid-template-columns:1fr 1fr;gap:0 18px}.upload-proposal .layout--table .decision-panel h2,.upload-proposal .layout--table .decision-panel>.field,.upload-proposal .layout--table .decision-panel>.action-row{grid-column:1}.upload-proposal .layout--table .decision-panel>.attach,.upload-proposal .layout--table .decision-panel>.discard{grid-column:2}
.upload-proposal .file-list--gallery{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:7px;padding-top:10px}.upload-proposal .file-list--gallery .file-row{display:flex;flex-direction:column;align-items:stretch;border:1px solid var(--line);padding:5px}.upload-proposal .file-list--gallery .file-name small{display:none}.upload-proposal .file-list--gallery .file-name strong{white-space:normal;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical}.upload-proposal .file-list--gallery .status{align-self:flex-start}.upload-proposal .tile-thumb{display:grid;place-items:center;height:62px;background:var(--surface-3);color:var(--muted)}

/* Refined shared components retain the live Pegasus tokens and shell. */
.upload-proposal .layout{gap:20px}
.upload-proposal .files-panel{padding:0;overflow:hidden}
.upload-proposal .decision-panel{padding:22px}
.upload-proposal .panel-head{padding:12px 16px;min-height:48px}
.upload-proposal .panel-head h2{font-size:14px;margin:0}
.upload-proposal .panel-head>span{font-size:11px;font-variant-numeric:tabular-nums}
.upload-proposal .file-row{min-height:52px;padding:9px 16px;border-radius:0;gap:12px}
.upload-proposal .file-row:hover{background:var(--surface-2)}
.upload-proposal .file-row>*:last-child{grid-column:auto;justify-self:end}
.upload-proposal .file-name strong{font-weight:550;font-size:11px;line-height:1.5}
.upload-proposal .file-name small{font-size:10px;margin-top:2px}
.upload-proposal .file-glyph{display:flex;color:var(--muted)}
.upload-proposal .status{border:0;border-radius:3px;font-weight:600;padding:3px 6px;font-size:10px}
.upload-proposal .decision-panel h2{font-size:16px;line-height:1.4;margin-bottom:20px}
.upload-proposal .decision-panel .field{margin:16px 0;gap:6px;max-width:none}
.upload-proposal .field label{font-size:11px;font-weight:650;color:var(--muted)}
.upload-proposal .decision-panel input{border-radius:3px;height:38px;background:#fff}
.upload-proposal #registration{font-weight:650;letter-spacing:.04em}
.upload-proposal .action-row{margin:20px 0 0;gap:8px;align-items:center}
.upload-proposal details{margin-top:22px;padding-top:16px}
.upload-proposal details summary{font-size:12px;font-weight:600}
.upload-proposal .discard summary{color:var(--muted)}
.upload-proposal .discard[open] summary{color:var(--danger)}
.upload-proposal .discard button{margin-top:4px}
.upload-proposal .suggestion{padding:12px;background:var(--surface-2);border:1px solid var(--line);border-radius:3px;gap:8px}
.upload-proposal .suggestion>span{font-size:10px;color:var(--muted);flex-basis:100%}
.upload-proposal .suggestion strong{font-size:12px}
.upload-proposal .suggestion .btn{margin-left:auto}
.upload-proposal .empty{padding:40px 16px;text-align:center;color:var(--muted);font-size:12px}
.upload-proposal .droparea{background:var(--surface-2);border-width:1px;border-radius:3px;padding:28px 18px;gap:12px}
.upload-proposal .droparea>span{font-size:11px;color:var(--muted);max-width:320px;line-height:1.7}
.upload-proposal .drop-glyph{width:28px;height:28px;color:var(--muted)}
.upload-proposal .droparea:focus-within{outline:2px solid var(--blue);outline-offset:3px}
.upload-proposal .steps{gap:0;margin-bottom:20px;border:1px solid var(--line);border-radius:3px;overflow:hidden}
.upload-proposal .step{font-size:12px;font-weight:550;padding:13px;border-bottom:3px solid transparent}
.upload-proposal .step.is-current{font-weight:700}
.upload-proposal .layout--single{gap:16px}
.upload-proposal .layout--destination{grid-template-columns:minmax(0,1fr) 400px}
.upload-proposal .layout--destination .decision-panel{padding:28px}
.upload-proposal .layout--destination .file-row{grid-template-columns:18px minmax(0,1fr);gap:5px 10px;padding:10px 14px}
.upload-proposal .layout--destination .file-row .status{display:inline-flex;grid-column:2;justify-self:start}
.upload-proposal .layout--destination .file-name strong{font-size:10px}
.upload-proposal .layout--table .decision-panel{grid-template-columns:minmax(0,1fr) minmax(260px,.7fr);grid-template-areas:'title attach' 'registration attach' 'reason discard' 'actions discard';column-gap:40px}
.upload-proposal .layout--table .decision-panel>h2{grid-area:title;margin-bottom:4px}
.upload-proposal .layout--table .decision-panel>.field{grid-area:registration;margin:10px 0}
.upload-proposal .layout--table .decision-panel>.field+.field{grid-area:reason}
.upload-proposal .layout--table .decision-panel>.action-row{grid-area:actions;margin-top:12px}
.upload-proposal .layout--table .decision-panel>.attach{grid-area:attach;margin-top:0;border-top:0;padding-top:0}
.upload-proposal .layout--table .decision-panel>.discard{grid-area:discard;align-self:end}
.upload-proposal .layout--table .file-row{grid-template-columns:22px minmax(0,1fr) auto;min-height:43px;padding-top:6px;padding-bottom:6px}
.upload-proposal .layout--table .file-name{display:flex;align-items:center;gap:20px}
.upload-proposal .layout--table .file-name small{margin:0 24px 0 auto;white-space:nowrap}
.upload-proposal .file-list--gallery{padding:14px;gap:12px;grid-template-columns:repeat(3,minmax(0,1fr))}
.upload-proposal .file-list--gallery .file-row{padding:0;gap:8px;border-radius:3px;overflow:hidden}
.upload-proposal .file-list--gallery .file-name{padding:0 9px}
.upload-proposal .file-list--gallery .file-name strong{font-size:10px;line-height:1.5}
.upload-proposal .file-list--gallery .status{margin:0 9px 10px;align-self:flex-start}
.upload-proposal .tile-thumb{height:90px;background:var(--surface-2);border-bottom:1px solid var(--line);display:flex;flex-direction:column;justify-content:center;gap:6px}
.upload-proposal .tile-thumb .icon{width:27px;height:27px;opacity:.55}
.upload-proposal .tile-thumb span{font-size:9px;letter-spacing:.12em;color:var(--muted)}
.dialog{border:1px solid var(--line);border-radius:4px;padding:24px}
.dialog h2{font-size:18px;margin:0 0 12px}.dialog p{color:var(--muted);line-height:1.6;font-size:13px}
.dialog .action-row{display:flex;justify-content:flex-end;gap:8px;margin-top:24px}
@media(max-width:1100px){.upload-proposal .layout--destination{grid-template-columns:1fr}.upload-proposal .layout--destination .file-row{grid-template-columns:20px minmax(0,1fr) auto}.upload-proposal .layout--destination .file-row .status{grid-column:3;grid-row:1}.upload-proposal .layout--destination .file-name strong{font-size:11px}}
@media(max-width:800px){.upload-proposal .layout{gap:16px}.upload-proposal .decision-panel,.upload-proposal .layout--destination .decision-panel{padding:18px}.upload-proposal .file-row{padding:10px 12px;gap:8px}.upload-proposal .file-row>*:last-child{grid-column:auto}.upload-proposal .layout--table .decision-panel{display:block}.upload-proposal .layout--table .decision-panel>.attach{margin-top:20px;padding-top:16px;border-top:1px solid var(--line)}.upload-proposal .file-list--gallery{grid-template-columns:repeat(3,minmax(0,1fr))}.upload-proposal .layout--table .file-name small{margin-right:0}}
@media(max-width:520px){.upload-proposal .file-list--gallery{grid-template-columns:repeat(2,minmax(0,1fr))}.upload-proposal .file-row{grid-template-columns:18px minmax(0,1fr)}.upload-proposal .file-row>.status{grid-column:2;justify-self:start}.upload-proposal .layout--table .file-name{display:block}.upload-proposal .layout--destination .file-row .status{grid-column:2;grid-row:auto}}

.mock-strip{position:fixed;bottom:10px;left:10px;z-index:90;background:var(--nav);color:#fff;padding:8px 10px;border-radius:3px;max-width:calc(100vw - 20px)}.mock-strip summary{cursor:pointer;font-size:11px}.mock-controls{display:flex;flex-wrap:wrap;gap:6px;margin-top:7px}.mock-controls a{color:#fff;border:1px solid #7b858a;padding:4px 6px;font-size:10px}.mock-controls a.active{background:#fff;color:var(--nav)}.mock-note{font-size:10px;margin:6px 0 0;color:#c8ced0}
.dialog-backdrop{position:fixed;inset:0;z-index:80;background:#0008;display:grid;place-items:center}.dialog{background:#fff;width:min(440px,calc(100vw - 30px));padding:20px;box-shadow:var(--shadow)}
@media(max-width:1100px){.upload-proposal .layout--split,.upload-proposal .layout--destination,.upload-proposal .layout--gallery{grid-template-columns:1fr}.upload-proposal .layout--destination .evidence-index{grid-column:1}.upload-proposal .layout--gallery .decision-panel{grid-row:1}.upload-proposal .layout--gallery .files-panel{grid-row:2}}
@media(max-width:800px){.upload-proposal .file-list--gallery{grid-template-columns:repeat(2,minmax(0,1fr))}.upload-proposal .layout--table .decision-panel{display:block}.upload-proposal .file-row{grid-template-columns:20px minmax(0,1fr) auto}}
@media(max-width:1100px){.upload-proposal .layout--split .decision-panel{grid-row:1}.upload-proposal .layout--split .files-panel{grid-row:2}}
body:not([data-state="decision"]):not([data-state="mixed"]) .upload-proposal .layout--table .decision-panel{display:block}
body[data-state="mixed"] .upload-proposal .layout--table .decision-panel{grid-template-areas:"title attach" "notice attach" "registration attach" "reason discard" "actions discard"}
.upload-proposal .layout--table .state-summary{grid-area:notice}
body[data-state="processing"] .upload-proposal .layout--table .decision-panel,body[data-state="storing"] .upload-proposal .layout--table .decision-panel{display:block}
.upload-proposal .case-candidate{border:1px solid var(--line);border-left:3px solid var(--navy);padding:16px;margin:12px 0;background:var(--surface-2)}
.upload-proposal .case-candidate>div{display:flex;gap:16px;align-items:center;justify-content:space-between}
.upload-proposal .case-candidate strong{font-size:17px}.upload-proposal .case-candidate>div>span{font-size:11px;color:var(--muted)}
.upload-proposal .case-candidate p{font-size:13px;margin:10px 0 16px}
.upload-proposal .image-record{display:grid;gap:4px}.upload-proposal .image-record>span,.upload-proposal .image-record small{font-size:11px;color:var(--muted)}
.upload-proposal .decision-secondary{border-top:1px solid var(--line);margin-top:22px;padding-top:18px}
body .upload-proposal .layout--table .decision-panel{display:grid;grid-template-columns:minmax(0,1.3fr) minmax(260px,.7fr);grid-template-areas:none;gap:28px}
.upload-proposal .layout--table .decision-secondary{margin:0;padding:0 0 0 24px;border-top:0;border-left:1px solid var(--line)}
body .upload-proposal .layout--table .decision-panel:not(:has(.case-primary)){display:block}
@media(max-width:800px){body .upload-proposal .layout--table .decision-panel{display:block}.upload-proposal .layout--table .decision-secondary{border-left:0;border-top:1px solid var(--line);margin-top:22px;padding:18px 0 0}}

`;

function html(variant, label, description) {
  const script = `
window.mockupErrors=[];window.addEventListener('error',event=>window.mockupErrors.push(event.message));
const params=new URLSearchParams(location.search);let state=params.get('state')||'decision';if(!${JSON.stringify(states)}.includes(state))state='decision';
document.body.dataset.state=state;document.getElementById('heading').textContent='Upload';
document.getElementById('surface').innerHTML=(${main.toString()})(state,'${variant}');
document.getElementById('controls').innerHTML=${JSON.stringify(options.map(([id,name])=>`<a href="pegasus_upload_${id}_v29.html?state=STATE" class="${id === variant ? 'active' : ''}">${id.toUpperCase()} · ${name}</a>`).join(''))}.replaceAll('STATE',state)+${JSON.stringify(states.map(s=>`<a href="?state=${s}" data-state-link="${s}">${s}</a>`).join(''))};
document.querySelectorAll('[data-state-link="'+state+'"]').forEach(x=>x.classList.add('active'));
document.getElementById('surface').addEventListener('click',e=>{
const button=e.target.closest('button');if(!button)return;
const count=state==='single'?'1 file':'11 files';
if(button.matches('[data-discard]'))showDialog('Discard '+count+'?','Source and processing record stay available.','Discard files','discarded');
if(button.matches('[data-propose]'))showDialog('Add to '+button.dataset.propose+'?',count+' · BH17RZV · '+button.dataset.claimant+' · '+button.dataset.stage,'Confirm and add','attached');
if(button.matches('[data-confirm]')){const input=document.getElementById('case-reference');if(!input.value.trim()){input.reportValidity();return}if(input.value.trim().toUpperCase()!=='QDOS26010'){input.setCustomValidity('No matching Case in this mockup. Try QDOS26010.');input.reportValidity();return}input.setCustomValidity('');showDialog('Add to QDOS26010?',count+' · BH17RZV · J. Morgan · Review','Confirm and add','attached')}
});
document.getElementById('surface').addEventListener('input',e=>e.target.setCustomValidity?.(''));
function showDialog(title,message,action,next){const previous=document.activeElement;const backdrop=document.createElement('div');backdrop.className='dialog-backdrop';backdrop.innerHTML='<div class="dialog" role="dialog" aria-modal="true" aria-labelledby="dialog-title"><h2 id="dialog-title">'+esc(title)+'</h2><p>'+esc(message)+'</p><div class="action-row"><button class="btn '+(next==='discarded'?'btn--danger':'btn--primary')+'" data-accept>'+action+'</button><button class="btn" data-close>Cancel</button></div></div>';document.body.append(backdrop);const close=()=>{backdrop.remove();previous?.focus()};backdrop.querySelector('[data-accept]').onclick=()=>{if(title.includes('QDOS25984')){location.href='?state=attached&case=QDOS25984'}else location.href='?state='+next};backdrop.querySelector('[data-close]').onclick=close;backdrop.addEventListener('keydown',x=>{if(x.key==='Escape')close();if(x.key==='Tab'){x.preventDefault();const buttons=backdrop.querySelectorAll('button');(document.activeElement===buttons[0]?buttons[1]:buttons[0]).focus()}});backdrop.querySelector('[data-close]').focus()}
if(state==='attached'&&params.get('case')==='QDOS25984'){document.querySelector('.decision-panel').innerHTML='<h2>Added to Case QDOS25984</h2><p>BH17RZV · A. Taylor · With Engineer</p><p>11 files</p>'+link('New upload','select')}

window.mockup={variant:'${variant}',state,files:document.querySelectorAll('[data-file-row]').length};
`;
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Upload · Pegasus · v29 ${variant.toUpperCase()}</title><style>${liveCss}\n${proposalCss}</style></head><body>${shellPrefix}<header class="page-header"><div class="page-title"><h1 id="heading">Upload</h1></div><div class="page-actions"><a class="btn" href="?state=select"><svg class="icon" aria-hidden="true"><use href="#icon-upload" /></svg>Upload more files</a></div></header><div class="upload-proposal" id="surface"></div></div></main></div></div><details class="mock-strip"><summary>Mockup controls · ${esc(label)}</summary><div class="mock-controls" id="controls"></div><p class="mock-note">Review only · ${esc(description)}</p></details><script>const fileNames=${JSON.stringify(fileNames)};const esc=${esc.toString()};const link=${link.toString()};${fileRows.toString()}${statusFor.toString()}${summary.toString()}${decision.toString()}${filePanel.toString()}${main.toString()}${script}</script></body></html>`;

}

await mkdir(here, { recursive: true });
for (const [id, label, description] of options) {
  await writeFile(path.join(here, `pegasus_upload_${id}_v29.html`),
    html(id,label,description).replace(/[ \t]+(?=\r?$)/gm, ''));
}
console.log(`Built ${options.length} self-contained mockups`);
