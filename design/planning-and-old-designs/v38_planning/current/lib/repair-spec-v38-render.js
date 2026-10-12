// v38 mockup only: renders the Repair Spec section body and its dialogs
// from the fixture, as the live _CaseEstimate.cshtml and _RepairSpecDiff.cshtml
// lay them out (the same classes, the same label strings), with each
// proposal as a rendering branch that marks what it changed with
// data-v38-change. Every word is the application's own label or fixture.
(() => {
  'use strict';

  const M = window.v38Model;
  const esc = (s) => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  const icon = (id) => `<svg class="icon" aria-hidden="true"><use href="#${id}"></use></svg>`;
  const money = M.money;
  const ABSENT = 'Not recorded';
  const fixed = (v, d = 2) => (v == null || v === '' ? '' : Number(v).toFixed(d));
  const hoursText = (v) => (v == null ? '—' : Number(v).toFixed(1));
  const mark = (id, on) => (on ? ` data-v38-change="${id}"` : '');

  // ---- helpers over the state ------------------------------------------------
  function versionsOf(spec) { return spec.versions; }
  function lastSent(specs) {
    for (const s of specs) for (const v of [...s.versions].reverse()) if (v.sent) return { spec: s, version: v };
    return null;
  }
  // A comparable: { key, label, lines, header, rate }.
  function comparables(state, selected) {
    const list = [];
    const push = (spec, version) => {
      const header = version ? { ...spec.header, rate: version.rate, uplift: false } : spec.header;
      const lines = version ? version.lines : spec.lines;
      const gross = M.totals(lines, header).printed.gross;
      const own = spec === selected;
      const key = version ? `${spec.id}:v${version.n}` : `${spec.id}:working`;
      const label = version
        ? `${own ? '' : spec.header.name + ' · '}v${version.n} · ${version.when} · ${money(gross)}${version.sent ? ' · Sent on report' : ''}`
        : `${own ? 'Working' : spec.header.name} · now · ${money(gross)}`;
      list.push({ key, label, lines, header, gross, spec, version });
    };
    push(selected, null);
    for (const v of [...selected.versions].reverse()) push(selected, v);
    for (const s of state.specs) if (s !== selected) { push(s, null); for (const v of [...s.versions].reverse()) push(s, v); }
    return list;
  }
  function specComparables(state) {
    return state.specs.map((s) => ({ key: `${s.id}:working`, label: s.header.name, lines: s.lines, header: s.header, gross: M.totals(s.lines, s.header).printed.gross, spec: s, version: null }));
  }

  // ---- the section body ---------------------------------------------------------
  function render(state) {
    const spec = state.specs[state.spec];
    const editing = state.mode === 'edit';
    const P = state.p; // { cols, cmp, supp, agreed, rate } proposal switches
    const h = state.preview ? state.preview.header : spec.header;
    const lines = state.preview ? state.preview.lines : spec.lines;
    const t = M.totals(lines, h);
    const printed = t.printed;
    const rate = t.rate;
    const anomalies = new Set(t.anomalies.map((a) => `${a.position}:${a.field}`));
    const others = state.specs.filter((s) => s !== spec);
    const ev = M.ENGINEER_VALUE;
    const specPercent = Math.max(1, M.percentOfValue(ev, printed.gross));
    const outcomeIsContract = state.contract;
    const sum = state.sum;
    const contractPercent = outcomeIsContract && sum > 0 ? sum / ev * 100 : null;
    const suggested = contractPercent != null && contractPercent >= 1 && contractPercent <= specPercent ? Math.round(contractPercent * 10) / 10 : specPercent;
    const rateInHead = P.rate !== 'bar';
    const rateInBar = P.rate === 'bar' || P.rate === 'both';

    const tabs = state.specs.map((s, i) => `<a class="estimate-tab" role="tab" href="#" aria-selected="${i === state.spec}" data-estimate-tab="${s.id}" data-spec-index="${i}"><span data-estimate-tab-label>${esc(s.header.name)}</span><span class="src-tag">${esc(M.ROUTE_TAG[s.route])}</span><span class="status status--plain ${s.current ? 'status--green' : 'status--amber'}">${s.current ? 'Current' : 'Draft'}</span></a>`).join('');
    const origin = `Populated ${spec.createdAt} by ${spec.createdBy}, ${M.ROUTE_WORDS[spec.route]} · ${spec.lines.length === 1 ? '1 line' : spec.lines.length + ' lines'} · labour rate ${money(M.effectiveRate(spec.header))}/h`;

    let actions = '';
    if (editing) {
      actions = `<div class="est-actions" data-estimate-actions>${spec.current ? '' : `<form method="post" action="#"><button type="submit" class="btn btn--small btn--primary" data-estimate-use>${icon('icon-check')}<span>Use repair spec</span></button></form>`}<form method="post" action="#"><button type="submit" class="btn btn--small" data-estimate-duplicate>${icon('icon-copy')}<span>Duplicate</span></button></form>${spec.current ? '' : `<a class="btn btn--small btn--danger" href="#" data-dialog-open="delete-estimate-dialog" data-estimate-discard>${icon('icon-trash-2')}<span>Discard</span></a>`}</div>`;
    }

    // Head cells. The rate control: P5 moves it to a bar under the grid.
    // The fixture's addresses are Liverpool, so no postcode is in scope and the chip reads the rule's name, as live does.
    const upliftChip = `<span class="src-tag" data-estimate-uplift-chip>London &amp; Home Counties</span>`;
    const rateEdit = `<div class="rate-pair" data-estimate-rate-pair><select class="fi" name="selectedRateCard" form="case-edit-form" aria-label="Labour-rate card" data-rate-card><option value="">Keep entered rate</option><option value="std" data-rate="83.28"${h.rateCard ? ' selected' : ''}>Standard — £83.28/h</option><option value="prestige" data-rate="103.06">Prestige and aluminium — £103.06/h</option></select><input class="fi mono" name="estimateLabourRate" form="case-edit-form" type="number" min="0" step="0.01" inputmode="decimal" value="${fixed(h.rate)}" data-rate-input aria-label="Labour rate (£/h)" /></div>`;
    const rateRead = `<div class="rate-pair" data-estimate-rate-pair><div class="fv">${h.rateCard ? esc(h.rateCard) : 'Keep entered rate'}</div><div class="fv mono">${money(h.rate)}</div></div>`;
    const upliftEdit = `<div class="uplift" data-estimate-uplift><label class="choice"><input type="checkbox" name="estimateRegionalUplift" form="case-edit-form" value="true"${h.uplift ? ' checked' : ''} data-uplift /><span>+ 15 %</span></label>${upliftChip}</div>`;
    const upliftRead = `<div class="uplift" data-estimate-uplift>${staticChoice(h.uplift, '+ 15 %')}${upliftChip}</div>`;
    const rateCells = editing
      ? `<div class="fc"><label for="estimate-labour-rate">Labour rate (£/h)</label>${rateEdit}</div><div class="fc"><label for="estimate-regional-uplift">Regional uplift</label>${upliftEdit}</div>`
      : `<div class="fc ro"><span class="lbl">Labour rate (£/h)</span>${rateRead}</div><div class="fc ro"><span class="lbl">Regional uplift</span>${upliftRead}</div>`;
    const headCells = editing
      ? `<div class="fc" data-estimate-name-cell><label for="estimate-name">Repair spec name</label><input id="estimate-name" class="fi" name="estimateName" form="case-edit-form" maxlength="100" required value="${esc(h.name)}" /></div>${rateInHead ? rateCells : ''}<div class="fc"><label for="estimate-other">Other costs (£)</label><input id="estimate-other" class="fi mono" name="estimateOtherCosts" form="case-edit-form" type="number" min="0" step="0.01" inputmode="decimal" value="${fixed(h.other)}" /></div><div class="fc"><label for="estimate-vat">VAT %</label><input id="estimate-vat" class="fi mono" name="estimateVatPercent" form="case-edit-form" type="number" min="0" max="100" step="0.01" inputmode="decimal" value="${h.vatPercent}" /></div><div class="fc"><label for="estimate-vat-status">Repairer VAT status</label><select id="estimate-vat-status" class="fi" name="estimateVatStatus" form="case-edit-form" data-vat-status><option${h.repairerStatus === 'Unknown' ? ' selected' : ''}>Unknown</option><option${h.repairerStatus === 'Registered' ? ' selected' : ''}>Registered</option><option${h.repairerStatus === 'NotRegistered' ? ' selected' : ''}>Not registered</option></select></div>`
      : `<div class="fc ro" data-estimate-name-cell><span class="lbl">Repair spec name</span><div class="fv">${esc(h.name)}</div></div>${rateInHead ? rateCells : ''}<div class="fc ro"><span class="lbl">Other costs (£)</span><div class="${h.other == null ? 'fv mono empty' : 'fv mono'}">${h.other == null ? ABSENT : money(h.other)}</div></div><div class="fc ro"><span class="lbl">VAT %</span><div class="fv mono">${h.vatPercent} %</div></div><div class="fc ro"><span class="lbl">Repairer VAT status</span><div class="fv">${h.repairerStatus === 'NotRegistered' ? 'Not registered' : h.repairerStatus}</div></div>`;
    const head = `<div class="est-head"${mark('P5', P.rate === 'bar')}><div class="fg g4">${headCells}</div></div>`;

    // The grid.
    const ops = M.OPERATIONS;
    const opSelect = (i, value) => `<select id="line-operation-${i}" name="lineOperation" form="case-edit-form" data-line="${i}" data-field="t">${ops.map(([v, l]) => `<option value="${v}"${v === value ? ' selected' : ''}>${l}</option>`).join('')}</select>`;
    const colsOf = (type) => (P.cols === 'dash' ? M.COLS[type] || M.COLS.Other : null);
    const numCell = (i, line, key, name, step, cls, label) => {
      const pos = i + 1;
      const viol = anomalies.has(`${pos}:${key}`);
      const avail = colsOf(line.t);
      const value = line[key];
      const has = value != null && value !== '' && Number(value) !== 0;
      const prev = state.preview && line.scaled && (key === 'u' || key === 'm') && has;
      if (avail && !avail[key] && !has) return `<td class="num na"${mark('P1', true)}>—</td>`;
      if (editing) {
        return `<td class="num${avail && !avail[key] ? ' v38-off' : ''}"${mark('P1', avail && !avail[key])}><label class="sr-only" for="line-${key}-${i}">${label} ${pos}</label><input id="line-${key}-${i}" class="num${viol ? ' viol' : ''}${prev ? ' is-previewed' : ''}" name="${name}" form="case-edit-form" type="number" min="0" step="${step}" inputmode="decimal" value="${has ? fixed(value, key === 'h' || key === 'ph' ? 1 : (key === 'q' ? 0 : 2)) : ''}"${viol ? ' title="Off-pattern"' : ''}${prev ? ' readonly' : ''} data-line="${i}" data-field="${key}" /></td>`;
      }
      const shown = !has ? '—' : key === 'q' ? String(value) : key === 'h' || key === 'ph' ? hoursText(value) : money(Number(value));
      return `<td class="num"${mark('P1', avail && !avail[key])}><span class="gv${viol ? ' viol' : ''}"${viol ? ' title="Off-pattern"' : ''}>${shown}</span></td>`;
    };
    const partCell = (i, line) => {
      const avail = colsOf(line.t);
      if (avail && !avail.p && !line.p) return `<td class="na"${mark('P1', true)}>—</td>`;
      return editing
        ? `<td><label class="sr-only" for="line-part-${i}">Part number ${i + 1}</label><input id="line-part-${i}" class="mono" name="linePartNumber" form="case-edit-form" value="${esc(line.p)}" data-line="${i}" data-field="p" /></td>`
        : `<td><span class="gv mono">${line.p ? esc(line.p) : '—'}</span></td>`;
    };
    const sourceTag = (line) => {
      const word = line.src === 'amended' ? 'amended' : line.src === 'manual' ? 'manual' : `imported · ${M.SOURCE_SHORT[spec.route] || 'AX'}`;
      return `<span class="src-tag${line.src === 'amended' ? ' src-tag--ai' : ''}">${word}</span>`;
    };
    const rows = lines.map((line, i) => {
      const cells = `${partCell(i, line)}${numCell(i, line, 'q', 'lineQuantity', '1', '', 'Quantity')}${numCell(i, line, 'u', 'linePartPounds', '0.01', '', 'Part amount')}${numCell(i, line, 'h', 'lineLabourHours', '0.1', '', 'Labour hours')}${numCell(i, line, 'ph', 'linePaintHours', '0.1', '', 'Paint hours')}${numCell(i, line, 'm', 'lineMaterials', '0.01', '', 'Material (£)')}`;
      if (editing) {
        return `<tr data-estimate-line><td><input type="hidden" name="lineId" value="line-${i}" form="case-edit-form" /><label class="sr-only" for="line-operation-${i}">Operation ${i + 1}</label>${opSelect(i, line.t)}</td><td><label class="sr-only" for="line-description-${i}">Description ${i + 1}</label><input id="line-description-${i}" name="lineDescription" form="case-edit-form" value="${esc(line.d)}" data-line="${i}" data-field="d" /></td>${cells}<td>${sourceTag(line)}</td><td><button type="submit" class="del" name="removeLine" value="${i}" form="case-edit-form" aria-label="Remove line ${i + 1}" data-remove-line="${i}">×</button></td></tr>`;
      }
      return `<tr data-estimate-line><td><span class="gv">${M.WORD[line.t]}</span></td><td><span class="gv">${esc(line.d)}</span></td>${cells}<td>${sourceTag(line)}</td><td></td></tr>`;
    }).join('');
    const phantom = editing ? `<tr class="phantom" data-estimate-phantom><td><input type="hidden" name="lineId" value="" form="case-edit-form" /><select name="lineOperation" form="case-edit-form" aria-label="Operation" data-phantom>${ops.map(([v, l]) => `<option value="${v}">${l}</option>`).join('')}</select></td><td><input name="lineDescription" form="case-edit-form" placeholder="Add a line" aria-label="Description" data-phantom /></td><td><input class="mono" name="linePartNumber" form="case-edit-form" aria-label="Part number" /></td><td class="num"><input class="num" name="lineQuantity" form="case-edit-form" type="number" min="1" step="1" inputmode="numeric" aria-label="Quantity" /></td><td class="num"><input class="num" name="linePartPounds" form="case-edit-form" type="number" min="0" step="0.01" inputmode="decimal" aria-label="Part amount" /></td><td class="num"><input class="num" name="lineLabourHours" form="case-edit-form" type="number" min="0" step="0.1" inputmode="decimal" aria-label="Labour hours" /></td><td class="num"><input class="num" name="linePaintHours" form="case-edit-form" type="number" min="0" step="0.1" inputmode="decimal" aria-label="Paint hours" /></td><td class="num"><input class="num" name="lineMaterials" form="case-edit-form" type="number" min="0" step="0.01" inputmode="decimal" aria-label="Material (£)" /></td><td><span class="src-tag">manual</span></td><td><button type="submit" class="del" name="removeLine" value="" form="case-edit-form" aria-label="Remove this line">×</button></td></tr>` : '';
    const empty = !editing && lines.length === 0 ? `<tr><td colspan="10" class="muted">None recorded</td></tr>` : '';
    const grid = `<div class="grid" data-estimate-grid${mark('P1', P.cols === 'dash')}><table><thead><tr><th scope="col" class="col-type">Type</th><th scope="col">Description</th><th scope="col" class="col-part">Part no.</th><th scope="col" class="num col-qty">Qty</th><th scope="col" class="num col-money">Unit (£)</th><th scope="col" class="num col-hours">Hours</th><th scope="col" class="num col-hours">Paint h</th><th scope="col" class="num col-money">Material (£)</th><th scope="col" class="col-source">Source</th><th scope="col" class="col-del"><span class="sr-only">Action</span></th></tr></thead><tbody data-estimate-grid-body>${rows}${empty}${phantom}</tbody></table></div>`;
    const addLine = editing ? `<div class="cluster est-add-line"><button type="button" class="btn btn--small btn--danger" data-estimate-delete-all data-dialog-open="delete-lines-dialog">${icon('icon-trash-2')}<span>Delete all lines</span></button><button type="button" class="btn btn--small" data-estimate-add-line>${icon('icon-plus')}<span>Add line</span></button></div>` : '';

    // P5 · the labour rate as a bar under the grid.
    const rateBar = rateInBar ? `<div class="bar est-rate v38-rate-bar" data-estimate-rate-bar${mark('P5', true)}><span class="bl">Labour rate (£/h)</span>${editing ? rateEdit : rateRead}<span class="bl v38-bl-2">Regional uplift</span>${editing ? upliftEdit : upliftRead}</div>` : '';

    // Target % of value (edit only; reading shows it only when scaled).
    let scaleBar = '';
    const pv = state.preview;
    const readout = pv ? `${money(pv.grossBefore)} → ${money(pv.grossAfter)} (${(pv.grossAfter / ev * 100).toFixed(1)}%) · prices ×${pv.priceFactor.toFixed(2)}${pv.atFloor ? ' · labour at floor' : ''}` : '';
    const sliderValue = pv ? (pv.grossAfter / ev * 100).toFixed(1) : suggested.toFixed(1);
    if (editing) {
      scaleBar = `<div class="bar est-scale" data-estimate-scale><span class="bl">Target % of value</span>${state.scaled ? '<span class="status status--amber status--plain" data-estimate-scaled>Scaled</span>' : ''}<input type="range" min="1" max="${specPercent.toFixed(1)}" step="0.1" value="${sliderValue}" aria-label="Target % of value" data-scale-range /><span class="dsc"><input type="number" name="targetPercent" min="1" max="${specPercent.toFixed(1)}" step="0.1" value="${sliderValue}" inputmode="decimal" aria-label="Target % of value" data-scale-percent /><label>%</label></span><span class="mono" data-scale-read aria-live="polite">${readout}</span><span class="status status--navy status--plain" data-scale-preview${pv ? '' : ' hidden'}>Preview</span><span class="est-floors">Floors <span class="dsc"><label for="estimate-floor-rate">labour £/h</label><input id="estimate-floor-rate" type="number" min="0" step="1" inputmode="decimal" value="${M.FLOORS.rate}" data-scale-floor-rate /></span><span class="dsc"><label for="estimate-floor-price">prices %</label><input id="estimate-floor-price" type="number" min="0" max="100" step="5" inputmode="decimal" value="${M.FLOORS.price}" data-scale-floor-price /></span></span>${contractPercent != null && contractPercent >= 1 && contractPercent <= specPercent ? `<span class="muted">Agreed sum suggests ${suggested.toFixed(1)}%.</span>` : contractPercent > specPercent ? '<span class="muted">Scaling cannot raise the repair spec to the agreed sum.</span>' : ''}<button type="button" class="btn btn--small btn--primary" data-scale-apply><span>Apply</span></button>${state.scaled ? '<button type="button" class="btn btn--small" data-scale-remove><span>Remove scaling</span></button>' : ''}</div>`;
    } else if (state.scaled) {
      scaleBar = `<div class="bar est-scale" data-estimate-scale-read><span class="bl">Target % of value</span><span class="status status--amber status--plain" data-estimate-scaled>Scaled</span></div>`;
    }

    // Contract repair (P4: the agreed sum drives the spec).
    const mismatch = outcomeIsContract && sum > 0 && Math.abs(printed.gross - sum) > 0.5;
    const mismatchText = printed.gross > sum ? `Spec ${money(printed.gross)} above agreed` : `Spec ${money(printed.gross)} below agreed`;
    const sumBox = editing && P.agreed !== 'today'
      ? `<span class="dsc"${mark('P4', true)}><label for="estimate-contract-sum">agreed total sum £</label><input id="estimate-contract-sum" type="number" min="0" step="0.01" inputmode="decimal" value="${sum > 0 ? fixed(sum) : ''}" data-contract-sum${outcomeIsContract ? '' : ' disabled'} /></span>`
      : `<span class="dsc"><span class="lbl">agreed total sum £</span><b class="mono" data-contract-sum-read>${sum > 0 ? money(sum) : ABSENT}</b></span>`;
    let mismatchChip = '';
    if (mismatch) {
      mismatchChip = editing && P.agreed === 'button'
        ? `<button type="button" class="btn btn--small" data-contract-rescale${mark('P4', true)}>${icon('icon-refresh-cw')}<span>Rescale to agreed sum</span></button><span class="status status--amber status--plain" data-contract-mismatch>${mismatchText}</span>`
        : `<span class="status status--amber status--plain" data-contract-mismatch>${mismatchText}</span>`;
    }
    const contractBar = `<div class="bar est-contract" data-estimate-contract><span class="bl">Contract repair</span>${editing ? `<label class="choice"><input type="checkbox"${outcomeIsContract ? ' checked' : ''} data-contract-agreed /><span>Contract repair agreed</span></label>` : staticChoice(outcomeIsContract, 'Contract repair agreed')}${sumBox}${mismatchChip}${outcomeIsContract && sum > 0 ? `<div class="fc ro est-contract-sentence"><span class="lbl">Contract repair</span><div class="fv derived" data-contract-sentence>${esc(M.contractSentence(sum))}</div></div>` : ''}</div>`;

    // Discounts and VAT.
    const d = h.discounts;
    const dsc = (key, label, id) => editing
      ? `<span class="dsc"><label for="estimate-discount-${id}">${label}</label><input id="estimate-discount-${id}" form="case-edit-form" type="number" min="0" max="100" step="0.01" inputmode="decimal" value="${d[key]}" data-discount="${key}" /></span>`
      : `<span class="dsc"><span class="dsl">${label}</span><span class="gv">${d[key]}%</span></span>`;
    const discounts = `<div class="bar" data-estimate-discounts><span class="bl">Discounts %</span>${dsc('Parts', 'Parts', 'parts')}${dsc('Materials', 'Materials', 'materials')}${dsc('Specialist', 'Specialist', 'specialist')}${dsc('Overall', 'Overall', 'overall')}</div>`;
    const cats = ['Labour', 'Parts', 'Materials', 'Specialist'];
    const vatBar = `<div class="bar" data-estimate-vat><span class="bl">VAT charged on</span>${cats.map((c) => editing ? `<label class="choice"><input type="checkbox" value="true" data-vat-category="${c}"${h.vat[c] ? ' checked' : ''} /><span>${c}</span></label>` : staticChoice(h.vat[c], c)).join('')}<span class="status status--amber status--plain" data-vat-overridden${h.vatOverridden ? '' : ' hidden'}>Overridden</span>${editing ? `<button type="button" class="btn btn--small vat-reset" data-vat-reset${h.vatOverridden ? '' : ' hidden'}>Reset to repairer status</button>` : ''}</div>`;

    // Supplementary (P3: automatic after an import).
    let supp = '';
    if (others.length > 0) {
      const auto = P.supp === 'auto';
      const options = auto ? comparables(state, spec).filter((c) => c.key !== `${spec.id}:working`) : specComparables(state).filter((c) => c.spec !== spec);
      const chosen = state.suppOf ? options.find((c) => c.key === state.suppOf) : null;
      const reason = state.suppReason || 'estimate';
      // The panel reads the recorded spec; a scaling preview changes nothing here until Apply.
      const suppDiff = chosen ? M.diff(chosen.lines, chosen.header, spec.lines, spec.header) : null;
      const diffHtml = suppDiff ? renderDiff(suppDiff) : '';
      const stmt = suppDiff ? M.statement(suppDiff, reason) : '';
      const dismiss = auto && editing ? `<button type="button" class="btn btn--small" data-supp-dismiss${mark('P3', true)}>${icon('icon-x')}<span>Dismiss</span></button>` : '';
      const fields = editing
        ? `<div class="fc"><label for="estimate-supplementary-of">Changes vs</label><select id="estimate-supplementary-of" class="fi" name="supplementaryOf" form="case-edit-form" data-supp-of${mark('P3', auto)}><option value=""${chosen ? '' : ' selected'}>Choose…</option>${options.map((c) => `<option value="${c.key}"${chosen && chosen.key === c.key ? ' selected' : ''}>${esc(c.label)}</option>`).join('')}</select></div><div class="fc"><label for="estimate-supplementary-reason">Reason</label><select id="estimate-supplementary-reason" class="fi" name="supplementaryReason" form="case-edit-form" data-supp-reason>${M.REASONS.map(([code, label]) => `<option value="${code}"${reason === code ? ' selected' : ''}>${label}</option>`).join('')}</select></div><div class="fc"><span class="lbl">Report</span><label class="choice"><input type="checkbox" name="supplementaryExplain" form="case-edit-form" value="true"${state.suppExplain ? ' checked' : ''} data-supp-explain /><span>Explain the change on the report</span></label></div>`
        : `<div class="fc ro"><span class="lbl">Changes vs</span><div class="${chosen ? 'fv' : 'fv empty'}"${mark('P3', auto && !!chosen)}>${chosen ? esc(chosen.label) : ABSENT}</div></div><div class="fc ro"><span class="lbl">Reason</span><div class="${chosen ? 'fv' : 'fv empty'}">${chosen ? M.REASONS.find((r) => r[0] === reason)[1] : ABSENT}</div></div><div class="fc ro"><span class="lbl">Report</span>${staticChoice(!!state.suppExplain, 'Explain the change on the report')}</div>`;
      if (!(auto && state.suppDismissed)) {
        supp = `<div class="sub-panel" data-estimate-supplementary${mark('P3', auto && !!chosen)}><h3>Supplementary${dismiss ? `<span class="rt v38-supp-tools">${dismiss}</span>` : ''}</h3><div class="fg g4">${fields}</div>${chosen ? `${diffHtml}<div class="fc ro"><span class="lbl">Supplementary damage</span><div class="fv derived multi" data-supplementary-statement>${esc(stmt)}</div></div>` : ''}</div>`;
      }
    }

    // Work-lists and the rollup (RenderBottom).
    const newParts = lines.filter((l) => l.t === 'Replace').map((l) => l.d);
    const repairs = lines.filter((l) => l.t === 'Repair' || l.t === 'RemoveAndRefit').map((l) => l.d);
    const operations = lines.filter((l) => ['Paint', 'Blend', 'Specialist', 'Other'].includes(l.t)).map((l) => l.d);
    const list = (title, items) => `<div class="wcol"><div class="wt">${title}</div><ul>${items.length ? items.map((x) => `<li>${esc(x)}</li>`).join('') : '<li class="muted">none</li>'}</ul></div>`;
    const vatOn = cats.filter((c) => h.vat[c]).map((c) => c.toLowerCase());
    const dRow = (key, label) => d[key] > 0 ? `<div class="rr"><dt>${label}</dt><dd>−${d[key]}%</dd></div>` : '';
    const bottom = `<div class="est-bottom"><div class="wl" data-estimate-worklists>${list('Main new parts required', newParts)}${list('Repairs required', repairs)}${list('Additional operations', operations)}</div><dl class="rollup" data-estimate-rollup><div class="rr"><dt>Parts</dt><dd data-rollup="parts">${money(printed.parts)}</dd></div><div class="rr"><dt>Panel labour</dt><dd data-rollup="panelLabour">${money(printed.panelLabour)}</dd></div><div class="rr"><dt>Paint labour</dt><dd data-rollup="paintLabour">${money(printed.paintLabour)}</dd></div><div class="rr"><dt>Materials</dt><dd data-rollup="materials">${money(printed.materials)}</dd></div><div class="rr"><dt>Specialist</dt><dd data-rollup="specialist">${money(printed.specialist)}</dd></div>${dRow('Parts', 'Parts discount')}${dRow('Materials', 'Materials discount')}${dRow('Specialist', 'Specialist discount')}${dRow('Overall', 'Overall discount')}${t.offPattern > 0 ? `<div class="rr" data-estimate-off-pattern><dt>Off-pattern items (treated as specialist)</dt><dd data-rollup="offPattern">${money(t.offPattern)}</dd></div>` : ''}<div class="rr"><dt>Net</dt><dd data-rollup="net">${money(printed.net)}</dd></div><div class="rr"><dt>VAT ${h.vatPercent} % on ${vatOn.length ? vatOn.join(', ') : 'nothing'}</dt><dd data-rollup="vat">${money(printed.vat)}</dd></div><div class="rr tot"><dt>Repair cost inc VAT${spec.current ? '' : ' · draft'}</dt><dd data-estimate-gross data-rollup="gross">${money(printed.gross)}</dd></div></dl></div>`;

    const body = `${head}${grid}${addLine}${rateBar}${scaleBar}${contractBar}${discounts}${vatBar}${supp}${bottom}`;
    const wrap = editing ? `<div class="stack" data-estimate-form><input type="hidden" name="estimateId" value="${spec.id}" form="case-edit-form" />${body}</div>` : `<div class="stack" data-estimate-read>${body}</div>`;

    return {
      body: `<div data-glass-controls="outcome" hidden></div><div class="estimate-tabs" role="tablist" aria-label="Repair specs" data-estimate-tabs>${tabs}</div><p class="muted est-origin" data-estimate-origin>${esc(origin)}</p><div data-glass-controls="session" hidden></div>${actions}${wrap}`,
      meta: `${M.ROUTE_WORDS[spec.route]} · ${spec.createdAt} · ${spec.lines.length === 1 ? '1 line' : spec.lines.length + ' lines'}`,
      gross: printed.gross,
      dialogs: `${renderCompare(state, spec)}${renderVersions(state, spec, editing)}${editing ? renderDeleteLines(lines.length) + (spec.current ? '' : renderDiscard(spec)) : ''}`,
    };
  }

  function staticChoice(on, label) {
    return `<span class="choice is-static" data-on="${on ? 'true' : 'false'}"><span class="tick" role="img" aria-label="${on ? 'Yes' : 'No'}">${on ? icon('icon-check') : ''}</span><span>${label}</span></span>`;
  }

  // _RepairSpecDiff: the one table of Added, Changed and Removed lines.
  function renderDiff(d) {
    const cell = (v, kind) => v == null || v === '' || Number(v) === 0 ? '—' : kind === 'money' ? money(Number(v)) : kind === 'hours' ? hoursText(v) : String(v);
    const row = (cls, word, line, fields = []) => {
      const c = (name) => fields.includes(name) ? ' diff' : '';
      return `<tr class="${cls}"><td>${word}</td><td>${M.WORD[line.t]}</td><td class="${c('description').trim()}">${esc(line.d)}</td><td class="mono${c('part number')}">${line.p ? esc(line.p) : '—'}</td><td class="num mono${c('quantity')}">${line.q || 1}</td><td class="num mono${c('unit amount')}">${cell(line.u, 'money')}</td><td class="num mono${c('hours')}">${hoursText(line.h)}</td><td class="num mono${c('paint hours')}">${hoursText(line.ph)}</td><td class="num mono${c('materials')}">${cell(line.m, 'money')}</td></tr>`;
    };
    const n = d.added.length + d.changed.length + d.removed.length;
    return `<p class="mono est-diff-sum" data-estimate-diff-sum>${d.grossDelta >= 0 ? '+' : '−'}${money(Math.abs(d.grossDelta))} net · ${d.added.length} added · ${d.changed.length} changed · ${d.removed.length} removed</p>${n ? `<div class="table-wrap"><table class="table table--compact est-diff" data-estimate-diff-table><thead><tr><th scope="col">Change</th><th scope="col">Type</th><th scope="col">Description</th><th scope="col">Part no.</th><th scope="col" class="num">Qty</th><th scope="col" class="num">Unit (£)</th><th scope="col" class="num">Hours</th><th scope="col" class="num">Paint h</th><th scope="col" class="num">Material (£)</th></tr></thead><tbody>${d.added.map((l) => row('added', 'Added', l)).join('')}${d.changed.map((c) => row('changed', 'Changed', c.to, c.fields)).join('')}${d.removed.map((l) => row('removed', 'Removed', l)).join('')}</tbody></table></div>` : ''}`;
  }

  // P2 · the side-by-side table: From and To, five figure columns each.
  function renderSideBySide(d, from, to) {
    const rf = M.effectiveRate(from.header), rt = M.effectiveRate(to.header);
    const side = (l, other, rate, isTo) => {
      if (!l) return '<td class="num">—</td><td class="num">—</td><td class="num">—</td><td class="num">—</td><td class="num">—</td>';
      const dif = (k) => isTo && other && (k === 'q' ? (other.q || 1) !== (l.q || 1) : (other[k] || 0) !== (l[k] || 0)) ? ' diff' : '';
      const hrs = (Number(l.h) || 0) + (Number(l.ph) || 0);
      return `<td class="num mono${dif('q')}">${l.q > 1 ? l.q : '—'}</td><td class="num mono${dif('u')}">${l.u ? money(Number(l.u)) : '—'}</td><td class="num mono${dif('h') || dif('ph')}">${hrs ? hrs.toFixed(1) : '—'}</td><td class="num mono${dif('m')}">${l.m ? money(Number(l.m)) : '—'}</td><td class="num mono">${money(M.lineValue(l, rate))}</td>`;
    };
    const line = (cls, word, a, b) => `<tr class="${cls}"><td>${word}</td><td>${M.WORD[(b || a).t]}${a && b && a.t !== b.t ? ` <span class="muted">(was ${M.WORD[a.t]})</span>` : ''}</td><td>${esc((b || a).d)}</td>${side(a, null, rf, false)}${side(b, a, rt, true)}</tr>`;
    const body = [
      ...d.added.map((l) => line('added', 'Added', null, l)),
      ...d.changed.map((c) => line('changed', 'Changed', c.from, c.to)),
      ...d.removed.map((l) => line('removed', 'Removed', l, null)),
      ...d.same.map((c) => line('', '', c.from, c.to)),
    ].join('');
    const rateNote = rf !== rt ? ` · rate ${money(rf)} → ${money(rt)}` : '';
    const sum = `<p class="mono est-diff-sum" data-estimate-diff-sum>${esc(from.label.split(' · ')[0])} ${money(d.grossFrom)} → ${esc(to.label.split(' · ')[0])} ${money(d.grossTo)} · ${d.grossDelta >= 0 ? '+' : '−'}${money(Math.abs(d.grossDelta))} · ${d.added.length} added · ${d.changed.length} changed · ${d.removed.length} removed${rateNote}</p>`;
    const group = (c, rate) => `<th scope="colgroup" colspan="5" class="num v38-cmp-group">${esc(c.label.split(' · ')[0])} · ${money(rate)}/h</th>`;
    return `${sum}<div class="table-wrap"><table class="table table--compact est-diff v38-cmp" data-estimate-diff-table data-estimate-compare-side><thead><tr><th scope="col" rowspan="2">Change</th><th scope="col" rowspan="2">Type</th><th scope="col" rowspan="2">Description</th>${group(from, rf)}${group(to, rt)}</tr><tr><th scope="col" class="num v38-cmp-first">Qty</th><th scope="col" class="num">Unit (£)</th><th scope="col" class="num">Hours</th><th scope="col" class="num">Material (£)</th><th scope="col" class="num">Line £</th><th scope="col" class="num v38-cmp-first">Qty</th><th scope="col" class="num">Unit (£)</th><th scope="col" class="num">Hours</th><th scope="col" class="num">Material (£)</th><th scope="col" class="num">Line £</th></tr></thead><tbody>${body}</tbody></table></div>`;
  }

  function renderCompare(state, spec) {
    if (state.specs.length < 2) return '';
    const side = state.p.cmp === 'side';
    const list = side ? comparables(state, spec) : specComparables(state);
    const specRows = state.specs.map((s) => { const tt = M.totals(s.lines, s.header).printed; return `<tr><td>${esc(s.header.name)}<br /><small class="muted">${M.ROUTE_TAG[s.route]} · ${s.createdAt}</small></td><td><span class="status status--plain ${s.current ? 'status--green' : 'status--amber'}">${s.current ? 'Current' : 'Draft'}</span></td><td class="num mono">${money(tt.net)}</td><td class="num mono">${money(tt.vat)}</td><td class="num mono">${money(tt.gross)}</td></tr>`; }).join('');
    const sel = (name, value) => `<label class="dsc">${name === 'from' ? 'From' : 'To'} <select class="fi" name="${name}" aria-label="${name === 'from' ? 'From' : 'To'}" data-cmp-${name}><option value="">Choose…</option>${list.map((c) => `<option value="${c.key}"${value === c.key ? ' selected' : ''}>${esc(c.label)}</option>`).join('')}</select></label>`;
    const from = list.find((c) => c.key === state.cmpFrom), to = list.find((c) => c.key === state.cmpTo);
    let diffHtml = '';
    if (from && to) {
      const d = M.diff(from.lines, from.header, to.lines, to.header);
      diffHtml = side ? renderSideBySide(d, from, to) : renderDiff(d);
    }
    return `<div id="compare-estimates-dialog" class="dialog-backdrop" data-dialog="compare-estimates-dialog" hidden><section class="dialog dialog--wide${side ? ' v38-cmp-dialog' : ''}" role="dialog" aria-modal="true" aria-labelledby="compare-estimates-dialog-title"${mark('P2', side)}><div class="dialog-head"><h2 id="compare-estimates-dialog-title" tabindex="-1">Compare repair specs</h2><button type="button" class="dialog-close" data-dialog-close aria-label="Close dialog">${icon('icon-x')}</button></div><div class="dialog-body noprint"><div class="table-wrap"><table class="table table--compact cmp-table" data-estimate-compare-table><thead><tr><th scope="col">Repair Spec</th><th scope="col">State</th><th scope="col" class="num">Net</th><th scope="col" class="num">VAT</th><th scope="col" class="num">Gross</th></tr></thead><tbody>${specRows}</tbody></table></div></div><div class="dialog-body stack" data-estimate-compare-diff><form method="get" action="#" class="cluster noprint" data-estimate-compare-form>${sel('from', state.cmpFrom)}${sel('to', state.cmpTo)}<button type="submit" class="btn btn--small" data-estimate-compare-go>Compare</button></form><div data-compare-result>${diffHtml}</div></div><div class="dialog-foot noprint">${side ? `<button type="button" class="btn" data-compare-print${mark('P2', true)}>${icon('icon-file-text')}<span>Print comparison sheet</span></button>` : ''}<button type="button" class="btn" data-dialog-close>Close dialog</button></div></section></div>`;
  }

  function renderVersions(state, spec, editing) {
    const side = state.p.cmp === 'side';
    const draftGross = M.totals(spec.lines, spec.header).printed.gross;
    const rows = [...spec.versions].reverse().map((v) => {
      const gross = M.totals(v.lines, { ...spec.header, rate: v.rate, uplift: false }).printed.gross;
      const compare = side ? `<button type="button" class="btn btn--small" data-version-compare="${v.n}"${mark('P2', true)}>Compare with current</button>` : '';
      const restore = editing ? `<button type="button" class="btn btn--small" data-estimate-restore="${v.n}">Restore</button>` : '';
      return `<tr data-estimate-version="${v.n}"><td><span class="mono">v${v.n}</span> ${v.sent ? '<span class="status status--plain status--green">Sent on report</span>' : ''}</td><td>${v.when}<br /><span class="muted">${esc(v.who)}</span></td><td>${esc(v.origin)}</td><td class="num mono">${v.lines.length}</td><td class="num mono">${money(v.rate)}</td><td class="num mono"><b>${money(gross)}</b></td><td><div class="cluster v38-version-acts">${compare}${restore}</div></td></tr>`;
    }).join('');
    return `<div id="versions-dialog" class="dialog-backdrop" data-dialog="versions-dialog" hidden><section class="dialog dialog--wide" role="dialog" aria-modal="true" aria-labelledby="versions-dialog-title"><div class="dialog-head"><h2 id="versions-dialog-title" tabindex="-1">Repair Spec versions</h2><button type="button" class="dialog-close" data-dialog-close aria-label="Close dialog">${icon('icon-x')}</button></div><div class="dialog-body"><div class="table-wrap"><table class="table table--compact versions-table" data-estimate-versions-table><thead><tr><th scope="col">Version</th><th scope="col">When</th><th scope="col">How it came about</th><th scope="col" class="num">Lines</th><th scope="col" class="num">Rate</th><th scope="col" class="num">Total inc VAT</th><th scope="col"><span class="sr-only">Action</span></th></tr></thead><tbody><tr class="cur" data-estimate-version="draft"><td><span class="mono">Draft</span> <span class="status status--plain status--amber">Working</span></td><td>now</td><td>The draft being edited</td><td class="num mono">${spec.lines.length}</td><td class="num mono">${money(M.effectiveRate(spec.header))}</td><td class="num mono"><b>${money(draftGross)}</b></td><td></td></tr>${rows}</tbody></table></div></div><div class="dialog-foot"><button type="button" class="btn" data-dialog-close>Close dialog</button></div></section></div>`;
  }

  function renderDeleteLines(count) {
    return `<div id="delete-lines-dialog" class="dialog-backdrop" data-dialog="delete-lines-dialog" hidden><section class="dialog dialog--compact" role="alertdialog" aria-modal="true" aria-labelledby="delete-lines-dialog-title"><div class="dialog-head"><h2 id="delete-lines-dialog-title" tabindex="-1">Delete all lines</h2><button type="button" class="dialog-close" data-dialog-close aria-label="Close dialog">${icon('icon-x')}</button></div><div class="dialog-body stack"><p class="consequence"><span data-delete-lines-count>${count}</span> lines</p></div><div class="dialog-foot"><button type="button" class="btn" data-dialog-close>Cancel</button><button type="button" class="btn btn--danger" data-delete-lines-confirm data-dialog-close>Delete all lines</button></div></section></div>`;
  }

  function renderDiscard(spec) {
    return `<div id="delete-estimate-dialog" class="dialog-backdrop" data-dialog="delete-estimate-dialog" hidden><section class="dialog dialog--compact" role="dialog" aria-modal="true" aria-labelledby="delete-estimate-dialog-title"><div class="dialog-head"><h2 id="delete-estimate-dialog-title" tabindex="-1">Discard repair spec</h2><button type="button" class="dialog-close" data-dialog-close aria-label="Close dialog">${icon('icon-x')}</button></div><form method="post" action="#"><div class="dialog-body stack"><p class="consequence">Discard ${esc(spec.header.name)} and its lines from this case?</p><div class="field"><label class="req" for="delete-reason">Reason</label><input type="text" id="delete-reason" name="reason" maxlength="500" required /></div></div><div class="dialog-foot"><button type="button" class="btn" data-dialog-close>Cancel</button><button type="submit" class="btn btn--danger">Discard</button></div></form></section></div>`;
  }

  window.v38Render = { render, comparables, specComparables, lastSent };
})();
