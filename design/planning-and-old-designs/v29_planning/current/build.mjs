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
  ['d', 'Operations table', 'A dense table makes mixed outcomes easy to compare, with actions in a fixed footer.'],
  ['e', 'Evidence gallery', 'A small image contact sheet gives visual context while a concise result card owns the decision.'],
];
const states = ['select', 'chosen', 'storing', 'processing', 'decision', 'registered', 'mixed', 'single', 'failed', 'discarded'];
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
  if (state === 'decision') return '<span class="status status--green">Ready</span>';
  if (state === 'single') return '<span class="status status--green">Vehicle images</span>';
  if (state === 'registered') return '<span class="status status--green">Vehicle images</span>';
  if (state === 'discarded') return '<span class="status status--neutral">Discarded</span>';
  return '<span class="status status--neutral">Selected</span>';
}
function fileRows(state, mode = 'rows') {
  const show = state === 'select' ? 0 : state === 'single' ? 1 : fileNames.length;
  if (!show) return '<p class="empty">No files selected.</p>';
  return fileNames.slice(0, show).map((name, i) => {
    const icon = mode === 'gallery' ? `<span class="tile-thumb" aria-hidden="true">${i + 1}</span>` : '<span class="file-glyph" aria-hidden="true">▧</span>';
    const detail = state === 'mixed' && i === 3 ? '<small>Open file · Could not be read</small>' : `<small>${(0.23 + i * 0.01).toFixed(2)} MB</small>`;
    return `<li class="file-row" data-file-row>${icon}<span class="file-name" title="${esc(name)}"><strong>${esc(name)}</strong>${detail}</span>${statusFor(state, i)}</li>`;
  }).join('');
}
function summary(state) {
  switch(state) {
    case 'storing': return '<strong>Uploading · 6 of 11 stored</strong>';
    case 'processing': return '<strong>11 stored · 3 processing</strong>';
    case 'mixed': return '<strong>1 file could not be read</strong>';
    case 'failed': return '<strong>1 upload failed</strong>';
    case 'discarded': return '<strong>Source and processing record retained</strong>';
    default: return '<strong>11 files selected</strong>';
  }
}
function decision(state) {
  if (state === 'single') return `<section class="decision-panel"><h2>Vehicle images · BH17RZV-01</h2><a class="record-link" href="?state=single">Open Vehicle images →</a><details class="attach"><summary>Add to an existing Case</summary><div class="field"><label for="case-reference">Case/PO</label><input id="case-reference" placeholder="Search by Case/PO or registration" /></div><div class="action-row"><button type="button" class="btn btn--primary" data-confirm>Review Case</button></div></details></section>`;
  if (state === 'select' || state === 'chosen') return `<section class="decision-panel"><h2>Select files</h2><div class="droparea"><span class="drop-glyph">⇧</span><strong>Drag files here or choose files</strong><span>EML, MSG, PDF, DOC, DOCX, JPG, PNG, MP4 or MOV · 100 MB each · 20 files</span><label class="btn" for="file-input">Choose files</label><input id="file-input" type="file" multiple aria-label="Choose files" /></div><div class="action-row">${state === 'chosen' ? link('Upload 11 files','storing','btn--primary') : ''}${state === 'chosen' ? link('Clear','select') : ''}</div></section>`;
  if (state === 'storing' || state === 'processing') return `<section class="decision-panel"><h2>${summary(state)}</h2>${state === 'storing' ? '<div class="progress" role="progressbar" aria-label="Files stored" aria-valuenow="6" aria-valuemin="0" aria-valuemax="11"><i style="width:55%"></i></div>' : link('Refresh status','decision')}</section>`;
  if (state === 'failed') return `<section class="decision-panel"><h2>1 upload failed</h2><div class="action-row">${link('Choose files again','chosen','btn--primary')}${link('New upload','select')}</div></section>`;
  if (state === 'discarded') return `<section class="decision-panel"><h2>Submission discarded</h2><p>Source and processing record retained.</p>${link('New upload','select','btn--primary')}</section>`;
  const registered = state === 'registered';
  return `<section class="decision-panel" id="decision"><h2>${registered ? 'Vehicle images · BH17RZV-01' : 'Choose destination'}</h2>${state === 'mixed' ? `<div class="state-summary">${summary(state)}</div>` : ''}${registered ? '<a class="record-link" href="?state=registered#decision">Open Vehicle images →</a>' : `<div class="field"><label for="registration">Vehicle registration</label><input id="registration" value="BH17RZV" /></div><div class="field"><label for="reason">Reason</label><input id="reason" required /></div><div class="action-row"><button type="button" class="btn btn--primary" data-register>Register Vehicle images</button>${link('Leave undecided','select')}</div>`}<details class="attach"><summary>Add to an existing Case</summary><div class="suggestion"><span>Possible Case</span><strong>QDOS26010 · BH17RZV · Review</strong><button type="button" class="btn btn--small" data-suggest>Use this Case</button></div><div class="field"><label for="case-reference">Case/PO</label><input id="case-reference" type="text" placeholder="Search by Case/PO or registration" /></div><div class="action-row"><button type="button" class="btn btn--primary" data-confirm>Review Case</button></div></details><details class="discard"><summary>Discard submission</summary><p>Discard all 11 files; source and processing record stay available.</p><button type="button" class="btn btn--danger" data-discard>Discard 11 files</button></details></section>`;
}
function filePanel(state, variant) {
  const gallery = variant === 'e';
  return `<section class="files-panel" aria-labelledby="files-title"><div class="panel-head"><h2 id="files-title">${state === 'single' ? 'Uploaded file' : 'Files in this submission'}</h2><span>${state === 'single' ? '1 file' : '11 files'}</span></div><ul class="file-list ${gallery ? 'file-list--gallery' : ''}">${fileRows(state, gallery ? 'gallery' : 'rows')}</ul></section>`;
}
function main(state, variant) {
  const files = filePanel(state, variant);
  const decide = decision(state);
  if (variant === 'a') return `<div class="layout layout--split">${files}${decide}</div>`;
  if (variant === 'b') return `<div class="steps"><span class="step ${state === 'select' || state === 'chosen' ? 'is-current' : ''}">1 · Select</span><span class="step ${state === 'storing' || state === 'processing' ? 'is-current' : ''}">2 · Process</span><span class="step ${['decision','registered','mixed'].includes(state) ? 'is-current' : ''}">3 · Decide</span></div><div class="layout layout--single">${decide}${files}</div>`;
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
.mock-strip{position:fixed;bottom:10px;left:10px;z-index:90;background:var(--nav);color:#fff;padding:8px 10px;border-radius:3px;max-width:calc(100vw - 20px)}.mock-strip summary{cursor:pointer;font-size:11px}.mock-controls{display:flex;flex-wrap:wrap;gap:6px;margin-top:7px}.mock-controls a{color:#fff;border:1px solid #7b858a;padding:4px 6px;font-size:10px}.mock-controls a.active{background:#fff;color:var(--nav)}.mock-note{font-size:10px;margin:6px 0 0;color:#c8ced0}
.dialog-backdrop{position:fixed;inset:0;z-index:80;background:#0008;display:grid;place-items:center}.dialog{background:#fff;width:min(440px,calc(100vw - 30px));padding:20px;box-shadow:var(--shadow)}
@media(max-width:1100px){.upload-proposal .layout--split,.upload-proposal .layout--destination,.upload-proposal .layout--gallery{grid-template-columns:1fr}.upload-proposal .layout--destination .evidence-index{grid-column:1}.upload-proposal .layout--gallery .decision-panel{grid-row:1}.upload-proposal .layout--gallery .files-panel{grid-row:2}}
@media(max-width:800px){.upload-proposal .file-list--gallery{grid-template-columns:repeat(2,minmax(0,1fr))}.upload-proposal .layout--table .decision-panel{display:block}.upload-proposal .file-row{grid-template-columns:20px minmax(0,1fr) auto}}
@media(max-width:1100px){.upload-proposal .layout--split .decision-panel{grid-row:1}.upload-proposal .layout--split .files-panel{grid-row:2}}
body[data-state="processing"] .upload-proposal .layout--table .decision-panel,body[data-state="storing"] .upload-proposal .layout--table .decision-panel{display:block}
`;

function html(variant, label, description) {
  const script = `
window.mockupErrors=[];window.addEventListener('error',event=>window.mockupErrors.push(event.message));
const params=new URLSearchParams(location.search);let state=params.get('state')||'decision';if(!${JSON.stringify(states)}.includes(state))state='decision';
document.body.dataset.state=state;document.getElementById('heading').textContent='Upload';
document.getElementById('surface').innerHTML=(${main.toString()})(state,'${variant}');
document.getElementById('controls').innerHTML=${JSON.stringify(options.map(([id,name])=>`<a href="pegasus_upload_${id}_v29.html?state=STATE" class="${id === variant ? 'active' : ''}">${id.toUpperCase()} · ${name}</a>`).join(''))}.replaceAll('STATE',state)+${JSON.stringify(states.map(s=>`<a href="?state=${s}" data-state-link="${s}">${s}</a>`).join(''))};
document.querySelectorAll('[data-state-link="'+state+'"]').forEach(x=>x.classList.add('active'));
document.getElementById('surface').addEventListener('click',e=>{if(e.target.matches('[data-discard]'))showDialog('Discard 11 files?','Source and processing record stay available.','Discard files','discarded');if(e.target.matches('[data-register]')){const reason=document.getElementById('reason');if(!reason.value.trim()){reason.setCustomValidity('Enter a reason.');reason.reportValidity();reason.focus()}else{reason.setCustomValidity('');location.href='?state=registered'}}if(e.target.matches('[data-confirm]'))showDialog('Add to QDOS26010?','11 files · BH17RZV · Review','Confirm and add','registered');if(e.target.matches('[data-suggest]')){const input=document.getElementById('case-reference');if(input){input.value='QDOS26010';input.closest('details').open=true;input.focus()}}});
function showDialog(title,message,action,next){const backdrop=document.createElement('div');backdrop.className='dialog-backdrop';backdrop.innerHTML='<div class="dialog" role="dialog" aria-modal="true" aria-labelledby="dialog-title"><h2 id="dialog-title">'+title+'</h2><p>'+message+'</p><div class="action-row"><button class="btn '+(next==='discarded'?'btn--danger':'btn--primary')+'" data-accept>'+action+'</button><button class="btn" data-close>Keep this page</button></div></div>';document.body.append(backdrop);backdrop.querySelector('[data-accept]').onclick=()=>location.href='?state='+next;backdrop.querySelector('[data-close]').onclick=()=>backdrop.remove();backdrop.addEventListener('keydown',x=>{if(x.key==='Escape')backdrop.remove()});backdrop.querySelector('[data-close]').focus()}
window.mockup={variant:'${variant}',state,files:document.querySelectorAll('[data-file-row]').length};
`;
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Upload · Pegasus · v29 ${variant.toUpperCase()}</title><style>${liveCss}\n${proposalCss}</style></head><body>${shellPrefix}<header class="page-header"><div class="page-title"><p class="eyebrow">Upload</p><h1 id="heading">Upload</h1></div><div class="page-actions"><a class="btn btn--primary" href="?state=select"><svg class="icon" aria-hidden="true"><use href="#icon-upload" /></svg>Upload more files</a></div></header><div class="upload-proposal" id="surface"></div></div></main></div></div><details class="mock-strip"><summary>Mockup controls · ${esc(label)}</summary><div class="mock-controls" id="controls"></div><p class="mock-note">Review only · ${esc(description)}</p></details><script>const fileNames=${JSON.stringify(fileNames)};const esc=${esc.toString()};const link=${link.toString()};${fileRows.toString()}${statusFor.toString()}${summary.toString()}${decision.toString()}${filePanel.toString()}${main.toString()}${script}</script></body></html>`;

}

await mkdir(here, { recursive: true });
for (const [id, label, description] of options) {
  await writeFile(path.join(here, `pegasus_upload_${id}_v29.html`),
    html(id,label,description).replace(/[ \t]+(?=\r?$)/gm, ''));
}
console.log(`Built ${options.length} self-contained mockups`);
