/* Offline interaction model. No network, persistence or application mutation. */
(() => {
 'use strict';
 const cfg=window.uploadDesign, root=document.querySelector('#upload-design-root');
 const qs=new URLSearchParams(location.search), allowed=cfg.states.map(s=>s[0]);
 let state=allowed.includes(qs.get('state'))?qs.get('state'):'ready';
 let selected=null, inspected=0, query='', searched=false, searchFailed=state==='search-error', searchOpen=['no-match','search-error'].includes(state);
 let custom=false, chosen=cfg.files.map(f=>({...f})), validation=[], timer=null, modalOpener=null, previewIndex=0;
 let lastAttached=cfg.cases[0], flowFiles=null;
 const modal=document.querySelector('#ud-dialog');
 const esc=v=>String(v).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
 const icon=id=>`<svg class="icon" aria-hidden="true"><use href="#icon-${id}"/></svg>`;
 const button=(text,action,cls='',extra='')=>`<button type="button" class="btn ${cls}" data-action="${action}" ${extra}>${text}</button>`;
 const initial=['select','chosen','upload-error'];
 const pending=['uploading','processing'];
 const settled=['attached','discarded','duplicate'];
 function fileSet(){
   if(state==='select')return [];
   if(custom||state==='chosen'||pending.includes(state)||state==='upload-error')return chosen;
   if(flowFiles)return flowFiles;
   if(state==='single')return cfg.files.slice(0,1);
   if(state==='document')return [{id:'instruction',name:'Instruction — Morgan — BH17RZV.pdf',size:329252,kind:'PDF document',tile:null}];
   return cfg.files;
 }
 const mb=n=>(n/1048576).toFixed(2)+' MiB';
 const plural=n=>n===1?'file':'files';
 const total=fs=>mb(fs.reduce((n,f)=>n+f.size,0));
 const isUnread=(i)=>state==='unreadable'||(state==='mixed'&&i===3)||((state==='attached')&&qs.get('mixed')==='1'&&i===3);
 function photo(f){
   if(f.url)return `<img class="ud-local-photo" src="${esc(f.url)}" alt="">`;
   if(f.tile!=null)return `<span class="ud-photo ud-tile-${f.tile}"></span>`;
   return `<span class="ud-file-icon">${icon(f.kind==='PDF document'?'file-text':'file')}</span>`;
 }
 function status(i){
   if(state==='uploading')return ['Uploading','is-running','upload'];
   if(state==='processing')return [i<3?'Processing':'Received',i<3?'is-running':'',''];
   if(state==='upload-error')return ['Not uploaded','is-error','alert-circle'];
   if(state==='attached')return [isUnread(i)?'Added · unreadable':'Added to Case','is-done','check'];
   if(state==='discarded')return ['Discarded','',''];
   if(state==='duplicate')return ['Already received','','check'];
   if(isUnread(i))return ['Could not be read','is-error','alert-circle'];
   if(state==='chosen')return ['Selected','',''];
   if(state==='incomplete'&&i===10)return ['Unavailable','is-error','alert-circle'];
   return ['Ready','','check'];
 }
 function rows(gallery=false){
   return `<ul class="${gallery?'ud-gallery':'ud-file-list'}">${fileSet().map((f,i)=>{
     const [label,cl,glyph]=status(i);
     return `<li class="ud-file ${isUnread(i)?'ud-file-error':''}" data-file="${esc(f.id)}"><button class="ud-thumb-button" data-preview="${i}" aria-label="Preview ${esc(f.name)}">${photo(f)}</button><div class="ud-file-name"><button data-preview="${i}" title="${esc(f.name)}">${esc(f.name)}</button><small>${mb(f.size)}${cfg.id==='d'?'':' · '+esc(f.kind)}</small></div>${state==='chosen'?`<button class="ud-remove" data-remove="${esc(f.id)}" aria-label="Remove ${esc(f.name)}">${icon('x')}</button>`:`<span class="ud-file-state ${cl}">${glyph?icon(glyph):''}${label}</span>`}</li>`;
   }).join('')}</ul>`;
 }
 function filePanel(){
   const fs=fileSet();
   return `<section class="ud-evidence" aria-labelledby="ud-files-title"><header class="ud-section-head"><h2 id="ud-files-title">${initial.includes(state)?'Selected files':'Files in this upload'}</h2><span>${fs.length} ${plural(fs.length)} · ${total(fs)}</span></header>${cfg.id==='d'?'<div class="ud-table-head" aria-hidden="true"><span></span><span>File name</span><span>Size</span><span>Outcome</span></div>':''}${rows(cfg.id==='c')}</section>`;
 }
 function guidedFiles(){
   const fs=fileSet();return `<details class="ud-review-files" ${state==='mixed'||state==='unreadable'?'open':''}><summary><strong>${fs.length} ${plural(fs.length)} received</strong><span class="ud-badge">${total(fs)}</span><span class="ud-mini-strip" aria-hidden="true">${fs.slice(0,5).map(f=>`<span>${photo(f)}</span>`).join('')}</span>${icon('chevron-right')}</summary>${rows()}</details>`;
 }
 function inspector(){
   const fs=fileSet();inspected=Math.min(inspected,fs.length-1);const f=fs[inspected];
   return `<section class="ud-inspector" aria-label="File inspection"><header class="ud-inspect-top"><strong>Files in this upload</strong><span>${inspected+1} of ${fs.length}</span></header><div class="ud-inspect-media"><div class="ud-inspect-photo">${photo(f)}</div></div><div class="ud-inspect-caption"><span title="${esc(f.name)}">${esc(f.name)}</span><button class="btn" data-preview="${inspected}">${icon('zoom-in')}<span>Open</span></button></div><div class="ud-filmstrip" aria-label="Choose a file to inspect">${fs.map((f,i)=>`<button class="ud-film-button" data-inspect="${i}" aria-pressed="${inspected===i}" aria-label="Inspect file ${i+1}: ${esc(f.name)}">${photo(f)}<small>${i+1}${isUnread(i)?' · Unreadable':''}</small></button>`).join('')}</div><details ${state==='mixed'||state==='unreadable'?'open':''}><summary>File names and outcomes · ${fs.length} ${plural(fs.length)}</summary>${rows()}</details></section>`;
 }
 function card(c){return `<button type="button" class="ud-case" data-case="${c.ref}" aria-pressed="${selected?.ref===c.ref}"><span class="ud-radio" aria-hidden="true"></span><span class="ud-case-top"><strong class="ud-case-ref">${c.ref}</strong><span class="ud-stage ud-stage--${c.tone}">${c.stage}</span></span><span class="ud-case-person"><span>${c.reg}</span><span>${c.claimant}</span></span><span class="ud-case-detail">${c.principal}</span><span class="ud-case-po">${c.po}</span></button>`;}
 function alert(title,body,tone=''){return `<div class="ud-alert ${tone?'ud-alert--'+tone:''}" role="${tone==='error'?'alert':'status'}" tabindex="-1">${icon(tone==='success'?'check-circle':'alert-circle')}<div><strong>${title}</strong>${body?'<p>'+body+'</p>':''}</div></div>`;}
 function record(){return state==='document'?'':`<div class="ud-record">${icon('images')}<div class="ud-record-copy"><small>Image intake</small><button class="ud-text-link" data-boundary="Image intake BH17RZV-01">BH17RZV-01 ${icon('arrow-right')}</button><p>Registered automatically · Awaiting instruction</p></div></div>`;}
 function leave(){return `<div class="ud-leave">${button('Leave undecided','leave')}<button class="ud-discard" data-action="discard">Discard upload</button></div>`;}
 function search(){return `<details class="ud-search" ${searchOpen?'open':''}><summary>${state==='no-match'?'Find a Case':'Find another Case'}${icon('chevron-right')}</summary><form class="ud-search-form" id="ud-search-form"><label class="sr-only" for="ud-case-query">Search by Case/PO, registration or claimant</label><input id="ud-case-query" type="search" value="${esc(query)}" placeholder="Case/PO, registration or claimant" autocomplete="off" maxlength="80"><button class="btn" type="submit">${icon('search')}<span>Search</span></button></form><div class="ud-search-results" aria-live="polite">${searchResults()}</div></details>`;}
 function searchResults(){
   if(searchFailed)return `<div class="ud-search-note" role="alert"><strong>Case search is unavailable.</strong><br>Your upload is retained. Try again in a moment.<br>${button('Retry search','retry-search')}</div>`;
   if(!searched)return '';
   if(!query.trim())return '<p class="ud-search-note">Enter a Case/PO, registration or claimant.</p>';
   const result=cfg.cases.filter(c=>Object.values(c).join(' ').toLowerCase().includes(query.toLowerCase().trim()));
   return result.length?result.map(card).join(''):`<p class="ud-search-note">No Cases or Triage items match “${esc(query)}”. Try another reference.</p>`;
 }
 function decision(){
   const fs=fileSet();
   if(pending.includes(state))return `<section class="ud-decision"><div class="ud-decision-head"><p class="ud-eyebrow">${state==='uploading'?'Transfer in progress':'Files received'}</p><h2>${state==='uploading'?'Uploading '+fs.length+' '+plural(fs.length):'Processing your files'}</h2><div class="ud-progress" role="progressbar" aria-label="${state==='uploading'?'Uploading files':'Processing files'}"></div><p class="ud-processing-copy">${state==='uploading'?'Keep this page open until the upload finishes.':'All '+fs.length+' '+plural(fs.length)+' are stored. The outcome will appear here when processing finishes.'}</p>${state==='processing'?'<div class="ud-action-slot">'+button('Refresh status','refresh')+'</div>':''}</div></section>`;
   if(settled.includes(state)){
     const discarded=state==='discarded', duplicate=state==='duplicate';
     return `<section class="ud-decision"><div class="ud-decision-head"><div class="ud-result-mark ${discarded?'is-neutral':''}">${icon(discarded?'archive':'check')}</div><p class="ud-eyebrow">${discarded?'Upload closed':duplicate?'Existing destination':'Upload complete'}</p><h2>${discarded?'Upload discarded':duplicate?'These files were already received':'Added to Case'}</h2><p class="ud-explainer">${discarded?'The source files and processing record are retained.':duplicate?'The existing association is shown below.':fs.length+' '+plural(fs.length)+' added to the confirmed destination.'}</p>${discarded?'':`<div class="ud-final-destination"><strong>${lastAttached.ref}</strong><p>${lastAttached.reg} · ${lastAttached.claimant}</p><p>${lastAttached.principal} · ${lastAttached.po}</p><span class="ud-stage ud-stage--${lastAttached.tone}">${lastAttached.stage}</span></div>`}${!discarded?button('Open '+lastAttached.ref,'open-case','btn--primary'):button('Start another upload','new','btn--primary')}${duplicate?'<p class="ud-explainer">An incorrect association can be corrected from the Case.</p>':''}</div></section>`;
   }
   if(state==='unreadable'||state==='incomplete')return `<section class="ud-decision"><div class="ud-decision-head"><p class="ud-eyebrow">Review required</p><h2>${state==='unreadable'?'The files could not be read':'This upload is incomplete'}</h2><p class="ud-explainer">${state==='unreadable'?'The originals are retained in Unidentified. Open a file to inspect it, or review the item in Unidentified.':'One file is unavailable. Refresh the status before choosing a Case; nothing has been added.'}</p><div class="ud-action-slot">${state==='unreadable'?'<button class="btn btn--primary" data-boundary="Unidentified item — Could not be read">Open Unidentified item</button>':button('Refresh status','refresh','btn--primary')}</div>${state==='unreadable'?leave():''}</div></section>`;
   if(state==='conflict')return `<section class="ud-decision"><div class="ud-decision-head">${alert('The Case changed. Nothing was added.','Your upload is retained. Refresh the details before choosing a Case again.','error')}<div class="ud-action-slot">${button('Refresh Case details','refresh','btn--primary')}</div></div></section>`;
   const candidates=state==='no-match'?[]:state==='multiple'?cfg.cases.slice(0,2):cfg.cases.slice(0,1);
   return `<section class="ud-decision" aria-labelledby="ud-decision-title"><div class="ud-decision-head"><p class="ud-eyebrow">${state==='no-match'?'No suggested match':'Choose a destination'}</p><h2 id="ud-decision-title">${state==='no-match'?'Find the right Case':'Which Case do these files belong to?'}</h2><p class="ud-explainer">${state==='no-match'?'Search all Cases and Triage items. The upload is retained while you decide.':candidates.length===1?'One possible Case for BH17RZV. Check the details before adding the files.':'Two possible Cases for BH17RZV. Choose the correct claimant and Case.'}</p></div><div class="ud-choice">${state==='conflict'?alert('The Case changed. Nothing was added.','Refresh the details, then review your choice again.','error')+button('Refresh Case details','refresh'):''}${state==='mixed'?alert('1 file could not be read','All 11 originals are retained. Check the marked file before adding this upload.'):''}<div class="ud-candidates">${candidates.map(card).join('')}</div><div class="ud-action-slot">${selected?button('Review and add to Case '+icon('arrow-right'),'review','btn--primary'):'<p class="ud-choice-hint">'+(candidates.length?'Select a Case to continue.':'')+'</p>'}</div>${search()}${state==='document'?'<div class="ud-search"><button class="ud-text-link" data-boundary="Editable new Case proposal — extracted instruction details">Review new Case proposal '+icon('arrow-right')+'</button><p class="ud-explainer">Use the extracted instruction details to create a new Case.</p></div>':''}</div><div class="ud-side-actions">${record()}${leave()}</div></section>`;
 }
 function selectPane(){
   const fs=fileSet();
   return `<section class="ud-upload-select"><div class="ud-selection-title"><p class="ud-eyebrow">Add files to Pegasus</p><h2>${fs.length?'Ready to upload':'Choose the files for this upload'}</h2></div>${validation.length?alert('Check the selected files',validation.map(esc).join('<br>'),'error'):''}${state==='upload-error'?alert('The upload did not complete','No files were confirmed as stored. Your selection is retained; try again.','error'):''}<div class="ud-drop" data-drop><svg class="icon" aria-hidden="true"><use href="#icon-upload"/></svg><h2>${fs.length?'Add more files':'Drop files here'}</h2><p>Images, documents, emails and video<br>JPG, JPEG, PNG, PDF, DOC, DOCX, EML, MSG, MP4 or MOV</p><input class="ud-file-input" id="ud-file-input" type="file" multiple accept=".jpg,.jpeg,.png,.pdf,.doc,.docx,.eml,.msg,.mp4,.mov"><label class="btn ${fs.length?'':'btn--primary'}" for="ud-file-input">${icon('plus')}<span>Choose files</span></label></div><div class="ud-limits"><span>20 files maximum</span><span>100 MiB per file</span><span>200 MiB per upload</span></div>${fs.length?`<div class="ud-select-actions"><p>${fs.length} ${plural(fs.length)} · ${total(fs)}</p><div class="button-row">${button('Clear','clear')}${button((state==='upload-error'?'Try again':'Upload '+fs.length+' '+plural(fs.length))+' '+icon('arrow-right'),'upload','btn--primary')}</div></div>`:''}</section>`;
 }
 function emptyAside(){return `<aside class="ud-empty-aside"><h3>One upload. One decision.</h3><p>Add related files together, then choose the Case they belong to.</p><ol><li><div><strong>Choose files</strong><small>Check your selection before uploading.</small></div></li><li><div><strong>Review the result</strong><small>See the outcome for every file.</small></div></li><li><div><strong>Confirm the Case</strong><small>Check the exact destination before adding.</small></div></li></ol></aside>`;}
 function steps(){const n=initial.includes(state)?0:settled.includes(state)?2:1;return `<ol class="ud-steps" aria-label="Upload progress">${['Choose files','Review','Complete'].map((s,i)=>`<li class="${i===n?'is-current':i<n?'is-complete':''}" ${i===n?'aria-current="step"':''}><b>${i<n?icon('check'):i+1}</b>${s}</li>`).join('')}</ol>`;}
 function render(focus){
   const fs=fileSet(), choosing=initial.includes(state), haveFiles=fs.length>0;
   document.title=cfg.id.toUpperCase()+' · '+cfg.name+' · Pegasus Upload';
   let inner;
   if(choosing)inner=selectPane()+(haveFiles?filePanel():emptyAside());
   else if(cfg.id==='b')inner=guidedFiles()+decision();
   else inner=decision()+(cfg.id==='e'?inspector():filePanel());
   root.innerHTML=`<header class="page-header"><div class="page-title"><h1>Upload</h1><p class="ud-subtitle">${choosing?'Bring related files into Pegasus.':'Review your upload and its destination.'}</p></div><div class="page-actions">${!choosing?'<div class="ud-meta"><span>Received today, 09:41</span></div>':''}${!choosing&&!pending.includes(state)?button(icon('plus')+' New upload','new'):''}</div></header><div class="ud-surface">${cfg.id==='b'?steps():''}<div class="ud-workspace ${choosing?'is-choosing':''}">${inner}</div></div>`;
   document.querySelector('[data-state-picker]').value=state;
   if(focus)root.querySelector(focus)?.focus();
 }
 function setState(next,{keep=false}={}){
   clearTimeout(timer);state=next;selected=null;validation=[];inspected=0;
   if(!keep){query='';searched=false;searchFailed=next==='search-error';searchOpen=['no-match','search-error'].includes(next);}
   if(next==='select'){for(const f of chosen)if(f.url)URL.revokeObjectURL(f.url);chosen=[];custom=false;flowFiles=null;}
   else if(next==='chosen'&&!chosen.length){chosen=cfg.files.map(f=>({...f}));custom=false;}
   qs.set('state',state);history.replaceState(null,'','?'+qs.toString());render();
   document.querySelector('h1')?.setAttribute('tabindex','-1');
   document.querySelector('h1')?.focus({preventScroll:true});
   document.querySelector('#ud-live-status').textContent=document.querySelector('.ud-decision h2,.ud-selection-title h2')?.textContent||'Upload updated';
 }
 function openDialog(title,body,footer,cls='',opener=document.activeElement){
   if(modal.open)modal.close();
   modalOpener=opener;
   modal.className='ud-modal '+cls;
   modal.innerHTML=`<header class="ud-modal-head"><h2 id="ud-dialog-title">${title}</h2><button class="ud-modal-close" data-close aria-label="Close dialog">${icon('x')}</button></header><div class="ud-modal-body">${body}</div><footer class="ud-modal-foot">${footer}</footer>`;
   modal.showModal();(modal.querySelector('[data-initial-focus]')||modal.querySelector('[data-close]')).focus();
 }
 function closeDialog(){modal.close();if(modalOpener?.isConnected)modalOpener.focus();}
 function review(){
   if(!selected)return;const fs=fileSet(),c=selected;
   openDialog('Add '+fs.length+' '+plural(fs.length)+' to this Case?',`<p>Confirm the exact destination for this upload.</p><div class="ud-confirm-case"><strong>${c.ref}</strong><dl><dt>Case/PO</dt><dd>${c.po}</dd><dt>Registration</dt><dd>${c.reg}</dd><dt>Claimant</dt><dd>${c.claimant}</dd><dt>Principal</dt><dd>${c.principal}</dd><dt>Stage</dt><dd>${c.stage}</dd></dl></div><p>${fs.length} original ${plural(fs.length)} · ${total(fs)}${state==='mixed'?'<br>1 file could not be read; its original will be included.':''}</p>`,button('Cancel','close','','data-initial-focus')+button('Confirm and add to '+c.ref,'confirm','btn--primary'));
 }
 function discard(){openDialog('Discard this upload?',`<p>Discard all ${fileSet().length} ${plural(fileSet().length)} in this upload. The source files and processing record will be retained.</p><label class="ud-confirm-check"><input id="ud-discard-check" type="checkbox" data-initial-focus><span>I understand that every file in this upload will be discarded.</span></label><p id="ud-discard-error" role="alert" hidden>Select the confirmation before discarding.</p>`,button('Cancel','close')+button('Discard upload','confirm-discard','btn--danger'));}
 function boundary(label){openDialog(esc(label),'<p>This opens the existing Pegasus screen for '+esc(label)+'.</p><p class="ud-boundary-note">Offline destination preview. This linked screen is outside the Upload design; no records have been changed.</p>',button('Return to upload','close','','data-initial-focus'));}
 function preview(i){
   previewIndex=i;const fs=fileSet(),f=fs[i],previousOpener=modal.open?modalOpener:document.activeElement;
   openDialog('File '+(i+1)+' of '+fs.length,`<div class="ud-preview-image">${photo(f)}</div><p class="ud-preview-name">${esc(f.name)}</p><p>${mb(f.size)} · ${esc(f.kind)}${isUnread(i)?' · Could not be read':''}</p>${f.tile==null&&!f.url?'<p>Preview unavailable. The original file remains available from its record.</p>':''}`,`<div class="ud-preview-foot">${button(icon('chevron-left')+' Previous','previous','','data-initial-focus')}${button('Open original','original')}${button('Next '+icon('chevron-right'),'next')}</div>`,'ud-preview-modal',previousOpener);
 }
 function announce(message){const toast=document.querySelector('#ud-toast');toast.textContent=message;toast.hidden=false;setTimeout(()=>toast.hidden=true,4300);}
 function selectFiles(list){
   const additions=Array.from(list);if(!additions.length)return;
   const base=custom?chosen:[];const all=[...base,...additions.map((f,i)=>({id:'local-'+Date.now()+'-'+i,name:f.name,size:f.size,kind:f.type.startsWith('image/')?'Image':f.type==='application/pdf'?'PDF document':'File',tile:null,source:f}))];
   validation=[];
   if(all.length>20)validation.push('Choose no more than 20 files.');
   if(all.reduce((n,f)=>n+f.size,0)>209715200)validation.push('The upload must be 200 MiB or less.');
   for(const f of all){if(!/\.(jpe?g|png|pdf|docx?|eml|msg|mp4|mov)$/i.test(f.name))validation.push(f.name+': this file type is not supported.');if(f.size>104857600)validation.push(f.name+': this file exceeds 100 MiB.');if(f.size===0)validation.push(f.name+': this file is empty.');}
   if(validation.length){render('.ud-alert');return;}
   for(const f of base)if(f.url)URL.revokeObjectURL(f.url);
   chosen=all.map(f=>({...f,url:f.source.type.startsWith('image/')?URL.createObjectURL(f.source):null}));custom=true;state='chosen';render();announce(chosen.length+' files selected locally.');
 }
 document.addEventListener('click',event=>{
   const t=event.target.closest('button,a');if(!t)return;
   if(t.hasAttribute('data-case')){selected=cfg.cases.find(c=>c.ref===t.dataset.case);render(`[data-case="${selected.ref}"]`);return;}
   if(t.hasAttribute('data-preview')){preview(Number(t.dataset.preview));return;}
   if(t.hasAttribute('data-inspect')){inspected=Number(t.dataset.inspect);render(`[data-inspect="${inspected}"]`);return;}
   if(t.hasAttribute('data-remove')){const f=chosen.find(f=>f.id===t.dataset.remove);if(f?.url)URL.revokeObjectURL(f.url);chosen=chosen.filter(f=>f.id!==t.dataset.remove);if(!chosen.length)state='select';render('#ud-file-input');return;}
   if(t.hasAttribute('data-boundary')){boundary(t.dataset.boundary);return;}
   if(t.hasAttribute('data-close')){closeDialog();return;}
   switch(t.dataset.action){
    case 'new':case 'clear':setState('select');break;
    case 'sample':flowFiles=null;chosen=cfg.files.map(f=>({...f}));custom=false;setState('chosen');break;
    case 'upload':if(custom){openDialog('Upload unavailable in this preview','<p>Your selected files stay on this device. This offline design can preview them, but cannot upload or process them.</p><p>Use Load sample files in Mockup controls to try the simulated upload journey.</p>',button('Return to selection','close','','data-initial-focus'));break;}flowFiles=chosen.map(f=>({...f}));setState('uploading',{keep:true});timer=setTimeout(()=>{setState('processing',{keep:true});timer=setTimeout(()=>setState('ready'),1200);},1000);break;
    case 'refresh':setState('ready');announce('Upload details refreshed. Review the current Case details.');break;
    case 'review':review();break;
    case 'close':closeDialog();break;
    case 'confirm':{
      if(document.querySelector('[data-conflict]').checked){closeDialog();setState('conflict');root.querySelector('.ud-alert')?.focus();break;}
      lastAttached=selected;if(state==='mixed')qs.set('mixed','1');else qs.delete('mixed');const count=fileSet().length;
      closeDialog();chosen=fileSet();custom=true;setState('attached');announce(count+' files added to '+lastAttached.ref+' in this preview.');break;
    }
    case 'leave':setState('select');announce('Upload retained. No Case was chosen.');break;
    case 'discard':discard();break;
    case 'confirm-discard':if(!document.querySelector('#ud-discard-check').checked){document.querySelector('#ud-discard-error').hidden=false;document.querySelector('#ud-discard-check').focus();}else{chosen=fileSet();custom=true;closeDialog();setState('discarded');}break;
    case 'retry-search':searchFailed=false;searched=true;searchOpen=true;render('#ud-case-query');break;
    case 'open-case':boundary('Case '+lastAttached.ref);break;
    case 'previous':preview((previewIndex+fileSet().length-1)%fileSet().length);break;
    case 'next':preview((previewIndex+1)%fileSet().length);break;
    case 'original':{const f=fileSet()[previewIndex];boundary('Original file: '+f.name);break;}
    default:
     if(t.matches('[data-rail-toggle]')){const app=document.querySelector('[data-app-shell]');app.classList.toggle('rail-collapsed');const expanded=!app.classList.contains('rail-collapsed');t.setAttribute('aria-expanded',String(expanded));t.setAttribute('aria-label',expanded?'Collapse navigation':'Expand navigation');}
     else if(t.matches('[data-dialog-open]'))boundary(t.dataset.dialogOpen==='account-dialog'?'Account':'Notifications');
     else if(t.matches('a[href="#"]')){event.preventDefault();boundary(t.textContent.trim()||t.getAttribute('aria-label')||'Pegasus');}
   }
 });
 document.addEventListener('submit',event=>{
   event.preventDefault();if(event.target.id==='ud-search-form'){query=document.querySelector('#ud-case-query').value;searched=true;searchOpen=true;selected=null;render('#ud-case-query');}
   else if(event.target.matches('.utility-search'))boundary('Search Pegasus');
 });
 document.addEventListener('input',event=>{if(event.target.id==='ud-case-query')query=event.target.value;});
 document.addEventListener('toggle',event=>{if(event.target.matches('.ud-search'))searchOpen=event.target.open;},true);
 document.addEventListener('change',event=>{
   if(event.target.matches('[data-state-picker]')){flowFiles=null;custom=false;chosen=cfg.files.map(f=>({...f}));qs.delete('mixed');setState(event.target.value);if(state==='confirm'){state='ready';selected=cfg.cases[0];render();review();}if(state==='discard'){state='ready';render();discard();}}
   if(event.target.id==='ud-file-input')selectFiles(event.target.files);
 });
 document.addEventListener('dragover',event=>{if(event.target.closest('[data-drop]')){event.preventDefault();event.target.closest('[data-drop]').classList.add('is-over');}});
 document.addEventListener('dragleave',event=>event.target.closest('[data-drop]')?.classList.remove('is-over'));
 document.addEventListener('drop',event=>{if(event.target.closest('[data-drop]')){event.preventDefault();selectFiles(event.dataTransfer.files);}});
 modal.addEventListener('cancel',event=>{event.preventDefault();closeDialog();});
 modal.addEventListener('click',event=>{if(event.target===modal){const r=modal.getBoundingClientRect();if(event.clientX<r.left||event.clientX>r.right||event.clientY<r.top||event.clientY>r.bottom)closeDialog();}});
 document.addEventListener('keydown',event=>{
   if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='k'){event.preventDefault();document.querySelector('#global-search').focus();}
   if(!modal.open||event.key!=='Tab')return;
   const controls=[...modal.querySelectorAll('button:not([disabled]),a[href],input:not([disabled]),select,[tabindex="0"]')].filter(el=>el.getClientRects().length);
   const first=controls[0],last=controls.at(-1);if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}
 });
 if(qs.get('embed')==='1')document.querySelector('.ud-mock').hidden=true;
 const dialogPreset=state==='confirm'||state==='discard';const preset=state;if(dialogPreset)state='ready';
 render();if(preset==='confirm'){selected=cfg.cases[0];render();review();}else if(preset==='discard')discard();
 window.mockupReady=true;
 window.uploadMockup={get state(){return state;},get selected(){return selected?.ref||null;},get files(){return fileSet().map(f=>({id:f.id,name:f.name,size:f.size}));},setState(next){flowFiles=null;custom=false;chosen=cfg.files.map(f=>({...f}));setState(next);}};
})();
