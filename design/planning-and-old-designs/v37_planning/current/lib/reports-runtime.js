// v37 Management Reports mockup runtime. One renderer for the live baseline
// ("live", a transcription of Pages/Administration/Reports.cshtml at
// origin/dev 970ef9f10) and designs A, B and C, all from the same fixtures.
// Page state comes from the query string (state=, opt=, report=, work=,
// workm=, sort=, dir=, msort=, mdir=, measure=, principal=) so every
// screenshot is reproducible without a click. Nothing is posted anywhere.
(function () {
  'use strict';
  var C = window.reportsMockup;
  var D = C.id;
  var live = D === 'live';
  var params = new URLSearchParams(location.search);
  var state = C.presets.some(function (p) { return p[0] === params.get('state'); }) ? params.get('state') : 'default';

  var opt = {};
  C.options.forEach(function (o) { opt[o.key] = o.values[0][0]; });
  if (state === 'proposals') { opt.queues = 'on'; opt.pipeline = 'on'; opt.compare = 'on'; opt.periods = 'on'; opt.outcomes = 'on'; }
  (params.get('opt') || '').split(',').forEach(function (pair) {
    var bits = pair.split(':');
    if (bits[0] in opt && bits[1]) opt[bits[0]] = bits[1];
  });
  var on = function (key) { return !live && opt[key] === 'on'; };

  var ui = {
    report: params.get('report') || 'engineers',
    work: params.get('work') || 'all',
    workm: params.get('workm') || 'all',
    sort: params.get('sort') || '',
    dir: params.get('dir') || 'asc',
    msort: params.get('msort') || '',
    mdir: params.get('mdir') || 'asc',
    measure: params.get('measure') || 'fees',
    principal: params.get('principal') || 'QDOS',
    period: params.get('period') || 'custom',
    person: state === 'person' ? 'p-rkhan' : '',
    fromMonth: '2026-05', toMonth: '2026-10',
  };
  var invalid = state === 'invalid';
  var caseList = {
    presetId: state === 'preset' ? C.caseListPresets[0].id : '',
    name: state === 'preset' ? C.caseListPresets[0].name : '',
    columns: state === 'preset' ? C.caseListPresets[0].columns.slice() : state === 'refused' ? [] : C.caseListGroups[0][1].map(function (c) { return c[0]; }),
    error: state === 'refused' ? 'Choose at least one column.' : '',
    from: C.clock.caseFrom, to: C.clock.caseTo, allTime: false, triage: false,
  };

  // ---------- helpers ----------
  var esc = function (v) { return String(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;'); };
  var icon = function (id, cls) { return '<svg class="' + (cls || 'icon') + '" aria-hidden="true"><use href="#icon-' + id + '" /></svg>'; };
  var money = function (n) { return '£' + Number(n).toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); };
  var sum = function (rows, f) { return rows.reduce(function (t, r) { return t + f(r); }, 0); };
  var pair = function (p, work) { return work === 'inspection' ? p[0] : work === 'audit' ? p[1] : p[0] + p[1]; };
  var dash = function (v) { return v == null ? '—' : v; };
  var badge = function (code) { return live || opt.badges === 'keep' ? '<span class="meta">' + code + '</span>' : ''; };
  // Item C: Engineer activity's note goes; the Case list's stays (decided).
  var notes = function () { return live || opt.notes === 'keep'; };
  var caseNotes = function () { return live || opt.notes !== 'drop'; };
  var sideTitle = function (measure, side) { return measure + ' · ' + side; };

  // ---------- the sections' data, by page state ----------
  var unavailable = {
    engineers: state === 'engineer-unavailable',
    principals: state === 'principal-unavailable' || (invalid && live),
    months: state === 'monthly-unavailable' || (invalid && live),
  };
  var empty = state === 'empty';

  function personName(id) { var p = C.people.find(function (x) { return x.id === id; }); return p ? p.name : ''; }

  function engineerRows() {
    if (unavailable.engineers) return null;
    if (empty || (invalid && live)) return [];
    var rows = (D === 'c' ? C.engineersLedger : C.engineers).slice();
    if (!live && D !== 'c') rows.push(C.automation); // item J
    if (ui.person) rows = rows.filter(function (r) { return r.name === personName(ui.person); });
    var key = { person: 'name', queries: 'queries', reports: 'sent' }[ui.sort] || 'name';
    rows.sort(function (a, b) {
      var x = a[key], y = b[key];
      var c = typeof x === 'string' ? x.localeCompare(y) : x - y;
      return ui.dir === 'desc' ? -c : c;
    });
    return rows;
  }

  // Design C reads whole months from the ledger; the others the period's MI-02.
  function ledgerIndexes() {
    var first = C.ledgerMonths.length - 1 - monthsBetween(ui.fromMonth, '2026-10');
    var last = C.ledgerMonths.length - 1 - monthsBetween(ui.toMonth, '2026-10');
    var out = []; for (var i = Math.max(first, 0); i <= Math.min(last, C.ledgerMonths.length - 1); i++) out.push(i);
    return out;
  }
  function monthsBetween(a, b) { var pa = a.split('-').map(Number), pb = b.split('-').map(Number); return (pb[0] - pa[0]) * 12 + (pb[1] - pa[1]); }
  function ledgerTotals(code) {
    var l = C.ledger[code], idx = ledgerIndexes();
    var add = function (list) { return idx.reduce(function (t, i) { return [t[0] + list[i][0], t[1] + list[i][1]]; }, [0, 0]); };
    var produced = add(l.produced);
    return { code: code, produced: produced, sent: add(l.sent), feeNotes: add(l.feeNotes), fees: [produced[0] * l.fee[0], produced[1] * l.fee[1]] };
  }

  function principalRows() {
    if (unavailable.principals) return null;
    if (empty) return [];
    var rows = D === 'c' ? Object.keys(C.ledger).map(ledgerTotals) : C.principals.slice();
    rows = rows.filter(function (r) { return pair(r.produced) > 0 || pair(r.sent) > 0; });
    if (!live) {
      var w = ui.work;
      var key = { code: function (r) { return r.code; }, produced: function (r) { return pair(r.produced, w); }, sent: function (r) { return pair(r.sent, w); }, fees: function (r) { return pair(r.fees, w); } }[ui.msort] || function (r) { return r.code; };
      rows.sort(function (a, b) {
        var x = key(a), y = key(b);
        var c = typeof x === 'string' ? x.localeCompare(y) : x - y;
        return ui.mdir === 'desc' ? -c : c;
      });
    }
    return rows;
  }

  function monthRows() {
    if (unavailable.months) return null;
    if (empty) return [];
    return C.months;
  }

  function turnaroundRows() {
    if (unavailable.principals) return null;
    var rows = D === 'c' ? C.turnaroundLedger : C.turnaround;
    if (empty) return rows.filter(function (r) { return r.held > 0; }).map(function (r) { return Object.assign({}, r, { produce: null, ready: null, send: null }); });
    return rows.filter(function (r) { return r.held > 0 || r.produce || r.ready || r.send; });
  }

  // ---------- shared markup ----------
  function metric(label, value, meta, attrs) {
    return '<div class="metric"' + (attrs || '') + '><span class="metric-label">' + esc(label) + '</span><span class="metric-value">' + esc(value) + '</span>' + (meta ? '<span class="metric-meta">' + esc(meta) + '</span>' : '') + '</div>';
  }
  function previousMeta(key, format) {
    if (!on('compare')) return '';
    var p = (D === 'c' ? C.previousLedger : C.previous)[key];
    return p == null ? '' : 'Previous period ' + (format ? format(p) : p);
  }
  function measureCell(value, max) {
    return '<span class="admin-report-measure"><meter min="0" max="' + Math.max(max, 1) + '" value="' + value + '" aria-hidden="true"></meter><span class="tabular">' + value + '</span></span>';
  }
  function download(key, label, glyph, cls) {
    var busy = (state === 'busy' || state === 'done') && key === 'workbook';
    if (busy && state === 'busy') return '<a class="btn' + (cls || '') + '" href="#" data-download="' + key + '" data-busy aria-busy="true" aria-disabled="true">' + icon('loader', 'icon busy-spin') + '<span>Downloading…</span></a>';
    if (busy && state === 'done') return '<a class="btn' + (cls || '') + '" href="#" data-download="' + key + '" data-busy-complete>' + icon('check', 'icon busy-done') + '<span>Downloaded</span></a>';
    return '<a class="btn' + (cls || '') + '" href="#" data-download="' + key + '" data-busy-download data-busy-label="Downloading…" data-busy-done="Downloaded" data-busy-download-failed="The file could not be downloaded. Try again.">' + icon(glyph || 'download') + '<span>' + esc(label || 'Download CSV') + '</span></a>';
  }
  // Live: the next direction is desc only after asc on the same column, so a
  // count's first click sorts smallest first; the arrow is a span and site.css
  // adds its own ::after on th[aria-sort], so a sorted head shows two (finding 13).
  // Proposals: a count's first click sorts largest first; the ::after is the arrow.
  function sortLink(scope, key, label, current, dir) {
    var active = current === key;
    var next = live
      ? (active && dir === 'asc' ? 'desc' : 'asc')
      : active ? (dir === 'asc' ? 'desc' : 'asc') : (key === 'person' || key === 'code' ? 'asc' : 'desc');
    var arrow = live ? ' <span data-sort-arrow>' + (active ? (dir === 'asc' ? '↑' : '↓') : '') + '</span>' : '';
    return '<a class="admin-report-sort" href="#" data-sort-toggle data-sort-scope="' + scope + '" data-sort="' + key + '" data-dir="' + next + '">' + esc(label) + arrow + '</a>';
  }
  function ariaSort(current, key, dir) { return current === key ? ' aria-sort="' + (dir === 'asc' ? 'ascending' : 'descending') + '"' : ''; }
  function panel(o) {
    return '<section class="panel" aria-labelledby="' + o.id + '-title" data-section="' + o.section + '"><div class="panel-head"><h2 id="' + o.id + '-title">' + esc(o.title) + '</h2>' + (o.meta || '') + (o.actions ? '<div class="panel-actions">' + o.actions + '</div>' : '') + '</div><div class="panel-body' + (o.bodyClass ? ' ' + o.bodyClass : '') + '">' + o.body + '</div></section>';
  }
  function headSelect(name, label, values, current, scope) {
    var id = 'rp-' + name;
    return '<form method="get" class="rp-head-field" data-auto-submit data-ui-form="' + (scope || name) + '"><label for="' + id + '">' + esc(label) + '</label><select id="' + id + '" name="' + name + '">' + values.map(function (v) { return '<option value="' + v[0] + '"' + (v[0] === current ? ' selected' : '') + '>' + esc(v[1]) + '</option>'; }).join('') + '</select></form>';
  }
  var workValues = [['all', 'All'], ['inspection', 'Inspection'], ['audit', 'Audit']];
  function table(head, body, attrs) {
    return '<div class="table-wrap' + (attrs && attrs.gap === false ? '' : ' section-gap') + '"><table class="table table--compact' + (attrs && attrs.cls ? ' ' + attrs.cls : '') + '"' + (attrs && attrs.extra || '') + '><thead><tr>' + head + '</tr></thead><tbody>' + body + '</tbody>' + (attrs && attrs.foot ? '<tfoot>' + attrs.foot + '</tfoot>' : '') + '</table></div>';
  }
  var th = function (label, num, extra) { return '<th scope="col"' + (num ? ' class="num"' : '') + (extra || '') + '>' + label + '</th>'; };
  var muted = function (cols, text) { return '<tr><td colspan="' + cols + '" class="muted">' + esc(text) + '</td></tr>'; };

  function personOptions() {
    var list = C.people;
    if (!live && opt.person === 'active') {
      var names = (D === 'c' ? C.engineersLedger : C.engineers).map(function (r) { return r.name; });
      list = list.filter(function (p) { return names.indexOf(p.name) >= 0; });
    }
    return [['', 'All people']].concat(list.map(function (p) { return [p.id, p.name]; }));
  }

  // ---------- MI-01 Engineer activity ----------
  function engineerTable(rows) {
    var head = th(sortLink('mi01', 'person', 'Person', ui.sort, ui.dir), false, ariaSort(ui.sort, 'person', ui.dir))
      + th(sortLink('mi01', 'queries', 'Queries received', ui.sort, ui.dir), true, ariaSort(ui.sort, 'queries', ui.dir))
      + th('Amendment requests', true)
      + th(sortLink('mi01', 'reports', 'Reports sent', ui.sort, ui.dir), true, ariaSort(ui.sort, 'reports', ui.dir))
      + th('Audit reports sent', true) + th('Received to sent');
    var body;
    if (rows === null) body = muted(6, 'Unavailable');
    else if (!rows.length) body = muted(6, 'No engineer activity was recorded for this period.');
    else {
      var mq = Math.max.apply(null, rows.map(function (r) { return r.queries; }));
      var ms = Math.max.apply(null, rows.map(function (r) { return r.sent; }));
      body = rows.map(function (r) {
        return '<tr><td>' + esc(r.name) + '</td><td class="num">' + measureCell(r.queries, mq) + '</td><td class="num tabular">' + r.amendments + '</td><td class="num">' + measureCell(r.sent, ms) + '</td><td class="num tabular">' + r.audit + '</td><td>' + esc(r.toSent) + '</td></tr>';
      }).join('');
    }
    return table(head, body, { extra: ' data-engineer-activity' });
  }
  function engineerTiles(rows) {
    var u = rows === null;
    return '<div class="metric-strip metric-strip--3' + (live ? ' section-gap' : '') + '" aria-label="Engineer activity totals">'
      + metric('Queries received', u ? 'Unavailable' : sum(rows, function (r) { return r.queries; }), u ? '' : previousMeta('queries'))
      + metric('Reports sent', u ? 'Unavailable' : sum(rows, function (r) { return r.sent; }), u ? '' : previousMeta('engineerSent'))
      + metric('Audit reports sent', u ? 'Unavailable' : sum(rows, function (r) { return r.audit; }), u ? '' : previousMeta('audit'))
      + '</div>';
  }
  var engineerNote = '<p class="muted admin-report-note">Queries received are credited to the Engineer assigned to the Case. Reports sent are credited to the staff member recorded as the sender.</p>';

  function liveEngineerPanel() {
    var rows = engineerRows();
    var from = invalid ? '2026-10-09T09:41' : C.clock.from, to = invalid ? '2026-09-08T09:41' : C.clock.to;
    var form = '<form method="get" class="form-grid form-grid--auto admin-report-filter" aria-label="Engineer activity filters" data-period-form>'
      + '<div class="field"><label for="report-from">From</label><input id="report-from" name="from" type="datetime-local" step="60" value="' + from + '" /></div>'
      + '<div class="field"><label for="report-to">To</label><input id="report-to" name="to" type="datetime-local" step="60" value="' + to + '" /></div>'
      + '<div class="field"><label for="report-engineer">Person</label><select id="report-engineer" name="engineerId">' + personOptions().map(function (p) { return '<option value="' + p[0] + '"' + (p[0] === ui.person ? ' selected' : '') + '>' + esc(p[1]) + '</option>'; }).join('') + '</select></div>'
      + '<div class="cluster"><button class="btn btn--dark" type="submit">Apply</button>' + download('csv-mi01') + download('workbook', 'Download workbook', 'file-spreadsheet') + '</div></form>'
      + (invalid ? '<div class="validation-summary validation-summary-errors" data-valmsg-summary="true"><ul><li>Choose a valid date range.</li></ul></div>' : '<div class="validation-summary-valid validation-summary" data-valmsg-summary="true"><ul><li style="display:none"></li></ul></div>');
    return '<section class="panel" aria-labelledby="mi01-title" data-section="mi01"><div class="panel-head"><h2 id="mi01-title">Engineer activity</h2><span class="meta">MI01</span></div><div class="panel-body">'
      + engineerNote + form + engineerTiles(rows) + engineerTable(rows) + '</div></section>';
  }

  function engineerPanel() {
    var rows = engineerRows();
    var person = headSelect('engineerId', 'Person', personOptions(), ui.person, 'person');
    return panel({ id: 'mi01', section: 'mi01', title: 'Engineer activity', meta: badge('MI01'), actions: person + download('csv-mi01', null, null, ' btn--small'), body: (notes() ? engineerNote : '') + engineerTiles(rows) + engineerTable(rows) });
  }

  // ---------- MI-02 Reports by Principal ----------
  function principalTiles(rows) {
    var u = rows === null;
    return '<div class="metric-strip metric-strip--3" aria-label="Reports by Principal totals">'
      + metric('Reports produced', u ? 'Unavailable' : sum(rows, function (r) { return pair(r.produced); }), u ? '' : previousMeta('produced'))
      + metric('Reports sent', u ? 'Unavailable' : sum(rows, function (r) { return pair(r.sent); }), u ? '' : previousMeta('sent'))
      + metric('Agreed fees', u ? 'Unavailable' : money(sum(rows, function (r) { return pair(r.fees); })), u ? '' : previousMeta('fees', money))
      + '</div>';
  }
  function principalColumnsTable(rows, withMonth) {
    // Today's tripled columns (live, and item D's "columns" variant).
    var measures = withMonth
      ? [['produced', 'Reports produced'], ['feeNotes', 'Fee notes produced', true], ['sent', 'Reports sent'], ['fees', 'Agreed fees']]
      : [['produced', 'Reports produced'], ['sent', 'Reports sent'], ['fees', 'Agreed fees']];
    var head = (withMonth ? th('Month') : '') + th('Principal') + measures.map(function (m) {
      return m[2] ? th(m[1], true) : th(m[1], true) + th(sideTitle(m[1], 'Inspection'), true) + th(sideTitle(m[1], 'Audit'), true);
    }).join('');
    var cols = withMonth ? 12 : 10;
    var body;
    if (rows === null) body = muted(cols, 'Unavailable');
    else if (!rows.length) body = muted(cols, 'No reports were recorded for this period.');
    else body = rows.map(function (r) {
      return '<tr>' + (withMonth ? '<td>' + esc(r.month) + '</td>' : '') + '<td class="mono">' + esc(r.code) + '</td>' + measures.map(function (m) {
        var v = r[m[0]], f = m[0] === 'fees' ? money : String;
        if (m[2]) return '<td class="num tabular">' + f(pair(v)) + '</td>';
        return '<td class="num tabular">' + f(pair(v)) + '</td><td class="num tabular">' + f(v[0]) + '</td><td class="num tabular">' + f(v[1]) + '</td>';
      }).join('') + '</tr>';
    }).join('');
    return { head: head, body: body };
  }
  function principalWorkTable(rows) {
    var w = ui.work;
    var head = th(sortLink('mi02', 'code', 'Principal', ui.msort, ui.mdir), false, ariaSort(ui.msort, 'code', ui.mdir))
      + th(sortLink('mi02', 'produced', 'Reports produced', ui.msort, ui.mdir), true, ariaSort(ui.msort, 'produced', ui.mdir))
      + th(sortLink('mi02', 'sent', 'Reports sent', ui.msort, ui.mdir), true, ariaSort(ui.msort, 'sent', ui.mdir))
      + th(sortLink('mi02', 'fees', 'Agreed fees', ui.msort, ui.mdir), true, ariaSort(ui.msort, 'fees', ui.mdir));
    var body;
    if (rows === null) body = muted(4, 'Unavailable');
    else if (!rows.length) body = muted(4, 'No reports were recorded for this period.');
    else {
      var mp = Math.max.apply(null, rows.map(function (r) { return pair(r.produced, w); }));
      var ms = Math.max.apply(null, rows.map(function (r) { return pair(r.sent, w); }));
      body = rows.map(function (r) {
        return '<tr><td class="mono">' + esc(r.code) + '</td><td class="num">' + measureCell(pair(r.produced, w), mp) + '</td><td class="num">' + measureCell(pair(r.sent, w), ms) + '</td><td class="num tabular">' + money(pair(r.fees, w)) + '</td></tr>';
      }).join('');
    }
    return { head: head, body: body };
  }
  function monthsWorkTable(rows) {
    var w = ui.workm;
    var head = th('Month') + th('Principal') + th('Reports produced', true) + th('Fee notes produced', true) + th('Reports sent', true) + th('Agreed fees', true);
    var body;
    if (rows === null) body = muted(6, 'Unavailable');
    else if (!rows.length) body = muted(6, 'No reports were recorded for this period.');
    else body = rows.map(function (r) {
      return '<tr><td>' + esc(r.month) + '</td><td class="mono">' + esc(r.code) + '</td><td class="num tabular">' + pair(r.produced, w) + '</td><td class="num tabular">' + pair(r.feeNotes, w) + '</td><td class="num tabular">' + pair(r.sent, w) + '</td><td class="num tabular">' + money(pair(r.fees, w)) + '</td></tr>';
    }).join('');
    return { head: head, body: body };
  }

  function livePrincipalPanel() {
    var rows = principalRows(), months = monthRows();
    var t = principalColumnsTable(rows, false), m = principalColumnsTable(months, true);
    return '<section class="panel" aria-labelledby="mi02-title" data-section="mi02"><div class="panel-head"><h2 id="mi02-title">Reports by Principal</h2><span class="meta">MI02</span><div class="panel-actions">' + download('csv-mi02', null, null, ' btn--small') + '</div></div><div class="panel-body">'
      + principalTiles(rows) + table(t.head, t.body)
      + '<h3 id="mi02-months-title" class="section-gap">By month</h3>'
      + '<div class="table-wrap" data-section="months"><table class="table table--compact" aria-labelledby="mi02-months-title" data-reports-by-month><thead><tr>' + m.head + '</tr></thead><tbody>' + m.body + '</tbody></table></div>'
      + '</div></section>';
  }
  function principalPanel() {
    var rows = principalRows();
    var columns = opt.work === 'columns';
    var t = columns ? principalColumnsTable(rows, false) : principalWorkTable(rows);
    var actions = (columns ? '' : headSelect('work', 'Work', workValues, ui.work)) + download('csv-mi02', null, null, ' btn--small');
    return panel({ id: 'mi02', section: 'mi02', title: 'Reports by Principal', meta: badge('MI02'), actions: actions, body: principalTiles(rows) + table(t.head, t.body) });
  }
  function monthsPanel() {
    var rows = monthRows();
    var columns = opt.work === 'columns';
    var t = columns ? principalColumnsTable(rows, true) : monthsWorkTable(rows);
    var actions = (columns ? '' : headSelect('workm', 'Work', workValues, ui.workm)) + download('csv-months', null, null, ' btn--small');
    return panel({ id: 'mi02-months', section: 'months', title: 'By month', meta: badge('MI02'), actions: actions, body: table(t.head, t.body, { gap: false, extra: ' data-reports-by-month' }) });
  }

  // ---------- MI-03 Turnaround ----------
  function turnaroundPanel() {
    var rows = turnaroundRows();
    var heldHere = live || !on('queues');
    var u = rows === null;
    var tiles = heldHere ? '<div class="metric-strip" aria-label="Turnaround totals">' + metric('Cases currently held', u ? 'Unavailable' : sum(rows, function (r) { return r.held; })) + '</div>' : '';
    var head = th('Principal') + (heldHere ? th('Currently held', true) + th('Oldest held since') : '') + th('Time to produce') + th('Time to ready') + th('Time to send');
    var cols = heldHere ? 6 : 4;
    var shown = u ? null : heldHere ? rows : rows.filter(function (r) { return r.produce || r.ready || r.send; });
    var body;
    if (shown === null) body = muted(cols, 'Unavailable');
    else if (!shown.length) body = muted(cols, 'No holding or turnaround activity was recorded for this period.');
    else body = shown.map(function (r) {
      return '<tr><td class="mono">' + esc(r.code) + '</td>' + (heldHere ? '<td class="num tabular">' + r.held + '</td><td>' + esc(dash(r.oldestHeld)) + '</td>' : '') + '<td>' + esc(dash(r.produce)) + '</td><td>' + esc(dash(r.ready)) + '</td><td>' + esc(dash(r.send)) + '</td></tr>';
    }).join('');
    var tbl = table(head, body, { gap: !!tiles });
    if (live) return '<section class="panel" aria-labelledby="mi03-title" data-section="mi03"><div class="panel-head"><h2 id="mi03-title">Turnaround</h2><span class="meta">MI03</span><div class="panel-actions">' + download('csv-mi03', null, null, ' btn--small') + '</div></div><div class="panel-body">' + tiles + tbl + '</div></section>';
    return panel({ id: 'mi03', section: 'mi03', title: 'Turnaround', meta: badge('MI03'), actions: download('csv-mi03', null, null, ' btn--small'), body: tiles + tbl });
  }

  // ---------- proposals: Queues (E), Cases by stage (F), Outcomes (O) ----------
  function queuesPanel() {
    var t = unavailable.principals ? null : (D === 'c' ? C.turnaroundLedger : C.turnaround);
    var held = t === null ? 'Unavailable' : sum(t, function (r) { return r.held; });
    var triage = sum(C.queues.triage, function (r) { return r.open; });
    var tiles = '<div class="metric-strip metric-strip--3" aria-label="Queues totals">' + metric('Cases currently held', held) + metric('Triages', t === null ? 'Unavailable' : triage) + metric('Unidentified', C.queues.unidentified, 'Oldest since ' + C.queues.unidentifiedOldest) + '</div>';
    var head = th('Principal') + th('Currently held', true) + th('Oldest held since') + th('Triages', true) + th('Oldest Triage since');
    var body = t === null ? muted(5, 'Unavailable') : t.map(function (r, i) {
      var q = C.queues.triage[i];
      return '<tr><td class="mono">' + esc(r.code) + '</td><td class="num tabular">' + r.held + '</td><td>' + esc(dash(r.oldestHeld)) + '</td><td class="num tabular">' + q.open + '</td><td>' + esc(dash(q.oldest)) + '</td></tr>';
    }).join('');
    return panel({ id: 'queues', section: 'queues', title: 'Queues', meta: '<span class="meta">Now</span>', actions: download('csv-queues', null, null, ' btn--small'), body: tiles + table(head, body) });
  }
  function stagesPanel() {
    var head = th('Principal') + C.stages.map(function (s) { return th(esc(s), true); }).join('');
    var body = C.pipeline.map(function (r) { return '<tr><td class="mono">' + esc(r.code) + '</td>' + r.counts.map(function (n) { return '<td class="num tabular">' + n + '</td>'; }).join('') + '</tr>'; }).join('');
    var foot = '<tr><th scope="row">Total</th>' + C.stages.map(function (s, i) { return '<td class="num tabular">' + sum(C.pipeline, function (r) { return r.counts[i]; }) + '</td>'; }).join('') + '</tr>';
    return panel({ id: 'stages', section: 'stages', title: 'Cases by stage', meta: '<span class="meta">Now</span>', actions: download('csv-stages', null, null, ' btn--small'), body: table(head, body, { gap: false, foot: foot }) });
  }
  function outcomesPanel() {
    var head = th('Principal') + C.outcomeWords.map(function (w) { return th(esc(w), true); }).join('') + th('Agrees', true) + th('Differs', true);
    var body = empty ? muted(7, 'No reports were recorded for this period.') : C.outcomes.map(function (r) { return '<tr><td class="mono">' + esc(r.code) + '</td>' + r.counts.map(function (n) { return '<td class="num tabular">' + n + '</td>'; }).join('') + '<td class="num tabular">' + r.agrees + '</td><td class="num tabular">' + r.differs + '</td></tr>'; }).join('');
    return panel({ id: 'outcomes', section: 'outcomes', title: 'Outcomes', actions: download('csv-outcomes', null, null, ' btn--small'), body: table(head, body, { gap: false }) });
  }

  // ---------- MI-04 Case list (unchanged in every design but its note and label) ----------
  function caseListPanel() {
    var presetPicker = '<form method="get" class="form-grid form-grid--auto admin-report-filter" aria-label="Preset" data-case-preset-form><div class="field"><label for="case-list-preset">Preset</label><select id="case-list-preset" name="preset"><option value="">No preset</option>'
      + C.caseListPresets.map(function (p) { return '<option value="' + p.id + '"' + (p.id === caseList.presetId ? ' selected' : '') + '>' + esc(p.name) + '</option>'; }).join('')
      + '</select></div><div class="cluster"><button class="btn" type="submit">Use preset</button></div></form>';
    var groups = C.caseListGroups.map(function (g) {
      return '<fieldset class="stack"><legend>' + esc(g[0]) + '</legend><div class="form-grid form-grid--auto">' + g[1].map(function (c) {
        return '<label class="choice"><input type="checkbox" name="Columns" value="' + c[0] + '"' + (caseList.columns.indexOf(c[0]) >= 0 ? ' checked' : '') + ' /> ' + esc(c[1]) + '</label>';
      }).join('') + '</div></fieldset>';
    }).join('');
    var presetButtons = '<div class="form-grid form-grid--auto admin-report-filter"><div class="field"><label for="case-list-preset-name">Preset name</label><input id="case-list-preset-name" name="PresetName" maxlength="100" value="' + esc(caseList.name) + '" /></div><div class="button-row">'
      + '<button class="btn" type="submit" data-case-action="create">Save as new preset</button>'
      + (caseList.presetId ? '<button class="btn" type="submit" data-case-action="save">Save preset</button><button class="btn btn--danger" type="submit" data-case-action="remove">Remove preset</button>' : '')
      + '</div></div>';
    var body = (caseNotes() ? '<p class="muted admin-report-note">One row per Case received in the period, open or closed. N/A means the column does not apply to that Case type; a blank cell means nothing is recorded yet.</p>' : '')
      + (caseList.error ? '<div class="notice notice--danger" role="alert">' + esc(caseList.error) + '</div>' : '')
      + presetPicker
      + '<form method="post" class="stack" aria-label="Case list" data-case-list-form>'
      + '<div class="form-grid form-grid--auto admin-report-filter"><div class="field"><label for="case-list-from">Received from</label><input id="case-list-from" name="ReceivedFrom" type="date" value="' + caseList.from + '" /></div><div class="field"><label for="case-list-to">Received to</label><input id="case-list-to" name="ReceivedTo" type="date" value="' + caseList.to + '" /></div><label class="choice"><input type="checkbox" name="AllTime" value="true" /> All time</label><label class="choice"><input type="checkbox" name="IncludeTriage" value="true" /> Include Triage Cases</label></div>'
      + groups
      + '<div class="button-row"><button class="btn btn--dark" type="submit" data-download="caselist-csv" data-busy-download>' + icon('download') + '<span>Download CSV</span></button><button class="btn" type="submit" data-download="caselist-workbook" data-busy-download>' + icon('file-spreadsheet') + '<span>Download workbook</span></button></div>'
      + presetButtons + '</form>';
    return '<section class="panel" aria-labelledby="mi04-title" data-case-list data-section="caselist"><div class="panel-head"><h2 id="mi04-title">Case list</h2>' + badge('MI04') + '</div><div class="panel-body stack">' + body + '</div></section>';
  }

  // ---------- the shared period bar (items B, H) ----------
  function periodBar() {
    var c = D === 'c';
    var presetsC = [['last6', 'Last 6 months'], ['year', 'This year'], ['last12', 'Last 12 months'], ['custom', 'Custom']];
    var presetsAB = [['this-month', 'This month'], ['last-month', 'Last month'], ['quarter', 'This quarter'], ['last12', 'Last 12 months'], ['custom', 'Custom']];
    var period = on('periods') ? '<div class="field"><label for="report-period">Period</label><select id="report-period" name="period" data-period-preset>' + (c ? presetsC : presetsAB).map(function (p) { return '<option value="' + p[0] + '"' + (p[0] === (c && ui.period === 'custom' ? 'last6' : ui.period) ? ' selected' : '') + '>' + esc(p[1]) + '</option>'; }).join('') + '</select></div>' : '';
    var fields = c
      ? '<div class="field"><label for="report-from">From</label><input id="report-from" name="from" type="month" value="' + (invalid ? '2026-10' : ui.fromMonth) + '" /></div><div class="field"><label for="report-to">To</label><input id="report-to" name="to" type="month" value="' + (invalid ? '2026-05' : ui.toMonth) + '" /></div>'
      : '<div class="field"><label for="report-from">From</label><input id="report-from" name="from" type="datetime-local" step="60" value="' + (invalid ? '2026-10-09T09:41' : C.clock.from) + '" /></div><div class="field"><label for="report-to">To</label><input id="report-to" name="to" type="datetime-local" step="60" value="' + (invalid ? '2026-09-08T09:41' : C.clock.to) + '" /></div>';
    var error = invalid ? '<div class="validation-summary validation-summary-errors" data-valmsg-summary="true"><ul><li>Choose a valid date range.</li></ul></div>' : '';
    return '<form method="get" class="panel rp-period" aria-label="Period" data-period-form><div class="panel-body"><div class="form-grid form-grid--auto admin-report-filter">' + period + fields + '<div class="cluster"><button class="btn btn--dark" type="submit">Apply</button></div></div>' + error + '</div></form>';
  }

  // ---------- design C: the ledger and the chosen Principal ----------
  var measures = [['produced', 'Reports produced'], ['feeNotes', 'Fee notes produced'], ['sent', 'Reports sent'], ['fees', 'Agreed fees']];
  function ledgerValue(code, i, m, w) {
    var l = C.ledger[code];
    if (m === 'fees') { var p = l.produced[i]; return pair([p[0] * l.fee[0], p[1] * l.fee[1]], w); }
    return pair(l[m][i], w);
  }
  function ledgerPanel() {
    var idx = ledgerIndexes(), m = ui.measure, w = ui.work, f = m === 'fees' ? money : String;
    var u = unavailable.months;
    var codes = Object.keys(C.ledger);
    var head = th('Principal') + idx.map(function (i) { return th(esc(C.ledgerMonths[i]), true); }).join('') + th('Total', true);
    var cols = idx.length + 2;
    var body, foot = '';
    if (u) body = muted(cols, 'Unavailable');
    else if (empty) body = muted(cols, 'No reports were recorded for this period.');
    else {
      body = codes.map(function (code) {
        var total = sum(idx, function (i) { return ledgerValue(code, i, m, w); });
        return '<tr' + (code === ui.principal ? ' aria-selected="true"' : '') + '><td class="mono"><button type="button" class="rp-pick mono" data-principal="' + code + '" aria-pressed="' + (code === ui.principal) + '">' + esc(code) + '</button></td>' + idx.map(function (i) { return '<td class="num tabular">' + f(ledgerValue(code, i, m, w)) + '</td>'; }).join('') + '<td class="num tabular">' + f(total) + '</td></tr>';
      }).join('');
      var monthTotals = idx.map(function (i) { return sum(codes, function (code) { return ledgerValue(code, i, m, w); }); });
      var max = Math.max.apply(null, monthTotals.concat([1]));
      var bars = opt.bars === 'on' ? '<tr class="rp-bars" aria-hidden="true"><td></td>' + monthTotals.map(function (t) { return '<td class="num"><span class="rp-bar" style="height:' + Math.max(2, Math.round(t / max * 48)) + 'px"></span></td>'; }).join('') + '<td></td></tr>' : '';
      foot = bars + '<tr><th scope="row">Total</th>' + monthTotals.map(function (t) { return '<td class="num tabular">' + f(t) + '</td>'; }).join('') + '<td class="num tabular">' + f(sum(monthTotals, function (t) { return t; })) + '</td></tr>';
    }
    var rows = principalRows();
    var actions = headSelect('measure', 'Measure', measures, m) + (opt.work === 'columns' ? '' : headSelect('work', 'Work', workValues, w)) + download('csv-mi02', null, null, ' btn--small');
    var tbl = '<div class="table-wrap section-gap"><table class="table table--compact rp-ledger" data-reports-by-month><thead><tr>' + head + '</tr></thead><tbody>' + body + '</tbody>' + (foot ? '<tfoot>' + foot + '</tfoot>' : '') + '</table></div>';
    var ledger = panel({ id: 'mi02', section: 'ledger', title: 'Reports by Principal', meta: badge('MI02'), actions: actions, body: principalTiles(rows) + tbl });
    return '<div class="rp-ledger-layout">' + ledger + (u || empty ? '' : detailPanel()) + '</div>';
  }
  function detailPanel() {
    var code = ui.principal, l = C.ledger[code], idx = ledgerIndexes().slice().reverse(), w = ui.work;
    var head = th('Month') + th('Reports produced', true) + th('Fee notes produced', true) + th('Reports sent', true) + th('Agreed fees', true);
    var body = idx.map(function (i) { return '<tr><td>' + esc(C.ledgerMonths[i]) + '</td><td class="num tabular">' + ledgerValue(code, i, 'produced', w) + '</td><td class="num tabular">' + ledgerValue(code, i, 'feeNotes', w) + '</td><td class="num tabular">' + ledgerValue(code, i, 'sent', w) + '</td><td class="num tabular">' + money(ledgerValue(code, i, 'fees', w)) + '</td></tr>'; }).join('');
    var t = unavailable.principals ? null : C.turnaroundLedger.find(function (r) { return r.code === code; });
    var def = function (k, v) { return '<dl class="definition"><dt>' + esc(k) + '</dt><dd>' + esc(v) + '</dd></dl>'; };
    var facts = t === null ? '<p class="muted">Unavailable</p>' : '<div class="definition-list section-gap">' + def('Time to produce', dash(t.produce)) + def('Time to ready', dash(t.ready)) + def('Time to send', dash(t.send)) + def('Currently held', t.held) + def('Oldest held since', dash(t.oldestHeld)) + '</div>';
    void l;
    return '<section class="panel" aria-labelledby="detail-title" data-section="detail"><div class="panel-head"><h2 id="detail-title" class="mono">' + esc(code) + '</h2></div><div class="panel-body">' + table(head, body, { gap: false }) + facts + '</div></section>';
  }

  // ---------- design B: the overview tiles and one report ----------
  var reports = function () {
    var list = [['engineers', 'Engineer activity'], ['principals', 'Reports by Principal'], ['months', 'By month'], ['turnaround', 'Turnaround']];
    if (on('outcomes')) list.push(['outcomes', 'Outcomes']);
    if (on('queues')) list.push(['queues', 'Queues']);
    if (on('pipeline')) list.push(['stages', 'Cases by stage']);
    list.push(['caselist', 'Case list']);
    return list;
  };
  function overview() {
    var e = engineerRows(), p = principalRows(), t = turnaroundRows();
    var tile = function (report, label, value, meta) {
      var pressed = ui.report === report;
      return '<button type="button" class="metric" data-report="' + report + '" aria-pressed="' + pressed + '"><span class="metric-label">' + esc(label) + '</span><span class="metric-value">' + esc(value) + '</span>' + (meta ? '<span class="metric-meta">' + esc(meta) + '</span>' : '') + '</button>';
    };
    var U = 'Unavailable';
    return '<div class="metric-strip metric-strip--5 rp-overview" aria-label="Period totals">'
      + tile('principals', 'Reports produced', p === null ? U : sum(p, function (r) { return pair(r.produced); }), p === null ? '' : previousMeta('produced'))
      + tile('principals', 'Reports sent', p === null ? U : sum(p, function (r) { return pair(r.sent); }), p === null ? '' : previousMeta('sent'))
      + tile('months', 'Agreed fees', p === null ? U : money(sum(p, function (r) { return pair(r.fees); })), p === null ? '' : previousMeta('fees', money))
      + tile('engineers', 'Queries received', e === null ? U : sum(e, function (r) { return r.queries; }), e === null ? '' : previousMeta('queries'))
      + tile(on('queues') ? 'queues' : 'turnaround', 'Cases currently held', t === null ? U : sum(t, function (r) { return r.held; }))
      + '</div>';
  }
  function reportChoice() {
    return '<form method="get" class="rp-report-choice" data-auto-submit data-ui-form="report"><div class="field"><label for="rp-report">Report</label><select id="rp-report" name="report">' + reports().map(function (r) { return '<option value="' + r[0] + '"' + (r[0] === ui.report ? ' selected' : '') + '>' + esc(r[1]) + '</option>'; }).join('') + '</select></div></form>';
  }
  function chosenReport() {
    var r = reports().some(function (x) { return x[0] === ui.report; }) ? ui.report : 'engineers';
    return { engineers: engineerPanel, principals: principalPanel, months: monthsPanel, turnaround: turnaroundPanel, outcomes: outcomesPanel, queues: queuesPanel, stages: stagesPanel, caselist: caseListPanel }[r]();
  }

  // ---------- pages ----------
  function header(actions) {
    return '<header class="page-header"><div class="page-title"><p class="eyebrow">Administration</p><h1>Management Reports</h1></div>' + (actions ? '<div class="page-actions">' + actions + '</div>' : '') + '</header>';
  }
  function page() {
    var nav = C.adminNav;
    if (live) return header('') + '<div class="admin-layout">' + nav + '<div class="stack">' + liveEngineerPanel() + livePrincipalPanel() + turnaroundPanel() + caseListPanel() + '</div></div>';
    var parts = [periodBar()];
    if (invalid) parts.push(caseListPanel());
    else if (D === 'a') {
      parts.push(engineerPanel(), principalPanel(), monthsPanel());
      if (on('outcomes')) parts.push(outcomesPanel());
      parts.push(turnaroundPanel());
      if (on('queues')) parts.push(queuesPanel());
      if (on('pipeline')) parts.push(stagesPanel());
      parts.push(caseListPanel());
    } else if (D === 'b') {
      parts.push(overview(), reportChoice(), chosenReport());
    } else {
      parts.push(ledgerPanel(), engineerPanel(), turnaroundPanel());
      if (on('outcomes')) parts.push(outcomesPanel());
      if (on('queues')) parts.push(queuesPanel());
      if (on('pipeline')) parts.push(stagesPanel());
      parts.push(caseListPanel());
    }
    return header(download('workbook', 'Download workbook', 'file-spreadsheet')) + '<div class="admin-layout">' + nav + '<div class="stack">' + parts.join('') + '</div></div>';
  }

  // ---------- behaviour ----------
  var root = document.getElementById('rp-root');
  function query() {
    var q = new URLSearchParams();
    if (state !== 'default') q.set('state', state);
    var o = C.options.filter(function (x) { return opt[x.key] !== x.values[0][0] && !(state === 'proposals' && opt[x.key] === 'on'); }).map(function (x) { return x.key + ':' + opt[x.key]; });
    if (o.length) q.set('opt', o.join(','));
    var defaults = { report: 'engineers', work: 'all', workm: 'all', sort: '', dir: 'asc', msort: '', mdir: 'asc', measure: 'fees', principal: 'QDOS', period: 'custom' };
    Object.keys(defaults).forEach(function (k) { if (ui[k] !== defaults[k]) q.set(k, ui[k]); });
    if (params.get('embed')) q.set('embed', '1');
    return q.toString();
  }
  function render() {
    root.innerHTML = page();
    var q = query();
    history.replaceState(null, '', location.pathname + (q ? '?' + q : ''));
    syncStrip();
  }
  function toast(text, tone) {
    var region = document.querySelector('[data-toast-region]');
    var el = document.createElement('div');
    el.className = 'toast' + (tone ? ' toast--' + tone : '');
    el.setAttribute('role', 'status');
    el.innerHTML = '<strong>' + esc(text) + '</strong>';
    region.appendChild(el);
    setTimeout(function () { el.remove(); }, 4200);
  }
  function downloadFails(key) {
    if (live && /^(csv-mi0|workbook)/.test(key)) return unavailable.engineers || unavailable.principals || unavailable.months || invalid;
    if (key === 'workbook') return unavailable.engineers || unavailable.principals || unavailable.months || invalid;
    if (key === 'csv-mi01') return unavailable.engineers;
    if (key === 'csv-mi02' || key === 'csv-mi03' || key === 'csv-queues') return unavailable.principals || (D === 'c' && key === 'csv-mi02' && unavailable.months);
    if (key === 'csv-months') return unavailable.months;
    return false;
  }
  function runDownload(button, key) {
    if (/^caselist/.test(key)) {
      var ticked = root.querySelectorAll('[data-case-list-form] input[name="Columns"]:checked').length;
      if (!ticked) { toast('Choose at least one column.', 'danger'); return; }
    } else if (downloadFails(key)) { toast('The file could not be downloaded. Try again.', 'danger'); return; }
    var saved = button.innerHTML;
    button.setAttribute('data-busy', ''); button.setAttribute('aria-busy', 'true');
    button.innerHTML = icon('loader', 'icon busy-spin') + '<span>Downloading…</span>';
    setTimeout(function () {
      button.removeAttribute('data-busy'); button.removeAttribute('aria-busy');
      button.setAttribute('data-busy-complete', '');
      button.innerHTML = icon('check', 'icon busy-done') + '<span>Downloaded</span>';
      setTimeout(function () { button.removeAttribute('data-busy-complete'); button.innerHTML = saved; }, 1600);
    }, 700);
  }
  root.addEventListener('click', function (event) {
    var target = event.target.closest('a, button');
    if (!target) return;
    var key = target.getAttribute('data-download');
    if (key) { event.preventDefault(); runDownload(target, key); return; }
    if (target.hasAttribute('data-sort-toggle')) {
      event.preventDefault();
      if (target.getAttribute('data-sort-scope') === 'mi01') { ui.sort = target.getAttribute('data-sort'); ui.dir = target.getAttribute('data-dir'); }
      else { ui.msort = target.getAttribute('data-sort'); ui.mdir = target.getAttribute('data-dir'); }
      render(); return;
    }
    if (target.hasAttribute('data-report')) { ui.report = target.getAttribute('data-report'); render(); return; }
    if (target.hasAttribute('data-principal')) { ui.principal = target.getAttribute('data-principal'); render(); return; }
    var action = target.getAttribute('data-case-action');
    if (action) {
      event.preventDefault();
      var name = root.querySelector('#case-list-preset-name').value.trim();
      if (action === 'remove') { caseList.presetId = ''; caseList.name = ''; render(); toast('The preset was removed.'); return; }
      if (!name) { caseList.error = 'Enter a preset name of up to 100 characters.'; render(); return; }
      if (action === 'create' && C.caseListPresets.some(function (p) { return p.name.toLowerCase() === name.toLowerCase(); })) { caseList.error = 'Another preset already has that name.'; render(); return; }
      caseList.error = ''; caseList.name = name; render(); toast(action === 'create' ? 'The preset was saved.' : 'The preset was updated.'); return;
    }
    if (target.tagName === 'A' && target.getAttribute('href') === '#') event.preventDefault();
  });
  root.addEventListener('change', function (event) {
    var el = event.target;
    var form = el.closest('[data-ui-form]');
    if (form) {
      var which = form.getAttribute('data-ui-form');
      if (which === 'person') ui.person = el.value;
      else if (which in ui) ui[which] = el.value;
      render(); return;
    }
    if (el.hasAttribute('data-period-preset')) { ui.period = el.value; render(); return; }
    if (el.name === 'Columns') { var v = el.value; caseList.columns = el.checked ? caseList.columns.concat([v]) : caseList.columns.filter(function (c) { return c !== v; }); }
  });
  root.addEventListener('submit', function (event) {
    event.preventDefault();
    var form = event.target;
    if (form.hasAttribute('data-period-form')) {
      var from = form.querySelector('[name="from"]').value, to = form.querySelector('[name="to"]').value;
      var person = form.querySelector('[name="engineerId"]');
      if (person) ui.person = person.value;
      if (from && to && from > to) { state = 'invalid'; invalid = true; location.search = query(); return; }
      if (D === 'c') { ui.fromMonth = from || ui.fromMonth; ui.toMonth = to || ui.toMonth; }
      if (invalid) { state = 'default'; location.search = query(); return; }
      render(); return;
    }
    if (form.hasAttribute('data-case-preset-form')) {
      var id = form.querySelector('select').value;
      var preset = C.caseListPresets.find(function (p) { return p.id === id; });
      caseList.presetId = id; caseList.name = preset ? preset.name : '';
      if (preset) caseList.columns = preset.columns.slice();
      caseList.error = '';
      render();
    }
  });

  // ---------- the mockup strip ----------
  function syncStrip() {
    var strip = document.querySelector('[data-mock]');
    if (!strip) return;
    if (params.get('embed')) { strip.hidden = true; return; }
    strip.querySelector('[data-state-picker]').value = state;
    strip.querySelectorAll('[data-option-picker]').forEach(function (s) { s.value = opt[s.getAttribute('data-option-picker')]; });
    strip.querySelectorAll('[data-keep-query]').forEach(function (a) { a.href = a.getAttribute('data-keep-query') + (location.search || ''); });
  }
  var strip = document.querySelector('[data-mock]');
  if (strip) {
    strip.addEventListener('change', function (event) {
      var el = event.target;
      if (el.hasAttribute('data-state-picker')) { state = el.value; location.search = 'state=' + state; return; }
      if (el.hasAttribute('data-option-picker')) { opt[el.getAttribute('data-option-picker')] = el.value; render(); }
    });
    if (localStorage.getItem('v37.strip') === 'open') strip.open = true;
    strip.addEventListener('toggle', function () { localStorage.setItem('v37.strip', strip.open ? 'open' : 'closed'); });
  }

  render();
  window.reportsMockupState = function () { return { design: D, state: state, opt: Object.assign({}, opt), ui: Object.assign({}, ui) }; };
  window.mockupReady = true;
})();
