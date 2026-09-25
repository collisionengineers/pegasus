// v30 mockup build. One self-contained HTML file per surface, every state and
// layer pre-rendered into <template>s and chosen at runtime by query string,
// plus the walkthrough frame and the self-check. Offline: the live site CSS,
// Inter and the Pegasus marks are inlined; the shell is rebuilt from
// _Layout.cshtml at origin/dev (no working-set strip).
//
//   node build.mjs
import { writeFile } from 'node:fs/promises';
import path from 'node:path';
import { current, esc, loadAssets, shell } from './lib/shared.mjs';
import { signin } from './lib/signin.mjs';
import { inbox } from './lib/inbox.mjs';
import { workCentre } from './lib/work-centre.mjs';
import { accountsSurface } from './lib/accounts.mjs';
import { uploadCss, uploadMain, uploadOptions, uploadScript, uploadStates } from './lib/upload.mjs';

const assets = await loadAssets();
const surfaces = [signin, inbox, workCentre, accountsSurface];

// Proposal-only classes are prefixed p30- so they read as this round's
// proposals; Stage 2 maps each accepted one into the site.css vocabulary.
const proposalCss = `
/* Sign in (H, I, J, K) */
.p30-brand-copy{display:flex;flex-direction:column;line-height:1.05}
.p30-brand-sub{display:none;font-size:11px;color:var(--muted);letter-spacing:.02em;margin-top:4px}
.opt-brand-compact .p30-brand img{width:64px;height:64px}
.opt-brand-compact .p30-brand-sub{display:block}
.p30-title-short{display:none}
.opt-title-short .p30-title-long{display:none}
.opt-title-short .p30-title-short{display:inline}
.p30-pw{position:relative}
.p30-pw input{width:100%;padding-right:40px}
.p30-reveal{display:none;position:absolute;right:2px;top:50%;transform:translateY(-50%);color:var(--muted)}
.p30-reveal:hover{background:var(--surface-3);color:var(--ink)}
.opt-reveal-on .p30-reveal{display:grid}
/* Inbox (L, M, N, O) */
.p30-filter{grid-template-columns:minmax(0,1fr) minmax(0,1fr) minmax(0,1fr) minmax(0,1.8fr)}
.p30-searchrow{display:flex;gap:6px;min-width:0}
.p30-searchrow input{flex:1;min-width:0}
.p30-searchrow .btn{flex:none}
.p30 .inbox-messages-head .sort-toggle{display:inline-flex;align-items:center;gap:4px}
.p30 .inbox-messages-head .sort-toggle .icon{width:13px;height:13px}
.p30 .inbox-row .row-time{min-width:68px}
.p30-att{display:inline-flex;align-items:center;gap:3px;vertical-align:middle}
.p30-att .icon{width:12px;height:12px}
.p30 .inbox-row .inbox-row-action{opacity:.35;transition:opacity .14s ease}
.p30 .inbox-row:hover .inbox-row-action,.p30 .inbox-row:focus-within .inbox-row-action{opacity:1}
.p30-att-list li{display:inline-flex;align-items:center;gap:6px;padding:4px 8px;font-size:11px;border-radius:var(--radius);background:var(--surface-2)}
.p30-att-list li .icon{width:13px;height:13px;color:var(--muted)}
.p30-att-list .p30-att-none{color:var(--muted);background:transparent;border-style:dashed}
/* Work Centre (Q, R, S, T) */
.p30 .wc-refresh .wc-refresh-outcome{white-space:nowrap}
.opt-metrics-toned .wc-metrics .metric{box-shadow:inset 0 3px 0 var(--tone,transparent)}
.opt-metrics-toned .wc-metrics .metric[data-value="not_ready"],.opt-metrics-toned .wc-metrics .metric[data-value="held"],.opt-metrics-toned .wc-metrics .metric[data-value="unidentified"]{--tone:var(--amber)}
.opt-metrics-toned .wc-metrics .metric[data-value="review"],.opt-metrics-toned .wc-metrics .metric[data-value="triage"]{--tone:var(--navy)}
.p30 .wc-list .row-button .side{min-width:168px}
.p30 .wc-list .row-button .side span{font-variant-numeric:tabular-nums}
.p30 .wc-list .row-button .side .wc-due{font-size:12px}
.p30-head-meta{margin-left:auto;white-space:nowrap}
.p30 .pane-empty{min-height:220px}
/* Staff accounts (U, V, W, X, Y, Z) */
.p30-dialog-title{gap:10px;min-width:0}
.p30-dialog-title h2{margin:0}
.p30-value{min-height:var(--control);display:flex;align-items:center;padding:6px 10px;border:1px solid var(--line);border-radius:var(--radius);background:var(--surface-2);color:var(--ink);font-size:13px}
.p30-fields .field:last-child{grid-column:auto}
.p30-settings-list{border:1px solid var(--line);border-radius:var(--radius);background:var(--surface-2)}
.p30-settings-list .account-settings-section{border:0;background:transparent;margin-top:0;padding-top:0}
.p30-settings-list .account-settings-section+.account-settings-section{border-top:1px solid var(--line)}
.p30-settings-list .settings-line{padding:12px 16px}
.p30-foot .left{display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-right:auto}
.p30-foot .left form{display:contents}
.p30-foot-gap{width:1px;height:24px;background:var(--line);margin:0 4px}
[data-temporary-password] output{font-size:14px;font-weight:700;letter-spacing:.04em}
@media (max-width:1180px){.p30-filter{grid-template-columns:repeat(2,minmax(0,1fr))}.p30-filter .inbox-filter__search{grid-column:1/-1}}
@media (max-width:760px){.p30-filter{grid-template-columns:minmax(0,1fr)}.p30-foot .left{margin-right:0;width:100%}}
`;

// Demo control, not product UI: the strip, the walkthrough embed and the
// runtime that swaps templates and wires dialogs.
const mockCss = `
.mock-strip{position:fixed;bottom:10px;left:10px;z-index:1400;background:var(--nav);color:#fff;padding:8px 10px;border-radius:3px;max-width:calc(100vw - 20px);font-size:11px;box-shadow:0 8px 24px rgba(0,0,0,.35)}
.mock-strip summary{cursor:pointer;font-weight:600}
.mock-strip .mock-row{display:flex;flex-wrap:wrap;gap:6px;margin-top:7px;align-items:center}
.mock-strip .mock-row>b{font-weight:600;color:#c8ced0;margin-right:2px;min-width:52px}
.mock-strip a{color:#fff;border:1px solid #7b858a;padding:3px 6px;font-size:10px;text-decoration:none;border-radius:2px}
.mock-strip a.active{background:#fff;color:var(--nav)}
.mock-strip a.is-disabled{opacity:.4;pointer-events:none}
.mock-strip .mock-note{margin:7px 0 0;color:#c8ced0;font-size:10px;max-width:60ch}
body.embed .mock-strip{display:none}
dialog:not([class]){border:1px solid #3f3d3a;padding:20px;background:#fff;box-shadow:0 20px 70px rgba(0,0,0,.35);max-width:min(100%,480px)}
dialog:not([class])::backdrop{background:rgba(19,20,21,.62)}
dialog:not([class]) h2{font-size:17px;margin-bottom:12px}
`;

const runtime = `
window.mockupErrors=[];window.addEventListener('error',e=>window.mockupErrors.push(e.message));
(function(){
  const manifest=window.mockupManifest;
  const params=new URLSearchParams(location.search);
  const states=manifest.states.map(s=>s.id);
  let state=params.get('state')||states[0];if(!states.includes(state))state=states[0];
  let layer=params.get('layer')||(manifest.layers.includes('proposal')?'proposal':manifest.layers[0]);if(!manifest.layers.includes(layer))layer=manifest.layers[0];
  const opt=Object.fromEntries((params.get('opt')||'').split(',').filter(Boolean).map(pair=>pair.split(':')));
  const body=document.body;
  body.dataset.surface=manifest.key;body.dataset.state=state;body.dataset.layer=layer;
  if(manifest.variant)body.dataset.variant=manifest.variant;
  if(layer==='proposal')body.classList.add('p30');
  if(params.get('embed')==='1')body.classList.add('embed');
  for(const o of manifest.opts){const value=opt[o.key]||o.values[0];body.classList.add('opt-'+o.key+'-'+value);body.dataset['opt'+o.key]=value;}
  const template=document.querySelector('template[data-key="'+state+'|'+layer+'"]');
  document.getElementById('surface-host').insertAdjacentHTML('afterbegin',template.innerHTML);
  document.querySelectorAll('[data-dialog-open-on-load="true"]').forEach(d=>d.hidden=false);
  document.querySelectorAll('dialog[data-native-open]').forEach(d=>{try{d.showModal()}catch(err){d.setAttribute('open','')}});
  document.addEventListener('click',e=>{
    const open=e.target.closest('[data-dialog-open]');
    if(open){const d=document.querySelector('[data-dialog="'+open.dataset.dialogOpen+'"]');if(d){d.hidden=false;(d.querySelector('[data-dialog-initial-focus]')||d.querySelector('h2'))?.focus();}return;}
    const nativeOpen=e.target.closest('[data-native-dialog-open]');
    if(nativeOpen){const d=document.getElementById(nativeOpen.dataset.nativeDialogOpen);if(d)d.showModal();return;}
    const close=e.target.closest('[data-dialog-close]');
    if(close){const d=close.closest('[data-dialog]');if(d)d.hidden=true;return;}
    if(e.target.matches('.dialog-backdrop')){e.target.hidden=true;return;}
    const dismiss=e.target.closest('[data-dismiss]');
    if(dismiss){dismiss.closest('.notice')?.remove();return;}
    const reveal=e.target.closest('[data-reveal]');
    if(reveal){const input=document.getElementById(reveal.getAttribute('aria-controls'));const on=input.type==='password';input.type=on?'text':'password';reveal.setAttribute('aria-pressed',on?'true':'false');reveal.setAttribute('aria-label',on?'Hide password':'Show password');return;}
    const rail=e.target.closest('[data-rail-toggle]');
    if(rail){const shellEl=document.querySelector('[data-app-shell]');const collapsed=shellEl.classList.toggle('rail-collapsed');rail.setAttribute('aria-expanded',collapsed?'false':'true');rail.setAttribute('aria-label',collapsed?rail.dataset.labelExpand:rail.dataset.labelCollapse);return;}
  });
  document.addEventListener('keydown',e=>{if(e.key==='Escape')document.querySelectorAll('[data-dialog]:not([hidden])').forEach(d=>d.hidden=true);});
  const href=(next)=>{const q=new URLSearchParams(location.search);for(const [k,v] of Object.entries(next)){if(v==null)q.delete(k);else q.set(k,v);}return '?'+q.toString();};
  const optString=(o)=>Object.entries(o).map(([k,v])=>k+':'+v).join(',')||null;
  const strip=document.getElementById('mock-strip');
  if(strip){
    const rows=[];
    if(manifest.variants){rows.push('<div class="mock-row"><b>Option</b>'+manifest.variants.map(v=>'<a href="'+v.file+href({})+'" class="'+(v.id===manifest.variant?'active':'')+'">'+v.id.toUpperCase()+' · '+v.label+'</a>').join('')+'</div>');}
    rows.push('<div class="mock-row" id="controls"><b>State</b>'+manifest.states.map(s=>'<a href="'+href({state:s.id})+'" data-state-link="'+s.id+'" class="'+(s.id===state?'active':'')+'">'+s.id+'</a>').join('')+'</div>');
    if(manifest.layers.length>1)rows.push('<div class="mock-row"><b>Layer</b>'+manifest.layers.map(l=>'<a href="'+href({layer:l})+'" class="'+(l===layer?'active':'')+'">'+l+'</a>').join('')+'</div>');
    for(const o of manifest.opts){rows.push('<div class="mock-row"><b>'+o.label+'</b>'+o.values.map(v=>'<a href="'+href({opt:optString({...Object.fromEntries(manifest.opts.map(x=>[x.key,body.dataset['opt'+x.key]])),[o.key]:v})})+'" class="'+(body.dataset['opt'+o.key]===v?'active':'')+(layer==='baseline'?' is-disabled':'')+'">'+v+'</a>').join('')+(o.item?' <span class="mock-note" style="margin:0">item '+o.item+'</span>':'')+'</div>');}
    rows.push('<div class="mock-row"><a href="pegasus_v30_walkthrough.html#area='+manifest.key+'&state='+state+'&layer='+layer+'">Open in the walkthrough</a></div>');
    strip.querySelector('.mock-body').innerHTML=rows.join('')+'<p class="mock-note">Review only · '+manifest.note+'</p>';
  }
})();
`;

function document_({ key, title, variant, variants, states, layers, opts, note, templates, extraCss = '', extraScript = '' }) {
  const manifest = { key, variant, variants, states: states.map(({ id, label }) => ({ id, label })), layers, opts, note };
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>${esc(title)} · Pegasus · v30</title>
<!--
  v30 mockup, ${esc(title)}. Temporary design review artifact: not application
  code, not design authority, not implementation evidence. The strip at the
  bottom left is demo control, not product UI. Built by build.mjs from the live
  shell, site.css and labels at origin/dev; states and layers are query strings.
-->
<style>${assets.css}
${proposalCss}
${extraCss}
${mockCss}</style>
</head>
<body>
${assets.sprite}
<div id="surface-host"></div>
${templates.map(({ state, layer, html }) => `<template data-key="${state}|${layer}">${html}</template>`).join('\n')}
<details class="mock-strip" id="mock-strip" open><summary>Mockup controls · ${esc(title)}</summary><div class="mock-body"></div></details>
<script>window.mockupManifest=${JSON.stringify(manifest)};</script>
<script>${runtime}</script>
${extraScript ? `<script>${extraScript}</script>` : ''}
</body>
</html>`;
}

const written = [];
const write = async (file, html) => {
  await writeFile(path.join(current, file), html.replace(/[ \t]+(?=\r?$)/gm, ''));
  written.push(file);
};

// The four polished surfaces.
for (const surface of surfaces) {
  const templates = [];
  for (const state of surface.states) {
    for (const layer of surface.layers) {
      const inner = surface.render(state.id, layer, assets);
      const html = surface.frame === 'auth' ? inner : shell({ route: surface.route, content: inner, assets });
      templates.push({ state: state.id, layer, html });
    }
  }
  await write(surface.file, document_({
    key: surface.key,
    title: surface.title,
    states: surface.states,
    layers: surface.layers,
    opts: surface.opts,
    note: 'Baseline is the live page rebuilt from origin/dev with a synthetic fixture; Proposal layers this round’s lettered items over it. Nothing here reads or writes Pegasus data.',
    templates,
  }));
}

// The five Upload alternatives, one file each, proposals only.
const variants = uploadOptions.map(([id, label]) => ({ id, label, file: `pegasus_upload_${id}_v30.html` }));
for (const [id, label, description] of uploadOptions) {
  const templates = uploadStates.map((state) => ({
    state,
    layer: 'proposal',
    html: shell({
      route: 'upload',
      assets,
      content: `<header class="page-header"><div class="page-title"><h1 id="heading">Upload</h1></div><div class="page-actions"><a class="btn" href="?state=select"><svg class="icon" aria-hidden="true"><use href="#icon-upload" /></svg><span>Upload more files</span></a></div></header>
<div class="upload-proposal" id="surface">${uploadMain(state, id)}</div>`,
    }),
  }));
  await write(`pegasus_upload_${id}_v30.html`, document_({
    key: `upload-${id}`,
    title: `Upload · ${id.toUpperCase()} ${label}`,
    variant: id,
    variants,
    states: uploadStates.map((s) => ({ id: s, label: s })),
    layers: ['proposal'],
    opts: [],
    note: description,
    templates,
    extraCss: uploadCss,
    extraScript: uploadScript,
  }));
}

// The walkthrough: one frame, every area, state, layer and switch.
const areas = [
  ...surfaces.map((s) => ({ key: s.key, file: s.file, title: s.title, states: s.states.map(({ id, label }) => ({ id, label })), layers: s.layers, opts: s.opts })),
  ...uploadOptions.map(([id, label]) => ({ key: `upload-${id}`, file: `pegasus_upload_${id}_v30.html`, title: `Upload · ${id.toUpperCase()} ${label}`, states: uploadStates.map((s) => ({ id: s, label: s })), layers: ['proposal'], opts: [] })),
];
await write('pegasus_v30_walkthrough.html', `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Pegasus v30 walkthrough</title>
<!--
  v30 walkthrough. A frame around the surface files: the bar at the top is demo
  control, not product UI. Temporary design review artifact: not application
  code, not design authority, not implementation evidence.
-->
<style>
  html,body{height:100%;margin:0;background:#2a2f32;font:12px/1.4 system-ui,-apple-system,"Segoe UI",sans-serif;color:#e7e9ea}
  body{display:grid;grid-template-rows:auto 1fr}
  .bar{display:flex;flex-wrap:wrap;align-items:center;gap:8px 14px;padding:6px 10px;background:#1b1e20;border-bottom:1px solid #33383b}
  .bar strong{font-weight:600}
  .bar label{display:inline-flex;align-items:center;gap:6px;color:#9aa0a3}
  .bar select,.bar button,.bar a.button{font:inherit;background:#2a2f32;color:#e7e9ea;border:1px solid #454b4f;border-radius:3px;padding:3px 8px;text-decoration:none;cursor:pointer}
  .bar select{max-width:46ch}
  .bar button[aria-pressed="true"]{background:#e7e9ea;color:#1b1e20}
  .bar button:disabled{opacity:.4;cursor:default}
  .bar .note{color:#9aa0a3;font-style:italic}
  .bar .opts{display:inline-flex;gap:10px;flex-wrap:wrap}
  .bar .opts span[role="group"]{display:inline-flex;gap:2px;align-items:center}
  .stage{display:flex;justify-content:center;overflow:auto;min-height:0}
  iframe{border:0;background:#fff;height:100%;width:100%;flex:none}
</style>
</head>
<body>
  <div class="bar" role="group" aria-label="Mockup controls (demo control, not product UI)">
    <strong>v30 walkthrough</strong>
    <label>Area <select id="area"></select></label>
    <label>State <select id="state"></select></label>
    <span role="group" aria-label="Layer" id="layers"><button type="button" data-layer="proposal" aria-pressed="true">Proposal</button><button type="button" data-layer="baseline" aria-pressed="false">Baseline</button></span>
    <span class="opts" id="opts"></span>
    <span role="group" aria-label="Width" id="widths"><button type="button" data-width="" aria-pressed="true">Fit</button><button type="button" data-width="1580" aria-pressed="false">1580</button><button type="button" data-width="1440" aria-pressed="false">1440</button><button type="button" data-width="760" aria-pressed="false">760</button></span>
    <a class="button" id="alone" href="#" target="_blank" rel="noopener">Open alone</a>
    <a class="button" href="pegasus_signin_designs_v30.html">3 login designs</a>
    <a class="button" href="pegasus_work_centre_designs_v30.html">3 Work Centre designs</a>
    <a class="button" href="pegasus_upload_designs_v30.html">5 new Upload designs</a>
    <span class="note" id="note"></span>
  </div>
  <div class="stage"><iframe id="view" title="Mockup"></iframe></div>
<script>
const areas=${JSON.stringify(areas)};
const areaSelect=document.getElementById('area'),stateSelect=document.getElementById('state'),view=document.getElementById('view'),alone=document.getElementById('alone'),note=document.getElementById('note'),optsHost=document.getElementById('opts');
const params=()=>new URLSearchParams(location.hash.slice(1));
const setHash=(next)=>{const q=params();for(const [k,v] of Object.entries(next)){if(v==null||v==='')q.delete(k);else q.set(k,v);}location.hash=q.toString();};
areaSelect.innerHTML=areas.map(a=>'<option value="'+a.key+'">'+a.title+'</option>').join('');
function render(){
  const q=params();
  const area=areas.find(a=>a.key===q.get('area'))||areas[0];
  areaSelect.value=area.key;
  stateSelect.innerHTML=area.states.map(s=>'<option value="'+s.id+'">'+s.label+'</option>').join('');
  const state=area.states.some(s=>s.id===q.get('state'))?q.get('state'):area.states[0].id;
  stateSelect.value=state;
  const layer=area.layers.includes(q.get('layer'))?q.get('layer'):area.layers[0];
  document.querySelectorAll('#layers button').forEach(b=>{b.setAttribute('aria-pressed',b.dataset.layer===layer?'true':'false');b.disabled=!area.layers.includes(b.dataset.layer);});
  const opt=Object.fromEntries((q.get('opt')||'').split(',').filter(Boolean).map(p=>p.split(':')));
  optsHost.innerHTML=area.opts.map(o=>'<span role="group" aria-label="'+o.label+'"><label>'+o.label+(o.item?' ('+o.item+')':'')+'</label>'+o.values.map(v=>'<button type="button" data-opt="'+o.key+'" data-value="'+v+'" aria-pressed="'+((opt[o.key]||o.values[0])===v?'true':'false')+'"'+(layer==='baseline'?' disabled':'')+'>'+v+'</button>').join('')+'</span>').join('');
  const width=q.get('w')||'';
  document.querySelectorAll('#widths button').forEach(b=>b.setAttribute('aria-pressed',b.dataset.width===width?'true':'false'));
  view.style.width=width?width+'px':'100%';
  const optString=Object.entries(opt).map(([k,v])=>k+':'+v).join(',');
  const src=area.file+'?state='+state+'&layer='+layer+(optString?'&opt='+optString:'');
  if(view.dataset.src!==src+'|embed'){view.src=src+'&embed=1';view.dataset.src=src+'|embed';}
  alone.href=src;
  note.textContent=area.states.find(s=>s.id===state).label+(area.layers.length===1?' · proposals only':'');
}
areaSelect.addEventListener('change',()=>setHash({area:areaSelect.value,state:null,opt:null}));
stateSelect.addEventListener('change',()=>setHash({state:stateSelect.value}));
document.getElementById('layers').addEventListener('click',e=>{const b=e.target.closest('button');if(b&&!b.disabled)setHash({layer:b.dataset.layer});});
document.getElementById('widths').addEventListener('click',e=>{const b=e.target.closest('button');if(b)setHash({w:b.dataset.width});});
optsHost.addEventListener('click',e=>{const b=e.target.closest('button[data-opt]');if(!b||b.disabled)return;const q=params();const opt=Object.fromEntries((q.get('opt')||'').split(',').filter(Boolean).map(p=>p.split(':')));opt[b.dataset.opt]=b.dataset.value;setHash({opt:Object.entries(opt).map(([k,v])=>k+':'+v).join(',')});});
window.addEventListener('hashchange',render);
render();
</script>
</body>
</html>`);

// The self-check: every surface × state × layer, the upload options × states,
// the generic invariants and each state's expected markers.
const checks = [
  ...surfaces.flatMap((s) => s.states.flatMap((state) => s.layers.map((layer) => ({ file: s.file, key: s.key, state: state.id, layer, expect: state.expect ?? [], forbid: state.forbid ?? [], opts: s.opts.map((o) => ({ key: o.key, values: o.values })) })))),
  ...uploadOptions.flatMap(([id]) => uploadStates.map((state) => ({ file: `pegasus_upload_${id}_v30.html`, key: `upload-${id}`, variant: id, state, layer: 'proposal', expect: ['.decision-panel', '#surface'], forbid: [], opts: [] }))),
];
await write('v30-selfcheck.html', `<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>v30 mockup self-check</title></head>
<body>
<h1>v30 mockup self-check</h1>
<pre id="result">Running…</pre>
<iframe id="target" title="Mockup under check" width="1580" height="1000"></iframe>
<script>
const checks=${JSON.stringify(checks)};
const fail=[];let okCount=0;
const frame=document.getElementById('target');
function check(name,condition){if(condition)okCount++;else fail.push(name);}
function load(url){return new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(new Error('Timed out: '+url)),8000);frame.onload=()=>{clearTimeout(timer);setTimeout(()=>resolve(frame.contentWindow),60);};frame.src=url;});}
(async()=>{
  try{
    for(const c of checks){
      const prefix=c.key+'/'+c.state+'/'+c.layer;
      const win=await load(c.file+'?state='+c.state+'&layer='+c.layer);
      const doc=win.document;
      check(prefix+' no script error',win.mockupErrors&&win.mockupErrors.length===0);
      check(prefix+' surface',doc.body.dataset.surface===c.key);
      check(prefix+' state',doc.body.dataset.state===c.state);
      check(prefix+' layer',doc.body.dataset.layer===c.layer);
      check(prefix+' main landmark',!!doc.querySelector('#main-content'));
      check(prefix+' no working-set strip',!doc.querySelector('.workspace-tabs'));
      check(prefix+' offline',!doc.querySelector('[src^="http"],[href^="http"],link[rel="stylesheet"]'));
      check(prefix+' one h1',doc.querySelectorAll('h1').length===1);
      for(const sel of c.expect)check(prefix+' expects '+sel,!!doc.querySelector(sel));
      for(const sel of c.forbid)check(prefix+' forbids '+sel,!doc.querySelector(sel));
      if(doc.body.dataset.surface!=='signin'){
        check(prefix+' rail current route',!!doc.querySelector('.nav-link[aria-current="page"]'));
        check(prefix+' refresh partial or none',doc.querySelectorAll('.refresh-button').length<=1);
      }
      const openDialog=doc.querySelector('[data-dialog]:not([hidden]) .dialog-close');
      if(openDialog){openDialog.click();check(prefix+' dialog closes',!doc.querySelector('[data-dialog]:not([hidden])'));}
      if(c.variant){
        check(prefix+' upload script',win.mockup?.variant===c.variant&&win.mockup?.state===c.state);
        check(prefix+' files',win.mockup?.files===(c.state==='select'?0:c.state==='single'?1:11));
        check(prefix+' controls',doc.querySelectorAll('#controls a').length===${uploadStates.length});
        if(['decision','registered','mixed','single','no-match','multiple'].includes(c.state)){
          check(prefix+' discard',!!doc.querySelector('[data-discard]'));
          check(prefix+' case search',!!doc.querySelector('#case-reference'));
          doc.querySelector('.discard').open=true;doc.querySelector('[data-discard]').click();
          check(prefix+' discard confirmation',!!doc.querySelector('.mock-dialog-backdrop [role="dialog"]'));
          doc.querySelector('.mock-dialog-backdrop [data-close]').click();
          doc.querySelector('.attach').open=true;doc.querySelector('#case-reference').value='QDOS26010';doc.querySelector('[data-confirm]').click();
          check(prefix+' Case confirmation',!!doc.querySelector('.mock-dialog-backdrop [role="dialog"]'));
          doc.querySelector('.mock-dialog-backdrop [data-close]').click();
        }
        if(['decision','registered','mixed','single','multiple'].includes(c.state)){
          const proposal=doc.querySelector('[data-propose]');
          check(prefix+' proposal visible',!!proposal&&!proposal.closest('details'));
          check(prefix+' Case first',doc.querySelector('.decision-panel').firstElementChild.classList.contains('case-primary'));
        }
      }
      if(c.key==='signin'&&c.layer==='proposal'&&c.state==='default'){
        const w2=await load(c.file+'?state=default&layer=proposal&opt=brand:compact,title:short,reveal:on');
        const d2=w2.document;
        check(prefix+' compact brand',d2.body.classList.contains('opt-brand-compact')&&d2.querySelector('.p30-brand-sub'));
        check(prefix+' short title',w2.getComputedStyle(d2.querySelector('.p30-title-long')).display==='none');
        const reveal=d2.querySelector('[data-reveal]');reveal.click();
        check(prefix+' reveal toggles',d2.getElementById('Password').type==='text'&&reveal.getAttribute('aria-pressed')==='true');
      }
      if(c.key==='work-centre'&&c.layer==='proposal'&&c.state==='default'){
        const w2=await load(c.file+'?state=default&layer=proposal&opt=metrics:toned');
        check(prefix+' toned metrics',w2.document.body.classList.contains('opt-metrics-toned'));
        check(prefix+' Updated in header',/Updated 09:41/.test(w2.document.querySelector('.wc-refresh-outcome').textContent));
        check(prefix+' no stray Updated line',!w2.document.querySelector('.metric-section .wc-freshness'));
      }
      if(c.key==='accounts'&&c.state==='settings-self'){
        const role=doc.getElementById('settings-alex-role');
        check(prefix+' role control',c.layer==='proposal'?role.classList.contains('p30-value'):role.disabled);
        check(prefix+' fact grid',c.layer==='proposal'?!doc.querySelector('.account-settings-facts'):!!doc.querySelector('.account-settings-facts'));
      }
    }
  }catch(error){fail.push('EXC '+error.message);}
  document.getElementById('result').textContent='RESULT '+JSON.stringify({fail,okCount});
})();
</script>
</body>
</html>`);

console.log(`Built ${written.length} files:\n${written.map((f) => '  ' + f).join('\n')}`);
