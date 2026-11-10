/* ============================================================
   Collision Engineers – guided vehicle photo capture.
   Converted from the v10.34 test build (handover of 5 November 2026) into a
   static asset of Pegasus.Web; src/Pegasus.Web/PhotoCapture/README.md has the rules.
   The page renders shell.html and two JSON blocks: #capture-assets (image
   URLs by logical name) and #capture-context (the link context).
   ============================================================ */
(function () {
'use strict';

/* ---------- page data ---------- */
function readJson(id){ const el=document.getElementById(id); if(!el) return null; try{ return JSON.parse(el.textContent); }catch(e){ return null; } }
const ASSETS = readJson('capture-assets') || {};
/* keyed by the path under img/ without the extension ("d/fd-1-o"); a key that keeps the extension ("d/fd-1-o.jpg") works too */
function assetUrl(name){ return ASSETS[name] || ASSETS[name+'.jpg'] || ASSETS[name+'.webp'] || ''; }
function asset(name){ return esc(assetUrl(name)); }
function esc(v){ return String(v==null?'':v).replace(/[&<>"']/g, c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'})[c]); }

/* The link credential: /c/{token} is a capture, /s/{slug} a standing link.
   It travels only in the X-Capture-Link header – never a query string, never storage, never a log. */
/* The API sits beside this script, so a path base the site is served under carries through. */
const API = document.currentScript ? new URL('api/v1/', document.currentScript.src).pathname : '/capture/api/v1/';
const PATHM = /\/(c|s)\/([^/?#]+)\/?$/.exec(location.pathname);
let LINK = PATHM ? PATHM[1]+'.'+PATHM[2] : '';
const isStanding = () => LINK.indexOf('s.')===0;

/* The one sentence the prototype never needed because it had no network (operator, 9 November 2026; R786). */
const COPY = { offline: 'We couldn’t reach Collision Engineers. Check your signal and try again.' };

let CTX = null, PRIOR = null, LIVE = false, GUIDED = true, GONE = false;
let SRC = null, CASE = { reg:'' }, STEPS = [], SEQ = [], TITLES = {}, KEY = '';

function setContext(ctx){
  CTX = ctx || {};
  if(!CTX.counts) CTX.counts = { total:0, byKind:{}, overviewPositions:[], damage:[] };
  /* what earlier visits sent, fixed before this visit queues anything: a later context's counts include this visit's own uploads */
  if(!PRIOR || !JOBS.length) PRIOR = CTX.counts;
  LIVE = !!CTX.liveCameraOnly;
  GUIDED = CTX.mode !== 'quick';
  /* originals are off while the capture has no Case (R801): what waits to go is dropped, and nothing more is sent */
  if(!CTX.originals) JOBS.forEach(j=>{ if(j.original && j.state==='queued') dropOriginal(j); });
}
/* Which start screen and first form: replaces the prototype's #claimsco / #bodyshop / #client hash. */
function mkSrc(kind, st, preset){ return { kind, preset:!!preset, name:(st&&st.name)||'', addresses:(st&&Array.isArray(st.addresses))?st.addresses:[], lifetimeDays:(st&&st.lifetimeDays)||7 }; }
function sourceFrom(ctx){
  const st = ctx.standing;
  /* a quick link sent from a Case: the handover's "case link sent by HQ" start and form for that kind of firm, with the registration fixed (R798, R808) */
  if(ctx.origin==='case' && ctx.quickLinkFor) return mkSrc(ctx.quickLinkFor==='bodyshop'?'bodyshop':'source', st, true);
  if(ctx.kind==='standing') return (st&&st.kind==='website') ? mkSrc('client',st) : mkSrc(st&&st.isRepairer?'bodyshop':'source', st);
  if(ctx.origin==='website') return mkSrc('client', st);
  if(ctx.origin==='staff') return mkSrc(st&&st.isRepairer?'bodyshop':'source', st);
  return null; /* a Case link, or a link a bodyshop/AMC forwarded to its customer */
}

/* Already received on this link (reopening): counts from the server. */
function rc(kind){ return (PRIOR.byKind && PRIOR.byKind[kind]) || 0; }
function rcDmg(zone, stage){ return (PRIOR.damage||[]).filter(d=>d.area===zone && (stage==null || d.stage===stage)).reduce((t,d)=>t+(d.count||0),0); }
/* Photographs only: paperwork and video are not counted as photos. */
function priorPhotos(){ return (PRIOR.total||0)-rc('paperwork')-rc('video'); }
function photosSent(){ return priorPhotos() + JOBS.filter(j=>!j.original && j.state==='done' && j.p.kind!=='paperwork' && j.p.kind!=='video').length; }
function nPhotos(n){ return n+' photo'+(n===1?'':'s'); }

const ICON = {
  camera:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z"/><circle cx="12" cy="13" r="3"/></svg>',
  check:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6 9 17l-5-5"/></svg>',
  back:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"/></svg>',
  trash:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/><line x1="10" x2="10" y1="11" y2="17"/><line x1="14" x2="14" y1="11" y2="17"/></svg>',
  plus:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14"/><path d="M12 5v14"/></svg>',
  image:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="18" x="3" y="3" rx="2" ry="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-3.1-3.1a2 2 0 0 0-2.8 0L6 21"/></svg>',
  pencil:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21.2 6.4 17.6 2.8a1 1 0 0 0-1.4 0L3 16v5h5L21.2 7.8a1 1 0 0 0 0-1.4z"/></svg>',
  list:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 6h13"/><path d="M8 12h13"/><path d="M8 18h13"/><path d="M3 6h.01"/><path d="M3 12h.01"/><path d="M3 18h.01"/></svg>',
  car:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2"/><circle cx="7" cy="17" r="2"/><path d="M9 17h6"/><circle cx="17" cy="17" r="2"/></svg>',
  gauge:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m12 14 4-4"/><path d="M3.34 19a10 10 0 1 1 17.32 0"/></svg>',
  rotate:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/></svg>',
  shield:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z"/><path d="m9 12 2 2 4-4"/></svg>'
};

/* Damage zones – a top-down car, bonnet at the top. Right of the diagram = driver side (UK). */
const ZONES = [
  {id:'front',  label:'Front',                   x:100, y:-12, rot:90},
  {id:'fp',     label:'Front passenger corner',  x:22,  y:12,  rot:45},
  {id:'ps-f',   label:'Passenger side front',    x:4,   y:80,  rot:0},
  {id:'ps-m',   label:'Passenger side middle',   x:4,   y:146, rot:0},
  {id:'ps-r',   label:'Passenger side rear',     x:4,   y:212, rot:0},
  {id:'rp',     label:'Rear passenger corner',   x:22,  y:278, rot:-45},
  {id:'rear',   label:'Rear',                    x:100, y:302, rot:-90},
  {id:'rd',     label:'Rear driver corner',      x:178, y:278, rot:-135},
  {id:'ds-r',   label:'Driver side rear',        x:196, y:212, rot:180},
  {id:'ds-m',   label:'Driver side middle',      x:196, y:146, rot:180},
  {id:'ds-f',   label:'Driver side front',       x:196, y:80,  rot:180},
  {id:'fd',     label:'Front driver corner',     x:178, y:12,  rot:135},
];

/* ---------- state ---------- */
const BLANK = () => ({ step:0, zones:[], damage:[], scene:[], other:[], overview:[], docs:[], plate:null, vin:null, dash:null, video:null, mileage:'', airbags:null, steering:null, notes:'',
  ins:{ sender:'', accident:'', phone:'', customer:'', vehicleRole:'', handler:'', instructed:'', view:'', reg:'', locType:'', locOther:'', roadworthy:'', outcome:'', hire:'' } });
let S = BLANK();
let returnTo = null; // when editing from summary
let lastStep = null;
let upBusy = 0;

/* localStorage holds only the step reached and the in-progress form, keyed by the link's linkKey. Never the token. */
function load(){
  try{ const r=localStorage.getItem(KEY); if(!r) return null; const v=JSON.parse(r); const s=BLANK();
    s.ins=Object.assign(s.ins, v.ins||{});
    if(Array.isArray(v.zones)) s.zones=v.zones.filter(z=>ZONES.some(x=>x.id===z));
    ['notes','mileage'].forEach(k=>{ if(typeof v[k]==='string') s[k]=v[k]; });
    ['mileageEst','noKeys','notesEdited'].forEach(k=>{ s[k]=!!v[k]; });
    ['airbags','steering'].forEach(k=>{ if(typeof v[k]==='string') s[k]=v[k]; });
    const i=STEPS.indexOf(v.stepName); s.step=i>=0?i:0;
    return s; }catch(e){ return null; } }
function save(){
  if(!KEY || GONE) return;
  try{ if(S.handoff){ localStorage.removeItem(KEY); return; }
    localStorage.setItem(KEY, JSON.stringify({ stepName:STEPS[S.step], zones:S.zones, notes:S.notes, notesEdited:!!S.notesEdited, mileage:S.mileage, mileageEst:!!S.mileageEst, noKeys:!!S.noKeys, airbags:S.airbags, steering:S.steering, ins:S.ins })); }catch(e){} }

/* Server copy of the typed details wins over localStorage. */
function applyServer(ctx){
  if(SRC && ctx.registration) S.ins.reg=String(ctx.registration).toUpperCase().replace(/[^A-Z0-9]/g,'');
  const d=ctx.details; if(!d) return;
  const I=S.ins, put=(k,v)=>{ if(v!=null && v!=='') I[k]=String(v); };
  put('handler',d.handler); put('roadworthy',d.roadworthy); put('outcome',d.outcome); put('hire',d.inHire); put('view',d.repairView); put('vehicleRole',d.vehicleRole);
  if(d.inspectionLocationChoice){ I.locType=String(d.inspectionLocationChoice); if(!savedAddress(I.locType) && d.inspectionLocation) I.locOther=String(d.inspectionLocation); }
  if(d.mileage!=null){ S.mileage=String(d.mileage); S.mileageEst=true; }
  if(d.airbags) S.airbags=String(d.airbags);
  if(d.steering) S.steering=String(d.steering);
  if(d.noKeys) S.noKeys=true;
  if(Array.isArray(d.damageAreas) && d.damageAreas.length) S.zones=d.damageAreas.filter(z=>ZONES.some(x=>x.id===z));
}
function savedAddress(name){ return SRC ? SRC.addresses.find(a=>a.name===name) : null; }
function locDisplay(){ const a=savedAddress(S.ins.locType); return a ? a.name+(a.address?' – '+a.address:'') : S.ins.locType; }

/* The body of POST start, POST handoff and POST details. */
function detailsBody(){
  const I=S.ins, nz=v=>{ const t=v==null?'':String(v).trim(); return t?t:null; };
  const saved=savedAddress(I.locType), mil=S.mileageEst ? String(S.mileage||'').replace(/\D/g,'') : '';
  return { registration:nz(SRC?I.reg:CASE.reg), incidentDate:nz(I.accident), customerName:nz(I.customer), mobile:nz(I.phone), email:null,
    details:{ handler:nz(I.handler), inspectionLocation:saved?nz(saved.address):nz(I.locOther), inspectionLocationChoice:nz(I.locType),
      roadworthy:nz(I.roadworthy), outcome:nz(I.outcome), inHire:nz(I.hire), repairView:nz(I.view), vehicleRole:nz(I.vehicleRole),
      mileage:mil?Number(mil):null, airbags:nz(S.airbags), steering:nz(S.steering), damageAreas:S.zones.slice(),
      noKeys:!!S.noKeys && !S.dash && !rc('dashboard') } };
}

/* ---------- helpers ---------- */
const $ = s => document.querySelector(s);
let main = null, cta = null, fileMulti = null, fileSingle = null, fileDocs = null, fileVideo = null;  /* bound in boot() */
let pending = null;

function note(t){ const n=$('#note'); if(!n||!t) return; n.textContent=t; n.classList.add('show'); clearTimeout(n._t); n._t=setTimeout(()=>n.classList.remove('show'),2200); }
function problemNote(data){ note(data && (data.detail || data.title) ? String(data.detail || data.title) : COPY.offline); }

/* Downscale on-device to ~1600px long edge, JPEG ~0.82 — this is what is uploaded; the File is kept for originals. */
function compress(file){
  const t=file.lastModified||Date.now();
  return new Promise(res=>{
    const img=new Image(); const url=URL.createObjectURL(file);
    const raw=()=>res({url, blob:file, type:file.type||'application/octet-stream', name:file.name, file:null, bytes:file.size, orig:file.size, w:0, h:0, t});
    img.onload=()=>{
      const max=1600; let w=img.width,h=img.height; const sc=Math.min(1,max/Math.max(w,h)); w=Math.round(w*sc); h=Math.round(h*sc);
      const c=document.createElement('canvas'); c.width=w; c.height=h; c.getContext('2d').drawImage(img,0,0,w,h);
      c.toBlob(b=>{ if(!b) return raw(); URL.revokeObjectURL(url); res({url:URL.createObjectURL(b), blob:b, type:'image/jpeg', name:file.name, file, bytes:b.size, orig:file.size, w, h, t}); },'image/jpeg',0.82);
    };
    img.onerror=raw;  /* the browser cannot decode it: send the file as it is */
    img.src=url;
  });
}
function fmt(b){ return b>1048576 ? (b/1048576).toFixed(1)+' MB' : Math.round(b/1024)+' KB'; }
function uuid(){
  if(window.crypto && crypto.randomUUID) return crypto.randomUUID();
  const b=crypto.getRandomValues(new Uint8Array(16)); b[6]=(b[6]&15)|64; b[8]=(b[8]&63)|128;
  const h=[...b].map(x=>x.toString(16).padStart(2,'0')).join(''); return h.slice(0,8)+'-'+h.slice(8,12)+'-'+h.slice(12,16)+'-'+h.slice(16,20)+'-'+h.slice(20);
}

/* ---------- API ---------- */
async function api(method, path, o){
  o=o||{};
  const headers={ 'X-Capture-Link':LINK, 'Accept':'application/json' }; let body=o.body;
  if(o.json!==undefined){ headers['Content-Type']='application/json'; body=JSON.stringify(o.json); }
  else if(o.type) headers['Content-Type']=o.type;
  /* a request that hangs is given up as if the network failed (status 0): 2 minutes for a file or a commit of its parts, 30 seconds otherwise */
  const ctl=new AbortController(), tm=setTimeout(()=>ctl.abort(), o.body!==undefined||o.slow ? 120000 : 30000);
  let r, data=null;
  try{
    r=await fetch(API+path+(o.query?'?'+o.query:''), { method, headers, body, credentials:'omit', cache:'no-store', signal:ctl.signal });
    if((r.headers.get('content-type')||'').indexOf('json')>=0){ try{ data=await r.json(); }catch(e){} }
  }catch(e){ return { status:0, ok:false, data:null, retryAfter:0 }; }
  finally{ clearTimeout(tm); }
  if(r.status===404) gone();
  return { status:r.status, ok:r.ok, data, retryAfter:+(r.headers.get('retry-after')||0) };
}
function gone(){
  if(GONE) return; GONE=true;
  document.querySelectorAll('.cam,.sheetwrap').forEach(x=>x.remove());
  if(camStream){ camStream.getTracks().forEach(tr=>tr.stop()); camStream=null; }
  document.documentElement.style.overflow=''; document.body.style.overflow='';
  $('#stepLabel').textContent=''; const pg=$('#prog'); if(pg) pg.style.width='0%';
  /* the same two lines as the page an unavailable link gets (R789) */
  main.innerHTML='<div class="done-wrap"><h1>This link is no longer active.</h1><p class="lede">Ask whoever sent it to you for a new one.</p></div>'; cta.innerHTML='';
}

/* ---------- upload queue: at most 2 in flight, retries with backoff, originals last ---------- */
const ONE=4194304, MAXUP=2;
/* an original over CaptureLimits.OriginalBytes is not sent; about 100 MB of originals wait to go at most, and one past that is not sent */
const ORIGMAX=25000000, ORIGPENDING=100000000;
const KINDAPI={overview:'overview', damage:'damage', plate:'plate', vin:'vin', dash:'dashboard', scene:'scene', other:'other-vehicle', docs:'paperwork', video:'video'};
const JOBS=[]; let active=0, pumpT=null;
const TAKEN={};
function ovTaken(){ const s=new Set(PRIOR.overviewPositions||[]); S.overview.forEach(p=>s.add(p.position)); return s; }
function ovNextPos(){ const s=ovTaken(); for(let k=1;k<=8;k++) if(!s.has(k)) return k; let m=8; s.forEach(v=>{ if(v>m) m=v; }); return m+1; }
/* A photo, document or video has been taken: give it its id and position, keep it, queue it. */
function addPhoto(kind, p){
  p.kind=KINDAPI[kind]; p.cid=p.cid||uuid();
  if(kind==='overview') p.position=ovNextPos();
  else { if(kind==='damage' && p.stage) p.apiStage=STAGEAPI[p.stage];
    const k=[p.kind,p.zone||'',p.apiStage||''].join(':'); TAKEN[k]=(TAKEN[k]||0)+1;
    p.position=(kind==='damage' ? (p.zone ? rcDmg(p.zone,p.apiStage) : rc('damage')) : rc(p.kind)) + TAKEN[k]; }
  if(Array.isArray(S[kind])) S[kind].push(p); else S[kind]=p;
  enqueue(p,false);
}
function enqueue(p, original){ const job={p, original, tries:0, at:0, partsDone:0, sha:null, state:'queued'}; if(!original) p.job=job; JOBS.push(job); upChanged(); pump(); }
function itemQuery(p, original){
  const q=new URLSearchParams(); q.set('kind',p.kind);
  if(p.zone) q.set('area',p.zone); if(p.apiStage) q.set('stage',p.apiStage);
  q.set('position',String(p.position)); q.set('takenAt',new Date(p.t||Date.now()).toISOString());
  q.set('live',p.live?'true':'false'); q.set('original',original?'true':'false');
  const nm=original?(p.file&&p.file.name):p.name; if(nm) q.set('name',nm);
  return q.toString();
}
function pump(){
  if(GONE || LINK.indexOf('c.')!==0) return;
  const now=Date.now(); let wait=Infinity;
  while(active<MAXUP){
    let job=null;
    for(const j of JOBS){ if(j.state!=='queued') continue; if(j.at>now){ wait=Math.min(wait,j.at-now); continue; } if(!job || (job.original && !j.original)) job=j; }
    if(!job) break;
    run(job);
  }
  clearTimeout(pumpT); if(wait<Infinity) pumpT=setTimeout(pump, wait+20);
}
async function run(job){
  job.state='busy'; active++;
  let r; try{ r=await sendJob(job); }catch(e){ r={status:0}; }
  active--;
  const st=r.status, code=r.data&&r.data.code;
  if(st===200||st===201){ job.state='done'; job.tries=0;
    if(!job.original){ job.p.uploaded=true; queueOriginal(job.p); } else job.p.file=null; }
  else if(st===404){ job.state='failed'; return; }
  else if(job.original && !CTX.originals) dropOriginal(job);
  else if(st===0 || st===429 || st>=500 || (st===409 && code==='not_ready')){
    job.tries++; job.state='queued';
    /* an original waits for its photo to be filed: 15 seconds, doubling to 2 minutes */
    const d=r.retryAfter ? r.retryAfter*1000 : (job.original && st===409) ? Math.min(120000, 15000*Math.pow(2, Math.min(job.tries-1,3))) : Math.min(30000, 1000*Math.pow(2, Math.min(job.tries,5))) + Math.random()*500;
    job.at=Date.now()+(st===409 ? Math.max(d,3000) : d); }
  else if(job.original) dropOriginal(job);  /* refused, as a capture that takes no originals answers 415: final, and nothing is said */
  else { job.state='failed'; job.p.failed=true; problemNote(r.data); }
  upChanged(); pump();
}
/* A photo is uploaded: its original follows when the capture takes originals, it is not too large and not too much is already waiting; otherwise the compressed photo stands. */
function queueOriginal(p){
  if(!p.file || p.origQueued || p.cancelled) return;
  const waiting=JOBS.reduce((a,j)=>a+(j.original && (j.state==='queued'||j.state==='busy') && j.p.file ? j.p.file.size : 0),0);
  if(!CTX.originals || p.file.size>ORIGMAX || waiting+p.file.size>ORIGPENDING){ p.file=null; return; }
  p.origQueued=true; enqueue(p,true);
}
function dropOriginal(job){ job.state='dropped'; job.p.file=null; }
function origPending(){ return JOBS.some(j=>j.original && (j.state==='queued'||j.state==='busy')); }
async function sendJob(job){
  const p=job.p, blob=job.original?p.file:p.blob, type=(job.original?p.file.type:p.type)||'application/octet-stream', q=itemQuery(p, job.original);
  if(blob.size<=ONE) return api('PUT','items/'+p.cid,{query:q, body:blob, type});
  const total=blob.size, parts=Math.ceil(total/ONE), qt=q+'&totalBytes='+total;
  if(!job.sha) job.sha=await sha256(blob);
  for(let n=job.partsDone+1;n<=parts;n++){
    const r=await api('PUT','items/'+p.cid+'/parts/'+n,{query:qt, body:blob.slice((n-1)*ONE, Math.min(total,n*ONE)), type});
    if(!r.ok) return r;
    job.partsDone=n; job.tries=0;
  }
  const r=await api('POST','items/'+p.cid+'/commit',{query:qt, json:{totalBytes:total, sha256:job.sha}, slow:true});
  /* an original's commit that is not ready keeps its parts (the photo is still being filed); every fifth time running they go again, in case they were lost */
  const keep=job.original && r.status===409 && r.data && r.data.code==='not_ready' && (job.tries+1)%5!==0;
  if(!r.ok && r.status>=400 && r.status<500 && r.status!==404 && r.status!==429 && !keep) job.partsDone=0;
  return r;
}
async function sha256(blob){ const d=await crypto.subtle.digest('SHA-256', await blob.arrayBuffer()); return [...new Uint8Array(d)].map(x=>x.toString(16).padStart(2,'0')).join(''); }
function cancelUpload(p){ if(!p) return; p.cancelled=true; p.file=null; if(p.job && p.job.state==='queued') p.job.state='cancelled'; if(p.url) URL.revokeObjectURL(p.url); upChanged(); }
/* Upload state changed: refresh the "uploading / uploaded" lines in place (a full render would steal focus from a field being typed in); the review page re-renders for its Send button. */
function upChanged(){
  upBusy=JOBS.filter(j=>!j.original && (j.state==='queued'||j.state==='busy')).length;
  if(GONE || !STEPS.length) return;
  if(STEPS[S.step]==='summary' || (STEPS[S.step]==='done' && S.handoff)) return render();
  document.querySelectorAll('.uplog[data-ul]').forEach(el=>{ const l=UPL[+el.dataset.ul]; if(l) el.innerHTML=uplogInner(l); });
}
/* Stalled: photos are waiting and the phone is offline or one has failed to go three times running; the app then says so (R786) with a retry. */
function stalled(){ return upBusy>0 && (navigator.onLine===false || JOBS.some(j=>!j.original && j.state==='queued' && j.tries>=3)); }
function retryHTML(){ return `<button type="button" class="linkbtn" data-retry>${COPY.offline}</button>`; }
function retryNow(){ JOBS.forEach(j=>{ if(j.state==='queued') j.at=0; }); pump(); }
window.addEventListener('online', retryNow);
window.addEventListener('offline', upChanged);
document.addEventListener('click', e=>{ if(e.target.closest && e.target.closest('[data-retry]')) retryNow(); });
/* leaving with photos still to go loses them: the browser asks first (its own words; best effort on iOS) */
window.addEventListener('beforeunload', e=>{ if(upBusy && !GONE){ e.preventDefault(); e.returnValue=''; } });

/* ---------- details, start, hand-off, complete ---------- */
let detailsSig='';
async function syncDetails(){
  if(GONE || LINK.indexOf('c.')!==0) return;
  const body=detailsBody(), sg=JSON.stringify(body); if(sg===detailsSig) return; detailsSig=sg;
  try{ const r=await api('POST','details',{json:body}); if(r.status===200 && r.data) setContext(r.data);
    /* the registration is locked once something has been received: put the capture's own back and send the rest again */
    else if(r.status===409 && r.data && r.data.code==='registration_locked' && SRC && CTX.registration && S.ins.reg!==String(CTX.registration)){
      S.ins.reg=String(CTX.registration); save(); detailsSig=''; if(STEPS[S.step]!=='done') render(); return syncDetails(); }
    else if(r.status!==404) detailsSig=''; }catch(e){ detailsSig=''; }
}
/* A standing link's "Take photos now" (or the website form's Continue) starts the capture; from then on the page is /c/<token>. */
async function beginCapture(btn){
  btn.disabled=true; let r;
  try{ r=await api('POST','start',{json:detailsBody()}); }catch(e){ r={status:0}; }
  if(GONE) return;
  if(r.status===201 && r.data && r.data.token){
    const oldKey=KEY; LINK='c.'+r.data.token;
    history.replaceState(null,'',location.pathname.replace(/\/s\/[^/]+\/?$/,'/c/'+encodeURIComponent(r.data.token)));
    try{ localStorage.removeItem(oldKey); }catch(e){}
    setContext(r.data.context||CTX); KEY='ce-capture-'+(r.data.linkKey||CTX.linkKey);
    detailsSig=JSON.stringify(detailsBody());
    pump();
    let j=S.step+1; while(STEPS[j]==='handoff') j++; go(j);
    return;
  }
  btn.disabled=false; if(r.status!==404) problemNote(r.data);
}
async function sendHandoff(btn){
  btn.disabled=true; let r;
  try{ r=await api('POST','handoff',{json:detailsBody()}); }catch(e){ r={status:0}; }
  if(GONE) return;
  if(r.status===201 && r.data && r.data.url){ S.handoffRes={url:String(r.data.url), message:String(r.data.message||''), registration:String(r.data.registration||''), session:String(r.data.sessionToken||'')}; S.handoff=true; go(STEPS.indexOf('handoff')); return; }
  btn.disabled=false; if(r.status!==404) problemNote(r.data);
}
async function completeCapture(){
  await syncDetails();
  /* a note emptied here is sent empty, so it replaces the earlier one; a note never touched on this visit is not sent */
  try{ return await api('POST','complete',{json:{note:(S.notes||S.notesEdited)?S.notes:null}}); }catch(e){ return {status:0}; }
}

const onMulti = async e=>{
  const files=[...e.target.files]; e.target.value='';
  const kd=(pending&&pending.kind)||'damage';
  for(const f of files){ const p=await compress(f); if(kd==='damage'&&DTAG) DTAG(p); addPhoto(kd,p); }
  DTAG=null;
  pending=null;
  render();
};
const onSingle = async e=>{
  const f=e.target.files[0]; e.target.value=''; if(!f||!pending) return;
  const p=await compress(f);
  addPhoto(pending.kind,p); pending=null; render();
};
function shoot(kind){ openCam2(kind); }
/* Rapid camera: in-page viewfinder, tap-tap-tap. */
async function openCam2(kind){
  if(!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia){ note('Rapid camera not available here – using the phone camera'); return pick(kind); }
  const ov=document.createElement('div'); ov.className='cam';
  ov.innerHTML=`<video playsinline autoplay muted></video><div class="flash"></div>
    <div class="top"><span class="cnt">0 taken</span><button class="done">Done</button></div>
    <div class="bot"><div class="strip"></div><div class="shutterrow"><span class="hintc">${camHint(kind)}</span><button class="shutter" aria-label="Take photo"></button></div></div>`;
  document.body.appendChild(ov);
  document.documentElement.style.overflow='hidden'; document.body.style.overflow='hidden';
  const video=ov.querySelector('video'), strip=ov.querySelector('.strip'), cnt=ov.querySelector('.cnt'), flash=ov.querySelector('.flash');
  if(kind==='overview') ovGuide(ov);
  if(kind==='damage') dmgGuide(ov);
  const fit=()=>{ const vv=window.visualViewport; if(vv){ ov.style.height=vv.height+'px'; ov.style.top=vv.offsetTop+'px'; } else { ov.style.height=window.innerHeight+'px'; } };
  fit(); if(window.visualViewport){ window.visualViewport.addEventListener('resize',fit); window.visualViewport.addEventListener('scroll',fit); } window.addEventListener('resize',fit);
  let taken=0;
  const close=()=>{ if(camStream){ camStream.getTracks().forEach(tr=>tr.stop()); camStream=null; } if(window.visualViewport){ window.visualViewport.removeEventListener('resize',fit); window.visualViewport.removeEventListener('scroll',fit); } window.removeEventListener('resize',fit); document.documentElement.style.overflow=''; document.body.style.overflow=''; if(ov._stopL) ov._stopL(); ov.remove(); render(); };
  ov.querySelector('.done').onclick=close;
  try{
    camStream=await navigator.mediaDevices.getUserMedia({ video:{ facingMode:{ideal:'environment'}, width:{ideal:2560}, height:{ideal:1920} }, audio:false });
    video.srcObject=camStream; await video.play(); ov._stopL=lightMeter(ov,video); fit();
  }catch(e){
    ov.remove(); document.documentElement.style.overflow=''; document.body.style.overflow=''; note('Rapid camera blocked here ('+(e&&e.name?e.name:'no camera access')+') – using the phone camera'); return pick(kind);
  }
  ov.querySelector('.shutter').onclick=()=>{
    if(!shotFromVideo(video, p=>{
      if(ov._tag) ov._tag(p); addPhoto(kind,p); taken++; cnt.textContent=`${taken} taken`; if(ov._ovStep) ov._ovStep();
      const im=document.createElement('img'); im.src=p.url; strip.appendChild(im); strip.scrollLeft=strip.scrollWidth;
    })) return;
    flash.classList.add('on'); setTimeout(()=>flash.classList.remove('on'),60);
    if(navigator.vibrate) navigator.vibrate(25);
  };
}
/* One shutter for both cameras: the photo is the frame at most 1600px on its long edge (JPEG 0.85). When the camera gives more and the capture takes originals,
   the full-resolution frame (JPEG 0.92) is kept as the photo's original and follows once the photo itself is uploaded. */
function shotFromVideo(video, take){
  const w=video.videoWidth, h=video.videoHeight; if(!w) return false;
  const sc=Math.min(1,1600/Math.max(w,h)), c=document.createElement('canvas'); c.width=Math.round(w*sc); c.height=Math.round(h*sc);
  c.getContext('2d').drawImage(video,0,0,c.width,c.height);
  let full=null; if(sc<1 && CTX.originals){ full=document.createElement('canvas'); full.width=w; full.height=h; full.getContext('2d').drawImage(video,0,0,w,h); }
  const t=Date.now();
  c.toBlob(b=>{
    if(!b){ if(full) full.width=0; return; }
    const p={url:URL.createObjectURL(b), blob:b, type:'image/jpeg', file:null, bytes:b.size, orig:b.size, w:c.width, h:c.height, t, live:true};
    take(p);
    if(full) full.toBlob(o=>{ full.width=0; if(o && !p.cancelled){ p.file=o; if(p.uploaded) queueOriginal(p); } },'image/jpeg',0.92);
  },'image/jpeg',0.85);
  return true;
}
let camStream=null;
function pick(kind,id){ pending={kind,id}; const el=(kind==='damage'||kind==='scene'||kind==='other'||kind==='overview'?fileMulti:fileSingle); if(LIVE && !['scene','other','docs','video'].includes(kind)) el.setAttribute('capture','environment'); else el.removeAttribute('capture');  /* live-only covers the vehicle photos; scene and other-vehicle photos, paperwork and video can come from the camera roll */ el.click(); }
function liveNote(){ return LIVE ? `<p class="hint livenote"><span class="livenote-ico">${ICON.shield}</span><span>For this job, photos must be taken live on this device. Choosing from your gallery is switched off.</span></p>` : ''; }
const onDocs = e=>{ for(const f of e.target.files){ addPhoto('docs',{name:f.name, bytes:f.size, orig:f.size, blob:f, type:f.type||'application/octet-stream', file:null, t:f.lastModified||Date.now()}); } e.target.value=''; render(); };
function pickVideo(){ fileVideo.click(); }
const onVideo = e=>{ const f=e.target.files[0]; e.target.value=''; if(!f) return; if(S.video) S.video=null; addPhoto('video',{name:f.name, bytes:f.size, orig:f.size, blob:f, type:f.type||'application/octet-stream', file:null, t:f.lastModified||Date.now()}); render(); };

/* ---------- v10 touches: light meter, framed camera, blur check, nudge ---------- */
function camHint(kind){ return kind==='overview' ? 'Tap the button for each photo. Try to get the whole vehicle in shot – our engineer needs to see around the whole vehicle.' : kind==='damage' ? 'Tap the button for each photo. Start well back, then slowly move closer, taking lots from different angles.' : 'Tap the button for each photo. Different distances and angles all help.'; }
const OVSEQ=[{l:'Front',x:100,y:-74},{l:'Front driver corner',x:240,y:-50},{l:'Driver side',x:262,y:146},{l:'Rear driver corner',x:240,y:342},{l:'Rear',x:100,y:366},{l:'Rear passenger corner',x:-40,y:342},{l:'Passenger side',x:-62,y:146},{l:'Front passenger corner',x:-40,y:-50}];
function ovGuide(ov){
  const g=document.createElement('div'); g.className='ovguide'; g.setAttribute('aria-live','polite'); ov.appendChild(g);
  const ex=document.createElement('div'); ex.className='ovex'; ex.innerHTML='<img alt="">'; ov.appendChild(ex); const exi=ex.firstChild;
  for(let k=1;k<=OVSEQ.length;k++){ const pre=new Image(); pre.src=assetUrl(String(k)); }
  const place=()=>{ const land=matchMedia('(orientation:landscape) and (max-height:520px)').matches; ex.style.bottom=land?(g.offsetHeight+22)+'px':'calc(35px + env(safe-area-inset-bottom,0px))'; };
  window.addEventListener('resize',place); if(window.visualViewport) window.visualViewport.addEventListener('resize',place);
  /* the next angle is the first of the eight not yet taken, here or on an earlier visit */
  const show=()=>{ const i=ovNextPos()-1; requestAnimationFrame(place);
    if(i<OVSEQ.length){ exi.src=assetUrl(String(i+1)); ex.style.display='block'; } else ex.style.display='none';
    if(i>=OVSEQ.length){ g.innerHTML=`<span class="og-tick">${ICON.check}</span><div class="ogt"><span>All the way round</span></div>`; return; }
    const m=OVSEQ[i]; g.innerHTML=`<div class="ogt"><b>${i+1} of ${OVSEQ.length}</b><span>${m.l}</span></div>`; };
  show(); ov._ovStep=show;
}

/* ---------- v10.13: damage capture in four stages, guided (customers) or quick (staff) ---------- */
const RING=['front','fd','ds-f','ds-m','ds-r','rd','rear','rp','ps-r','ps-m','ps-f','fp'];
/* the three overview angles that show each zone best: [one side, straight on, other side] */
const AV={front:[8,1,2],fd:[1,2,3],'ds-f':[2,3,4],'ds-m':[2,3,4],'ds-r':[2,3,4],rd:[3,4,5],rear:[4,5,6],rp:[5,6,7],'ps-f':[8,7,6],'ps-m':[8,7,6],'ps-r':[8,7,6],fp:[1,8,7]};
const XT={
  wfd:{l:'Front wheel and tyre',h:'Photograph the front wheel on the driver side, including the tyre.'},
  wrd:{l:'Rear wheel and tyre',h:'Photograph the rear wheel on the driver side, including the tyre.'},
  wfp:{l:'Front wheel and tyre',h:'Photograph the front wheel on the passenger side, including the tyre.'},
  wrp:{l:'Rear wheel and tyre',h:'Photograph the rear wheel on the passenger side, including the tyre.'},
  under:{l:'Under the bumper (if you can)',h:'If you can do it safely, crouch down and photograph underneath the rear bumper.'},
  exh:{l:'Exhaust (if you can)',h:'If you can see it, photograph the exhaust and the area around it.'},
  lowd:{l:'Low, along the side',h:'Crouch down at one end and photograph along the driver side – dents show up in the reflections.'},
  lowp:{l:'Low, along the side',h:'Crouch down at one end and photograph along the passenger side – dents show up in the reflections.'},
  lowf:{l:'Low, across the front',h:'Crouch down and photograph across the front – dents show up in the reflections.'},
  lowr:{l:'Low, across the back',h:'Crouch down and photograph across the back – dents show up in the reflections.'}
};
const XORD=Object.keys(XT);
const ZX={front:[],fd:[],'ds-f':[],'ds-m':[],'ds-r':[],rd:[],rear:[],rp:[],'ps-r':[],'ps-m':[],'ps-f':[],fp:[]};  /* extras: open, no suggestions */
const DST=[{id:'whole',n:'Overview',api:'overview'},{id:'closer',n:'Closer',api:'closer'},{id:'detail',n:'Close-ups',api:'close-up'},{id:'extra',n:'Extras',api:'extra'}];
const STAGEAPI={whole:'overview',closer:'closer',detail:'close-up',extra:'extra'};
const VCAP=['From one side','Straight on','From the other side'];
const ZL=id=>ZONES.find(z=>z.id===id).label;
/* Damaged areas, never more than two:
   - touching boxes (up to 3) = one area; a gap starts a new area
   - a touching run of 4+ is split into two areas
   - if that still leaves more than two, the closest areas are merged until there are two */
function dmgAreas(){
  const sel=RING.map(id=>S.zones.includes(id)); if(!sel.some(Boolean)) return [];
  let runs=[];
  if(sel.every(Boolean)) runs=[RING.slice()];
  else { const st=sel.findIndex((v,i)=>v&&!sel[(i+11)%12]); let cur=null; for(let k=0;k<12;k++){ const i=(st+k)%12; if(sel[i]){ if(!cur){ cur=[]; runs.push(cur); } cur.push(RING[i]); } else cur=null; } }
  runs=runs.flatMap(r=>{ if(r.length<4) return [r]; const h=Math.ceil(r.length/2); return [r.slice(0,h),r.slice(h)]; });
  const pos=r=>r.includes('front')&&r[0]!=='front'&&RING.indexOf(r[0])>6?-1:RING.indexOf(r[0]);
  runs.sort((x,y)=>pos(x)-pos(y));
  let groups=runs.map(r=>[r]);
  const gap=(g1,g2)=>{ const e=RING.indexOf(g1[g1.length-1].slice(-1)[0]), f=RING.indexOf(g2[0][0]); return (f-e+12)%12; };
  while(groups.length>2){ const L=groups.length; let bi=0, bg=99; for(let i=0;i<L;i++){ const g=gap(groups[i],groups[(i+1)%L]); if(g<bg){ bg=g; bi=i; } }
    if(bi<L-1) groups.splice(bi,2,[...groups[bi],...groups[bi+1]]); else { const m=[...groups[L-1],...groups[0]]; groups=[m,...groups.slice(1,L-1)]; } }
  const rl=ids=>ids.length===1?ZL(ids[0]):ZL(ids[0])+(ids.length===2?' and ':' to ')+ZL(ids[ids.length-1]).toLowerCase();
  return groups.map(g=>{ const ids=g.flat(), mainRun=g.reduce((m,r)=>r.length>m.length?r:m,g[0]), key=mainRun[Math.floor((mainRun.length-1)/2)];
    const parts=g.map((r,i)=>i?rl(r).charAt(0).toLowerCase()+rl(r).slice(1):rl(r)); const label=parts.length>2?parts.slice(0,-1).join(', ')+' and '+parts[parts.length-1]:parts.join(' and ');
    const extras=[...new Set(ids.flatMap(z=>ZX[z]))].sort((x,y)=>XORD.indexOf(x)-XORD.indexOf(y));
    return {ids,label,key,mark:mainRun.length<=3?mainRun:[key],views:AV[key],extras}; });
}
function dmgSync(){ const sig=dmgAreas().map(a=>a.label).join('|'); if(S.dmgSig!==sig){ S.dmgSig=sig; S.dmgAi=0; S.dmgSt=0; S.dmgXi=0; S.dmgFin=0; } }
/* counts per area include what an earlier visit to this link already sent (by the area's zone key) */
const dmgCount=a=>S.damage.filter(p=>p.area===a.label&&p.stage!=='extra').length + rcDmg(a.key,'overview') + rcDmg(a.key,'closer') + rcDmg(a.key,'close-up');
const stCount=(a,st)=>S.damage.filter(p=>p.area===a.label&&p.stage===st).length + rcDmg(a.key,STAGEAPI[st]);
const dmgAny=a=>S.damage.some(p=>p.area===a.label) || rcDmg(a.key)>0;
const dmgTotal=()=>S.damage.length + rc('damage');
const weakAreas=()=>dmgAreas().filter(a=>dmgCount(a)<3);
const stTarget=(a,si)=>si===3?0:3;
function dexSrc(a,si,j){ const v=a.views[j%3];
  if(si===0) return asset(`d/${a.key}-${v}-o`);
  if(si===1) return asset(`d/${a.key}-${v}-m`);
  if(si===2) return asset(`d/${a.key}-${a.views[[1,0,2][j%3]]}-c`);
  if(!a.extras.length) return asset(`d/x-${/^(p|rp|fp)/.test(a.key)?'wfp':'wfd'}`);
  return asset(`d/x-${a.extras[Math.min(j,a.extras.length-1)]}`); }
function dHint(a,si,j){
  if(si===0) return `Stand about 3 big steps back so we can see the whole damaged area. ${['Start from one side.','Now straight on.','Now from the other side.'][j%3]}`;
  if(si===1) return `Now just 1 big step away. ${['Start from one side.','Now straight on.','Now from the other side.'][j%3]}`;
  if(si===2) return 'Take some close-ups of each dent, scratch or crack. If light bounces off it, take a step to one side.';
  return 'Optional – add anything else you’d like us to see, or feel free to skip.'; }
let DMGRUN=null, DTAG=null;
function dmgGuide(ov){
  const run=DMGRUN||{scope:'all'}; DMGRUN=null; if(run.scope==='none') return;
  dmgSync(); const areas=dmgAreas(); if(!areas.length) return;
  if(!(S.dmgAi>=0&&S.dmgAi<areas.length)) S.dmgAi=0;
  if(!(S.dmgSt>=0&&S.dmgSt<4)) S.dmgSt=0;
  const steps=document.createElement('div'); steps.className='dsteps'; ov.appendChild(steps);
  const toast=document.createElement('div'); toast.className='dtoast'; ov.appendChild(toast);
  const g=document.createElement('div'); g.className='ovguide dguide'; g.setAttribute('aria-live','polite'); ov.appendChild(g);
  const ex=document.createElement('div'); ex.className='ovex'; ov.appendChild(ex);
  const hint=ov.querySelector('.hintc'); const finish=()=>ov.querySelector('.done').click();
  areas.forEach(a=>{ for(let si=0;si<4;si++) for(let j=0;j<(si===3?a.extras.length:3);j++){ const im=new Image(); im.src=dexSrc(a,si,j).replace(/&amp;/g,'&'); } });
  let tt=null; const say=m=>{ toast.textContent=m; toast.classList.add('on'); clearTimeout(tt); tt=setTimeout(()=>toast.classList.remove('on'),1800); };
  const place=()=>{ const land=matchMedia('(orientation:landscape) and (max-height:520px)').matches; ex.style.bottom=land?(g.offsetHeight+22)+'px':'calc(35px + env(safe-area-inset-bottom,0px))'; if(hint) hint.style.marginBottom=land?'':Math.max(12,g.offsetHeight-60)+'px'; };
  window.addEventListener('resize',place); if(window.visualViewport) window.visualViewport.addEventListener('resize',place);
  const nextArea=()=>{ const a=areas[S.dmgAi]; S.dmgDone=Object.assign({},S.dmgDone,{[a.label]:1}); const more=S.dmgAi<areas.length-1;
    if(more){ S.dmgAi++; S.dmgSt=0; S.dmgXi=0; } else S.dmgFin=1;
    if(run.scope!=='all'||!more){ finish(); return; }
    say(`✓ Done – next, ${areas[S.dmgAi].label.toLowerCase()}`); show(); };
  const advance=()=>{ if(S.dmgSt<3){ S.dmgSt++; if(S.dmgSt===3) S.dmgXi=0; if(run.scope==='stage'){ finish(); return; } say(`✓ ${DST[S.dmgSt-1].n} done – now ${S.dmgSt===3?'any extras (optional)':DST[S.dmgSt].n.toLowerCase()}`); show(); } else nextArea(); };
  const show=()=>{ const a=areas[S.dmgAi], si=S.dmgSt, c=stCount(a,DST[si].id), j=si===3?(S.dmgXi||0):c, filled=si===3?j:c;
    steps.innerHTML=DST.map((d,k)=>`<div class="${k<si?'ok':k===si?'on':''}">${d.n}${k===si?`<span class="dots">${Array.from({length:stTarget(a,si)},(_,q)=>`<i class="${q<filled?'f':''}"></i>`).join('')}</span>`:''}</div>`).join('');
    if(si===3){ ex.style.display='none'; } else { ex.innerHTML=`<img src="${dexSrc(a,si,j)}" alt="">`; ex.style.display='block'; }
    const lastA=S.dmgAi>=areas.length-1;
    const lab=si===3?'Anything else':si===2?'Each dent or scratch':VCAP[j%3];
    const btn=si===3?(run.scope==='all'&&!lastA?'Next area ›':'Finish ›'):c>=2?'Next ›':'';
    g.innerHTML=`<div class="ogt"><b>${areas.length>1?`Area ${S.dmgAi+1} of ${areas.length} · `:''}${DST[si].n}</b><span>${lab}</span>${btn?`<button type="button" class="nxt">${btn}</button>`:''}</div>`;
    const nb=g.querySelector('.nxt'); if(nb) nb.onclick= si===3 ? nextArea : advance;
    if(hint) hint.textContent=dHint(a,si,j);
    requestAnimationFrame(place); };
  ov._tag=p=>{ const a=areas[S.dmgAi]; p.area=a.label; p.zone=a.key; p.stage=DST[S.dmgSt].id; };
  ov._ovStep=()=>{ const a=areas[S.dmgAi], si=S.dmgSt;
    if(si===3){ show(); return; }
    show(); if(stCount(a,DST[si].id)>=3) setTimeout(advance,450); };
  show();
}
const APT={1:[100,-78],2:[232,-52],3:[282,146],4:[232,344],5:[100,370],6:[-32,344],7:[-82,146],8:[-32,-52]};
function miniSVG(a,si){
  const zs=a.mark.map(id=>ZONES.find(z=>z.id===id));
  let dx=zs.reduce((t,z)=>t+z.x,0)/zs.length, dy=zs.reduce((t,z)=>t+z.y,0)/zs.length;
  { const vx=100-dx, vy=146-dy, vl=Math.hypot(vx,vy)||1; dx+=vx/vl*28; dy+=vy/vl*28; }  /* pull the damage mark onto the car body */
  const icon='<g transform="translate(-7 -7) scale(.58)" fill="none" stroke="#fff" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z"/><circle cx="12" cy="13" r="3"/></g>';
  const ox=APT[a.views[1]][0]-dx, oy=APT[a.views[1]][1]-dy, ol=Math.hypot(ox,oy)||1, ux=ox/ol, uy=oy/ol;
  const D=[135,55,24][Math.min(si,2)], side=[46,40,0][Math.min(si,2)], cx0=dx+ux*D, cy0=dy+uy*D;
  const cams=si===2?[[cx0,cy0]]:[[cx0-uy*side,cy0+ux*side],[cx0,cy0],[cx0+uy*side,cy0-ux*side]];
  if(si===0&&['ds-f','ds-r','ps-f','ps-r'].includes(a.key)){ const sx=a.key[0]==='d'?1:-1; cams.length=0; [-40,0,40].forEach(t=>{ const r=t*Math.PI/180; cams.push([dx+sx*D*Math.cos(r),dy+D*Math.sin(r)]); }); }  /* side front/rear overview: spread round the damage */
  const xs=[25,175,dx-24,dx+24,...cams.flatMap(c=>[c[0]-22,c[0]+22])], ys=[6,286,dy-24,dy+24,...cams.flatMap(c=>[c[1]-22,c[1]+22])];
  const x0=Math.min(...xs)-10, y0=Math.min(...ys)-10, vw=Math.max(...xs)+10-x0, vh=Math.max(...ys)+10-y0;
  return `<svg viewBox="${x0.toFixed(0)} ${y0.toFixed(0)} ${vw.toFixed(0)} ${vh.toFixed(0)}" xmlns="http://www.w3.org/2000/svg" aria-hidden="true"><image href="${asset('car-top')}" x="25" y="6" width="150" height="280"/><circle cx="${dx}" cy="${dy}" r="22" fill="rgba(219,8,22,.22)" stroke="#db0816" stroke-width="3" stroke-dasharray="7 5"/>${cams.map(([x,y])=>`<line x1="${x.toFixed(1)}" y1="${y.toFixed(1)}" x2="${dx}" y2="${dy}" stroke="#db0816" stroke-width="2.5" stroke-dasharray="7 7" opacity=".6"/><g transform="translate(${x.toFixed(1)} ${y.toFixed(1)})"><circle r="19" fill="#db0816"/><g transform="scale(1.4)">${icon}</g></g>`).join('')}</svg>`;
}
function dmgGroups(areas){
  const h=areas.map(a=>{ const ix=S.damage.map((p,i)=>p.area===a.label?i:-1).filter(i=>i>=0); return ix.length?`<p class="dgh"><b>${a.label}</b><span>${ix.length} photo${ix.length===1?'':'s'}</span></p><div class="grid">${ix.map(i=>thumbHTML(S.damage[i],'damage:'+i)).join('')}</div>`:''; }).join('');
  const other=S.damage.map((p,i)=>areas.some(a=>a.label===p.area)?-1:i).filter(i=>i>=0);
  return h+(other.length?`<p class="dgh"><b>${areas.length?'Other photos':'Photos'}</b><span>${other.length}</span></p><div class="grid">${other.map(i=>thumbHTML(S.damage[i],'damage:'+i)).join('')}</div>`:'');
}
/* The review page's damage photos, grouped by area as the damage step groups them (handover item 5): one heading per marked area in the app's area order, with a count that includes what an earlier visit to this link already sent for that area, then this visit's photos in the order taken. A photo whose area is no longer marked keeps the step's "Other photos" heading. */
function reviewDamage(){
  const areas=dmgAreas(), strip=ps=>ps.length?`<div class="strip">${ps.map(simg).join('')}</div>`:'';
  const head=(label,n)=>`<p class="dgh"><b>${esc(label)}</b><span>${n} photo${n===1?'':'s'}</span></p>`;
  const groups=areas.map(a=>{ const ps=S.damage.filter(p=>p.area===a.label), n=ps.length+rcDmg(a.key); return n?head(a.label,n)+strip(ps):''; }).join('');
  const other=S.damage.filter(p=>!areas.some(a=>a.label===p.area));
  return groups+(other.length?head(areas.length?'Other photos':'Photos',other.length)+strip(other):'');
}
const GT=['Overview of the damaged area','A bit closer','Close-ups','Extras'];
function dmgGuidedPage(){
  dmgSync(); const areas=dmgAreas(); const n=dmgTotal();
  if(!areas.length){
    main.innerHTML=`<h2>Damage photos</h2><p class="lede">First, show us where the damage is on the car.</p>`;
    cta.innerHTML=`<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn primary" id="dloc">Choose damaged areas</button>`;
    $('#dloc').onclick=()=>go(STEPS.indexOf('location')); return;
  }
  if(S.dmgFin){
    main.innerHTML=`<h2>Damage photos</h2><p class="lede">That’s everything we need for the damage – thank you. Add more if there’s anything else you’d like us to see.</p>
      ${dmgGroups(areas)}<div class="counter"><span><b>${n}</b> photo${n===1?'':'s'} taken</span></div>${uplogHTML(S.damage)}`;
    cta.innerHTML=`<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn outline" id="add2">${ICON.camera}Add more</button><button class="btn primary" id="next">Continue</button>`;
    $('#add2').onclick=()=>{ DTAG=null; DMGRUN={scope:'none'}; shoot('damage'); };
    $('#back').onclick=()=>{ S.dmgFin=0; S.dmgAi=areas.length-1; S.dmgSt=3; render(); };
    return;
  }
  const a=areas[S.dmgAi], si=S.dmgSt, st=DST[si];
  if(S.dmgAi>0 && si===0 && !dmgAny(a)){
    main.innerHTML=`<div class="gsteps">${DST.map(d=>`<div>${d.n}</div>`).join('')}</div>
      <h2>Now the ${a.label.toLowerCase()}</h2><p class="garea">Damaged area ${S.dmgAi+1} of ${areas.length}</p>
      <p class="lede">Same four steps as before: an overview, a bit closer, close-ups, then any extras. We’ll guide you through them in the camera.</p>
      <figure class="gmini">${miniSVG(a,0)}</figure>
      <div class="gtiles">${[0,1,2].map(k=>`<figure><img src="${dexSrc(a,k,k===2?0:1)}" alt=""><figcaption>${k+1}. ${DST[k].n}</figcaption></figure>`).join('')}</div>`;
    cta.innerHTML=`<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn primary" id="dgo">${ICON.camera}Start</button>`;
    $('#back').onclick=()=>{ S.dmgAi--; S.dmgSt=3; render(); };
    $('#dgo').onclick=()=>{ DTAG=p=>{ p.area=a.label; p.zone=a.key; p.stage='whole'; }; DMGRUN={scope:'area'}; shoot('damage'); };
    return;
  }
  const ph=S.damage.map((p,i)=>[p,i]).filter(([p])=>p.area===a.label&&p.stage===st.id), c=stCount(a,st.id);
  const tiles = si<3 ? a.views.map((v,j)=>({src:dexSrc(a,si,j),cap:si===2?['Dents','Scratches','Cracks'][j]:VCAP[j]})) : a.extras.map((x,j)=>({src:dexSrc(a,3,j),cap:XT[x].l}));
  const txt=[`Stand about <b>3 big steps back</b> and take 2–3 photos, moving round so we can see the damage from different angles.`,
    `Now just <b>1 big step away</b>. Take 2–3 photos from different angles.`,
    `Take some <b>close-ups</b> of each dent, scratch or crack.`,
    `If you’d like us to see anything else, add it here – or feel free to skip.`][si];
  const tip=si===2?'<div class="gtip"><b>Tip</b><span>If light is bouncing off the damage, take a step to one side.</span></div>':'';
  main.innerHTML=`<div class="gsteps">${DST.map((d,k)=>`<div class="${k<si?'dn':k===si?'on':''}">${d.n}</div>`).join('')}</div>
    <h2>${si+1}. ${GT[si]}${si===3?'<span class="opt">Optional</span>':''}</h2>
    ${areas.length>1?`<p class="garea">Damaged area ${S.dmgAi+1} of ${areas.length}</p>`:''}
    <p class="lede">${txt}</p>
    ${si<3?`<figure class="gmini">${miniSVG(a,si)}<figcaption>${['about 3 big steps back','just 1 big step away','close up'][si]}</figcaption></figure>`:''}
    <div class="gtiles">${tiles.map(t=>`<figure><img src="${t.src}" alt=""><figcaption>${t.cap}</figcaption></figure>`).join('')}</div>
    ${tip}
    ${c?`<p class="dgh"><b>Your photos</b><span>${c}</span></p><div class="grid">${ph.map(([p,i])=>thumbHTML(p,'damage:'+i)).join('')}</div>`:''}
    ${uplogHTML(S.damage)}`;
  const take=()=>{ DTAG=p=>{ p.area=a.label; p.zone=a.key; p.stage=st.id; }; DMGRUN={scope:'stage'}; shoot('damage'); };
  const nextStep=()=>{ if(si<3){ S.dmgSt++; if(S.dmgSt===3) S.dmgXi=0; render(); return; } S.dmgDone=Object.assign({},S.dmgDone,{[a.label]:1}); if(S.dmgAi<areas.length-1){ S.dmgAi++; S.dmgSt=0; S.dmgXi=0; } else S.dmgFin=1; render(); };
  if(!c) cta.innerHTML = si<3 ? `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn primary" id="dtake">${ICON.camera}Take 2–3 photos</button>`
    : `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn outline" id="dnext">Skip extras</button><button class="btn primary" id="dtake">${ICON.camera}Add extras</button>`;
  else cta.innerHTML=`<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn outline" id="dtake">${ICON.camera}Add more</button><button class="btn primary" id="dnext">${si<3?'Next step':'Done'}</button>`;
  $('#dtake').onclick=()=>{ if(si===3) S.dmgXi=0; take(); };
  const dn=$('#dnext'); if(dn) dn.onclick=nextStep;
  if(si>0) $('#back').onclick=()=>{ S.dmgSt--; render(); };
  else if(S.dmgAi>0) $('#back').onclick=()=>{ S.dmgAi--; S.dmgSt=3; render(); };
}
const CHECKQ={plate:['Can you read the plate?','Check every letter and number is sharp before you carry on.'],dash:['Can you read the mileage?','Check the mileage is clear, with no glare across it.']};
function lightMeter(ov, video){
  const top=ov.querySelector('.top'); if(!top) return ()=>{};
  const b=document.createElement('span'); b.className='lightb'; b.innerHTML='<b></b><span>Checking light</span>';
  const dn=top.querySelector('.done'); top.insertBefore(b, dn||null);
  const c=document.createElement('canvas'); c.width=48; c.height=36; const x=c.getContext('2d',{willReadFrequently:true});
  const t=setInterval(()=>{ try{ if(!video.videoWidth) return; x.drawImage(video,0,0,48,36); const d=x.getImageData(0,0,48,36).data; let s=0; for(let i=0;i<d.length;i+=4) s+=0.299*d[i]+0.587*d[i+1]+0.114*d[i+2]; const ok=s/(d.length/4)>60; b.className='lightb '+(ok?'good':'low'); b.lastChild.textContent=ok?'Light: good':'Too dark – find more light'; }catch(e){} },700);
  return ()=>clearInterval(t);
}
function sharpScore(url){ return new Promise(res=>{ const im=new Image(); im.onload=()=>{ try{ const w=240,h=Math.max(3,Math.round(240*im.height/im.width)); const c=document.createElement('canvas'); c.width=w; c.height=h; const x=c.getContext('2d'); x.drawImage(im,0,0,w,h); const d=x.getImageData(0,0,w,h).data; const g=new Float32Array(w*h); for(let i=0;i<w*h;i++) g[i]=0.299*d[i*4]+0.587*d[i*4+1]+0.114*d[i*4+2]; let s=0,s2=0,n=0; for(let y=1;y<h-1;y++) for(let X=1;X<w-1;X++){ const i=y*w+X, l=g[i-1]+g[i+1]+g[i-w]+g[i+w]-4*g[i]; s+=l; s2+=l*l; n++; } const m=s/n; res(s2/n-m*m); }catch(e){ res(999); } }; im.onerror=()=>res(999); im.src=url; }); }
async function frameCam(key){
  if(!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) return pick(key);
  const FR={plate:['plate','Fill the box with the number plate'],vin:['plate','Fill the box with the VIN'],dash:['dash','Put the mileage in the box, with the rest of the clocks in shot too']}[key]||['dash',''];
  const ov=document.createElement('div'); ov.className='cam fcam';
  ov.innerHTML=`<video playsinline autoplay muted></video><div class="frm ${FR[0]}"><i></i><i></i><i></i><i></i></div><p class="gtext">${FR[1]}</p><div class="flash"></div>
    <div class="top"><button class="xbtn" aria-label="Close camera">${ICON.back}</button><span class="cnt">${key==='vin'?'VIN':TITLES[key]}</span></div>
    <div class="bot"><div class="shutterrow"><button class="shutter" aria-label="Take photo"></button></div></div>`;
  document.body.appendChild(ov); document.documentElement.style.overflow='hidden'; document.body.style.overflow='hidden';
  const video=ov.querySelector('video'), flash=ov.querySelector('.flash'); let stopL=null;
  const close=()=>{ if(stopL) stopL(); if(camStream){ camStream.getTracks().forEach(tr=>tr.stop()); camStream=null; } document.documentElement.style.overflow=''; document.body.style.overflow=''; ov.remove(); render(); };
  ov.querySelector('.xbtn').onclick=close;
  try{ camStream=await navigator.mediaDevices.getUserMedia({ video:{ facingMode:{ideal:'environment'}, width:{ideal:2560}, height:{ideal:1920} }, audio:false }); video.srcObject=camStream; await video.play(); stopL=lightMeter(ov,video); }
  catch(e){ ov.remove(); document.documentElement.style.overflow=''; document.body.style.overflow=''; note('Camera blocked here – using the phone camera'); return pick(key); }
  ov.querySelector('.shutter').onclick=()=>{ if(!shotFromVideo(video, p=>{ addPhoto(key,p); close(); })) return;
    flash.classList.add('on'); setTimeout(()=>flash.classList.remove('on'),60); if(navigator.vibrate) navigator.vibrate(25); };
}
function sheet(inner){
  const sh=document.createElement('div'); sh.className='sheetwrap';
  sh.innerHTML=`<div class="sheet" role="dialog" aria-modal="true" aria-labelledby="shh">${inner}</div>`;
  document.body.appendChild(sh); requestAnimationFrame(()=>sh.classList.add('in'));
  sh._close=()=>{ sh.classList.remove('in'); setTimeout(()=>sh.remove(),220); };
  return sh;
}
function nudge(n){
  const sh=sheet(`<h2 id="shh">Could you add a few more?</h2>
    <p class="lede">${(()=>{ const w=weakAreas(); return w.length ? (w.length===1 ? (dmgCount(w[0]) ? `You’ve only taken ${dmgCount(w[0])} of the ${w[0].label.toLowerCase()}.` : `You haven’t taken any of the ${w[0].label.toLowerCase()} yet.`) : `Some areas need more photos: ${w.map(a=>{ const c=dmgCount(a); return a.label.toLowerCase()+' ('+(c?c+' so far':'none yet')+')'; }).join(', ')}.`) : `You’ve taken ${n}.`; })()} Our engineer can only assess what they can see. Most people send at least 3 of each damaged area, and with plenty of photos we’re less likely to need to come back to you.</p>
    <button class="btn primary" id="nmore">${ICON.camera}Add more photos</button><button class="btn plain" id="nanyway">Continue with ${n}</button>`);
  const close=sh._close;
  sh.onclick=e=>{ if(e.target===sh) close(); };
  sh.querySelector('#nmore').onclick=()=>{ close(); const as=dmgAreas(), w=as.findIndex(a=>dmgCount(a)<3); if(w>=0){ S.dmgAi=w; S.dmgSt=0; S.dmgXi=0; } DMGRUN={scope:w>=0?'area':'none'}; shoot('damage'); };
  sh.querySelector('#nanyway').onclick=()=>{ close(); let j=S.step+1; while(STEPS[j]==='handoff') j++; go(j); };
}
/* After Send: what the server says is still missing, in its own words; the heading and buttons are the operator's (9 November 2026; R787). */
function missingSheet(list){
  const sh=sheet(`<h2 id="shh">Could you add a few more?</h2>
    <ul class="misslist">${list.map(m=>`<li>${esc(m.label)}</li>`).join('')}</ul>
    <button class="btn primary" id="nmore">${ICON.camera}Add more</button><button class="btn plain" id="nanyway">That’s everything</button>`);
  sh.querySelector('#nmore').onclick=()=>{ sh._close(); S._sending=false; returnTo='summary'; routeMissing(list[0].code); };
  sh.querySelector('#nanyway').onclick=()=>{ sh._close(); S._sending=false; S._sent=true; render(); };
}
function routeMissing(code){
  const c=String(code||'');
  if(c==='overview') return go(STEPS.indexOf('corners'));
  if(c==='plate'||c==='vin') return go(STEPS.indexOf('plate'));
  if(c==='dashboard') return go(STEPS.indexOf('dash'));
  const m=/^damage:([^:]+):(.+)$/.exec(c);
  if(m){ dmgSync(); const ai=dmgAreas().findIndex(a=>a.ids.includes(m[1])); if(ai>=0){ S.dmgAi=ai; S.dmgSt=Math.max(0,DST.findIndex(d=>d.api===m[2])); S.dmgXi=0; S.dmgFin=0; } return go(STEPS.indexOf('damage')); }
  returnTo=null; go(STEPS.indexOf('summary'));
}

/* ---------- SVG bits ---------- */
function carSVG({interactive=false, selected=[]}={}){
  const chev = 'M-9 -7 L-2 0 L-9 7 M-1 -7 L6 0 L-1 7 M7 -7 L14 0 L7 7';
  const z = interactive ? ZONES.map(zn=>`<g class="zone ${selected.includes(zn.id)?'on':''}" data-id="${zn.id}" transform="translate(${zn.x} ${zn.y})" tabindex="0" role="button" aria-pressed="${selected.includes(zn.id)}" aria-label="${zn.label}"><title>${zn.label}</title><circle class="hit" r="17"/><path class="chev" d="${chev}" transform="rotate(${zn.rot}) translate(-2 0)"/></g>`).join('') : '';
  const labels = interactive ? `<text class="sidelabel" x="-28" y="145" transform="rotate(-90 -28 145)" text-anchor="middle">PASSENGER SIDE</text><text class="sidelabel" x="228" y="145" transform="rotate(90 228 145)" text-anchor="middle">DRIVER SIDE</text><text class="sidelabel" x="100" y="-42" text-anchor="middle" dominant-baseline="hanging">FRONT</text>` : '';
  return `<svg viewBox="${interactive?'-38 -46 276 376':'0 -4 200 300'}" xmlns="http://www.w3.org/2000/svg" aria-hidden="${!interactive}"><image href="${asset('car-top')}" x="25" y="6" width="150" height="280" preserveAspectRatio="xMidYMid meet"/>${labels}${z}</svg>`;
}
/* little line-art examples (original, generic) */
const EX = {
  plate: () => `<svg viewBox="0 0 300 120" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 110V60c0-12 10-22 24-26l40-14h132l40 14c14 4 24 14 24 26v50" opacity=".35"/><rect x="90" y="62" width="120" height="34" rx="3" fill="#f6d94f" stroke="#111"/><rect x="92" y="64" width="16" height="30" fill="#1c4ca8" stroke="none"/><text x="156" y="86" text-anchor="middle" font-family="Tw Cen MT Std, sans-serif" font-weight="700" font-size="20" fill="#111" stroke="none" letter-spacing="2">${esc(regShown()||'AB12 CDE')}</text><path d="M70 50h-8v8M230 50h8v8M70 110h-8v-8M230 110h8v-8" stroke="#db0816" stroke-width="3"/></svg>`,
  dash:  `<svg viewBox="0 0 300 120" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10 110c20-50 60-80 140-80s120 30 140 80" opacity=".35"/><circle cx="95" cy="80" r="34"/><circle cx="205" cy="80" r="34"/><path d="M95 80l-16-20M205 80l20-16"/><rect x="128" y="86" width="44" height="18" rx="2"/><text x="150" y="99" text-anchor="middle" font-size="11" font-family="monospace" fill="currentColor" stroke="none">105000</text><path d="M150 44l-6 10h12z" stroke="#db0816"/><circle cx="150" cy="66" r="4" stroke="#db0816"/></svg>`,
};

/* ---------- renderers ---------- */
/* Nothing sent is taken back: the bin shows only while an item is neither uploaded nor on its way. */
function canDel(p){ return !!p && !p.uploaded && !(p.job && p.job.state==='busy'); }
/* a review-strip picture; one the server refused is outlined */
function simg(p){ return `<img src="${p.url}" alt=""${p.failed?' class="failed"':''}>`; }
function thumbHTML(p, onDel, tag){
  return `<div class="thumb${p.failed?' failed':''}"><img src="${p.url}" alt="">${tag?`<span class="tag">${esc(tag)}</span>`:''}${canDel(p)?`<button class="del" aria-label="Remove photo" data-del="${onDel}">${ICON.trash}</button>`:''}</div>`;
}
let UPL=[];
function uplogHTML(list){
  if(!list.length) return '';
  UPL.push(list);
  return `<div class="uplog" data-ul="${UPL.length-1}">${uplogInner(list)}</div>`;
}
function uplogInner(list){
  const live=list.filter(p=>!p.cancelled), n=live.filter(p=>p.uploaded).length, busy=live.some(p=>!p.uploaded&&!p.failed), tot=live.reduce((a,p)=>a+p.bytes,0), orig=live.reduce((a,p)=>a+p.orig,0);
  return `<span class="dot ${busy?'busy':''}"></span>${busy?(stalled()?retryHTML():'Uploading…'):`${n} of ${live.length} uploaded`} · ${fmt(tot)} sent${orig>tot?` (compressed from ${fmt(orig)})`:''}`;
}
function allPhotos(){ return [...S.overview, S.plate, S.vin, ...S.damage, S.dash, ...S.scene, ...S.other].filter(Boolean); }
function countLine(n){ return n?`<div class="counter"><span><b>${n}</b> photo${n===1?'':'s'} taken</span></div>`:''; }
function docsField(hintText, multi){
  return `<div class="field"><label>Documents <span class="hint optl">(optional)</span></label>
        <span class="hint hint-b">${hintText}</span>
        ${S.docs.map((d,i)=>`<div class="docrow"><span class="docname">${esc(d.name)}</span><span class="hint">${fmt(d.bytes)}</span>${canDel(d)?`<button class="del docdel" data-deldoc="${i}" aria-label="Remove">${ICON.trash}</button>`:''}</div>`).join('')}
        <button class="btn outline btn-sm" id="adddoc">${ICON.plus}Add documents or photos</button>
        ${multi?'<span class="hint hint-t">Multi-page? Add each page as a photo – you can pick several at once.</span>':''}
        ${uplogHTML(S.docs)}
      </div>`;
}
function locField(I){
  return `<div class="field"><label for="loc">Inspection location</label>
        <select id="loc" data-ik="locType" class="finput fselect">
          <option value="" ${I.locType?'':'selected'} disabled>Choose a location…</option>
          ${SRC.addresses.map(a=>`<option value="${esc(a.name)}" ${I.locType===a.name?'selected':''}>${esc(a.name)}${a.address?' – '+esc(a.address):''}</option>`).join('')}
          <option value="With the customer" ${I.locType==='With the customer'?'selected':''}>With the customer</option>
          <option value="Somewhere else" ${I.locType==='Somewhere else'?'selected':''}>Somewhere else</option>
        </select>
        ${(I.locType==='With the customer'||I.locType==='Somewhere else')?`<input type="text" data-ik="locOther" value="${esc(I.locOther)}" placeholder="Address and postcode (leave blank if unsure)" class="mt8">`:''}
      </div>`;
}
function fmtDate(v){ return v ? new Date(v+'T00:00:00').toLocaleDateString('en-GB',{day:'numeric',month:'short',year:'numeric'}) : '—'; }

const R = {
  welcome(){
    const tot=priorPhotos();
    const again = tot>0 ? `<p class="lede"><strong>You've already sent us ${nPhotos(tot)} – add more?</strong></p>` : '';
    if(SRC && SRC.kind==='client'){
      main.innerHTML = `<div class="welcome">
      <h1>Send us photos of your vehicle</h1>
      ${again}
      <p class="lede">Use this page to send Collision Engineers photos of your damaged vehicle for assessment. It takes about <strong>3 minutes</strong> and there’s nothing to download.</p>
      <ul class="checklist">
        <li><span class="chip">${ICON.list}</span><div><b>Your details</b><small>Registration, your name and a contact number</small></div></li>
        <li><span class="chip">${ICON.car}</span><div><b>Mark where the accident damage is</b><small>Tap the areas on a diagram</small></div></li>
        <li><span class="chip">${ICON.camera}</span><div><b>Take guided photos</b><small>Overview all the way round, the damage, number plate, mileage – plus an optional walk-around video</small></div></li>
      </ul>
      <p class="hint mt18">Please have the car in front of you in good light, and try to keep your reflection out of shot. The more photos you take, the better.</p>
      </div>`;
      cta.innerHTML = `<button class="btn primary" id="next">Start</button>`;
      return;
    }
    if(SRC && SRC.kind==='bodyshop'){
      main.innerHTML = `<div class="welcome">
      <h1>${SRC.preset?`Photos needed for ${esc(regShown())}`:`Vehicle in for assessment at ${esc(SRC.name)}`}</h1>
      ${again}
      ${SRC.preset?`<p class="lede">Collision Engineers have been instructed on this vehicle and are waiting for the photos.</p><span class="reg"><span>GB</span>${esc(regShown())}</span>`:''}
      <p class="lede">${SRC.preset?'Confirm a few details, then take the guided photos – or send the customer a link if the car isn’t with you.':'Use this link whenever a vehicle comes in that Collision Engineers will be assessing. Tell us the basics, then take the guided photos – about three minutes.'}</p>
      <ul class="checklist">
        <li><span class="chip">${ICON.list}</span><div><b>The job</b><small>Reg, who’s handling the claim, your view on repair</small></div></li>
        <li><span class="chip">${ICON.car}</span><div><b>Mark where the accident damage is</b><small>Tap the areas on a diagram</small></div></li>
        <li><span class="chip">${ICON.camera}</span><div><b>Guided photos</b><small>Overview all the way round, the damage, number plate, mileage – plus an optional walk-around video</small></div></li>
      </ul>
      </div>`;
      cta.innerHTML = `<button class="btn primary" id="next">Start</button>`;
      return;
    }
    if(SRC){
      main.innerHTML = `<div class="welcome">
      <h1>${SRC.preset?`Photos needed for ${esc(regShown())}`:`New instruction from ${esc(SRC.name)}`}</h1>
      ${again}
      ${SRC.preset?`<p class="lede">Thanks for the instruction – we’re waiting on the photos for this one.</p><span class="reg"><span>GB</span>${esc(regShown())}</span>`:''}
      <p class="lede">Use this link to send Collision Engineers a vehicle for assessment. Tell us about the claim, then take the guided photos – or send the customer a web link to upload the images themselves.</p>
      <ul class="checklist">
        <li><span class="chip">${ICON.list}</span><div><b>The claim</b><small>Customer, reg, where the car is, what you’re looking for</small></div></li>
        <li><span class="chip">${ICON.car}</span><div><b>Mark where the accident damage is</b><small>Tap the areas on a diagram</small></div></li>
        <li><span class="chip">${ICON.camera}</span><div><b>Guided photos</b><small>Overview all the way round, the damage, number plate, mileage – plus an optional walk-around video</small></div></li>
      </ul>
      </div>`;
      cta.innerHTML = `<button class="btn primary" id="next">Start</button>`;
      return;
    }
    main.innerHTML = `<div class="welcome">
      <h1>Let’s get photos of your vehicle</h1>
      ${again}
      ${CASE.reg?`<p class="lede">You’ve been sent this link so our engineers can assess the damage to:</p>
      <span class="reg"><span>GB</span>${esc(regShown())}</span>`:''}
      <p class="lede">It takes about <strong>3 minutes</strong> and there’s nothing to download.</p>
      <ul class="checklist">
        <li><span class="chip">${ICON.car}</span><div><b>Mark where the accident damage is</b><small>Tap the areas on a diagram</small></div></li>
        <li><span class="chip">${ICON.camera}</span><div><b>Take guided photos</b><small>Overview all the way round, the damage, number plate, mileage – plus an optional walk-around video</small></div></li>
        <li><span class="chip">${ICON.list}</span><div><b>Add any notes</b><small>Anything you want the engineer to know</small></div></li>
      </ul>
      <p class="hint mt18">Please have the car in front of you in good light, and try to keep your reflection out of shot. The more photos you take, the better.</p>
    </div>`;
    cta.innerHTML = `<button class="btn primary" id="next">Start</button>`;
  },
  instruction(){
    const I=S.ins;
    const seg=(k,opts)=>`<div class="seg" data-ik="${k}">${opts.map(o=>`<button data-v="${o}" class="${I[k]===o?'on':''}">${o}</button>`).join('')}</div>`;
    const today=new Date().toISOString().slice(0,10);
    const bindInputs=()=>{
      main.querySelectorAll('input[data-ik]').forEach(el=>el.oninput=e=>{ let v=e.target.value; if(el.dataset.ik==='reg'){ v=v.toUpperCase().replace(/[^A-Z0-9]/g,''); e.target.value=v; } S.ins[el.dataset.ik]=v; save(); updateCTA(); });
      main.querySelectorAll('.seg[data-ik] button').forEach(b=>b.onclick=()=>{ S.ins[b.parentElement.dataset.ik]=b.dataset.v; save(); render(); });
      const sel=$('#loc'); if(sel) sel.onchange=e=>{ S.ins.locType=e.target.value; save(); render(); const i=main.querySelector('input[data-ik="locOther"]'); if(i) i.focus(); };
      const ad=$('#adddoc'); if(ad) ad.onclick=()=>fileDocs.click();
      main.querySelectorAll('[data-deldoc]').forEach(b=>b.onclick=()=>{ const d=S.docs[+b.dataset.deldoc]; if(canDel(d)){ S.docs.splice(+b.dataset.deldoc,1); cancelUpload(d); } render(); });
    };
    /* A standing link starts the capture here; afterwards this form only edits its details. A quick Case link hands its own capture on, once: a second tap shows the link already made. */
    const bindStart=()=>{ const nb=$('#next'); if(nb && isStanding()) nb.onclick=()=>beginCapture(nb); const hb=$('#handoff'); if(hb) hb.onclick=()=>{ if(SRC.preset && S.handoffRes){ S.handoff=true; go(STEPS.indexOf('handoff')); return; } if(SRC.kind==='source' && !S.ins.locType) S.ins.locType='With the customer'; sendHandoff(hb); }; };
    const handoffBtn=()=>(isStanding()||SRC.preset)?`<button class="btn outline" id="handoff" ${handoffOK()?'':'disabled'}>Send customer a link</button>`:'';
    if(SRC.kind==='client'){
      main.innerHTML = `<h2>Your details</h2>
      <p class="lede">So we can match your photos to your claim and get back to you if we need anything.</p>
      <div class="field"><label for="reg">Vehicle registration</label><input id="reg" type="text" data-ik="reg" value="${esc(I.reg)}" placeholder="e.g. RY65OFG" autocapitalize="characters" autocomplete="off" class="reginput"${SRC.preset||regLocked()?' readonly':''}></div>
      <div class="field"><label for="customer">Your name</label><input id="customer" type="text" data-ik="customer" value="${esc(I.customer)}" autocomplete="name"></div>
      <div class="field"><label for="phone">Mobile number</label><input id="phone" type="text" inputmode="tel" data-ik="phone" value="${esc(I.phone)}" autocomplete="tel"><span class="hint">We’ll only use this about your assessment</span></div>
      <div class="field"><label for="handler">Who is dealing with your claim? <span class="hint optl">(optional)</span></label><input id="handler" type="text" data-ik="handler" value="${esc(I.handler)}" placeholder="Solicitor or claims company" autocomplete="off"></div>
      <div class="field"><label for="accident">Accident date</label><input id="accident" type="date" data-ik="accident" value="${esc(I.accident||'')}" max="${today}" class="finput"></div>`;
      bindInputs();
      cta.innerHTML = `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn primary" id="next" ${insOK()?'':'disabled'}>Continue</button>`;
      bindStart();
      return;
    }
    if(SRC.kind==='bodyshop'){
      main.innerHTML = `<h2>About this job</h2>
      <p class="lede">A few details so we can match your photos to the instruction when it arrives – or chase it if it doesn’t.</p>
      <div class="field"><label for="reg">Vehicle registration</label><input id="reg" type="text" data-ik="reg" value="${esc(I.reg)}" placeholder="e.g. RY65OFG" autocapitalize="characters" autocomplete="off" class="reginput"${SRC.preset||regLocked()?' readonly':''}></div>
      <div class="field"><label for="customer">Customer name <span class="hint optl">(optional)</span></label><input id="customer" type="text" data-ik="customer" value="${esc(I.customer)}"></div>
      ${locField(I)}
      <div class="field"><label for="handler">Who is handling the claim?</label><input id="handler" type="text" list="handlers" data-ik="handler" value="${esc(I.handler)}" placeholder="Start typing, or choose from the list" autocomplete="off"><datalist id="handlers"><option value="Customer direct (no claims company)"><option value="Not sure yet"></datalist><span class="hint">The accident management company, credit hire firm or solicitor – or choose “Not sure yet”</span></div>
      <div class="field"><label>Your view on the vehicle</label>${seg('view',['Repairable','Total loss','Not sure'])}<span class="hint">Our engineer makes the final call</span></div>
      ${docsField('Your estimate, wheel alignment print out, etc. Choose a file or take a photo of each page.', false)}`;
      bindInputs();
      cta.innerHTML = `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button>${handoffBtn()}<button class="btn primary" id="next" ${insOK()?'':'disabled'}>Take photos now</button>`;
      bindStart();
      return;
    }
    main.innerHTML = `<h2>About this claim</h2>
      <p class="lede">The more our engineer knows up front, the more accurate the report will be.</p>
      <div class="field"><label for="reg">Vehicle registration</label><input id="reg" type="text" data-ik="reg" value="${esc(I.reg)}" placeholder="e.g. RY65OFG" autocapitalize="characters" autocomplete="off" class="reginput"${SRC.preset||regLocked()?' readonly':''}></div>
      <div class="field"><label for="accident">Accident date <span class="hint optl">(optional)</span></label><input id="accident" type="date" data-ik="accident" value="${esc(I.accident||'')}" max="${today}" class="finput"><span class="hint">Helpful if the vehicle has had more than one claim</span></div>
      <div class="field"><label for="customer">Customer / client name <span class="hint optl">(optional)</span></label><input id="customer" type="text" data-ik="customer" value="${esc(I.customer)}"></div>
      ${locField(I)}
      <div class="field"><label>Is the vehicle roadworthy?</label>${seg('roadworthy',['Yes','No','Not sure'])}<span class="hint">Your view – our engineer makes the final call</span></div>
      <div class="field"><label>Preferred outcome</label>${seg('outcome',['Repair','Total loss','No preference'])}</div>
      <div class="field"><label>Is the customer in hire, or going into hire?</label>${seg('hire',['Yes','No','Not sure'])}</div>
      ${docsField('Repair estimate, taxi suspension notice, etc. Choose a file or take a photo of the paperwork.', true)}`;
    bindInputs();
    cta.innerHTML = `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button>${handoffBtn()}<button class="btn primary" id="next" ${insOK()?'':'disabled'}>Take photos now</button>`;
    bindStart();
  },
  handoff(){
    const H=S.handoffRes; if(!H){ S.handoff=false; return go(STEPS.indexOf('instruction')); }
    const reg=H.registration||regShown(), link=H.url, msg=H.message+H.url;
    main.innerHTML = `<h2>Send the customer a link</h2>
      <p class="lede">We’ve created a one-time link for <strong>${esc(reg)}</strong>. Forward it to ${esc(S.ins.customer||'the customer')} and they’ll be guided through the same photo steps. Their photos go straight to our engineers as they take them.</p>
      <div class="big linkbox">
        <div class="linktxt" id="linktxt">${esc(link)}</div>
        <div class="sharerow">
          <a class="btn primary sharebtn" href="https://wa.me/?text=${encodeURIComponent(msg)}" target="_blank" rel="noopener">WhatsApp</a>
          <a class="btn outline sharebtn" href="sms:?&amp;body=${encodeURIComponent(msg)}">Text message</a>
          <button class="btn ghost sharebtn-c" id="copy">Copy</button>
        </div>
      </div>
      <div class="field"><label>Message preview</label><textarea rows="5" readonly class="mutedtext">${esc(msg)}</textarea></div>
      <p class="hint">The link expires in ${SRC.lifetimeDays} days. You’ll see on the HQ side when it’s been opened and when the photos arrive, and the customer’s photos go through the same steps (overview all the way round, the damage, number plate and dashboard).</p>`;
    $('#copy').onclick=async()=>{ try{ await navigator.clipboard.writeText(link); note('Link copied'); }catch(e){ note('Copy the link from the box above'); } };
    cta.innerHTML = `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn primary" id="finish">I’ve sent it – finish</button>`;
    $('#back').onclick=()=>{ S.handoff=false; if(!SRC.preset) S.handoffRes=null; go(STEPS.indexOf('instruction')); };
    $('#finish').onclick=()=>{
      /* documents added on the form go onto the capture just made for the customer, under this phone's own session credential, which never marks the customer's link opened */
      if(H.session && JOBS.some(j=>j.state==='queued')){ LINK='c.'+H.session; detailsSig=JSON.stringify(detailsBody()); pump(); }
      go(STEPS.indexOf('done')); };
  },
  location(){
    main.innerHTML = `<h2>Where is the damage?</h2>
      <p class="lede">Tap every area of the car that has been damaged in this accident. You can choose more than one.</p>
      <div class="carwrap" id="car">${carSVG({interactive:true, selected:S.zones})}</div>
      <div class="chips" id="chips"></div>`;
    renderChips();
    main.querySelectorAll('.zone').forEach(z=>{
      const tog=()=>{ const id=z.dataset.id; S.zones = S.zones.includes(id)? S.zones.filter(x=>x!==id) : [...S.zones,id]; z.classList.toggle('on'); z.setAttribute('aria-pressed', S.zones.includes(id)); renderChips(); save(); updateCTA(); };
      z.addEventListener('click',tog); z.addEventListener('keydown',e=>{ if(e.key==='Enter'||e.key===' '){e.preventDefault();tog();} });
    });
    cta.innerHTML = `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn primary" id="next" ${S.zones.length?'':'disabled'}>Continue</button>`;
  },
  damage(){
    if(GUIDED) return dmgGuidedPage();
    dmgSync(); const n=dmgTotal(), areas=dmgAreas(), a0=areas[0];
    main.innerHTML = `<h2>Damage photos</h2>
      <p class="lede">For each damaged area: an overview, a bit closer, close-ups, then any extras. The camera guides you through it.</p>
      ${areas.length?`<div class="chips chips-tight">${areas.map(a=>`<span>${a.label}</span>`).join('')}</div><p class="hint m0-10"><button class="linkbtn" id="chgarea">Change damaged areas</button></p>`:''}
      ${liveNote()}
      ${n||!a0?'':`<div class="dexs four">${[0,1,2,3].map(k=>`<figure><div class="dexi"><img src="${dexSrc(a0,k,k===2?0:1)}" alt=""></div><figcaption>${k+1}. ${DST[k].n}</figcaption></figure>`).join('')}</div>`}
      ${n?`${dmgGroups(areas)}<div class="counter"><span><b>${n}</b> photo${n===1?'':'s'} taken</span>${n>=4?`<span class="ok">${ICON.check} Keep going if there’s more to show</span>`:''}</div>`:''}
      ${uplogHTML(S.damage)}`;
    main.insertAdjacentHTML('beforeend', `<p class="hint mt14">Camera not working? <button class="linkbtn" id="altcam">Use the phone’s own camera instead</button></p>`);
    $('#altcam').onclick=()=>pick('damage');
    cta.innerHTML = `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button>${n>=4?`<button class="btn outline" id="next">Continue</button><button class="btn primary" id="add2">${ICON.camera}Add more</button>`:`<button class="btn primary" id="add2">${ICON.camera}${n?'Take more':'Take photos'}</button>`}`;
    $('#add2').onclick=()=>{ DMGRUN={scope:S.dmgFin?'none':'all'}; shoot('damage'); };
    const ca=$('#chgarea'); if(ca) ca.onclick=()=>go(STEPS.indexOf('location'));
  },
  corners(){
    const n=S.overview.length+rc('overview');
    main.innerHTML = `<h2>Overview shots</h2>
      <p class="lede">Take 8 photos, walking round the whole vehicle in the order shown. Stand well back so the full car is in frame, and keep your reflection out of shot.</p>
      ${liveNote()}
      ${n?'':`<figure class="ovgimg"><img src="${asset('guide')}" alt="The 8 overview photos in order: 1 front, 2 front driver corner, 3 driver side, 4 rear driver corner, 5 rear, 6 rear passenger corner, 7 passenger side, 8 front passenger corner"></figure>`}
      <div class="grid">${S.overview.map((p,i)=>thumbHTML(p,'overview:'+i)).join('')}</div>
      ${n?`<div class="counter"><span><b>${n}</b> photo${n===1?'':'s'} taken</span>${n>=4?`<span class="ok">${ICON.check} Keep going if there’s more to show</span>`:''}</div>`:''}
      ${uplogHTML(S.overview)}`;
    cta.innerHTML = `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button>${n>=4?`<button class="btn outline" id="next">Continue</button><button class="btn primary" id="ovmore">${ICON.camera}Add more</button>`:`<button class="btn primary" id="ovmore">${ICON.camera}${n?'Take more':'Take overview photos'}</button>`}`;
    $('#ovmore').onclick=()=>openCam2('overview');
  },
  plate(){ singleShot('plate', 'Number plate', 'A clear photo of the registration plate confirms we’re looking at the right vehicle.', EX.plate(), 'Line the plate up inside the red corners and make sure it’s readable.'); },
  dash(){  singleShot('dash', 'Dashboard', 'Start the car and take a photo of the instrument cluster so we can see the mileage and any warning lights.', EX.dash, 'If the car won’t start, turn the ignition to the “on” position instead.'); },
  extras(){
    const nd=S.docs.length+rc('paperwork'), ns=S.scene.length+rc('scene'), no=S.other.length+rc('other-vehicle'), hasVideo=!!S.video||rc('video')>0;
    const row=(id,title,sub,done,btn)=>`<div class="xrow ${done?'done':''}"><div class="xt"><b>${title}</b><small>${sub}</small></div><button class="xadd" data-x="${id}">${done?ICON.check+(btn||'Added'):ICON.plus+'Add'}</button></div>`;
    main.innerHTML = `<h2>Extras</h2>
      <p class="lede">Optional, but they help our engineer get the full picture. Add whatever you have, then carry on.</p>
      <div class="xlist">
        ${row('video','Walk-around video','A slow walk once round the car, under a minute', hasVideo, 'Added')}
        ${row('scene','Accident scene photos', ns?`${ns} photo${ns===1?'':'s'} added`:'Where it happened', ns>0, 'Add more')}
        ${row('other','Other vehicle photos', no?`${no} photo${no===1?'':'s'} added`:'The other vehicle involved', no>0, 'Add more')}
        ${row('docs','Estimates or paperwork', nd?`${nd} file${nd===1?'':'s'} added`:'Repair estimates, invoices, letters', nd>0, 'Add more')}
        <label class="xnote ${S.notes?'done':''}" for="notes"><b>Anything you’d like to tell us?</b><small>Type a note here for our engineer to read – for example, the boot doesn’t close.</small><textarea id="notes" rows="4" placeholder="Type your note here">${esc(S.notes)}</textarea></label>
      </div>
      ${uplogHTML([...S.scene,...S.other,...S.docs,...(S.video?[S.video]:[])])}`;
    main.querySelectorAll('[data-x]').forEach(b=>b.onclick=()=>{ const x=b.dataset.x;
      if(x==='video') pickVideo();
      else if(x==='scene'||x==='other'){ pending={kind:x}; fileMulti.removeAttribute('capture'); fileMulti.click(); }
      else if(x==='docs') fileDocs.click();
      else { S._notesOpen=true; render(); const ta=$('#notes'); if(ta) ta.focus(); } });
    const ta=$('#notes'); if(ta) ta.oninput=e=>{ S.notes=e.target.value; S.notesEdited=true; save(); ta.closest('.xnote').classList.toggle('done',!!S.notes.trim()); };
    cta.innerHTML = `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn primary" id="next">Continue to review</button>`;
  },
  summary(){
    const edit = s => `<button class="linkbtn" data-edit="${s}">${ICON.pencil} Edit</button>`;
    const nDmg=dmgTotal(), nScene=S.scene.length+rc('scene'), nOther=S.other.length+rc('other-vehicle'), live=allPhotos().filter(p=>!p.cancelled), up=live.filter(p=>p.uploaded);
    main.innerHTML = `<h2>Check and send</h2>
      <p class="lede">Have a quick look over everything before you send it to us.</p>
      <div class="sec"><div class="h"><b>Vehicle</b></div><div class="kv"><span class="muted">Registration</span><span>${esc(regShown())}</span>${SRC&&SRC.kind!=='client'&&SRC.name?`<span class="muted">${SRC.kind==='bodyshop'?'Repairer':'Instructed by'}</span><span>${esc(SRC.name)}</span>`:''}</div></div>
      ${SRC&&SRC.kind==='client'?`<div class="sec"><div class="h"><b>Your details</b>${edit('instruction')}</div><div class="kv"><span class="muted">Name</span><span>${esc(S.ins.customer)}</span><span class="muted">Mobile</span><span>${esc(S.ins.phone)}</span><span class="muted">Claim handled by</span><span class="tr">${esc(S.ins.handler||'—')}</span><span class="muted">Accident date</span><span>${esc(fmtDate(S.ins.accident))}</span></div></div>`:''}
      ${SRC&&SRC.kind==='bodyshop'?`<div class="sec"><div class="h"><b>The job</b>${edit('instruction')}</div><div class="kv"><span class="muted">Customer</span><span>${esc(S.ins.customer||'—')}</span><span class="muted">Inspection location</span><span class="tr">${esc(locDisplay())}${S.ins.locOther?'<br><span class="muted">'+esc(S.ins.locOther)+'</span>':''}</span><span class="muted">Claim handled by</span><span class="tr">${esc(S.ins.handler)}</span><span class="muted">Your view</span><span>${esc(S.ins.view)}</span><span class="muted">Documents</span><span>${S.docs.length? esc(S.docs.map(d=>d.name).join(', ')) : '—'}</span></div></div>`:''}
      ${SRC&&SRC.kind==='source'?`<div class="sec"><div class="h"><b>The claim</b>${edit('instruction')}</div><div class="kv"><span class="muted">Accident date</span><span>${esc(fmtDate(S.ins.accident))}</span><span class="muted">Customer / client</span><span>${esc(S.ins.customer||'—')}</span><span class="muted">Inspection location</span><span class="tr">${esc(locDisplay())}${S.ins.locOther?'<br><span class="muted">'+esc(S.ins.locOther)+'</span>':''}</span><span class="muted">Roadworthy</span><span>${esc(S.ins.roadworthy)}</span><span class="muted">Preferred outcome</span><span>${esc(S.ins.outcome)}</span><span class="muted">In hire / going into hire</span><span>${esc(S.ins.hire)}</span><span class="muted">Documents</span><span>${S.docs.length? esc(S.docs.map(d=>d.name).join(', ')) : '—'}</span></div></div>`:''}
      <div class="sec"><div class="h"><b>Damage location</b>${edit('location')}</div><div class="chips">${S.zones.map(id=>`<span>${ZL(id)}</span>`).join('')}</div></div>
      <div class="sec"><div class="h"><b>Overview shots</b>${edit('corners')}</div><div class="strip">${S.overview.map(simg).join('')}</div></div>
      <div class="sec"><div class="h"><b>Plate, VIN and dashboard</b>${edit('plate')}</div><div class="strip">${[S.plate,S.vin,S.dash].filter(Boolean).map(simg).join('')}</div></div>
      <div class="sec"><div class="h"><b>Damage photos (${nDmg})</b>${edit('damage')}</div>${reviewDamage()}</div>
      <div class="sec"><div class="h"><b>Walk-around video</b>${edit('extras')}</div><div class="kv"><span class="muted">${S.video?`Added · ${fmt(S.video.bytes)}`:rc('video')?'Added':'Not added'}</span><span></span></div></div>
      ${(!S.dash&&S.noKeys)?`<div class="sec"><div class="h"><b>Dashboard</b>${edit('dash')}</div><span class="hint">Skipped – no keys available</span></div>`:''}
      <div class="sec"><div class="h"><b>Accident scene images (${nScene})</b>${edit('extras')}</div>${S.scene.length?`<div class="strip">${S.scene.map(simg).join('')}</div>`:(nScene?'':'<span class="hint">None added</span>')}</div>
      <div class="sec"><div class="h"><b>Other vehicle images (${nOther})</b>${edit('extras')}</div>${S.other.length?`<div class="strip">${S.other.map(simg).join('')}</div>`:(nOther?'':'<span class="hint">None added</span>')}</div>
      <div class="sec"><div class="h"><b>Your notes</b>${edit('extras')}</div>${S.notes?`<p class="notetext">${esc(S.notes)}</p>`:'<span class="hint">None</span>'}${S.mileageEst&&S.mileage?`<div class="kv mt8"><span class="muted">Estimated mileage</span><span>${Number(S.mileage).toLocaleString('en-GB')} miles</span></div>`:''}</div>
      <div class="uplog mb8"><span class="dot ${upBusy?'busy':''}"></span>${upBusy?(stalled()?retryHTML():'Still uploading the last photos…'):`${up.length<live.length?`${up.length} of ${live.length}`:`All ${nPhotos(up.length)}${S.video&&S.video.uploaded?' + video':''}`} uploaded · ${fmt(up.reduce((a,p)=>a+p.bytes,0)+(S.video&&S.video.uploaded?S.video.bytes:0))}`}</div>`;
    main.querySelectorAll('[data-edit]').forEach(b=>b.onclick=()=>{ returnTo='summary'; go(STEPS.indexOf(b.dataset.edit)); });
    cta.innerHTML = `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn primary" id="next" ${upBusy?'disabled':''}>Send to Collision Engineers</button>`;
  },
  done(){
    if(S.handoff){
      main.innerHTML = `<div class="done-wrap">
      <span class="tick">${ICON.check}</span>
      <h1>Instruction received</h1>
      <p class="lede">We’ve logged ${esc((S.handoffRes&&S.handoffRes.registration)||regShown())}${S.ins.customer?' for '+esc(S.ins.customer):''}${SRC.name?' from '+esc(SRC.name):''}. The job is now waiting for the customer’s photos – we’ll pick it up as soon as they arrive.</p>
      <p class="hint">Status: awaiting customer photos · link valid ${SRC.lifetimeDays} days</p>
      <p class="hint">${upBusy?(stalled()?retryHTML():'Still uploading the last photos…'):'You can close this page now.'}</p>
      </div>`;
      cta.innerHTML = '';
      return;
    }
    if(!S._sent){
      $('#stepLabel').textContent='Sending';
      const rows=[`${nPhotos(photosSent())}${S.video||rc('video')?' and a video':''} received`,'Your details saved',...(origPending()?['Full-quality originals carrying on in the background']:[]),'Sent to our engineering team'];
      main.innerHTML=`<div class="sendwrap"><h2>Sending your photos</h2><p class="lede">Keep this page open for a moment.</p><ul class="stages">${rows.map(r=>`<li><span class="dot"></span>${r}</li>`).join('')}</ul></div>`;
      cta.innerHTML='';
      if(!S._sending){ S._sending=true; S._result=null; completeCapture().then(r=>{ S._result=r; }); }
      const li=[...main.querySelectorAll('.stages li')]; let i=0;
      const finish=()=>{ if(STEPS[S.step]!=='done' || GONE) return; const r=S._result; if(!r){ setTimeout(finish,300); return; }
        if(r.status===200 && r.data){ const miss=Array.isArray(r.data.missing)?r.data.missing:[];
          if(miss.length) missingSheet(miss); else { S._sending=false; S._sent=true; render(); } return; }
        S._sending=false; if(r.status!==404){ problemNote(r.data); go(STEPS.indexOf('summary')); } };
      const tick=()=>{ if(STEPS[S.step]!=='done') return; if(i<li.length){ li[i].classList.add('on'); li[i].querySelector('.dot').innerHTML=ICON.check; i++; setTimeout(tick,600);} else setTimeout(finish,500); };
      setTimeout(tick,350);
      return;
    }
    /* a quick Case link for a Bodyshop names no firm, so it says nothing of where the report goes */
    const who = SRC&&SRC.kind!=='client' ? (SRC.name ? `The report comes back to ${esc(SRC.name)}` : '') : 'Your report goes to whoever is handling your claim';
    /* started from a standing or website link by whoever holds the phone, where opening that link again starts a new capture (R809) */
    const startedHere = CTX.origin==='staff' || CTX.origin==='website';
    main.innerHTML = `<div class="done-wrap left">
      <span class="tick">${ICON.check}</span>
      <h1>Thanks, that’s everything</h1>
      <p class="lede">${regShown()?'The photos of '+esc(regShown()):'Your photos'} are with our engineering team.</p>
      <p class="m6-10"><strong>What happens next</strong></p>
      <ol class="nextsteps"><li><b>An engineer reviews the photos</b><span>We check everything you’ve sent.</span></li><li><b>We’ll be in touch if we need anything else</b><span>Usually by text or email.</span></li>${who?`<li><b>${who}</b></li>`:''}</ol>
      <div class="later">${ICON.camera}<span><b>Forgot something?</b> ${startedHere?'Open the link again and enter the same registration to send more photos for this vehicle.':'Open the same link again to send more photos for this vehicle.'}</span></div>
      <p class="hint mt14">${nPhotos(photosSent())} · ${new Date().toLocaleString('en-GB',{dateStyle:'medium',timeStyle:'short'})}. You can close this page now.</p>
    </div>`;
    cta.innerHTML = '';
  }
};

function singleShot(key, title, lede, example, tip){
  const p=S[key], prior=rc(KINDAPI[key]);
  main.innerHTML = `<h2>${p&&CHECKQ[key]?CHECKQ[key][0]:title}</h2><p class="lede">${p&&CHECKQ[key]?CHECKQ[key][1]:lede}</p>
    ${liveNote()}
    <div class="big">${p?`<img class="preview" src="${p.url}" alt="">`:example}</div>
    ${p?`<div id="sharp" class="okline checking">Checking the photo…</div>`:`${countLine(prior)}<p class="hint">${tip}</p>`}
    ${uplogHTML(p?[p]:[])}`;
  if(key==='dash' && !p){
    const box=document.createElement('div'); box.className='subbox';
    box.innerHTML=`<button class="linkbtn lb15" id="nokeys">Don’t have the keys?</button>
      <div id="nokeysbox" class="mt12 ${S.noKeys?'':'hidden'}">
        <p class="hint mb10">No problem – skip this step. If you know roughly what the mileage is, pop it in here.</p>
        <div class="field mb12"><label for="estmil">Estimated mileage <span class="hint optl">(optional)</span></label><div class="unit"><input id="estmil" type="number" inputmode="numeric" placeholder="e.g. 105000" value="${S.mileageEst?esc(S.mileage):''}"><em>miles</em></div></div>
        <button class="btn outline btn-sm" id="skipdash">Skip this step</button>
      </div>`;
    main.appendChild(box);
    box.querySelector('#nokeys').onclick=()=>{ S.noKeys=!S.noKeys; box.querySelector('#nokeysbox').classList.toggle('hidden'); save(); };
    box.querySelector('#estmil').oninput=e=>{ S.mileage=e.target.value; S.mileageEst=!!e.target.value; save(); };
    box.querySelector('#skipdash').onclick=()=>{ S.noKeys=true; save(); let j=S.step+1; while(STEPS[j]==='handoff') j++; go(j); };
  }
  if(key==='dash' && p){ S.noKeys=false; S.mileageEst=false; }
  if(key==='plate'){
    const v=S.vin, bs=SRC&&SRC.kind==='bodyshop';
    const box=document.createElement('div'); box.className='subbox';
    box.innerHTML=`<p class="m0-4"><strong>${bs?'VIN plate':'Also helpful: the VIN'}</strong> <span class="hint">${bs?'':'(optional)'}</span></p>
      <p class="hint mb10">${bs?'A clear shot of the VIN plate or chassis number if it’s easy to get to.':'The 17-character chassis number. Usually in a small plate at the bottom of the windscreen on the passenger’s side, or on a sticker that can be seen when you open the front door. Skip it if you can’t find it.'}</p>
      ${v?`<div class="guide done m0"><div class="txt f1"><b>VIN captured</b><span>${fmt(v.bytes)}</span><div class="mt8"><button class="linkbtn" id="vinretake">${ICON.rotate} Retake</button></div></div><div class="shot"><img src="${v.url}" alt=""><span class="ok">${ICON.check}</span></div></div>`:`${countLine(rc('vin'))}<button class="btn outline btn-sm" id="vintake">${ICON.camera}Take VIN photo</button>`}`;
    main.appendChild(box);
    const b=box.querySelector('#vintake')||box.querySelector('#vinretake'); b.onclick=()=>frameCam('vin');
  }
  /* a photo sent on an earlier visit to this link counts: the step can be passed without retaking it */
  cta.innerHTML = p
    ? `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn outline" id="retake2">${ICON.rotate}Retake</button><button class="btn primary" id="next">${CHECKQ[key]?'Yes, it’s readable':'Continue'}</button>`
    : prior ? `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn outline" id="take">${ICON.camera}Take photo</button><button class="btn primary" id="next">Continue</button>`
    : `<button class="btn ghost" id="back" aria-label="Back">${ICON.back}</button><button class="btn primary" id="take">${ICON.camera}Take photo</button>`;
  if(!p) $('#take').onclick=()=>frameCam(key);
  if(p){ $('#retake2').onclick=()=>frameCam(key);
    const sh=$('#sharp');
    sharpScore(p.url).then(v=>{ if(!sh||!sh.isConnected) return;
      if(v<35){ sh.className='warnline'; sh.innerHTML=`${ICON.shield}<span><b>This one looks blurry.</b> Hold the phone still for a second after you tap, then retake.</span>`;
        const r=$('#retake2'), n=$('#next'); if(r&&n&&!returnTo){ r.className='btn primary'; n.className='btn outline'; n.textContent='Use anyway'; } }
      else { sh.className='okline'; sh.innerHTML=`${ICON.check}<span>Looks sharp</span>`; } }); }
}

function renderChips(){ const c=$('#chips'); if(!c) return; c.innerHTML = S.zones.length ? S.zones.map(id=>`<span>${ZL(id)}</span>`).join('') : '<span class="none">Nothing selected yet</span>'; }
/* the server's rule for a registration: 2 to 10 letters or digits */
function regOK(){ return /^[A-Z0-9]{2,10}$/.test(S.ins.reg||''); }
/* once anything has been received, or this visit has sent something, the capture's registration is fixed */
function regLocked(){ return LINK.indexOf('c.')===0 && ((CTX.counts.total||0)>0 || JOBS.some(j=>!j.original && j.state!=='cancelled')); }
function handoffOK(){ const I=S.ins; if(!regOK()) return false; if(SRC&&SRC.kind==='bodyshop') return !!I.handler.trim(); return true; }
function insOK(){ const I=S.ins; if(SRC&&SRC.kind==='client') return regOK() && I.customer.trim() && I.phone.replace(/\D/g,'').length>=10 && I.accident; if(SRC&&SRC.kind==='bodyshop') return regOK() && I.locType && I.handler.trim() && I.view; return regOK() && I.locType && I.roadworthy && I.outcome && I.hire; }
/* the server's spaced form ("RY65 OFG") once it holds the registration being shown */
function regShown(){ const typed=SRC ? (S.ins.reg||'').toUpperCase().trim() : CASE.reg; return CTX.registrationDisplay && typed===CTX.registration ? String(CTX.registrationDisplay) : typed; }

function updateCTA(){
  const n=$('#next'); if(!n) return;
  const k=STEPS[S.step];
  if(k==='location') n.disabled=!S.zones.length;
  if(k==='instruction'){ n.disabled=!insOK(); const h=$('#handoff'); if(h) h.disabled=!handoffOK(); }
}

function render(){
  if(GONE || !STEPS.length) return;
  UPL=[];
  const k=STEPS[S.step];
  const si=SEQ.indexOf(k), sn=SEQ.length-2;
  $('#stepLabel').textContent = k==='welcome' ? regShown() : k==='done' ? 'Complete' : k==='handoff' ? TITLES[k] : `Step ${si} of ${sn} · ${TITLES[k]}`;
  const pg=$('#prog'); if(pg) pg.style.width = (k==='done'?100:k==='handoff'?(2/sn)*100:(si/sn)*100)+'%';
  R[k]();
  if(k==='damage' && !returnTo){ const nb=$('#next'); if(nb && (dmgTotal()<8 || weakAreas().length)) nb.onclick=()=>nudge(dmgTotal()); }
  if(returnTo){ const nb=$('#next'); if(nb){ nb.textContent='Back to review'; nb.className='btn primary'; } const ab=$('#add2'); if(ab) ab.className='btn outline'; }
  main.querySelectorAll('[data-del]').forEach(b=>b.onclick=()=>{ const [kind,i]=b.dataset.del.split(':'); const p=S[kind][+i]; if(canDel(p)){ S[kind].splice(+i,1); cancelUpload(p); } render(); });
  const next=$('#next'), back=$('#back');
  const skip=(i,dir)=>{ let j=i; while(STEPS[j]==='handoff') j+=dir; return j; };
  if(next && !next.onclick) next.onclick=()=>{ if(returnTo){ const r=returnTo; returnTo=null; go(STEPS.indexOf(r)); } else go(skip(S.step+1,1)); };
  if(back && !back.onclick) back.onclick=()=>{ if(returnTo){ const r=returnTo; returnTo=null; go(STEPS.indexOf(r)); } else go(skip(S.step-1,-1)); };
  if(lastStep!==k){ main.scrollTop=0; window.scrollTo(0,0); lastStep=k; }
}
function go(i){ S.step=Math.max(0,Math.min(STEPS.length-1,i)); save(); syncDetails(); render(); }

/* ---------- boot ---------- */
function startWith(ctx){
  setContext(ctx);
  SRC=sourceFrom(CTX);
  CASE={ reg:String(CTX.registration||'') };
  STEPS = SRC ? ['welcome','instruction','handoff','location','corners','damage','plate','dash','extras','summary','done'] : ['welcome','location','corners','damage','plate','dash','extras','summary','done'];
  SEQ = STEPS.filter(s=>s!=='handoff');
  TITLES = {welcome:'Start',instruction:SRC&&SRC.kind==='client'?'Your details':SRC&&SRC.kind==='bodyshop'?'The job':'The claim',handoff:'Send to customer',location:'Damage location',damage:'Damage photos',corners:'Overview shots',plate:'Registration plate',dash:'Dashboard',video:'Walk-around video',extras:'Extras',scene:'Accident scene',questions:'Notes',summary:'Review',done:'Submitted'};
  KEY='ce-capture-'+(CTX.linkKey||'');
  S=load()||BLANK();
  applyServer(CTX);
  const nm=STEPS[S.step];
  if(nm==='done'||nm==='handoff') S.step=0;
  if(isStanding() && S.step>STEPS.indexOf('instruction')) S.step=Math.max(0,STEPS.indexOf('instruction'));
  detailsSig=JSON.stringify(detailsBody());
  render();
}
function boot(){
  main=$('#main'); cta=$('#cta'); fileMulti=$('#fileMulti'); fileSingle=$('#fileSingle'); fileDocs=$('#fileDocs'); fileVideo=$('#fileVideo');
  const logo=document.querySelector('header img'); if(logo && !logo.getAttribute('src') && assetUrl('logo')) logo.src=assetUrl('logo');
  fileMulti.addEventListener('change', onMulti); fileSingle.addEventListener('change', onSingle); fileDocs.addEventListener('change', onDocs); fileVideo.addEventListener('change', onVideo);
  if(!LINK){ gone(); return; }
  const inline=readJson('capture-context');
  if(inline) startWith(inline);
  /* GET context marks the link opened and refreshes the counts already received */
  api('GET','context').then(r=>{
    if(GONE) return;
    if(r.status===200 && r.data){ if(!STEPS.length) startWith(r.data); else { setContext(r.data); applyServer(r.data); if(STEPS[S.step]!=='done') render(); } }
    else if(!STEPS.length) main.textContent=COPY.offline;
  }).catch(()=>{ if(!STEPS.length) main.textContent=COPY.offline; });
}
if(document.readyState==='loading') document.addEventListener('DOMContentLoaded', boot); else boot();
})();
