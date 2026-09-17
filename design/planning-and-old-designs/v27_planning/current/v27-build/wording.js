/* ---- report wording well (proposal `wording`, drawn on instruction) ----
   Every narrative block the report prints, in print order, composed from
   the recorded fields; while editing each block can be edited in place,
   renamed, removed, reordered, recomposed, and free paragraphs added. */
PROPOSALS.push(['wording', 'Report wording']);
S.p.wording = true;
var SALVAGE_TEXT = {
  A: 'Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category A (scrap only — the vehicle must be crushed in its entirety with no parts recovery). We suggest that the sale of the salvage will realise in the order of {s}. We have not taken any action towards removal of the salvage at this time.',
  B: 'Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category B (break for spare parts — the body shell must be crushed). We suggest that the sale of the salvage will realise in the order of {s}. We have not taken any action towards removal of the salvage at this time.',
  S: 'Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category S (structural damage) and can be sold as repairable salvage. Further information is available at www.abi.org.uk. We suggest that the sale of the salvage will realise in the order of {s}. We have not taken any action towards removal of the salvage at this time.',
  N: 'Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category N (non-structural damage) and can be sold as repairable salvage. Further information is available at www.abi.org.uk. We suggest that the sale of the salvage will realise in the order of {s}. We have not taken any action towards removal of the salvage at this time.'
};
function fieldValue(id, readSelector) { var el = $(id); if (!el) return ''; return F.editing ? el.value : (el.parentElement.querySelector('.fv') ? el.parentElement.querySelector('.fv').textContent.trim() : el.value); }
function switchOn(path) { var cb = document.querySelector('[data-report-switch="' + path + '"]'); return !!cb && cb.checked; }
function settlementText() {
  var outcome = $('f-assessment-outcome').value, salv = parseFloat($('f-assessment-salvage-value').value) || 0;
  if (F.empty) return '';
  if (outcome === 'total_loss') return 'We consider that an equitable settlement would be ' + money(EV - salv) + ', which represents the pre-accident engineer value of the vehicle of ' + money(EV) + ' less the value of the salvage of ' + money(salv) + '.';
  if (outcome === 'cash_in_lieu') return 'We recommend settlement by way of a cash in lieu payment based upon the estimated repair cost of ' + money(REPAIR) + '.';
  if (outcome === 'contract_repair') return 'A contract repair has been agreed for the total sum of ' + money(REPAIR) + '. Costs cannot increase above this figure.';
  return 'This vehicle is considered a repairable proposition and we have calculated a repair cost of ' + money(REPAIR) + '. We recommend a repair reserve of ' + money(Math.ceil(REPAIR / 50) * 50) + ' would be reasonable on this occasion.';
}
var WB = [
  { id: 'nature', t: 'Nature of incident', src: function () { return $('section-damage').querySelector('[data-damage-narrative]').textContent; }, always: true },
  { id: 'engcom', t: "Engineer's comments", src: function () { var bits = [q1('[data-composed="mileage"]') || MILEAGE_SENTENCE.lookup]; if ($('f-assessment-legal-status').value === 'unroadworthy') { var r = fieldValue('f-assessment-unroadworthy-reason').trim(); if (r) bits.push('Please note the vehicle is unroadworthy: ' + r.charAt(0).toLowerCase() + r.slice(1)); } return bits.join('\n\n'); }, always: true },
  { id: 'suppl', t: 'Supplementary damage', src: function () { return S.p.supp && UI.suppCompare && UI.suppPrint ? suppSentence() : ''; }, auto: true },
  { id: 'pavcom', t: 'PAV commentary', src: function () { return fieldValue('f-report-valuation-commentary-text'); }, opt: true, sw: 'report.valuation_commentary' },
  { id: 'unrel', t: 'Unrelated damage', src: function () { var d = fieldValue('edit-damage.unrelated').trim(); return d ? 'Unrelated pre-existing damage was noted: ' + d + '. This damage is inconsistent with the reported incident and has been disregarded for the purposes of this assessment.' : ''; }, opt: true, sw: 'report.include_unrelated_damage' },
  { id: 'hist', t: 'Vehicle history check', src: function () { return fieldValue('edit-vehicle-history'); }, always: true, pass: true },
  { id: 'precon', t: 'Pre-incident condition', src: function () { return q1('[data-composed="condition"]') || 'The vehicle is considered to be in average condition for its age and type.'; }, always: true },
  { id: 'settle', t: 'Settlement', src: settlementText, always: true },
  { id: 'salv', t: 'Salvage', src: function () { var c = $('f-assessment-category').value; return $('f-assessment-outcome').value === 'total_loss' && SALVAGE_TEXT[c] ? SALVAGE_TEXT[c].replace('{s}', money(parseFloat($('f-assessment-salvage-value').value) || 0)) : ''; }, vis: function () { return $('f-assessment-outcome').value === 'total_loss'; } }
];
function q1(sel) { var el = document.querySelector(sel); return el ? el.textContent : ''; }
var WBS = {}; WB.forEach(function (w) { WBS[w.id] = { edited: false, text: '', off: false, title: null }; });
var WB_CUSTOM = [], WB_SEQ = 1, WB_ORDER = WB.map(function (w) { return w.id; }), WB_DRAG = null;
function wbAll() { return WB.concat(WB_CUSTOM); }
function wbTitle(w) { return (WBS[w.id] && WBS[w.id].title) || w.t; }
function wbOn(w) { if (WBS[w.id].off) return false; if (w.custom) return true; if (w.vis) return w.vis(); if (w.always) return true; if (w.auto) return !!(S.p.supp && UI.suppCompare && UI.suppPrint); return switchOn(w.sw); }
function wbReset() { WBS = {}; WB.forEach(function (w) { WBS[w.id] = { edited: false, text: '', off: false, title: null }; }); WB_CUSTOM = []; WB_ORDER = WB.map(function (w) { return w.id; }); }
function renderWording() {
  var list = $('wbList'); if (!list) return;
  if (!S.p.wording) { list.innerHTML = ''; return; }
  var ordered = WB_ORDER.map(function (id) { return wbAll().filter(function (w) { return w.id === id; })[0]; }).filter(Boolean);
  var editing = F.editEng;
  list.innerHTML = ordered.filter(wbOn).map(function (w) {
    var st = WBS[w.id], txt = st.edited ? st.text : w.src();
    var chip = w.custom ? '<span class="src-tag">manual</span>' : st.edited ? '<span class="src-tag src-tag--warn">edited · no longer tracking fields</span>' : w.pass ? '<span class="src-tag">pass-through</span>' : w.auto ? '<span class="src-tag">composed · Estimate</span>' : '<span class="src-tag src-tag--lookup">composed · tracks fields</span>';
    if (!editing) return '<div class="fc ro wb-read"><span class="lbl">' + wbTitle(w) + ' ' + chip + '</span><div class="fv derived multi' + (txt ? '' : ' empty') + '">' + (txt || 'Not recorded') + '</div></div>';
    return '<div class="wb' + (st.edited ? ' edited' : '') + '" data-wb="' + w.id + '"><div class="wbh"><span class="grip" draggable="true" title="Drag to reorder">⠿</span><span class="wbt" contenteditable="true" spellcheck="false" data-wb-title="' + w.id + '">' + wbTitle(w) + '</span>' + chip + (st.edited && !w.custom ? '<a data-wb-recompose="' + w.id + '">Recompose from fields</a>' : '') + '<button type="button" class="wbx" data-wb-remove="' + w.id + '" aria-label="Remove ' + wbTitle(w) + '">×</button></div><textarea data-wb-text="' + w.id + '"' + (w.custom && !txt ? ' placeholder="Paragraph"' : '') + '>' + txt.replace(/</g, '&lt;') + '</textarea></div>';
  }).join('');
  $$('#wbList textarea').forEach(function (ta) { ta.style.height = 'auto'; ta.style.height = ta.scrollHeight + 'px'; });
  var add = $('wbAdd'); if (!add) return;
  if (!editing) { add.innerHTML = ''; return; }
  var back = WB.filter(function (w) { if (WBS[w.id].off) return w.vis ? w.vis() : true; return w.opt && !wbOn(w); });
  add.innerHTML = back.map(function (w) { return '<button type="button" class="btn btn--small" data-wb-add="' + w.id + '">+ ' + wbTitle(w) + '</button>'; }).join('') + '<button type="button" class="btn btn--small" data-wb-new>+ New paragraph</button>';
}
document.addEventListener('click', function (e) {
  var t = e.target, b;
  if ((b = t.closest('[data-wb-remove]'))) { var id = b.getAttribute('data-wb-remove'), w = wbAll().filter(function (x) { return x.id === id; })[0]; if (w.custom) { WB_CUSTOM = WB_CUSTOM.filter(function (x) { return x.id !== id; }); delete WBS[id]; WB_ORDER = WB_ORDER.filter(function (x) { return x !== id; }); } else if (w.sw) { var cb = document.querySelector('[data-report-switch="' + w.sw + '"]'); if (cb) { cb.checked = false; cb.dispatchEvent(new Event('change', { bubbles: true })); } } else WBS[id].off = true; UI.dirty = true; renderWording(); return; }
  if ((b = t.closest('[data-wb-add]'))) { var id2 = b.getAttribute('data-wb-add'), w2 = WB.filter(function (x) { return x.id === id2; })[0]; WBS[id2].off = false; if (w2.sw) { var cb2 = document.querySelector('[data-report-switch="' + w2.sw + '"]'); if (cb2) { cb2.checked = true; cb2.dispatchEvent(new Event('change', { bubbles: true })); } } UI.dirty = true; renderWording(); return; }
  if ((b = t.closest('[data-wb-new]'))) { var nid = 'c' + (WB_SEQ++); WB_CUSTOM.push({ id: nid, t: 'New paragraph', custom: true }); WB_ORDER.push(nid); WBS[nid] = { edited: true, text: '', off: false, title: null }; UI.dirty = true; renderWording(); var last = $$('#wbList .wbt').pop(); if (last) last.focus(); return; }
  if ((b = t.closest('[data-wb-recompose]'))) { var rid = b.getAttribute('data-wb-recompose'); WBS[rid] = { edited: false, text: '', off: false, title: WBS[rid].title }; UI.dirty = true; renderWording(); return; }
}, true);
document.addEventListener('input', function (e) {
  var t = e.target;
  if (t.matches('[data-wb-text]')) { var id = t.getAttribute('data-wb-text'), w = wbAll().filter(function (x) { return x.id === id; })[0]; t.style.height = 'auto'; t.style.height = t.scrollHeight + 'px'; if (w.custom) { WBS[id].text = t.value; UI.dirty = true; return; } var live = w.src(); WBS[id].edited = t.value !== live; WBS[id].text = t.value; var box = t.closest('.wb'); box.classList.toggle('edited', WBS[id].edited); var head = box.querySelector('.wbh'); var chipEl = head.querySelector('.src-tag'); chipEl.className = WBS[id].edited ? 'src-tag src-tag--warn' : (w.pass ? 'src-tag' : 'src-tag src-tag--lookup'); chipEl.textContent = WBS[id].edited ? 'edited · no longer tracking fields' : (w.pass ? 'pass-through' : w.auto ? 'composed · Estimate' : 'composed · tracks fields'); var link = head.querySelector('[data-wb-recompose]'); if (WBS[id].edited && !link) { link = document.createElement('a'); link.setAttribute('data-wb-recompose', id); link.textContent = 'Recompose from fields'; head.insertBefore(link, head.querySelector('.wbx')); } else if (!WBS[id].edited && link) link.remove(); UI.dirty = true; return; }
  if (S.p.wording && t.closest('#case-record') && !t.closest('#wbList')) renderWording();
});
document.addEventListener('change', function (e) { if (S.p.wording && e.target.closest('#case-record') && !e.target.closest('#wbList')) renderWording(); });
document.addEventListener('focusout', function (e) {
  var t = e.target; if (!t.matches || !t.matches('[data-wb-title]')) return;
  var id = t.getAttribute('data-wb-title'), w = wbAll().filter(function (x) { return x.id === id; })[0], clean = (t.textContent || '').trim().replace(/\s+/g, ' ');
  if (!clean) { renderWording(); return; }
  if (w.custom) w.t = clean; else WBS[id].title = clean === w.t ? null : clean;
  UI.dirty = true;
});
document.addEventListener('keydown', function (e) { if (e.key === 'Enter' && e.target.matches && e.target.matches('[data-wb-title]')) { e.preventDefault(); e.target.blur(); } });
document.addEventListener('dragstart', function (e) { var g = e.target.closest && e.target.closest('.wb .grip'); if (!g) return; WB_DRAG = g.closest('.wb').getAttribute('data-wb'); g.closest('.wb').classList.add('dragging'); e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', WB_DRAG); });
document.addEventListener('dragend', function () { WB_DRAG = null; $$('.wb.dragging,.wb.dropmark').forEach(function (el) { el.classList.remove('dragging', 'dropmark'); }); });
document.addEventListener('dragover', function (e) { var box = e.target.closest && e.target.closest('.wb'); if (!WB_DRAG || !box || box.getAttribute('data-wb') === WB_DRAG) return; e.preventDefault(); $$('.wb.dropmark').forEach(function (el) { el.classList.remove('dropmark'); }); box.classList.add('dropmark'); });
document.addEventListener('drop', function (e) { var box = e.target.closest && e.target.closest('.wb'); if (!WB_DRAG || !box) return; e.preventDefault(); var target = box.getAttribute('data-wb'); if (target === WB_DRAG) return; WB_ORDER = WB_ORDER.filter(function (x) { return x !== WB_DRAG; }); WB_ORDER.splice(WB_ORDER.indexOf(target), 0, WB_DRAG); WB_DRAG = null; UI.dirty = true; renderWording(); });
var WORDING_API = { order: function () { return WB_ORDER; }, state: function () { return WBS; }, moveBefore: function (a, b) { WB_ORDER = WB_ORDER.filter(function (x) { return x !== a; }); WB_ORDER.splice(WB_ORDER.indexOf(b), 0, a); renderWording(); }, reset: wbReset };
