// v28 proposals layer, part three: the fifth pass (20 September 2026). Closes
// the gaps found by a deep read of the operator's reference file
// pegasus_case_dashboard_2026-09-15.html against the first four passes: the
// features added to that file after the v27 inventory, the four v27 switches
// v28 had dropped, and the corrections. Ids P37 to P51 (P49 dropped 20 September), plus P30-drag and the
// P34, P22 and P29 corrections. Every feature is built from the live record's
// own parts and marked data-v28-proposal; ?skip=P41 turns one off.
// Loads after proposals-record.js and hooks the parts it built.
(function () {
  'use strict';
  var params = new URLSearchParams(window.location.search);
  if (params.get('proposals') === 'off') return;
  var skipped = (params.get('skip') || '').split(',').map(function (s) { return s.trim().toUpperCase(); });
  var on = function (id) { return skipped.indexOf(id) < 0; };
  var $ = function (id) { return document.getElementById(id); };
  var $$ = function (selector, root) { return Array.prototype.slice.call((root || document).querySelectorAll(selector)); };
  var money = function (n) { return '£' + Number(n || 0).toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); };
  var number = function (text) { var m = String(text || '').replace(/,/g, '').match(/-?[0-9]+(\.[0-9]+)?/); return m ? parseFloat(m[0]) : 0; };
  var clean = function (text) { return String(text || '').replace(/\s+/g, ' ').trim(); };
  var escape = function (text) { return String(text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;'); };
  var mark = function (el, id) { el.setAttribute('data-v28-proposal', id); return el; };
  var icon = function (name) { return '<svg class="icon" aria-hidden="true"><use href="#icon-' + name + '" /></svg>'; };
  var editing = false;

  function cell(sectionId, label) {
    var section = $(sectionId); if (!section) return null;
    return $$('.fc', section).filter(function (fc) { var l = fc.querySelector('label, .lbl'); return l && clean(l.textContent) === label; })[0] || null;
  }
  function value(sectionId, label) {
    var fc = cell(sectionId, label); if (!fc) return '';
    var control = fc.querySelector('select.fi, input.fi, textarea.fi');
    if (editing && control) {
      if (control.tagName === 'SELECT') { var o = control.selectedOptions[0]; return o && o.value ? clean(o.textContent) : ''; }
      return clean(control.value);
    }
    var fv = fc.querySelector('.fv'); if (!fv || fv.classList.contains('empty')) return '';
    var copy = fv.cloneNode(true); $$('.src-tag, .prov, .icon', copy).forEach(function (n) { n.remove(); });
    return clean(copy.textContent);
  }
  function ribbon(label) {
    var item = $$('.ribbon .ribbon-item').filter(function (i) { return clean((i.querySelector('.ribbon-label') || {}).textContent) === label; })[0];
    return item ? clean(item.querySelector('.ribbon-value').textContent) : '';
  }
  function engineer() { return ribbon('Engineer') || 'Engineer'; }
  function engineersValue() { var el = document.querySelector('[data-decision-engineer-value] span'); return el ? number(el.textContent) : 0; }
  function repairCost() { var el = document.querySelector('#section-estimate [data-estimate-gross]'); return el ? number(el.textContent) : 0; }
  function stamp() { var d = new Date(); return { day: d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }), time: d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' }) }; }
  function toast(text, undo) {
    var region = document.querySelector('[data-toast-region]') || document.body;
    var note = document.createElement('div'); note.className = 'toast v28-undo'; note.setAttribute('role', 'status'); mark(note, 'P41');
    note.innerHTML = '<span>' + escape(text) + '</span>' + (undo ? '<button type="button" class="btn btn--small">Undo</button>' : '');
    if (undo) note.querySelector('button').addEventListener('click', function () { undo(); note.remove(); });
    region.appendChild(note); setTimeout(function () { note.remove(); }, 8000);
  }

  // ---- P44 · Every act logged ---------------------------------------------
  // A System note in the live history row style, prepended to the Notes
  // timeline for each act the layer performs, as the reference logs every act.
  function log(text) {
    if (!on('P44')) return;
    var timeline = document.querySelector('[data-case-history]'); if (!timeline) return;
    var s = stamp();
    var note = document.createElement('div'); note.className = 'note sys'; note.setAttribute('data-history-event', 'v28_act'); mark(note, 'P44');
    note.innerHTML = '<div class="nw"><span>' + s.day + '</span> <span>' + s.time + '</span></div><div class="nb"><b>' + escape(engineer()) + '</b> — ' + escape(text) + '</div>';
    timeline.insertBefore(note, timeline.firstChild);
  }
  function p44Wire() {
    var section = $('section-estimate');
    document.addEventListener('click', function (event) {
      var t = event.target, hit;
      if (t.closest('#section-estimate button.del')) log('Repair line removed.');
      else if (t.closest('.toast.v28-undo button')) log('Undo: the removed item was restored.');
      else if ((hit = t.closest('.reason-dialog [data-yes]')) && /Delete all lines/.test(clean(hit.closest('.reason-dialog').textContent))) log('All repair lines deleted.');
      else if ((hit = t.closest('.v28-radio')) && hit.closest('.v28-radiorow')) log(clean(hit.closest('.v28-radiorow').getAttribute('aria-label')) + ' set to ' + clean(hit.textContent) + '.');
      else if ((hit = t.closest('[data-wb-remove]'))) log('Report wording block removed.');
      else if ((hit = t.closest('[data-wb-add-back]'))) log('Report wording block added back to the report.');
      else if (t.closest('[data-wb-new]')) log('Manual report paragraph added.');
      else if (t.closest('[data-wb-recompose]')) log('Report wording block recomposed from the fields.');
      else if (t.closest('[data-wb-up], [data-wb-down]')) log('Report wording reordered.');
      else if (t.closest('.v28-scale [data-apply]')) log('Repair specification scaled: ' + clean((document.querySelector('.v28-scale [data-read]') || {}).textContent) + '.');
      else if (t.closest('.v28-scale [data-remove]')) log('Scaling removed.');
      else if (t.closest('[data-bank-save]')) log('Unroadworthy reason wording saved to the firm bank.');
    }, true); // capture: P16 stops a line-delete click from bubbling
    document.addEventListener('change', function (event) {
      var t = event.target;
      if (t.id === 'estimate-labour-rate' || t.id === 'estimate-rate-card') log('Labour rate set to ' + money(number(($('estimate-labour-rate') || {}).value)) + '/h.');
      else if (t.id === 'v28-uplift') log('Regional uplift ' + (t.checked ? 'applied: + 15 % on the labour rate.' : 'removed.'));
      else if (t.matches('.v28-contract [data-agreed]')) log(t.checked ? 'Contract repair agreed; outcome set to Contract repair.' : 'Contract repair un-ticked.');
      else if (t.matches('.v28-contract [data-sum]')) log('Agreed contract sum set to ' + money(number(t.value)) + '.');
      else if (t.matches('[data-wb-text]')) log('Report wording block edited by hand.');
      else if (t.matches('[data-report-switch]')) log('Report content switch "' + clean((t.closest('label') || {}).textContent) + '" ' + (t.checked ? 'on.' : 'off.'));
    });
    document.addEventListener('focusout', function (event) {
      var h = event.target.closest && event.target.closest('[data-wb-title]');
      if (h && h.getAttribute('data-v28-was') !== null && h.getAttribute('data-v28-was') !== clean(h.textContent)) log('Report heading "' + h.getAttribute('data-v28-was') + '" renamed to "' + clean(h.textContent) + '".');
    });
    document.addEventListener('focusin', function (event) {
      var h = event.target.closest && event.target.closest('[data-wb-title]');
      if (h) h.setAttribute('data-v28-was', clean(h.textContent));
    });
    if (section) void 0;
  }

  // ---- P43 · Versions history for the Repair Spec --------------------------
  // Every import, scale, clear, restore and send freezes the outgoing draft as
  // a version with how it came about; the version a sent report used is marked.
  // The two fixture estimates are what v28-build/enrich.mjs enters by hand.
  var FIXTURE = [
    { name: 'Example Bodyshop estimate', rate: 48, materials: 185, lines: [['Replace', 'Rear bumper cover', 'EX-1001', 1, 412.50, 2.5, 3.0]] },
    { name: 'Example Bodyshop supplementary', rate: 48, materials: 210, lines: [['Replace', 'Rear bumper cover and reinforcement', 'EX-1002', 1, 538.00, 3.5, 3.0]] }
  ];
  var VERSIONS = [];
  var versionsButton = null;
  function draftSnapshot() {
    var rows = $$('#section-estimate tr[data-estimate-line]').map(function (tr) {
      var g = function (n) { var el = tr.querySelector('[name="' + n + '"]'); return el ? el.value : ''; };
      return [g('lineOperation'), g('lineDescription'), g('linePartNumber'), number(g('lineQuantity')) || 1, number(g('linePartPounds')), number(g('lineLabourHours')), number(g('linePaintHours'))];
    });
    if (!rows.length) { var f = FIXTURE[FIXTURE.length - 1]; rows = f.lines.map(function (l) { return l.slice(); }); }
    return { rate: number(($('estimate-labour-rate') || { value: FIXTURE[1].rate }).value), materials: number(($('estimate-materials') || { value: FIXTURE[1].materials }).value), lines: rows, gross: repairCost() };
  }
  function grossOf(v) { if (v.gross) return v.gross; var n = v.lines.reduce(function (s, l) { return s + l[3] * l[4] + l[5] * v.rate; }, 0) + v.materials; return n; }
  function freeze(origin, extra) {
    var snap = draftSnapshot(); var s = stamp();
    var last = VERSIONS[VERSIONS.length - 1];
    var key = JSON.stringify([snap.rate, snap.materials, snap.lines]);
    if (last && last.key === key && !extra) return last;
    var v = { n: VERSIONS.length + 1, when: s.day + ' ' + s.time, who: engineer(), origin: origin, rate: snap.rate, materials: snap.materials, lines: snap.lines, gross: snap.gross, key: key, sent: null };
    if (extra) Object.keys(extra).forEach(function (k) { v[k] = extra[k]; });
    VERSIONS.push(v);
    if (versionsButton) versionsButton.querySelector('span').textContent = 'Versions (' + VERSIONS.length + ')';
    return v;
  }
  function p43Versions() {
    var section = $('section-estimate'); if (!section) return;
    var tabs = $$('.estimate-tab', section);
    var created = $$('[data-history-event="estimate_created"] .nw').map(function (nw) { return clean(nw.textContent); });
    FIXTURE.forEach(function (f, i) {
      var tab = tabs.filter(function (t) { return clean((t.querySelector('span') || {}).textContent) === f.name; })[0];
      if (!tab && tabs.length) return;
      VERSIONS.push({ n: VERSIONS.length + 1, when: created[created.length - 1 - i] || '06 May 2031 11:30', who: 'development-offline-administrator', origin: 'Entered by hand as "' + f.name + '"', rate: f.rate, materials: f.materials, lines: f.lines, gross: 0, key: JSON.stringify([f.rate, f.materials, f.lines]), sent: i === 1 ? 'Sent on report' : null });
    });
    // The origin line under the tabs.
    var selected = section.querySelector('.estimate-tab[aria-selected="true"] span');
    var current = VERSIONS.filter(function (v) { return selected && v.origin.indexOf(clean(selected.textContent)) >= 0; })[0] || VERSIONS[VERSIONS.length - 1];
    if (current) {
      var origin = document.createElement('p'); origin.className = 'muted v28-origin'; mark(origin, 'P43');
      var lines = $$('#section-estimate tr[data-estimate-line]').length || current.lines.length;
      origin.innerHTML = 'Populated ' + escape(current.when) + ' by <b>' + escape(current.who) + '</b>, ' + escape(current.origin.replace(/^Entered by hand/, 'entered by hand')) + ' · ' + lines + (lines === 1 ? ' line' : ' lines') + ' · labour rate <b>' + money(current.rate) + '/h</b>';
      var anchor = section.querySelector('[data-estimate-actions]') || section.querySelector('[data-estimate-tabs]');
      if (anchor) anchor.parentNode.insertBefore(origin, anchor.nextSibling);
    }
    // Versions (n) under More, beside Compare.
    var more = section.querySelector('[data-estimate-more] .menu-body');
    if (more) {
      versionsButton = document.createElement('button'); versionsButton.type = 'button'; versionsButton.className = 'btn'; versionsButton.setAttribute('data-v28-versions', ''); mark(versionsButton, 'P43');
      versionsButton.innerHTML = icon('history') + '<span>Versions (' + VERSIONS.length + ')</span>';
      var compare = more.querySelector('[data-estimate-compare]');
      more.insertBefore(versionsButton, compare ? compare.nextSibling : null);
      versionsButton.addEventListener('click', function () { more.closest('details').open = false; openVersions(); });
    }
    // Acts that freeze the outgoing draft.
    document.addEventListener('click', function (event) {
      var t = event.target, hit;
      if ((hit = t.closest('.reason-dialog [data-yes]')) && /Delete all lines/.test(clean(hit.closest('.reason-dialog').textContent))) freeze('Before all lines were deleted');
      else if (t.closest('.v28-scale [data-apply]')) { var read = clean((document.querySelector('.v28-scale [data-read]') || {}).textContent); freeze('Scaled: ' + read, { force: true }); }
      else if (t.closest('.v28-scale [data-remove]')) freeze('Restored to the estimate', { force: true });
    });
    document.addEventListener('submit', function (event) {
      var form = event.target;
      if (form.matches && /CompleteEstimateImport/.test(form.getAttribute('action') || '')) freeze('Before the Glass’s import was completed');
      if (form.matches && form.classList.contains('v28-delivery')) { var v = freeze('As sent on the report'); v.sent = 'Sent on report'; log('Repair specification as sent frozen as v' + v.n + '.'); }
    }, true);
  }
  function openVersions() {
    var old = $('v28-versions-dialog'); if (old) old.remove();
    var dialog = document.createElement('div'); dialog.id = 'v28-versions-dialog'; dialog.className = 'dialog-backdrop'; dialog.setAttribute('data-dialog', 'v28-versions-dialog'); mark(dialog, 'P43');
    var draft = draftSnapshot();
    var row = function (v, isDraft) {
      return '<tr' + (isDraft ? ' class="cur"' : '') + '><td><span class="mono">' + (isDraft ? 'Draft' : 'v' + v.n) + '</span>' + (v.sent ? ' <span class="status status--plain status--green">' + v.sent + '</span>' : '') + (isDraft ? ' <span class="status status--plain status--amber">Working</span>' : '') + '</td>'
        + '<td>' + escape(isDraft ? 'now' : v.when) + '<br><span class="muted">' + escape(isDraft ? engineer() : v.who) + '</span></td>'
        + '<td>' + escape(isDraft ? 'The draft being edited' : v.origin) + '</td>'
        + '<td class="num mono">' + v.lines.length + '</td><td class="num mono">' + money(v.rate) + '</td><td class="num mono"><b>' + money(isDraft ? (repairCost() || grossOf(v)) : grossOf(v)) + '</b></td>'
        + '<td>' + (isDraft ? '' : '<div class="button-row"><button type="button" class="btn btn--small" data-v28-compare="' + v.n + '">Compare with current</button>' + (editing ? '<button type="button" class="btn btn--small" data-v28-restore="' + v.n + '">Restore</button>' : '') + '</div>') + '</td></tr>';
    };
    dialog.innerHTML = '<section class="dialog dialog--wide" role="dialog" aria-modal="true" aria-labelledby="v28-versions-title"><div class="dialog-head"><h2 id="v28-versions-title" tabindex="-1">Repair Spec versions</h2>'
      + '<button type="button" class="dialog-close" data-v28-close aria-label="Close dialog">' + icon('x') + '</button></div>'
      + '<div class="dialog-body"><p class="muted">Every import, scale, clear and restore freezes the outgoing draft; sending a report freezes the version that went out.</p><div class="table-wrap"><table class="table table--compact v28-versions"><thead><tr><th>Version</th><th>When</th><th>How it came about</th><th class="num">Lines</th><th class="num">Rate</th><th class="num">Total inc VAT</th><th></th></tr></thead><tbody>'
      + row(draft, true) + VERSIONS.slice().reverse().map(function (v) { return row(v, false); }).join('') + '</tbody></table></div></div>'
      + '<div class="dialog-foot"><button type="button" class="btn" data-v28-close>Close</button></div></section>';
    document.body.appendChild(dialog);
    var close = function () { dialog.remove(); };
    dialog.addEventListener('click', function (event) {
      var t = event.target, hit;
      if (t === dialog || t.closest('[data-v28-close]')) close();
      else if ((hit = t.closest('[data-v28-compare]'))) { close(); var opener = document.querySelector('[data-dialog-open="compare-estimates-dialog"]'); if (opener) opener.click(); else toast('Compare needs two estimates.'); }
      else if ((hit = t.closest('[data-v28-restore]'))) restoreVersion(+hit.getAttribute('data-v28-restore'), close);
    });
    document.addEventListener('keydown', function esc(event) { if (event.key === 'Escape') { close(); document.removeEventListener('keydown', esc); } });
    var h = dialog.querySelector('h2'); if (h) h.focus();
  }
  function restoreVersion(n, close) {
    var v = VERSIONS.filter(function (x) { return x.n === n; })[0]; if (!v) return;
    var out = freeze('Before v' + n + ' was restored');
    var rows = $$('#section-estimate tr[data-estimate-line]');
    v.lines.forEach(function (l, i) {
      var tr = rows[i]; if (!tr) return;
      var set = function (name, val) { var el = tr.querySelector('[name="' + name + '"]'); if (el) { el.value = val; el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); } };
      set('lineOperation', l[0]); set('lineDescription', l[1]); set('linePartNumber', l[2]); set('lineQuantity', l[3]); set('linePartPounds', l[4].toFixed(2)); set('lineLabourHours', l[5]); set('linePaintHours', l[6]);
    });
    var rate = $('estimate-labour-rate'); if (rate) { rate.value = v.rate.toFixed(2); rate.dispatchEvent(new Event('input', { bubbles: true })); }
    var mat = $('estimate-materials'); if (mat) { mat.value = v.materials.toFixed(2); mat.dispatchEvent(new Event('input', { bubbles: true })); }
    close();
    log('Repair specification restored from v' + n + ' (' + v.origin + '); the outgoing draft was frozen as v' + out.n + '.');
    toast('Restored v' + n + '.');
  }

  // ---- P30 · Wording blocks drag ------------------------------------------
  // The reference drags its blocks; the third pass drew up and down buttons.
  // Both now: a grip drags a block above the one it lands on (or to the end on
  // the add row); the buttons stay for the keyboard. A drop is carried out by
  // pressing P30's own move buttons, so its order stays the one it holds.
  function p30Drag() {
    var panel = document.querySelector('.v28-wording'); if (!panel || !editing) return;
    var list = panel.querySelector('[data-wb-list]'), adder = panel.querySelector('[data-wb-add]');
    var dragging = null;
    var decorate = function () {
      $$('.v28-wb', list).forEach(function (box) {
        if (box.querySelector('.v28-grip')) return;
        var grip = document.createElement('span'); grip.className = 'v28-grip'; grip.setAttribute('draggable', 'true'); grip.title = 'Drag to reorder; the window order is the print order'; grip.innerHTML = icon('grip-vertical'); mark(grip, 'P30');
        var head = box.querySelector('.v28-wbh'); head.insertBefore(grip, head.firstChild);
        grip.addEventListener('dragstart', function (e) { dragging = box.getAttribute('data-wb'); box.classList.add('dragging'); e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', dragging); e.dataTransfer.setDragImage(box, 20, 20); });
        grip.addEventListener('dragend', function () { dragging = null; box.classList.remove('dragging'); $$('.dropmark', panel).forEach(function (b) { b.classList.remove('dropmark'); }); });
        box.addEventListener('dragover', function (e) { if (!dragging || dragging === box.getAttribute('data-wb')) return; e.preventDefault(); $$('.dropmark', panel).forEach(function (b) { b.classList.remove('dropmark'); }); box.classList.add('dropmark'); });
        box.addEventListener('dragleave', function () { box.classList.remove('dropmark'); });
        box.addEventListener('drop', function (e) { e.preventDefault(); if (dragging && dragging !== box.getAttribute('data-wb')) moveBefore(dragging, box.getAttribute('data-wb')); });
      });
    };
    var moveBefore = function (id, targetId) {
      var ids = function () { return $$('.v28-wb', list).map(function (b) { return b.getAttribute('data-wb'); }); };
      var from = ids().indexOf(id), to = targetId ? ids().indexOf(targetId) : ids().length;
      if (from < 0 || to < 0) return;
      var steps = from < to ? to - from - 1 : from - to;
      var dir = from < to ? 'down' : 'up';
      for (var i = 0; i < steps; i++) { var b = list.querySelector('.v28-wb[data-wb="' + id + '"] [data-wb-' + dir + ']'); if (!b || b.disabled) break; b.click(); }
      log('Report wording reordered.');
    };
    adder.addEventListener('dragover', function (e) { if (dragging) { e.preventDefault(); adder.classList.add('dropmark'); } });
    adder.addEventListener('dragleave', function () { adder.classList.remove('dropmark'); });
    adder.addEventListener('drop', function (e) { e.preventDefault(); adder.classList.remove('dropmark'); if (dragging) moveBefore(dragging, null); });
    new MutationObserver(decorate).observe(list, { childList: true });
    decorate();
  }

  // ---- P34 · Apply and Remove scaling freeze a version (see p43Versions) ---
  // ---- P22 · Attach: Breakdown is the Repair Spec document -----------------
  function p22Attach() {
    var box = document.querySelector('.v28-attach [data-att="Breakdown"]'); if (!box) return;
    box.setAttribute('data-att', 'Repair Spec'); box.closest('label').title = 'The repair specification as its own PDF, the document Print Estimate opens';
    var span = box.closest('label').querySelector('span'); if (span) span.textContent = 'Repair Spec';
    var images = document.querySelector('.v28-attach [data-att="Images"]'); if (images) images.closest('label').title = 'Included images alone, two per page';
    var report = document.querySelector('.v28-attach [data-att="Report"]'); if (report) report.closest('label').title = 'The Engineer’s report PDF';
    var fee = document.querySelector('.v28-attach [data-att="Fee note"]'); if (fee) fee.closest('label').title = 'Fee note PDF from the principal fee table';
    mark(box.closest('label'), 'P22');
  }

  // ---- Computed reserve on Decisions --------------------------------------
  function reserveCell() {
    var reserve = cell('section-settlement', 'Reserve'); if (!reserve) return;
    var fc = document.createElement('div'); fc.className = 'fc ro v28-composed'; mark(fc, 'P30');
    fc.innerHTML = '<span class="lbl">Repair reserve (computed)</span><div class="fv derived mono" data-composed="reserve"></div>';
    reserve.parentNode.insertBefore(fc, reserve);
    var input = reserve.querySelector('input.fi');
    var refresh = function () {
      var cost = repairCost(), outcome = value('section-settlement', 'Outcome').toLowerCase();
      var computed = cost ? Math.ceil(cost / 50) * 50 : 0;
      fc.querySelector('[data-composed="reserve"]').textContent = /repairable/.test(outcome) && computed ? money(computed) + ' (repair cost rounded up to the next £50)' : 'Not applicable';
      if (editing && input && !input.value && computed && /repairable/.test(outcome)) input.placeholder = computed.toFixed(2);
    };
    document.addEventListener('input', refresh); document.addEventListener('change', refresh); refresh();
  }

  // ---- P29 · Salvage not applicable ---------------------------------------
  function salvageNa() {
    var cat = document.querySelector('#section-settlement [data-decision="assessment.category"]'); if (!cat) return;
    var line = document.createElement('div'); line.className = 'dec wide v28-salvage-na'; mark(line, 'P29');
    line.innerHTML = '<span class="dl">Salvage</span><div class="fc ro"><span class="lbl">Salvage</span><div class="fv">Not applicable</div></div>';
    cat.parentNode.insertBefore(line, cat);
    var was = null;
    var refresh = function () {
      var tl = /total loss/i.test(value('section-settlement', 'Outcome'));
      line.hidden = tl;
      if (was !== null && was !== tl) log(tl ? 'Salvage recorded as applicable.' : 'Salvage recorded as not applicable.');
      was = tl;
    };
    document.addEventListener('change', refresh); refresh();
  }

  // ---- P37 · Off-pattern cells read amber ---------------------------------
  // A figure that does not fit the line's operation stays as imported and
  // reads amber with a tooltip; the roll-up carries the amount as specialist.
  function p37OffPattern() {
    var body = document.querySelector('#section-estimate [data-estimate-grid-body], #section-estimate table tbody'); if (!editing || !body) return;
    var rollup = document.querySelector('#section-estimate [data-estimate-rollup]');
    var line = document.createElement('div'); line.className = 'rr v28-offpattern'; line.hidden = true; mark(line, 'P37');
    line.innerHTML = '<dt>Off-pattern items (treated as specialist)</dt><dd></dd>';
    if (rollup) { var net = $$('.rr', rollup).filter(function (r) { return /^Net/.test(clean(r.querySelector('dt').textContent)); })[0]; rollup.insertBefore(line, net || null); }
    var refresh = function () {
      var total = 0;
      $$('tr[data-estimate-line]', body).forEach(function (tr) {
        var op = (tr.querySelector('[name="lineOperation"]') || {}).value || '';
        var check = function (name, off) {
          var el = tr.querySelector('[name="' + name + '"]'); if (!el) return;
          var bad = off && number(el.value) > 0;
          el.classList.toggle('viol', bad); if (bad) { el.title = 'Off-pattern'; el.setAttribute('aria-label', 'Off-pattern'); el.setAttribute('data-v28-proposal', 'P37'); } else { el.removeAttribute('title'); el.removeAttribute('aria-label'); el.removeAttribute('data-v28-proposal'); }
          if (bad && name === 'linePartPounds') total += (number((tr.querySelector('[name="lineQuantity"]') || {}).value) || 1) * number(el.value);
        };
        check('linePartPounds', /^(Repair|RemoveAndRefit|Paint|Blend)$/.test(op));
        check('lineLabourHours', /^(Paint|Blend)$/.test(op));
        check('linePaintHours', !/^(Paint|Blend|Repair|Replace)$/.test(op));
      });
      line.hidden = !total; line.querySelector('dd').textContent = money(total);
    };
    body.addEventListener('input', refresh); body.addEventListener('change', refresh); refresh();
  }

  // ---- P48 · Materials per line -------------------------------------------
  function p48Materials() {
    var table = document.querySelector('#section-estimate table'); if (!editing || !table) return;
    var head = table.querySelector('thead tr'), rows = $$('tbody tr[data-estimate-line]', table); if (!head || !rows.length) return;
    var th = document.createElement('th'); th.scope = 'col'; th.className = 'num col-money'; th.textContent = 'Material £'; mark(th, 'P48');
    var after = head.children[6]; head.insertBefore(th, after ? after.nextSibling : null);
    var total = $('estimate-materials'); var seed = total ? number(total.value) : 0;
    var USES = /^(Paint|Blend|Repair|Specialist|Other)$/;
    var draw = function (tr, i) {
      var td = document.createElement('td'); mark(td, 'P48');
      td.innerHTML = '<input class="num" type="number" min="0" step="0.01" inputmode="decimal" aria-label="Materials, line ' + (i + 1) + '" data-v28-material />';
      var ref = tr.children[6]; tr.insertBefore(td, ref ? ref.nextSibling : null);
      var input = td.querySelector('input');
      var op = tr.querySelector('[name="lineOperation"]');
      var fit = function () { var ok = USES.test(op ? op.value : '') || number(input.value) > 0; input.disabled = !ok; input.placeholder = ok ? '' : '—'; };
      if (op) op.addEventListener('change', fit); fit();
      return input;
    };
    var inputs = rows.map(draw);
    // The one fixture line is a Replace, so the estimate figure sits on it as received.
    if (inputs[0] && seed) { inputs[0].disabled = false; inputs[0].value = seed.toFixed(2); }
    if (total) { total.readOnly = true; total.classList.add('v28-derived'); var lbl = total.closest('.fc').querySelector('label'); if (lbl) lbl.textContent = 'Paint materials (£, column total)'; }
    var rr = $$('#section-estimate [data-estimate-rollup] .rr dt').filter(function (dt) { return clean(dt.textContent) === 'Materials'; })[0]; if (rr) rr.textContent = 'Materials (column total)';
    var sum = function () { var s = inputs.reduce(function (a, i) { return a + (i.disabled ? 0 : number(i.value)); }, 0); if (total) { total.value = s.toFixed(2); total.dispatchEvent(new Event('input', { bubbles: true })); } };
    inputs.forEach(function (i) { i.addEventListener('input', sum); i.addEventListener('change', sum); });
  }

  // ---- P38 · Placements ---------------------------------------------------
  function p38Placements() {
    var claimSection = $('section-claim') || $('section-overview');
    // Claimant VAT status sits with the claimant.
    var vat = document.querySelector('#section-settlement [data-field="settlement.claimant_vat_registered"]');
    var claimant = $$('.sub-panel', claimSection).filter(function (p) { return clean((p.querySelector('h3') || {}).textContent) === 'Claimant'; })[0];
    if (vat && claimant) { var grid = claimant.querySelector('.fg') || claimant; grid.appendChild(vat); vat.setAttribute('data-v28-moved', 'P38'); mark(vat, 'P38'); }
    // The three report content switches sit on Valuation.
    var switches = document.querySelector('[data-report-switches]') || document.querySelector('#section-report [data-field="report-content"] .fv');
    var valuation = document.querySelector('#section-valuation > .panel-body');
    if (switches && valuation) {
      var fc = switches.closest('.fc') || switches;
      var holder = document.createElement('div'); holder.className = 'sub-panel'; mark(holder, 'P38');
      holder.innerHTML = '<h3>On the report</h3>';
      holder.appendChild(fc); valuation.appendChild(holder);
    }
    // Unrelated damage and its deduction sit on Vehicle.
    var damage = $('section-damage'), vehicle = $('section-vehicle') && $('section-vehicle').querySelector('.panel-body');
    if (damage && vehicle) {
      var cells = $$('.fc', damage).filter(function (c) { return /^Unrelated/i.test(clean((c.querySelector('label, .lbl') || {}).textContent)); });
      if (cells.length) {
        var sub = document.createElement('div'); sub.className = 'sub-panel'; mark(sub, 'P38');
        sub.innerHTML = '<h3>Unrelated damage</h3><div class="fg g2"></div>';
        cells.forEach(function (c) { sub.querySelector('.fg').appendChild(c); });
        var nested = vehicle.querySelector('.record-section.v28-nested');
        vehicle.insertBefore(sub, nested || null);
      }
    }
    // Sign-off Engineer on Case details. The assigned Engineer is the ribbon's
    // fact and is not repeated here (one fact, one home).
    var signoff = cell('section-report', 'Sign-off Engineer');
    var caseGrid = document.querySelector('#section-overview .sub-panel .fg');
    if (signoff && caseGrid) { caseGrid.appendChild(signoff); signoff.setAttribute('data-v28-moved', 'P38'); }
  }

  // ---- P39 · Sign-off follows the assigned Engineer ------------------------
  function p39Signoff() {
    var select = $('f-sign-off-engineer'); if (!select) return;
    var item = $$('.ribbon .ribbon-item').filter(function (i) { return clean((i.querySelector('.ribbon-label') || {}).textContent) === 'Engineer'; })[0]; if (!item) return;
    var follow = function () {
      var name = clean(item.querySelector('.ribbon-value').textContent);
      var option = $$('option', select).filter(function (o) { return clean(o.textContent) === name; })[0];
      var assigned = document.querySelector('[data-v28-assigned]'); if (assigned) assigned.textContent = name;
      if (option && select.value !== option.value) { select.value = option.value; select.dispatchEvent(new Event('change', { bubbles: true })); log('Sign-off Engineer set to ' + name + ', following the hand-off.'); }
    };
    new MutationObserver(follow).observe(item.querySelector('.ribbon-value'), { childList: true, characterData: true, subtree: true });
    mark(select, 'P39');
    if (params.get('demo') === 'signoff') {
      var first = $$('option', select).filter(function (o) { return o.value; })[0];
      if (first) item.querySelector('.ribbon-value').innerHTML = '<span>' + escape(clean(first.textContent)) + '</span>';
    }
  }

  // ---- P40 · Report date stamped on generate -------------------------------
  function p40ReportDate() {
    var date = $('f-report-date'); if (!date) return;
    mark(date, 'P40');
    var stampDate = function () {
      if (date.value) return;
      date.value = new Date().toISOString().slice(0, 10); date.dispatchEvent(new Event('input', { bubbles: true })); date.dispatchEvent(new Event('change', { bubbles: true }));
      log('Report date stamped ' + stamp().day + ' on generate.');
    };
    document.addEventListener('submit', function (event) { if (/GenerateReport/.test(event.target.getAttribute('action') || '')) stampDate(); }, true);
    document.addEventListener('click', function (event) { if (event.target.closest('[data-v28-produce]')) stampDate(); });
  }

  // ---- P41 · Working images -------------------------------------------------
  var IMG = {}; // occurrence -> { rot, full }
  function tileKey(tile) { var a = tile.querySelector('[data-file-name]'); return a ? a.getAttribute('data-file-name') : tile.getAttribute('data-image-tile'); }
  function reportCount() {
    var tiles = $$('#section-files .image-tile').filter(function (t) { return !t.hidden; });
    var n = tiles.filter(function (t) { var i = t.querySelector('.v28-include'); return i && i.classList.contains('on'); }).length;
    $$('[data-report-image-count], [data-v28-image-count]').forEach(function (c) { c.textContent = n + ' of ' + tiles.length + ' in report'; });
  }
  function mirrorStrip() {
    var strip = document.querySelector('[data-report-images-strip]'); if (!strip) return;
    var order = $$('#section-files .image-tile').filter(function (t) { return !t.hidden; }).map(tileKey);
    var items = $$('.th', strip);
    order.forEach(function (key) { var th = items.filter(function (t) { return t.getAttribute('data-file-name') === key; })[0]; if (th) strip.appendChild(th); });
    items.forEach(function (th) { th.hidden = order.indexOf(th.getAttribute('data-file-name')) < 0; });
  }
  function p41Images() {
    var grid = document.querySelector('#section-files [data-image-grid]'); if (!grid) return;
    var pane = grid.closest('[data-file-tab-panel]') || grid.parentNode;
    if (!editing) return;
    var dragging = null;
    var decorate = function (tile) {
      if (tile.querySelector('.v28-img-tools')) return;
      var key = tileKey(tile); IMG[key] = IMG[key] || { rot: 0, full: false };
      var tools = document.createElement('div'); tools.className = 'v28-img-tools'; mark(tools, 'P41');
      tools.innerHTML = '<span class="v28-grip" draggable="true" title="Drag to reorder; order is the report order">' + icon('grip-vertical') + '</span>'
        + '<button type="button" class="icon-button" data-img-rotate title="Rotate 90°" aria-label="Rotate">' + icon('rotate-cw') + '</button>'
        + '<button type="button" class="icon-button" data-img-full title="Print on its own page" aria-label="Full page" aria-pressed="false">' + icon('image') + '</button>'
        + '<button type="button" class="icon-button" data-img-remove title="Remove image" aria-label="Remove">' + icon('x') + '</button>';
      var chip = document.createElement('span'); chip.className = 'status status--plain status--navy v28-fullchip'; chip.textContent = 'Full page'; chip.hidden = true; mark(chip, 'P41');
      tile.appendChild(chip); tile.appendChild(tools);
      var grip = tools.querySelector('.v28-grip');
      grip.addEventListener('dragstart', function (e) { dragging = tile; tile.classList.add('dragging'); e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', key); });
      grip.addEventListener('dragend', function () { dragging = null; tile.classList.remove('dragging'); $$('.dropmark', grid).forEach(function (t) { t.classList.remove('dropmark'); }); });
      tile.addEventListener('dragover', function (e) { if (!dragging || dragging === tile) return; e.preventDefault(); $$('.dropmark', grid).forEach(function (t) { t.classList.remove('dropmark'); }); tile.classList.add('dropmark'); });
      tile.addEventListener('dragleave', function () { tile.classList.remove('dropmark'); });
      tile.addEventListener('drop', function (e) { e.preventDefault(); if (dragging && dragging !== tile) { grid.insertBefore(dragging, tile); mirrorStrip(); log('Images reordered: "' + tileKey(dragging) + '" moved. Order is the report order.'); } });
    };
    grid.addEventListener('click', function (event) {
      var t = event.target, tile = t.closest('.image-tile'); if (!tile) return;
      if (t.closest('.v28-img-tools')) { event.preventDefault(); event.stopPropagation(); }
      if (t.closest('[data-img-rotate]')) rotate(tile);
      else if (t.closest('[data-img-full]')) fullPage(tile);
      else if (t.closest('[data-img-remove]')) remove(tile);
      setTimeout(reportCount, 0);
    }, true);
    function rotate(tile) {
      var key = tileKey(tile), st = IMG[key]; st.rot = (st.rot + 90) % 360;
      var img = tile.querySelector('[data-tile-image]'); if (img) img.style.transform = st.rot ? 'rotate(' + st.rot + 'deg)' : '';
      var badge = tile.querySelector('[data-tile-rotation]'); if (badge) { badge.textContent = st.rot ? st.rot + '°' : ''; badge.hidden = !st.rot; }
      var vimg = document.querySelector('[data-viewer-stage] img'); if (vimg && viewerKey() === key) vimg.style.transform = img.style.transform;
      log('Image "' + key + '" rotated to ' + st.rot + '°.');
    }
    function fullPage(tile) {
      var key = tileKey(tile), st = IMG[key]; st.full = !st.full;
      tile.querySelector('.v28-fullchip').hidden = !st.full;
      var b = tile.querySelector('[data-img-full]'); b.setAttribute('aria-pressed', String(st.full)); b.title = 'Full page';
      log('Image "' + key + '" full page ' + (st.full ? 'on.' : 'off.'));
    }
    function remove(tile) {
      var key = tileKey(tile); tile.hidden = true; mirrorStrip(); reportCount();
      log('Image "' + key + '" removed.');
      toast('Image removed: ' + key, function () { tile.hidden = false; mirrorStrip(); reportCount(); log('Image "' + key + '" restored.'); });
    }
    $$('.image-tile', grid).forEach(decorate);
    // Add images from a file, by the button or by dropping on the grid.
    var add = document.createElement('div'); add.className = 'button-row v28-img-add'; mark(add, 'P41');
    add.innerHTML = '<button type="button" class="btn btn--small" data-img-add>' + icon('plus') + '<span>Add images…</span></button><input type="file" accept="image/*" multiple hidden data-img-file />';
    pane.appendChild(add);
    var file = add.querySelector('[data-img-file]');
    add.querySelector('[data-img-add]').addEventListener('click', function () { file.click(); });
    file.addEventListener('change', function () { ingest(file.files); file.value = ''; });
    ['dragenter', 'dragover'].forEach(function (ev) { grid.addEventListener(ev, function (e) { if (Array.prototype.indexOf.call(e.dataTransfer.types, 'Files') >= 0) { e.preventDefault(); grid.classList.add('dragover'); } }); });
    ['dragleave', 'drop'].forEach(function (ev) { grid.addEventListener(ev, function () { grid.classList.remove('dragover'); }); });
    grid.addEventListener('drop', function (e) { if (e.dataTransfer.files && e.dataTransfer.files.length) { e.preventDefault(); ingest(e.dataTransfer.files); } });
    function ingest(files) {
      Array.prototype.forEach.call(files, function (f) {
        if (!/^image\//.test(f.type)) return;
        var reader = new FileReader();
        reader.onload = function () {
          var template = grid.querySelector('.image-tile'); if (!template) return;
          var tile = template.cloneNode(true); tile.hidden = false; tile.removeAttribute('data-v28-proposal'); mark(tile, 'P41');
          var id = 'v28-' + Date.now() + '-' + Math.floor(Math.random() * 1000); tile.setAttribute('data-image-tile', id);
          $$('.v28-img-tools, .v28-fullchip, .v28-include', tile).forEach(function (n) { n.remove(); });
          $$('form, details', tile).forEach(function (n) { n.remove(); });
          var a = tile.querySelector('a.th'); if (a) { a.setAttribute('href', '#'); a.setAttribute('data-file-name', f.name); a.removeAttribute('data-evidence-item'); }
          var img = tile.querySelector('img'); if (img) { img.src = reader.result; img.alt = f.name; img.style.transform = ''; }
          var name = tile.querySelector('.th-name'); if (name) name.textContent = f.name;
          var rot = tile.querySelector('[data-tile-rotation]'); if (rot) { rot.hidden = true; rot.textContent = ''; }
          grid.appendChild(tile); decorate(tile);
          var tick = document.createElement('span'); tick.className = 'v28-include on'; tick.textContent = '✓'; mark(tick, 'P27'); (tile.querySelector('a.th') || tile).appendChild(tick);
          (tile.querySelector('a.th') || tile).addEventListener('click', function (e) { if (e.target.closest('.v28-img-tools')) return; e.preventDefault(); tick.classList.toggle('on'); tick.textContent = tick.classList.contains('on') ? '✓' : ''; reportCount(); });
          var strip = document.querySelector('[data-report-images-strip]');
          if (strip && strip.querySelector('.th')) { var th = strip.querySelector('.th').cloneNode(true); th.hidden = false; th.classList.remove('off'); th.setAttribute('data-file-name', f.name); th.setAttribute('href', '#'); var si = th.querySelector('img'); if (si) si.src = reader.result; var sn = th.querySelector('.th-name'); if (sn) sn.textContent = f.name; mark(th, 'P41'); strip.appendChild(th); }
          reportCount(); log('Image "' + f.name + '" added.');
        };
        reader.readAsDataURL(f);
      });
    }
    // The viewer gains Full page and Remove.
    var viewerTools = document.querySelector('[data-viewer-view-tools]');
    if (viewerTools) {
      var vb = document.createElement('span'); vb.className = 'case-viewer-set v28-viewer-tools'; mark(vb, 'P41');
      vb.innerHTML = '<button type="button" class="btn btn--small" data-viewer-full>' + icon('image') + '<span>Full page</span></button><button type="button" class="btn btn--small" data-viewer-remove>' + icon('x') + '<span>Remove</span></button>';
      viewerTools.appendChild(vb);
      vb.addEventListener('click', function (event) {
        var key = viewerKey(); var tile = $$('.image-tile', grid).filter(function (t) { return tileKey(t) === key; })[0]; if (!tile) return;
        if (event.target.closest('[data-viewer-full]')) fullPage(tile);
        if (event.target.closest('[data-viewer-remove]')) { remove(tile); var close = document.querySelector('[data-viewer-close]'); if (close) close.click(); }
      });
    }
    function viewerKey() { var n = document.querySelector('[data-viewer-name]'); return n ? clean(n.textContent) : ''; }
    reportCount();
  }

  // ---- P42 · Produce documents preview ---------------------------------------
  function wordingBlocks() {
    var panel = document.querySelector('.v28-wording'); if (!panel) return [];
    if (editing) return $$('.v28-wb', panel).map(function (b) { return { title: clean(b.querySelector('[data-wb-title]').textContent), text: b.querySelector('[data-wb-text]').value }; });
    return $$('.fc.ro', panel).map(function (fc) { var l = fc.querySelector('.lbl').cloneNode(true); $$('.src-tag', l).forEach(function (n) { n.remove(); }); return { title: clean(l.textContent), text: clean(fc.querySelector('.fv').textContent) }; });
  }
  function includedImages() {
    var tiles = $$('#section-files .image-tile').filter(function (t) { return !t.hidden; });
    var inReport = function (t) { var i = t.querySelector('.v28-include'); return i ? i.classList.contains('on') : (t.getAttribute('data-preparation-role') || 'NotUsed') !== 'NotUsed'; };
    return tiles.filter(inReport).map(function (t) { var img = t.querySelector('img'); var k = tileKey(t); return { name: k, src: img ? img.src : '', full: !!(IMG[k] && IMG[k].full), rot: IMG[k] ? IMG[k].rot : 0 }; });
  }
  function fileName(kind) {
    var ref = clean((document.querySelector('.ribbon-ref .ribbon-value') || {}).textContent);
    var reg = clean(((document.querySelector('.ribbon-ref .ribbon-label') || {}).textContent || '').split('·').pop());
    var named = document.querySelector('[data-file-name-out]');
    if (kind === 'report' && named) return clean(named.textContent).replace(/\.pdf$/, '');
    return ref + ' ' + reg + ' ' + (kind === 'spec' ? 'Repair Spec' : 'Images');
  }
  function buildDoc(kind) {
    var ref = clean((document.querySelector('.ribbon-ref .ribbon-value') || {}).textContent);
    var reg = clean(((document.querySelector('.ribbon-ref .ribbon-label') || {}).textContent || '').split('·').pop());
    var vehicle = [value('section-vehicle', 'Make'), value('section-vehicle', 'Model')].filter(Boolean).join(' ');
    var title = clean((document.querySelector('[data-report-title]') || {}).textContent) || 'Report';
    var head = function (kindName) {
      return '<div class="v28-doc-head"><div class="co">COLLISION <span>ENGINEERS</span></div><div class="tag">Independent Automotive Engineering Experts<br>Engineer’s ' + kindName + '</div></div>'
        + '<div class="addr">Collision Engineers Ltd · Our ref ' + escape(ref) + '</div>';
    };
    var details = '<table class="v28-doc-details"><tr><td>Our reference</td><td>' + escape(ref) + '</td></tr>'
      + '<tr><td>Principal / their ref</td><td>' + escape(ribbon('Principal')) + (value('section-overview', 'Claim reference') ? ' · ' + escape(value('section-overview', 'Claim reference')) : '') + '</td></tr>'
      + '<tr><td>Claimant</td><td>' + escape(ribbon('Claimant')) + '</td></tr>'
      + '<tr><td>Vehicle</td><td>' + escape([vehicle, reg].filter(Boolean).join(' · ')) + '</td></tr>'
      + '<tr><td>Date of incident</td><td>' + escape(value('section-overview', 'Incident date') || '—') + '</td></tr>'
      + '<tr><td>Assessed</td><td>' + escape(value('section-inspection', 'Assessed') || value('section-inspection', 'Inspection date') || '—') + ' · ' + escape(engineer()) + '</td></tr></table>';
    var worklists = (function () { var wl = document.querySelector('[data-estimate-worklists]'); return wl ? '<div class="v28-doc-wl">' + wl.innerHTML + '</div>' : ''; })();
    var sheet = function (standalone) {
      var imgs = includedImages(); if (!imgs.length) return '';
      var html = standalone ? '' : '<h2 class="pbreak">Images (' + imgs.length + ')</h2>';
      var pair = 0;
      imgs.forEach(function (im) {
        if (im.full) { if (pair) { html += '</div>'; pair = 0; } html += '<div class="v28-doc-img full pbreak"><img src="' + im.src + '" data-rot="' + im.rot + '" alt=""><div class="cap">' + escape(im.name) + ' \u00b7 full page</div></div>'; return; }
        if (!pair) html += '<div class="v28-doc-pair pbreak">';
        html += '<div class="v28-doc-img"><img src="' + im.src + '" data-rot="' + im.rot + '" alt=""><div class="cap">' + escape(im.name) + '</div></div>';
        pair += 1; if (pair === 2) { html += '</div>'; pair = 0; }
      });
      if (pair) html += '</div>';
      return html;
    };
    if (kind === 'spec') {
      var rate = value('section-estimate', 'Labour rate (£/h)') || (($('estimate-labour-rate') || {}).value || '');
      return '<div class="v28-doc">' + head('Repair Specification') + '<h1>Repair specification — ' + escape(reg) + '</h1>' + details + worklists
        + '<h2>Costs</h2><p>Labour rate ' + money(number(rate)) + '/h · Repair cost including VAT <b>' + money(repairCost()) + '</b>.</p>'
        + '<div class="foot">Produced by Pegasus · ' + escape(ref) + ' · this document accompanies our ' + escape(title) + '.</div></div>';
    }
    if (kind === 'images') return '<div class="v28-doc">' + head('Image Pack') + '<h1>Images — ' + escape(reg) + ' (' + includedImages().length + ')</h1>' + sheet(true) + '<div class="foot">Produced by Pegasus · ' + escape(ref) + '.</div></div>';
    var blocks = wordingBlocks().filter(function (b) { return b.text && b.text !== 'Not recorded'; }).map(function (b) { return '<h2>' + escape(b.title) + '</h2><p>' + escape(b.text).replace(/\n/g, '<br>') + '</p>'; }).join('');
    var ev = engineersValue(), cost = repairCost(), outcome = value('section-settlement', 'Outcome').toLowerCase(), salvage = number(value('section-settlement', 'Salvage value'));
    var box = function (l, v, acc) { return '<div class="vbox' + (acc ? ' acc' : '') + '"><div class="l">' + l + '</div><div class="v">' + v + '</div></div>'; };
    var boxes = '<div class="v28-doc-boxes">' + (
      /total loss/.test(outcome) ? box('Pre-accident value', money(ev)) + box('Salvage', money(salvage)) + box('Recommended settlement', money(ev - salvage), true)
      : /contract/.test(outcome) ? box('Pre-accident value', money(ev)) + box('Contract repair (total)', money(number((document.querySelector('.v28-contract [data-sum]') || {}).value) || cost), true)
      : /cash in lieu/.test(outcome) ? box('Pre-accident value', money(ev)) + box('Cash in lieu — estimated repair cost', money(cost), true)
      : box('Pre-accident value', money(ev)) + box('Repair cost (inc VAT)', money(cost)) + box('Repair reserve', money(Math.ceil(cost / 50) * 50), true)) + '</div>';
    var signoff = value('section-overview', 'Sign-off Engineer') || value('section-report', 'Sign-off Engineer') || engineer();
    return '<div class="v28-doc">' + head('Report') + '<h1>' + escape(title) + '</h1>' + details + blocks + boxes + worklists
      + '<div class="sig">Prepared and authorised by<br><b>' + escape(signoff) + '</b> — Collision Engineers Ltd<br>' + stamp().day + '</div>' + sheet(false)
      + '<div class="foot">Produced by Pegasus · ' + escape(ref) + ' · ' + escape(fileName('report')) + '.pdf</div></div>';
  }
  var previewDialog = null;
  function openPreview(kind) {
    if (previewDialog) previewDialog.remove();
    kind = kind || 'report';
    var dialog = document.createElement('div'); dialog.id = 'v28-preview-dialog'; dialog.className = 'dialog-backdrop'; dialog.setAttribute('data-dialog', 'v28-preview-dialog'); dialog.setAttribute('data-kind', kind); mark(dialog, 'P42');
    var titles = { report: 'Report', spec: 'Repair Spec', images: 'Images' };
    dialog.innerHTML = '<section class="dialog dialog--wide v28-preview" role="dialog" aria-modal="true" aria-labelledby="v28-preview-title"><div class="dialog-head"><h2 id="v28-preview-title" tabindex="-1">Preview — ' + titles[kind] + ' as it will print</h2>'
      + '<button type="button" class="dialog-close" data-v28-close aria-label="Close dialog">' + icon('x') + '</button></div>'
      + '<div class="dialog-body"><p class="muted">Save as PDF from the print dialog; the file name is preset.</p><div class="v28-doc-sheet" data-v28-sheet>' + buildDoc(kind) + '</div></div>'
      + '<div class="dialog-foot"><span class="v28-doc-kinds" role="group" aria-label="Document">' + Object.keys(titles).map(function (k) { return '<button type="button" class="btn btn--small" data-v28-kind="' + k + '" aria-pressed="' + (k === kind) + '">' + titles[k] + '</button>'; }).join('') + '</span>'
      + '<button type="button" class="btn btn--primary" data-v28-pdf>' + icon('file-output') + '<span>' + titles[kind] + ' PDF</span></button><button type="button" class="btn" data-v28-close>Close</button></div></section>';
    document.body.appendChild(dialog); previewDialog = dialog;
    dialog.addEventListener('click', function (event) {
      var t = event.target, hit;
      if (t === dialog || t.closest('[data-v28-close]')) { dialog.remove(); previewDialog = null; }
      else if ((hit = t.closest('[data-v28-kind]'))) openPreview(hit.getAttribute('data-v28-kind'));
      else if (t.closest('[data-v28-pdf]')) producePdf(kind);
    });
    var h = dialog.querySelector('h2'); if (h) h.focus();
  }
  function producePdf(kind) {
    var old = document.title; document.title = fileName(kind);
    document.body.setAttribute('data-v28-print', 'doc');
    var done = function () { document.title = old; document.body.removeAttribute('data-v28-print'); window.removeEventListener('afterprint', done); };
    window.addEventListener('afterprint', done);
    window.print(); setTimeout(done, 2000);
    log((kind === 'spec' ? 'Repair Spec' : kind === 'images' ? 'Images' : 'Report') + ' PDF produced as ' + fileName(kind) + '.pdf.');
  }
  function p42Produce() {
    var card = document.querySelector('#section-report [data-report-preview-card]'); if (!card) return;
    var button = document.createElement('button'); button.type = 'button'; button.className = 'btn'; button.setAttribute('data-v28-produce', ''); mark(button, 'P42');
    button.innerHTML = icon('file-output') + '<span>Produce PDF…</span>';
    card.appendChild(button);
    button.addEventListener('click', function () { openPreview('report'); });
    // P9's Print Estimate opens the same Repair Spec document.
    var print = document.querySelector('#section-estimate [data-estimate-more] a[data-document-preview], #section-estimate a[data-document-preview]');
    if (print) { print.setAttribute('data-v28-proposal', 'P42'); print.addEventListener('click', function (event) { event.preventDefault(); event.stopImmediatePropagation(); var d = print.closest('details'); if (d) d.open = false; openPreview('spec'); }, true); }
  }

  // ---- P50 · One place for an image ------------------------------------------
  // Everything about an image lives on its tile on the Images tab: its report
  // role (Not used, Close-up, Overview, Supporting) and order, beside the P41
  // tools. The Report section's "Images in report" strip and "Report image
  // preparation" cards, live surfaces that repeated the same images, are
  // hidden. The count moves to the Images tab.
  var ROLES = [['NotUsed', 'Not used'], ['CloseUp', 'Close-up'], ['Overview', 'Overview'], ['Supporting', 'Supporting']];
  function p50OnePlace() {
    var grid = document.querySelector('#section-files [data-image-grid]'); if (!grid) return;
    // Read mode: the tiles carry no role, so it is read from the strip before the strip goes.
    $$('[data-report-images-strip] [data-report-image]').forEach(function (th) { var tile = grid.querySelector('.image-tile[data-image-tile="' + th.getAttribute('data-report-image') + '"]'); if (tile && !tile.hasAttribute('data-preparation-role')) tile.setAttribute('data-preparation-role', th.getAttribute('data-report-image-role') || 'Supporting'); });
    ['[data-report-images]', '[data-report-preparation]'].forEach(function (sel) {
      $$('#section-report ' + sel).forEach(function (el) { el.setAttribute('data-v28-removed', 'P50'); el.hidden = true; });
    });
    var pane = grid.closest('[data-file-tab-panel]') || grid.parentNode;
    var addRow = pane.querySelector('.v28-img-add');
    var count = document.createElement('b'); count.className = 'v28-img-count'; count.setAttribute('data-v28-image-count', ''); mark(count, 'P50');
    var line = document.createElement('p'); line.className = 'v28-img-line'; line.appendChild(count); mark(line, 'P50');
    pane.insertBefore(line, addRow || null);
    var role = function (tile) { return tile.getAttribute('data-preparation-role') || 'NotUsed'; };
    var label = function (code) { return (ROLES.filter(function (r) { return r[0] === code; })[0] || ROLES[0])[1]; };
    var recount = function () {
      var tiles = $$('.image-tile', grid).filter(function (t) { return !t.hidden; });
      var n = tiles.filter(function (t) { return role(t) !== 'NotUsed'; }).length;
      count.textContent = n + ' of ' + tiles.length + ' in report';
      var live = document.querySelector('[data-report-image-count]'); if (live) live.textContent = count.textContent;
    };
    var decorate = function (tile) {
      if (tile.querySelector('.v28-role')) return;
      var box = document.createElement('div'); box.className = 'v28-role'; mark(box, 'P50');
      if (editing) {
        box.innerHTML = '<label><span class="lbl">In report</span><select class="fi" data-v28-role>' + ROLES.map(function (r) { return '<option value="' + r[0] + '"' + (r[0] === role(tile) ? ' selected' : '') + '>' + r[1] + '</option>'; }).join('') + '</select></label>'
          + '<label data-v28-order-wrap' + (role(tile) === 'Supporting' ? '' : ' hidden') + '><span class="lbl">Order</span><input class="fi num" type="number" min="1" step="1" data-v28-order value="' + escape(tile.getAttribute('data-preparation-order') || '') + '" /></label>';
      } else {
        box.innerHTML = role(tile) === 'NotUsed' ? '<span class="muted">Not in report</span>' : '<span class="status status--plain status--navy">' + label(role(tile)) + (role(tile) === 'Supporting' && tile.getAttribute('data-preparation-order') ? ' \u00b7 ' + escape(tile.getAttribute('data-preparation-order')) : '') + '</span>';
      }
      tile.appendChild(box);
      var tick = tile.querySelector('.v28-include');
      var sync = function (from) {
        var select = box.querySelector('[data-v28-role]');
        if (from === 'tick' && tick) { var on = tick.classList.contains('on'); if (on && role(tile) === 'NotUsed') tile.setAttribute('data-preparation-role', 'Supporting'); if (!on) tile.setAttribute('data-preparation-role', 'NotUsed'); if (select) select.value = role(tile); }
        if (from === 'select' && select) { tile.setAttribute('data-preparation-role', select.value); if (tick) { tick.classList.toggle('on', select.value !== 'NotUsed'); tick.textContent = select.value !== 'NotUsed' ? '\u2713' : ''; } log('Image "' + tileKey(tile) + '" set to ' + label(select.value) + ' in the report.'); }
        var wrap = box.querySelector('[data-v28-order-wrap]'); if (wrap) wrap.hidden = role(tile) !== 'Supporting';
        recount();
      };
      box.addEventListener('change', function (e) { if (e.target.matches('[data-v28-role]')) sync('select'); if (e.target.matches('[data-v28-order]')) { tile.setAttribute('data-preparation-order', e.target.value); log('Image "' + tileKey(tile) + '" order set to ' + e.target.value + '.'); } });
      box.addEventListener('click', function (e) { e.stopPropagation(); });
      tile.addEventListener('click', function () { setTimeout(function () { sync('tick'); }, 0); });
    };
    $$('.image-tile', grid).forEach(decorate);
    new MutationObserver(function () { $$('.image-tile', grid).forEach(decorate); recount(); }).observe(grid, { childList: true });
    recount();
  }

  // ---- P45 · Sub-cards fold ---------------------------------------------------
  function p45Fold() {
    var binders = window.pegasusMountBinders || [];
    $$('.record-section .sub-panel').forEach(function (panel) {
      var h3 = panel.querySelector(':scope > h3'); if (!h3 || panel.hasAttribute('data-collapse')) return;
      var section = panel.closest('.record-section'); var sectionKey = section ? section.getAttribute('data-section') : 'record';
      var slug = clean(h3.firstChild && h3.firstChild.nodeType === 3 ? h3.firstChild.textContent : h3.textContent).toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 20);
      var key = ('case.' + sectionKey + '.' + slug).slice(0, 40);
      panel.setAttribute('data-collapse', key); if (!panel.hasAttribute('data-v28-proposal')) mark(panel, 'P45');
      var toggle = document.createElement('button'); toggle.type = 'button'; toggle.className = 'icon-button panel-collapse v28-sub-collapse'; toggle.setAttribute('data-collapse-toggle', '');
      toggle.setAttribute('aria-expanded', 'true'); toggle.setAttribute('data-label-collapse', 'Collapse ' + clean(h3.textContent)); toggle.setAttribute('data-label-expand', 'Expand ' + clean(h3.textContent)); toggle.setAttribute('aria-label', 'Collapse ' + clean(h3.textContent));
      toggle.innerHTML = icon('chevron-down');
      h3.appendChild(toggle);
      binders.forEach(function (bind) { try { bind(panel.parentNode); } catch (e) { /* not this binder */ } });
      if (!panel.dataset.collapseBound) {
        // The live binder was not reachable; fold locally with the same class.
        toggle.addEventListener('click', function () { var c = panel.classList.toggle('is-collapsed'); toggle.setAttribute('aria-expanded', String(!c)); });
      }
    });
  }

  // ---- P47 · Page-wide stale banner ------------------------------------------
  function p47Stale() {
    var stale = document.querySelector('[data-report-stale]');
    if (!stale && params.get('demo') !== 'stale') return;
    var block = document.querySelector('[data-sticky-block]') || document.querySelector('.ribbon'); if (!block) return;
    var banner = document.createElement('div'); banner.className = 'notice notice--warning v28-stale'; banner.setAttribute('role', 'status'); mark(banner, 'P47');
    banner.innerHTML = icon('alert-triangle') + '<span>Report preview is out of date: the Case has been edited since it was generated. Regenerate before sending.</span><a class="btn btn--small" href="#section-report">Go to Report</a>';
    block.appendChild(banner);
  }

  // ---- P51 · Original report, for an Audit Case ------------------------------
  // What an Audit audits: who wrote the original report, when, and what it
  // found. The firms are the ones Core's third-party report profiles know
  // (ThirdPartyReportProfiles.cs); the facts are the ones its extraction
  // records (ThirdPartyReportIdentity.Issuer, ReportDate;
  // ThirdPartyReportDamage.Roadworthiness, Outcome). Shown only on an Audit
  // Case (FRD-01: standalone, or linked by Create audit; every Audit is "a.").
  // The fixture Case is an Inspection, so ?demo=audit makes it read as one.
  var FIRMS = ['Laird Assessors', 'Exclusive Vehicle Assessors', 'Connexus Vehicle Assessors', 'Montgomery Assessors'];
  var ORIGINAL = { firm: '', date: '', road: '', outcome: '' };
  function p51OriginalReport() {
    var main = $('case-main'), nav = document.querySelector('[data-section-nav]'); if (!main || !nav) return;
    var after = $('section-claim') || $('section-overview'); if (!after) return;
    var section = document.createElement('section');
    section.className = 'record-section panel'; section.id = 'section-original-report'; section.setAttribute('data-section', 'original-report'); section.setAttribute('aria-labelledby', 'section-original-report-title'); mark(section, 'P51');
    var radios = function (name, options) {
      return '<div class="v28-radiorow" role="radiogroup" aria-label="' + name + '" data-original="' + name + '">' + [['', 'Not recorded']].concat(options).map(function (o, i) {
        return '<button type="button" class="v28-radio" role="radio" aria-checked="' + (i === 0) + '" tabindex="' + (i === 0 ? '0' : '-1') + '" data-value="' + o[0] + '"><span class="dot"></span>' + o[1] + '</button>';
      }).join('') + '</div>';
    };
    var body = editing
      ? '<div class="fg g4">'
        + '<div class="fc"><label for="v28-original-firm">Assessor</label><select id="v28-original-firm" class="fi" data-original="firm"><option value=""></option>' + FIRMS.map(function (f) { return '<option>' + f + '</option>'; }).join('') + '<option value="other">Other\u2026</option></select></div>'
        + '<div class="fc" data-original-other hidden><label for="v28-original-other">Assessor name</label><input id="v28-original-other" class="fi" data-original="other" maxlength="100" /></div>'
        + '<div class="fc"><label for="v28-original-date">Report date</label><input id="v28-original-date" class="fi" type="date" data-original="date" /></div>'
        + '<div class="fc"><label>Roadworthiness</label>' + radios('road', [['roadworthy', 'Roadworthy'], ['unroadworthy', 'Unroadworthy']]) + '</div>'
        + '<div class="fc span2"><label>Repairable status</label>' + radios('outcome', [['repairable', 'Repairable'], ['total_loss', 'Total loss'], ['cash_in_lieu', 'Cash in lieu'], ['contract_repair', 'Contract repair']]) + '</div>'
        + '</div>'
      : '<div class="fg g4">'
        + '<div class="fc ro"><span class="lbl">Assessor</span><div class="fv empty" data-original-read="firm">Not recorded</div></div>'
        + '<div class="fc ro"><span class="lbl">Report date</span><div class="fv empty" data-original-read="date">Not recorded</div></div>'
        + '<div class="fc ro"><span class="lbl">Roadworthiness</span><div class="fv empty" data-original-read="road">Not recorded</div></div>'
        + '<div class="fc ro"><span class="lbl">Repairable status</span><div class="fv empty" data-original-read="outcome">Not recorded</div></div>'
        + '</div>';
    section.innerHTML = '<div class="panel-head"><h2 id="section-original-report-title">Original report</h2></div><div class="panel-body stack">' + body + '</div>';
    after.parentNode.insertBefore(section, after.nextSibling);
    // A link in the section row, after Claim.
    var claimLink = nav.querySelector('[data-section-link="claim"]') || nav.querySelector('[data-section-link="overview"]');
    var link = claimLink.cloneNode(true);
    link.setAttribute('data-section-link', 'original-report'); link.removeAttribute('aria-current'); link.setAttribute('href', '#section-original-report'); link.setAttribute('aria-current', 'false');
    var span = link.querySelector('span'); if (span) span.textContent = 'Original report'; var use = link.querySelector('use'); if (use) use.setAttribute('href', '#icon-file-text');
    mark(link, 'P51'); claimLink.parentNode.insertBefore(link, claimLink.nextSibling);
    link.addEventListener('click', function (event) { event.preventDefault(); event.stopPropagation(); section.scrollIntoView({ block: 'start' }); });
    // Shown only for an Audit.
    var isAudit = function () { return /^Audit/.test(value('section-overview', 'Case type')); };
    var show = function () { var on = isAudit(); section.hidden = !on; link.hidden = !on; };
    document.addEventListener('change', show); show();
    // Editing: the radios drive their own value; every change is logged.
    var labelOf = function (name, value) { var b = section.querySelector('[data-original="' + name + '"] [data-value="' + value + '"]'); return b ? clean(b.textContent) : value; };
    section.addEventListener('click', function (event) {
      var button = event.target.closest('.v28-radio'); if (!button) return;
      var row = button.closest('.v28-radiorow'); $$('.v28-radio', row).forEach(function (b) { b.setAttribute('aria-checked', String(b === button)); b.setAttribute('tabindex', b === button ? '0' : '-1'); });
      ORIGINAL[row.getAttribute('data-original')] = button.getAttribute('data-value');
      log('Original report ' + (row.getAttribute('data-original') === 'road' ? 'roadworthiness' : 'repairable status') + ' recorded as ' + clean(button.textContent) + '.');
    });
    section.addEventListener('change', function (event) {
      var t = event.target, key = t.getAttribute('data-original'); if (!key) return;
      if (key === 'firm') { var other = section.querySelector('[data-original-other]'); other.hidden = t.value !== 'other'; ORIGINAL.firm = t.value === 'other' ? '' : t.value; if (t.value !== 'other') log('Original report assessor recorded as ' + t.value + '.'); }
      if (key === 'other') { ORIGINAL.firm = clean(t.value); log('Original report assessor recorded as ' + ORIGINAL.firm + '.'); }
      if (key === 'date') { ORIGINAL.date = t.value; log('Original report date recorded as ' + t.value + '.'); }
    });
    if (params.get('demo') === 'audit') {
      var type = cell('section-overview', 'Case type'); var typeValue = type && type.querySelector('.fv'); if (typeValue) typeValue.textContent = 'Audit';
      var reference = document.querySelector('.ribbon-value'); if (reference && !/^a\./.test(clean(reference.textContent))) reference.textContent = 'a.' + clean(reference.textContent);
      document.title = document.title.replace(/QDOS/, 'a.QDOS');
      show();
      document.documentElement.classList.add('v28-instant');
      [400, 700, 900, 1080].forEach(function (at) { setTimeout(function () { window.scrollTo({ top: section.getBoundingClientRect().top + window.scrollY - 200, behavior: 'auto' }); }, at); });
      // Example values so the section can be read; the fixture holds none.
      if (editing) {
        var firm = $('v28-original-firm'); firm.value = FIRMS[0];
        $('v28-original-date').value = '2031-04-10';
        ['road', 'unroadworthy', 'outcome', 'repairable'].forEach(function (v, i, a) { if (i % 2 === 0) { var b = section.querySelector('[data-original="' + v + '"] [data-value="' + a[i + 1] + '"]'); if (b) { var row = b.closest('.v28-radiorow'); $$('.v28-radio', row).forEach(function (x) { x.setAttribute('aria-checked', String(x === b)); x.setAttribute('tabindex', x === b ? '0' : '-1'); }); } } });
      } else {
        var fill = function (key, text) { var el = section.querySelector('[data-original-read="' + key + '"]'); el.textContent = text; el.classList.remove('empty'); };
        fill('firm', FIRMS[0]); fill('date', '10 Apr 2031'); fill('road', 'Unroadworthy'); fill('outcome', 'Repairable');
      }
    }
  }

  function run() {
    var record = document.querySelector('.case-record'); if (!record || !$('section-overview')) return;
    editing = record.classList.contains('is-editing');
    [['P44', p44Wire], ['P43', p43Versions], ['P30', p30Drag], ['P22', p22Attach], ['P30', reserveCell], ['P29', salvageNa], ['P37', p37OffPattern], ['P48', p48Materials],
      ['P38', p38Placements], ['P39', p39Signoff], ['P40', p40ReportDate], ['P41', p41Images], ['P50', p50OnePlace], ['P42', p42Produce], ['P45', p45Fold], ['P47', p47Stale], ['P51', p51OriginalReport]].forEach(function (proposal) {
      if (!on(proposal[0])) return;
      try { proposal[1](); } catch (error) { console.error('v28 proposal ' + proposal[0] + ' failed: ' + error.message); }
    });
    var demo = params.get('demo');
    if (demo === 'versions') openVersions();
    if (demo === 'produce') openPreview('report');
    if (demo === 'spec') openPreview('spec');
    if (demo === 'images') { var tab = $$('#section-files [role="tab"]').filter(function (t) { return /^Images/.test(clean(t.textContent)); })[0]; if (tab) tab.click(); document.documentElement.classList.add('v28-instant'); var link = document.querySelector('[data-section-link="files"]'); if (link) setTimeout(function () { link.click(); }, 200); [500, 700, 850, 1000, 1080].forEach(function (at) { setTimeout(function () { var grid = document.querySelector('#section-files [data-image-grid]'); if (grid) window.scrollTo({ top: grid.getBoundingClientRect().top + window.scrollY - 300, behavior: 'auto' }); }, at); }); }
  }
  // After proposals-record.js, which runs on a timeout after the document loads.
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { setTimeout(run, 10); }); else setTimeout(run, 10);
})();
