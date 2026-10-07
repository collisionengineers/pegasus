/* v31 Triage Case proposals: one runtime, three layouts (data-design a|b|c).
   Markup mirrors the live partials (_TriageCase, _CaseRibbon's Actions menu,
   _CaseFiles tabs, _CaseCorrespondence, _ComposeForm, _ReasonDialog); every
   action is simulated against the in-page state. */
(() => {
  'use strict';
  const cfg = window.triageDesign;
  const fx = cfg.fixture;
  const params = new URLSearchParams(location.search);
  const opts = Object.fromEntries((params.get('opt') || '').split(',').filter(Boolean).map((p) => p.split(':')));
  const presetKey = cfg.presetState[params.get('state')] ? params.get('state') : 'open';
  if (params.get('embed') === '1') document.documentElement.classList.add('tv-embed');

  const esc = (v) => String(v ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
  const icon = (id) => `<svg class="icon" aria-hidden="true"><use href="#icon-${id}" /></svg>`;
  const tones = { open: 'navy', 'awaiting information': 'amber', 'finding recorded': 'navy', completed: 'green', cancelled: 'neutral' };
  const chip = (label) => `<span class="status status--${tones[label.toLowerCase()] || 'neutral'}">${esc(label)}</span>`;
  const fileSize = (n) => `${(n / 1_000_000).toFixed(1)} MB`;

  let s;
  function load(key) {
    const p = structuredClone(cfg.presetState[key]);
    s = {
      ...p,
      notice: null,
      imageIndex: 0,
      tab: p.images ? 'images' : 'files',
      filesTab: 'documents',
      images: cfg.images.slice(0, p.images).map((img) => ({ ...img, tags: img.tags.map((t) => [...t]) })),
      history: fx.history.map((h) => [...h]),
      primary: (opts.primary || 'on') === 'on',
    };
    if (p.chaserSent) s.history.unshift(['03/10/2026', '09:30', 'alex', 'Sent evidence recorded', 'Chaser sent.']);
    if (p.state === 'Awaiting information') s.history.unshift(['03/10/2026', '09:31', 'alex', 'Awaiting information', 'Requested further photographs.']);
    if (p.finding) s.history.unshift(['04/10/2026', '11:02', 'alex', 'Finding recorded', 'Damage limited to offside panels.']);
    if (p.state === 'Completed') s.history.unshift(['04/10/2026', '11:05', 'alex', 'Completed', 'Complete.']);
    if (p.state === 'Cancelled') s.history.unshift(['03/10/2026', '10:20', 'R. Khan', 'Cancelled', 'Duplicate request.']);
    if (p.linkedResponse) s.history.unshift(['04/10/2026', '08:40', 'alex', 'Response evidence linked', 'Requester replied with photographs.']);
  }
  load(presetKey);

  const derived = () => {
    const mutable = !['Completed', 'Cancelled'].includes(s.state);
    const purpose = s.state === 'Cancelled' ? null : s.state === 'Completed' ? 'outcome' : 'chaser';
    const canSend = s.email && purpose && !s.blocked;
    return { mutable, purpose, canSend, correction: s.state === 'Completed' && !!s.finding };
  };

  /* ---------- Actions menu (the ribbon) ---------- */
  function actions() {
    const d = derived();
    const items = [];
    if (d.mutable) items.push({ key: 'assign', label: s.assignee ? 'Reassign' : 'Assign', glyph: 'user', dialog: 'triage-assign-dialog' });
    if (d.mutable) items.push({ key: 'determinations', label: 'Record finding', glyph: 'clipboard-list', dialog: 'triage-determinations-dialog' });
    if (d.correction) items.push({ key: 'correction', label: 'Record correction', glyph: 'save', dialog: 'triage-correction-dialog' });
    if (s.state === 'Finding recorded') items.push({ key: 'complete', label: 'Complete Triage', glyph: 'check', post: 'complete' });
    if (d.canSend) items.push(d.purpose === 'outcome'
      ? { key: 'reply', label: 'Reply with finding', glyph: 'reply', composer: true }
      : { key: 'chaser', label: 'Send chaser', glyph: 'send', composer: true });
    if (d.mutable) items.push(s.linkedCase
      ? { key: 'unlink', label: 'Unlink case', glyph: 'unlink', dialog: 'triage-unlink-case-dialog' }
      : { key: 'link', label: 'Link case', glyph: 'link', dialog: 'triage-link-case-dialog' });
    const end = d.mutable
      ? { key: 'cancel', label: 'Cancel Triage', glyph: 'x', dialog: 'triage-cancel-dialog', danger: true }
      : { key: 'reopen', label: 'Reopen', glyph: 'undo', dialog: 'triage-reopen-dialog' };
    // The state's next step, offered beside the menu when the Primary variable is on.
    const leadKey = { Open: 'determinations', 'Awaiting information': 'determinations', 'Finding recorded': 'complete', Completed: 'reply', Cancelled: 'reopen' }[s.state];
    let lead = null;
    if (s.primary) {
      lead = [...items, end].find((i) => i.key === leadKey) || null;
    }
    return { items: items.filter((i) => i !== lead), end: end === lead ? null : end, lead };
  }

  const actionButton = (i, cls = 'btn') => {
    const attrs = i.dialog ? `data-dialog-open="${i.dialog}"` : i.composer ? 'data-composer-open' : `data-post="${i.post}"`;
    return `<button type="button" class="${cls}${i.danger ? ' btn--danger' : ''}" ${attrs} data-action-key="${i.key}">${icon(i.glyph)}<span>${esc(i.label)}</span></button>`;
  };

  function ribbon() {
    const { items, end, lead } = actions();
    const facts = [
      ['Triage reference', `<span class="mono">${esc(fx.reference)}</span>`, 'ribbon-ref'],
      ['Registration', `<span class="mono">${esc(fx.registration)}</span>`],
      ['Principal', esc(fx.principal)],
      ['Source', s.email ? fx.sourceEmail : fx.sourceUpload],
      ['Opened', fx.opened],
      ['Assignee', `<span data-ribbon-assignee>${esc(s.assignee ? (s.assignee === 'alex' ? 'alex' : s.assignee) : 'Unassigned')}</span>`],
      ['Case', s.linkedCase ? `<a href="#" data-boundary="Opens Case ${esc(s.linkedCase)}.">${esc(s.linkedCase)}</a>` : '<span>None</span>'],
    ];
    const menu = `<details class="menu" data-menu data-triage-actions><summary class="btn"><span>Actions</span>${icon('chevron-down')}</summary><div class="menu-body">${items.map((i) => actionButton(i)).join('')}${end ? `${items.length ? '<div class="menu-sep"></div>' : ''}${actionButton(end)}` : ''}</div></details>`;
    return `<div class="ribbon triage-ribbon" aria-label="Triage identity"><div class="ribbon-facts">${facts.map(([l, v, c]) => `<div class="ribbon-item${c ? ` ${c}` : ''}"><span class="ribbon-label">${l}</span><span class="ribbon-value">${v}</span></div>`).join('')}</div><div class="ribbon-chips">${chip(s.state)}</div><div class="ribbon-actions">${lead ? actionButton(lead, 'btn btn--primary') : ''}${menu}</div></div>`;
  }

  /* ---------- Sections ---------- */
  const caption = (img) => `<span class="gallery-caption"><strong>${esc(img.file)}</strong></span>`;

  function readout(compact) {
    const v = (label, value) => `<div class="fc ro"><span class="lbl">${label}</span><div class="fv${value ? '' : ' empty'}">${esc(value || 'Not recorded')}</div></div>`;
    const body = `<div class="fg g2" data-triage-readout>${v('Roadworthiness', s.finding?.road)}${v('Repair outcome', s.finding?.repair)}</div>`;
    return compact ? body : `<section class="panel section-gap" aria-labelledby="triage-determinations-title"><div class="panel-head"><h2 id="triage-determinations-title">Finding</h2></div><div class="panel-body">${body}</div></section>`;
  }

  function stageA() {
    if (!s.images.length) return '';
    const img = s.images[s.imageIndex];
    return `<section class="tv-stage" aria-labelledby="triage-images-title" data-vehicle-images>
      <div class="blockhead"><h2 id="triage-images-title">Vehicle images</h2><span class="muted tv-count">${s.imageIndex + 1} / ${s.images.length}</span></div>
      <div class="tv-stage-frame">
        <button type="button" class="tv-stage-image" data-viewer-open="${s.imageIndex}" aria-label="Open ${esc(img.file)}"><img src="${img.src}" alt="${esc(img.file)}"></button>
        <button type="button" class="tv-nav tv-nav--prev" data-step="-1" aria-label="Previous image">${icon('chevron-left')}</button>
        <button type="button" class="tv-nav tv-nav--next" data-step="1" aria-label="Next image">${icon('chevron-right')}</button>
      </div>
      <div class="tv-stage-caption"><strong>${esc(img.file)}</strong></div>
      <ul class="tv-strip" data-evidence-set>${s.images.map((im, i) => `<li><button type="button" class="tv-thumb${i === s.imageIndex ? ' is-current' : ''}" data-pick="${i}" aria-label="${esc(im.file)}" aria-current="${i === s.imageIndex}"><img src="${im.src}" alt=""></button></li>`).join('')}</ul>
    </section>`;
  }

  function viewerB() {
    const img = s.images[s.imageIndex];
    return `<section class="panel tv-viewer-panel" aria-labelledby="triage-images-title" data-vehicle-images>
      <div class="panel-head"><h2 id="triage-images-title">Vehicle images</h2><span class="muted tv-count">${s.imageIndex + 1} / ${s.images.length}</span>
        <div class="panel-actions"><button type="button" class="btn btn--small btn--icon" data-viewer-open="${s.imageIndex}" aria-label="Full screen" title="Full screen">${icon('zoom-in')}</button></div></div>
      <div class="tv-viewer-frame">
        <img src="${img.src}" alt="${esc(img.file)}">
        <button type="button" class="tv-nav tv-nav--prev" data-step="-1" aria-label="Previous image">${icon('chevron-left')}</button>
        <button type="button" class="tv-nav tv-nav--next" data-step="1" aria-label="Next image">${icon('chevron-right')}</button>
      </div>
      <div class="tv-viewer-caption"><strong>${esc(img.file)}</strong><small class="muted">${fileSize(img.size)}</small></div>
      <ul class="tv-grid" data-evidence-set>${s.images.map((im, i) => `<li><button type="button" class="tv-thumb${i === s.imageIndex ? ' is-current' : ''}" data-pick="${i}" aria-label="${esc(im.file)}" aria-current="${i === s.imageIndex}"><img src="${im.src}" alt=""></button></li>`).join('')}</ul>
    </section>`;
  }

  function sheetC() {
    return `<ul class="tv-sheet" data-evidence-set data-vehicle-images>${s.images.map((im, i) => `<li><button type="button" class="gallery-item tv-sheet-item" data-viewer-open="${i}"><span class="gallery-image"><img src="${im.src}" alt="${esc(im.file)}"></span>${caption(im)}</button></li>`).join('')}</ul>`;
  }

  function correspondenceRows() {
    const rows = [];
    if (s.email) rows.push({ id: 'm1', when: '02/10/2026 14:12', from: fx.requester, to: fx.mailbox, subject: fx.subject, cls: 'Pre-instruction', body: 'Please triage the attached vehicle. Photographs attached.\n\nRegards,\nSAB Solicitors', atts: s.images.map((i) => i.file) });
    if (s.chaserSent) rows.push({ id: 'm2', when: '03/10/2026 09:30', from: fx.mailbox, to: fx.requester, subject: `Re: ${fx.subject}`, cls: 'Additional image request', body: fx.chaserBody, atts: [] });
    if (s.linkedResponse || s.candidate) rows.push({ id: 'm3', when: '04/10/2026 08:12', from: fx.requester, to: fx.mailbox, subject: `Re: Re: ${fx.subject}`, cls: 'In-progress case', body: 'Further photographs attached as requested.', atts: ['IMG_4210_offside_rear.jpg'] });
    if (s.replySent) rows.push({ id: 'm4', when: '05/10/2026 10:05', from: fx.mailbox, to: fx.requester, subject: `Re: ${fx.subject}`, cls: 'Report sent', body: fx.outcomeBody, atts: [] });
    return rows;
  }

  function correspondence() {
    const d = derived();
    const rows = correspondenceRows();
    const status = s.send ? `<div class="notice" role="status" data-send-status>${icon('mail')}<span>Latest send: ${esc(s.send)}</span>${s.send === 'Unknown' ? `<button type="button" class="btn btn--small" data-reconcile>${icon('history')}<span>Reconcile status</span></button>` : ''}</div>` : '';
    const blocked = s.blocked && s.send !== 'Unknown' ? `<div class="notice notice--warning" role="alert">${icon('alert-triangle')}<span>The existing correspondence operation must finish or be resolved before another action.</span></div>` : '';
    const compose = d.canSend ? `<div class="button-row"><button type="button" class="btn btn--small" data-composer-open>${icon(d.purpose === 'outcome' ? 'reply' : 'send')}<span>${d.purpose === 'outcome' ? 'Reply with finding' : 'Send chaser'}</span></button></div>` : '';
    const table = rows.length ? `<div class="table-wrap"><table class="table table--compact"><thead><tr><th>Received</th><th>Sender</th><th>Subject</th><th>Classification</th><th><span class="sr-only">Action</span></th></tr></thead><tbody>${rows.map((r) => `<tr data-correspondence-row="${r.id}"><td><time>${r.when}</time></td><td>${esc(r.from)}</td><td>${esc(r.subject)}</td><td>${esc(r.cls)}</td><td class="row-actions nowrap"><button type="button" class="btn btn--small" data-message-open="${r.id}">Open message</button></td></tr>`).join('')}</tbody></table></div>` : '<p class="muted" data-correspondence-empty>No retained correspondence is associated with this Case.</p>';
    return `<div class="correspondence stack" data-correspondence>${status}${blocked}${compose}${table}</div>`;
  }

  function documents() {
    return `<div class="doc-list stack" data-document-list>${fx.documents.filter((doc) => s.email || !doc.mail).map((doc, i) => `<div class="doc-row" data-document-row="d${i}">${icon(doc.mail ? 'mail' : 'file-text')}<div class="doc-row-name"><b>${esc(doc.name)}</b><small>${esc(doc.origin)} · ${doc.when} · ${doc.size}</small></div><span class="doc-row-tags"><span class="src-tag">${esc(doc.role)}</span></span><span class="doc-row-actions"><a class="btn btn--small btn--icon" href="#" data-boundary="Downloads ${esc(doc.name)}." title="Save as" aria-label="Save as ${esc(doc.name)}">${icon('download')}</a></span></div>`).join('')}</div>`;
  }

  function filesTabs() {
    const docCount = fx.documents.filter((doc) => s.email || !doc.mail).length;
    const rows = correspondenceRows().length;
    const tab = (key, label) => `<button type="button" class="tab" role="tab" id="tv-files-tab-${key}" aria-selected="${s.filesTab === key}" aria-controls="tv-files-${key}" tabindex="${s.filesTab === key ? 0 : -1}" data-files-tab="${key}">${label}</button>`;
    return `<div class="tabs" role="tablist" aria-label="Files" data-file-tabs>${tab('documents', `Documents · ${docCount}`)}${tab('correspondence', `Correspondence · ${rows}`)}</div>
      <div id="tv-files-documents" role="tabpanel" aria-labelledby="tv-files-tab-documents" data-file-tab-panel="documents"${s.filesTab === 'documents' ? '' : ' hidden'}>${documents()}</div>
      <div id="tv-files-correspondence" role="tabpanel" aria-labelledby="tv-files-tab-correspondence" data-file-tab-panel="correspondence"${s.filesTab === 'correspondence' ? '' : ' hidden'}>${correspondence()}${cfg.id === 'c' ? response(true) : ''}</div>`;
  }

  function filesPanel(withHead = true) {
    const head = `<div class="panel-head"><h2 id="section-files-title">Files</h2><span class="status status--green status--plain">Box · confirmed</span><div class="panel-actions"><a class="btn btn--small btn--primary" href="#" data-boundary="Opens Upload for this Case.">${icon('upload')}<span>Add evidence</span></a><details class="menu" data-menu><summary class="btn btn--small"><span>More</span>${icon('chevron-down')}</summary><div class="menu-body"><a class="btn" href="#" data-boundary="Opens the Box folder.">${icon('external-link')}<span>Open in Box</span></a></div></details></div></div>`;
    return withHead
      ? `<section class="panel section-gap" id="section-files" aria-labelledby="section-files-title">${head}<div class="panel-body stack">${filesTabs()}</div></section>`
      : `<div class="tv-files-inline" id="section-files">${head}<div class="panel-body stack">${filesTabs()}</div></div>`;
  }

  function response(inline) {
    const d = derived();
    if (!(s.linkedResponse || (d.mutable && s.candidate))) return '';
    const linked = s.linkedResponse ? `<div class="timeline"><div class="timeline-item"><div class="timeline-title">04/10/2026 08:40</div><div class="timeline-text">Requester replied with photographs.</div></div></div>${d.mutable ? `<form class="stack" data-form="unlink_response"><div class="field"><label class="req" for="tv-unlink-response-reason">Reason</label><textarea id="tv-unlink-response-reason" rows="3" required maxlength="500"></textarea></div><div class="button-row"><button type="submit" class="btn">${icon('x')}<span>Unlink response</span></button></div></form>` : ''}` : '';
    const link = d.mutable && s.candidate && !s.linkedResponse ? `<form class="stack" data-form="link_response"><div class="field"><label class="req" for="tv-response-candidate">Approved-mailbox reply</label><select id="tv-response-candidate" required><option value="">Select exact response evidence</option><option>04/10/2026 08:12 — ${esc(fx.mailbox)} — message &lt;a81f@sab.example&gt; — Sent evidence 7c41…</option></select></div><div class="field"><label class="req" for="tv-response-reason">Reason</label><textarea id="tv-response-reason" rows="3" required maxlength="500"></textarea></div><div class="button-row"><button type="submit" class="btn btn--dark">${icon('link')}<span>Record and link exact response</span></button></div></form>` : '';
    const body = `<div class="stack">${linked}${link}</div>`;
    if (inline) return `<div class="tv-subsection"><h3 class="tab-panel-title tv-visible-title">Exact response evidence</h3>${body}</div>`;
    return `<section class="panel section-gap" aria-labelledby="triage-response-title" data-response-evidence><div class="panel-head"><h2 id="triage-response-title">Exact response evidence</h2></div><div class="panel-body">${body}</div></section>`;
  }

  function notes(card) {
    const d = derived();
    const form = d.mutable ? `<form class="stack" data-form="note"><div class="field"><label class="req" for="tv-note">Note</label><textarea id="tv-note" rows="${card ? 2 : 3}" required maxlength="500"></textarea></div><div class="button-row"><button type="submit" class="btn btn--dark">${icon('plus')}<span>Add note</span></button></div></form>` : '';
    const list = `<div class="notes-list">${s.history.map(([date, time, who, ev, reason]) => `<div class="note-entry"><div class="note-meta"><span>Date ${date}</span><span>Time ${time}</span><span>ID ${esc(who)}</span></div><div>${esc(ev)}: ${esc(reason)}</div></div>`).join('')}</div>`;
    return `<section class="panel${card ? '' : ' section-gap'}" aria-labelledby="triage-history-title"><div class="panel-head"><h2 id="triage-history-title">Notes</h2></div><div class="panel-body">${form}${list}</div></section>`;
  }

  function correspondenceCard() {
    const d = derived();
    const rows = correspondenceRows();
    const last = rows[rows.length - 1];
    if (!s.email) return '';
    return `<section class="panel" aria-labelledby="tv-corr-card-title" data-correspondence-card><div class="panel-head"><h2 id="tv-corr-card-title">Correspondence</h2>${d.canSend ? `<div class="panel-actions"><button type="button" class="btn btn--small" data-composer-open>${icon(d.purpose === 'outcome' ? 'reply' : 'send')}<span>${d.purpose === 'outcome' ? 'Reply with finding' : 'Send chaser'}</span></button></div>` : ''}</div><div class="panel-body stack">
      ${s.send ? `<div class="tv-line">${icon('mail')}<span>Latest send: ${esc(s.send)}</span>${s.send === 'Unknown' ? `<button type="button" class="btn btn--small" data-reconcile>${icon('history')}<span>Reconcile status</span></button>` : ''}</div>` : ''}
      ${last ? `<button type="button" class="tv-last-message" data-message-open="${last.id}"><small>${last.when} · ${esc(last.from)}</small><strong>${esc(last.subject)}</strong></button>` : ''}
      <button type="button" class="tv-link" data-goto-correspondence>Correspondence · ${rows.length}</button></div></section>`;
  }

  /* ---------- Page ---------- */
  function notices() {
    if (!s.notice) return '';
    const reply = s.notice.reply && derived().canSend ? `<button type="button" class="tv-link" data-composer-open>Reply with finding</button>` : '';
    return `<div class="notice notice--${s.notice.tone || 'info'} mb-2" role="status" aria-live="polite" data-notice tabindex="-1">${icon(s.notice.tone === 'success' ? 'check' : 'info')}<span>${esc(s.notice.text)}</span>${reply}<button type="button" class="dismiss" data-dismiss aria-label="Dismiss">${icon('x')}</button></div>`;
  }

  function body() {
    if (cfg.id === 'a') {
      return `<div class="record-body">${stageA()}${readout(false)}${response(false)}${filesPanel()}${notes(false)}</div>`;
    }
    if (cfg.id === 'b') {
      return `<div class="record-body"><div class="tv-split${s.images.length ? '' : ' tv-split--no-images'}">${s.images.length ? viewerB() : ''}<aside class="tv-aside stack" aria-label="Triage status"><section class="panel" aria-labelledby="triage-determinations-title"><div class="panel-head"><h2 id="triage-determinations-title">Finding</h2></div><div class="panel-body">${readout(true)}</div></section>${correspondenceCard()}${response(false).replace('panel section-gap', 'panel')}${notes(true)}</aside></div>${filesPanel()}</div>`;
    }
    const rows = correspondenceRows().length;
    const docs = fx.documents.filter((doc) => s.email || !doc.mail).length;
    const tab = (key, label) => `<button type="button" class="tab" role="tab" id="tv-tab-${key}" aria-selected="${s.tab === key}" aria-controls="tv-panel-${key}" tabindex="${s.tab === key ? 0 : -1}" data-page-tab="${key}">${label}</button>`;
    const fact = (label, value) => `<div class="tv-fact"><span class="ribbon-label">${label}</span><span class="${value ? '' : 'tv-empty'}">${esc(value || 'Not recorded')}</span></div>`;
    return `<div class="tv-tabrow"><div class="tabs" role="tablist" aria-label="Triage record">${s.images.length ? tab('images', `Images · ${s.images.length}`) : ''}${tab('files', `Files · ${docs + rows}`)}${tab('notes', `Notes · ${s.history.length}`)}</div><div class="tv-facts" aria-label="Finding" data-triage-readout>${fact('Roadworthiness', s.finding?.road)}${fact('Repair outcome', s.finding?.repair)}</div></div>
      <div class="record-body">
        ${s.images.length ? `<div id="tv-panel-images" role="tabpanel" aria-labelledby="tv-tab-images"${s.tab === 'images' ? '' : ' hidden'}>${sheetC()}</div>` : ''}
        <div id="tv-panel-files" role="tabpanel" aria-labelledby="tv-tab-files"${s.tab === 'files' ? '' : ' hidden'}>${filesPanel(false)}</div>
        <div id="tv-panel-notes" role="tabpanel" aria-labelledby="tv-tab-notes"${s.tab === 'notes' ? '' : ' hidden'}>${notes(true)}</div>
      </div>`;
  }

  function page() {
    return `<header class="page-header"><div class="page-title"><p class="eyebrow">Triage</p><h1>${esc(fx.reference)}</h1></div><div class="page-actions"><a class="btn" href="#" data-boundary="Returns to Cases › Triage.">${icon('arrow-right')}<span>Back to Cases</span></a><div class="refresh-button"><button type="button" class="btn btn--small" data-refresh title="Refresh">${icon('refresh-cw')}<span>Refresh</span></button></div></div></header>
      ${notices()}
      <article class="record triage-record tv-record" data-triage-record data-triage-state="${esc(s.state)}">${cfg.id === 'c' ? `<div class="tv-sticky">${ribbon()}</div>` : ribbon()}${body()}</article>`;
  }

  /* ---------- Dialogs ---------- */
  const closeX = `<button type="button" class="dialog-close" data-dialog-close aria-label="Close dialog">${icon('x')}</button>`;
  const backdrop = (id, title, inner, cls = '') => `<div class="dialog-backdrop" data-dialog="${id}" id="${id}" hidden><section class="dialog ${cls}" role="dialog" aria-modal="true" aria-labelledby="${id}-title"><div class="dialog-head"><h2 id="${id}-title" tabindex="-1">${esc(title)}</h2>${closeX}</div>${inner}</section></div>`;
  const reasonDialog = (id, title, action, consequence) => backdrop(id, title, `<form data-form="${action}"><div class="dialog-body stack">${consequence ? `<div class="notice notice--warning">${icon('alert-triangle')}<span>${esc(consequence)}</span></div>` : ''}<div class="field"><label class="req" for="${id}_reason">Reason for action</label><textarea id="${id}_reason" rows="3" required maxlength="500" data-dialog-initial-focus></textarea></div></div><div class="dialog-foot"><button type="button" class="btn" data-dialog-close>Cancel</button><button type="submit" class="btn btn--primary">Confirm Action</button></div></form>`);
  const findingFields = (prefix) => {
    const f = s.finding;
    const opt = (v, label, cur) => `<option value="${v}"${cur === label ? ' selected' : ''}>${label}</option>`;
    return `<div class="fg g2 is-editing"><div class="fc"><label for="${prefix}-road">Roadworthiness</label><select id="${prefix}-road" class="fi" data-field="road" data-dialog-initial-focus>${opt('', 'Not recorded', f ? '' : 'Not recorded')}${opt('Roadworthy', 'Roadworthy', f?.road)}${opt('Unroadworthy', 'Unroadworthy', f?.road)}</select></div><div class="fc"><label for="${prefix}-repair">Repair outcome</label><select id="${prefix}-repair" class="fi" data-field="repair">${opt('', 'Not recorded', f ? '' : 'Not recorded')}${opt('Repairable', 'Repairable', f?.repair)}${opt('TotalLoss', 'Total loss', f?.repair)}</select></div><div class="fc span2"><label class="req" for="${prefix}-reason">Reason</label><textarea id="${prefix}-reason" class="fi" rows="3" required maxlength="500"></textarea></div></div>`;
  };

  function dialogs() {
    const d = derived();
    const out = [];
    if (d.mutable) {
      out.push(backdrop('triage-assign-dialog', 'Assign', `<form data-form="assign"><div class="dialog-body"><div class="field"><label class="req" for="triage-assignee">Assignee</label><select id="triage-assignee" required data-dialog-initial-focus><option value=""></option>${fx.roster.map((r) => `<option value="${esc(r.replace(' (you)', ''))}">${esc(r)}</option>`).join('')}</select></div></div><div class="dialog-foot">${s.assignee ? '<button type="button" class="btn" data-unassign>Unassign</button>' : ''}<button type="button" class="btn" data-dialog-close>Cancel</button><button type="submit" class="btn btn--primary">Assign</button></div></form>`, 'dialog--compact'));
      out.push(backdrop('triage-determinations-dialog', 'Record finding', `<form data-form="determinations"><div class="dialog-body stack">${findingFields('triage-det')}<div><label class="check"><input type="checkbox" id="triage-det-complete" data-complete-tick> Complete Triage</label>${s.email && !s.blocked ? '<label class="check"><input type="checkbox" id="triage-det-reply" data-reply-tick> Reply with finding</label>' : ''}</div></div><div class="dialog-foot"><button type="button" class="btn" data-dialog-close>Cancel</button><button type="submit" class="btn btn--primary">${icon('save')}<span>Record finding</span></button></div></form>`));
      out.push(reasonDialog('triage-cancel-dialog', 'Cancel Triage', 'cancel', 'Cancelled Triage can be reopened with a reason.'));
      out.push(s.linkedCase
        ? reasonDialog('triage-unlink-case-dialog', 'Unlink case', 'unlink_case')
        : backdrop('triage-link-case-dialog', 'Link case', `<form data-form="link_case"><div class="dialog-body stack"><div class="field"><label class="req" for="triage-link-case-id">Case ID</label><input id="triage-link-case-id" required maxlength="100" data-dialog-initial-focus></div><div class="field"><label class="req" for="triage-link-case-reason">Reason for action</label><textarea id="triage-link-case-reason" rows="3" required maxlength="500"></textarea></div></div><div class="dialog-foot"><button type="button" class="btn" data-dialog-close>Cancel</button><button type="submit" class="btn btn--primary">Link case</button></div></form>`));
    } else {
      out.push(reasonDialog('triage-reopen-dialog', 'Reopen', 'reopen'));
    }
    if (d.correction) out.push(backdrop('triage-correction-dialog', 'Record correction', `<form data-form="correction"><div class="dialog-body stack">${findingFields('triage-cor')}</div><div class="dialog-foot"><button type="button" class="btn" data-dialog-close>Cancel</button><button type="submit" class="btn btn--primary">Record correction</button></div></form>`));
    for (const r of correspondenceRows()) {
      out.push(backdrop(`case-message-${r.id}`, r.subject, `<div class="dialog-body"><dl class="tv-message-meta"><dt>From</dt><dd>${esc(r.from)}</dd><dt>To</dt><dd>${esc(r.to)}</dd><dt>Received</dt><dd>${r.when}</dd></dl><div class="tv-message-body">${esc(r.body)}</div>${r.atts.length ? `<ul class="tv-message-atts">${r.atts.map((a) => `<li>${icon('paperclip')}<span>${esc(a)}</span></li>`).join('')}</ul>` : ''}</div><div class="dialog-foot"><button type="button" class="btn" data-boundary="Opens the Inbox record’s Reply for this Case.">${icon('reply')}<span>Reply</span></button><button type="button" class="btn" data-boundary="Opens the Inbox record’s Reply all for this Case.">${icon('reply')}<span>Reply all</span></button><button type="button" class="btn" data-boundary="Opens the Inbox record’s Forward for this Case.">${icon('forward')}<span>Forward</span></button><a class="btn btn--dark" href="#" data-boundary="Opens the full Inbox record.">${icon('external-link')}<span>Open full message</span></a></div>`, 'dialog--wide'));
    }
    out.push(`<div class="dialog-backdrop" data-dialog="tv-boundary" id="tv-boundary" hidden><section class="dialog dialog--compact" role="dialog" aria-modal="true" aria-labelledby="tv-boundary-title"><div class="dialog-head"><h2 id="tv-boundary-title" tabindex="-1">Mockup destination</h2>${closeX}</div><div class="dialog-body"><p data-boundary-copy></p><p class="muted">Outside this Triage page design.</p></div><div class="dialog-foot"><button type="button" class="btn" data-dialog-close>Return to the mockup</button></div></section></div>`);
    return out.join('');
  }

  function composer() {
    const d = derived();
    const outcome = d.purpose === 'outcome';
    const title = outcome ? 'Reply with finding' : 'Send chaser';
    const atts = [...s.images.map((i) => i.file), ...fx.documents.filter((doc) => doc.mail).map((doc) => doc.name)];
    return `<div class="mail-compose-backdrop" data-composer hidden><section class="dialog mail-compose-dialog" role="dialog" aria-modal="true" aria-labelledby="mail-compose-title" tabindex="-1">
      <header class="dialog-head"><div><p class="eyebrow">Triage · ${esc(fx.reference)}</p><h1 id="mail-compose-title">${title}</h1></div><button type="button" class="dialog-close" data-composer-close aria-label="Close composer">${icon('x')}</button></header>
      <div class="dialog-body mail-compose-body"><form class="mail-compose-form" data-form="send"><div class="mail-compose-form-fields">
        <div class="mail-compose-addresses"><div class="field mail-compose-from"><label for="tv-from">From</label><input id="tv-from" value="${esc(fx.mailbox)}" readonly></div><div class="field mail-compose-to"><label class="req" for="tv-to">To</label><input id="tv-to" value="${esc(fx.requester)}" required maxlength="500" data-composer-focus></div><div class="field mail-compose-cc"><label for="tv-cc">Cc</label><input id="tv-cc" maxlength="500"></div></div>
        <div class="field mail-compose-subject"><label class="req" for="tv-subject">Subject</label><input id="tv-subject" value="Re: ${esc(fx.subject)}" required maxlength="500"></div>
        <div class="field mail-compose-message"><label class="req" for="tv-body">Message</label><textarea id="tv-body" rows="${outcome ? 10 : 7}" required maxlength="5000">${esc(outcome ? fx.outcomeBody : fx.chaserBody)}</textarea></div>
        ${atts.length ? `<fieldset class="field mail-compose-attachments"><legend>Attachments</legend>${atts.map((a, i) => `<label><input type="checkbox" value="att${i}"> ${esc(a)}</label>`).join('')}</fieldset>` : ''}
      </div><footer class="mail-compose-footer"><button type="submit" class="btn btn--primary">${icon('send')}<span>${outcome ? 'Send reply' : 'Send chaser'}</span></button></footer></form></div></section></div>`;
  }

  function viewer() {
    return `<div class="tv-viewer" data-viewer role="dialog" aria-modal="true" aria-labelledby="tv-viewer-title" hidden><div class="tv-viewer-bar"><h2 id="tv-viewer-title" tabindex="-1" data-viewer-title></h2><span class="tv-viewer-count" data-viewer-count></span><span class="tv-spacer"></span><a class="btn btn--small btn--icon" href="#" data-boundary="Downloads the image." aria-label="Download" title="Download">${icon('download')}</a><button type="button" class="btn btn--small btn--icon" data-viewer-close aria-label="Close viewer">${icon('x')}</button></div><div class="tv-viewer-stage"><button type="button" class="tv-nav tv-nav--prev" data-viewer-step="-1" aria-label="Previous image">${icon('chevron-left')}</button><img alt="" data-viewer-img><button type="button" class="tv-nav tv-nav--next" data-viewer-step="1" aria-label="Next image">${icon('chevron-right')}</button></div></div>`;
  }

  /* ---------- Behaviour ---------- */
  const root = document.getElementById('tv-root');
  const dialogHost = document.getElementById('tv-dialogs');
  const live = document.getElementById('tv-live');
  let returnFocus = null;
  let openLayer = null;

  function render(focusSelector) {
    root.innerHTML = page();
    dialogHost.innerHTML = dialogs() + composer() + viewer();
    if (focusSelector) root.querySelector(focusSelector)?.focus();
  }

  const focusables = (el) => [...el.querySelectorAll('a[href],button:not([disabled]),input:not([disabled]):not([type=hidden]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])')].filter((x) => !x.closest('[hidden]') && x.offsetParent !== null);

  function setInert(on) {
    for (const el of [document.querySelector('[data-app-shell]'), document.querySelector('.tv-mock')]) if (el) el.inert = on;
  }

  function openLayerEl(el, opener, focusEl) {
    closeMenus();
    if (openLayer) closeLayer(false);
    returnFocus = opener || document.activeElement;
    el.hidden = false;
    openLayer = el;
    setInert(true);
    (focusEl || el.querySelector('[data-dialog-initial-focus],[data-composer-focus]') || focusables(el)[0] || el).focus();
  }

  function closeLayer(restore = true) {
    if (!openLayer) return;
    openLayer.hidden = true;
    openLayer = null;
    setInert(false);
    if (restore && returnFocus && document.contains(returnFocus)) returnFocus.focus();
  }

  const openDialog = (id, opener) => { const el = dialogHost.querySelector(`[data-dialog="${id}"]`); if (el) openLayerEl(el, opener); };

  function openComposer(opener) { openLayerEl(dialogHost.querySelector('[data-composer]'), opener); }

  function boundary(text, opener) {
    const el = dialogHost.querySelector('[data-dialog="tv-boundary"]');
    el.querySelector('[data-boundary-copy]').textContent = text;
    openLayerEl(el, opener);
  }

  function showViewer(index, opener) {
    s.imageIndex = index;
    const el = dialogHost.querySelector('[data-viewer]');
    paintViewer();
    openLayerEl(el, opener, el.querySelector('[data-viewer-close]'));
  }
  function paintViewer() {
    const el = dialogHost.querySelector('[data-viewer]');
    const img = s.images[s.imageIndex];
    el.querySelector('[data-viewer-img]').src = img.src;
    el.querySelector('[data-viewer-img]').alt = img.file;
    el.querySelector('[data-viewer-title]').textContent = img.file;
    el.querySelector('[data-viewer-count]').textContent = `${s.imageIndex + 1} / ${s.images.length}`;
  }

  function closeMenus(except) { document.querySelectorAll('details[data-menu][open]').forEach((m) => { if (m !== except) m.open = false; }); }

  function act(kind, opener) {
    const say = (text, extra = {}) => { s.notice = { text, tone: 'info', ...extra }; };
    const stamp = (ev, reason) => s.history.unshift(['05/10/2026', '10:15', 'alex', ev, reason]);
    switch (kind) {
      case 'complete': s.state = 'Completed'; stamp('Completed', 'Complete.'); say('Triage completed.', { reply: true }); break;
      default: break;
    }
    closeLayer(false);
    render('[data-notice]');
    live.textContent = s.notice?.text || '';
  }

  function submit(form) {
    if (!form.checkValidity()) { form.reportValidity(); return; }
    const kind = form.dataset.form;
    const val = (sel) => form.querySelector(sel)?.value?.trim() || '';
    const reason = val('textarea');
    const stamp = (ev, why) => s.history.unshift(['05/10/2026', '10:15', 'alex', ev, why]);
    let text = null;
    switch (kind) {
      case 'assign': { const who = val('select'); s.assignee = who; stamp('Assigned', who); text = `Assigned to ${who}.`; break; }
      case 'determinations': case 'correction': {
        const road = form.querySelector('[data-field="road"]').selectedOptions[0].textContent;
        const repair = form.querySelector('[data-field="repair"]').selectedOptions[0].textContent;
        s.finding = { road: road === 'Not recorded' ? null : road, repair: repair === 'Not recorded' ? null : repair };
        if (!s.finding.road && !s.finding.repair) s.finding = null;
        if (kind === 'determinations' && s.state !== 'Completed') s.state = 'Finding recorded';
        stamp(kind === 'correction' ? 'Finding superseded' : 'Finding recorded', reason);
        text = 'Finding recorded.';
        if (form.querySelector('[data-complete-tick]')?.checked) { s.state = 'Completed'; stamp('Completed', reason); text = 'Triage completed.'; }
        if (form.querySelector('[data-reply-tick]')?.checked && derived().canSend) { s.notice = { text, tone: 'info' }; closeLayer(false); render(); live.textContent = text; openComposer(root.querySelector('[data-triage-actions] summary')); return; }
        break;
      }
      case 'cancel': s.state = 'Cancelled'; stamp('Cancelled', reason); text = 'Triage cancelled.'; break;
      case 'reopen': s.state = 'Open'; stamp('Reopened', reason); text = 'Triage reopened.'; break;
      case 'link_case': s.linkedCase = val('input').toUpperCase() || 'QDOS26214'; stamp('Case linked', reason); text = 'Case linked.'; break;
      case 'unlink_case': s.linkedCase = null; stamp('Case unlinked', reason); text = 'Case unlinked.'; break;
      case 'note': stamp('Note', reason); text = 'Note added.'; break;
      case 'link_response': s.linkedResponse = true; stamp('Response evidence linked', reason); text = 'Response evidence linked.'; break;
      case 'unlink_response': s.linkedResponse = false; stamp('Response evidence unlinked', reason); text = 'Response evidence unlinked.'; break;
      case 'send': {
        const outcome = derived().purpose === 'outcome';
        s.send = 'Sent';
        if (outcome) s.replySent = true; else s.chaserSent = true;
        stamp('Sent evidence recorded', outcome ? 'Reply sent.' : 'Chaser sent.');
        text = outcome ? 'Reply sent.' : 'Chaser sent.';
        break;
      }
      default: return;
    }
    s.notice = { text, tone: 'info' };
    closeLayer(false);
    render('[data-notice]');
    live.textContent = text;
  }

  document.addEventListener('submit', (e) => {
    const form = e.target.closest('form[data-form]');
    if (!form) return;
    e.preventDefault();
    submit(form);
  });

  document.addEventListener('click', (e) => {
    const t = e.target.closest('button, a, summary');
    const menu = e.target.closest('details[data-menu]');
    if (!menu) closeMenus();
    if (!t) return;
    if (t.matches('summary')) { closeMenus(t.parentElement); return; }
    if (t.dataset.dialogOpen) { e.preventDefault(); openDialog(t.dataset.dialogOpen, menu ? menu.querySelector('summary') : t); return; }
    if (t.hasAttribute('data-composer-open')) { e.preventDefault(); openComposer(menu ? menu.querySelector('summary') : t); return; }
    if (t.dataset.post) { act(t.dataset.post, t); return; }
    if (t.dataset.boundary) { e.preventDefault(); boundary(t.dataset.boundary, t); return; }
    if (t.hasAttribute('data-dialog-close')) { closeLayer(); return; }
    if (t.hasAttribute('data-composer-close')) { closeLayer(); return; }
    if (t.hasAttribute('data-unassign')) { s.assignee = null; s.history.unshift(['05/10/2026', '10:15', 'alex', 'Unassigned', '']); s.notice = { text: 'Unassigned.' }; closeLayer(false); render('[data-notice]'); return; }
    if (t.hasAttribute('data-dismiss')) { s.notice = null; render(); root.querySelector('[data-triage-actions] summary')?.focus(); return; }
    if (t.hasAttribute('data-refresh')) { const label = t.querySelector('span'); label.textContent = 'Refreshing…'; setTimeout(() => { s.notice = null; render('[data-refresh]'); }, 400); return; }
    if (t.hasAttribute('data-reconcile')) { s.send = 'Sent'; s.blocked = false; s.notice = { text: 'Chaser status: Sent.' }; render('[data-notice]'); return; }
    if (t.dataset.step) { s.imageIndex = (s.imageIndex + Number(t.dataset.step) + s.images.length) % s.images.length; render(`[data-step="${t.dataset.step}"]`); return; }
    if (t.dataset.pick) { s.imageIndex = Number(t.dataset.pick); s.cropping = false; render(`[data-pick="${t.dataset.pick}"]`); return; }
    if (t.dataset.viewerOpen !== undefined && t.dataset.viewerOpen !== '') { showViewer(Number(t.dataset.viewerOpen), t); return; }
    if (t.dataset.viewerStep) { s.imageIndex = (s.imageIndex + Number(t.dataset.viewerStep) + s.images.length) % s.images.length; paintViewer(); return; }
    if (t.hasAttribute('data-viewer-close')) { closeLayer(false); render(); returnFocus?.isConnected ? returnFocus.focus() : root.querySelector('[data-vehicle-images] button')?.focus(); return; }
    if (t.dataset.messageOpen) { openDialog(`case-message-${t.dataset.messageOpen}`, t); return; }
    if (t.hasAttribute('data-goto-correspondence')) { s.filesTab = 'correspondence'; render('[data-files-tab="correspondence"]'); document.getElementById('section-files').scrollIntoView({ block: 'start' }); return; }
    if (t.dataset.filesTab) { s.filesTab = t.dataset.filesTab; render(`[data-files-tab="${s.filesTab}"]`); return; }
    if (t.dataset.pageTab) { s.tab = t.dataset.pageTab; render(`[data-page-tab="${s.tab}"]`); return; }
  });

  document.addEventListener('change', (e) => {
    const box = e.target;
    const form = box.closest?.('form');
    if (!form) return;
    if (box.matches('[data-reply-tick]') && box.checked) form.querySelector('[data-complete-tick]').checked = true;
    if (box.matches('[data-complete-tick]') && !box.checked) { const reply = form.querySelector('[data-reply-tick]'); if (reply) reply.checked = false; }
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      const openMenu = document.querySelector('details[data-menu][open]');
      if (openMenu && !openLayer) { openMenu.open = false; openMenu.querySelector('summary').focus(); return; }
      if (openLayer) { e.preventDefault(); const viewerOpen = openLayer.matches('[data-viewer]'); closeLayer(); if (viewerOpen) { const keep = returnFocus; render(); (keep?.isConnected ? keep : root.querySelector('[data-vehicle-images] button'))?.focus(); } return; }
    }
    if (openLayer && e.key === 'Tab') {
      const items = focusables(openLayer);
      if (!items.length) return;
      const first = items[0];
      const last = items[items.length - 1];
      if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
      else if (!openLayer.contains(document.activeElement)) { e.preventDefault(); first.focus(); }
    }
    if (openLayer?.matches('[data-viewer]') && (e.key === 'ArrowRight' || e.key === 'ArrowLeft') && !e.target.matches('select')) {
      s.imageIndex = (s.imageIndex + (e.key === 'ArrowRight' ? 1 : -1) + s.images.length) % s.images.length; paintViewer();
    }
    const tab = e.target.closest?.('[role=tab]');
    if (tab && ['ArrowRight', 'ArrowLeft', 'Home', 'End'].includes(e.key)) {
      const tabs = [...tab.parentElement.querySelectorAll('[role=tab]')];
      let i = tabs.indexOf(tab);
      i = e.key === 'Home' ? 0 : e.key === 'End' ? tabs.length - 1 : (i + (e.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
      e.preventDefault();
      tabs[i].click();
    }
  });

  /* ---------- Mockup controls ---------- */
  const mock = document.querySelector('.tv-mock');
  if (mock) {
    const picker = mock.querySelector('[data-state-picker]');
    picker.value = presetKey;
    picker.addEventListener('change', () => { const u = new URL(location.href); u.searchParams.set('state', picker.value); location.href = u.toString(); });
    const primary = mock.querySelector('[data-primary-picker]');
    primary.value = s.primary ? 'on' : 'off';
    primary.addEventListener('change', () => { s.primary = primary.value === 'on'; render(); });
    const opener = mock.querySelector('[data-dialog-picker]');
    opener.addEventListener('change', () => {
      const v = opener.value; opener.value = '';
      if (v === 'composer') { if (derived().canSend) openComposer(opener); else boundary('This state offers no reply.', opener); }
      else if (v === 'viewer') { if (s.images.length) showViewer(0, opener); }
      else if (v) { if (dialogHost.querySelector(`[data-dialog="${v}"]`)) openDialog(v, opener); else boundary('This state does not offer that dialog.', opener); }
    });
  }

  render();
  const startDialog = params.get('dialog');
  if (startDialog === 'composer' && derived().canSend) openComposer();
  else if (startDialog === 'viewer' && s.images.length) showViewer(0);
  else if (startDialog === 'menu') root.querySelector('[data-triage-actions]').open = true;
  else if (startDialog) openDialog(startDialog);
  if (params.get('tab') && cfg.id === 'c') { s.tab = params.get('tab'); render(); }
  if (params.get('files')) { s.filesTab = params.get('files'); render(); if (params.get('tab')) { s.tab = params.get('tab'); render(); } }
  window.triageMockup = { state: () => s, derived, actions };
  window.mockupReady = true;
})();
