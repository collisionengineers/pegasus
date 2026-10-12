// v38 mockup only: the Repair Spec fixture and the arithmetic the mockup
// mirrors from Core (EstimateTotals.Compute, RepairSpecificationScaling,
// RepairSpecificationComparison.Compare and SupplementaryStatement). Pure
// data and functions: the build inlines this file into the page (the
// export line stripped) and the runtime reads it as window.v38Model.
//
// The fixture is the operator's reference file's own repair grid
// (pegasus_case_dashboard_2026-09-15.html, ROWS and SUPP_ROWS) mapped to
// today's seven line types; the spec names, dates and figures are fixture.
const v38Model = (() => {
  'use strict';

  const OPERATIONS = [
    ['Replace', 'Replace'],
    ['Repair', 'Repair'],
    ['RemoveAndRefit', 'R&I'],
    ['Paint', 'Paint'],
    ['Blend', 'Blend'],
    ['Specialist', 'Specialist'],
    ['Other', 'Other'],
  ];
  const WORD = Object.fromEntries(OPERATIONS);

  // P1 · the columns a line type prices (q qty, u unit £, h hours, ph paint
  // hours, m material £, p part no.). Core's anomalies already name the
  // off-pattern cells; this map only decides which boxes are drawn.
  const COLS = {
    Replace: { p: 1, q: 1, u: 1, h: 1, ph: 0, m: 1 },
    Repair: { p: 0, q: 0, u: 0, h: 1, ph: 0, m: 1 },
    RemoveAndRefit: { p: 0, q: 0, u: 0, h: 1, ph: 0, m: 0 },
    Paint: { p: 0, q: 0, u: 0, h: 1, ph: 1, m: 1 },
    Blend: { p: 0, q: 0, u: 0, h: 1, ph: 1, m: 1 },
    Specialist: { p: 0, q: 1, u: 1, h: 1, ph: 0, m: 1 },
    Other: { p: 0, q: 0, u: 0, h: 1, ph: 0, m: 0 },
  };

  // A line: t type, d description, p part no., q qty, u unit £, h hours,
  // ph paint hours, m material £, src imported | amended | manual.
  const L = (t, d, f = {}) => ({ t, d, p: '', q: null, u: null, h: null, ph: null, m: null, src: 'imported', ...f });

  const ROWS_V1 = [
    L('Replace', 'Bumper rear (primed)', { q: 1, u: 286.40, p: '51127268970' }),
    L('Replace', 'Tailgate', { q: 1, u: 512.30, p: '41627211266' }),
    L('Replace', 'Lamp assembly rear O/S', { q: 1, u: 148.75, p: '63217255912' }),
    L('Replace', 'Impact absorber rear', { q: 1, u: 96.20, p: '51127212155' }),
    L('Repair', 'Rear panel — repair', { h: 3.5 }),
    L('RemoveAndRefit', 'Rear bumper — R&I', { h: 0.8 }),
    L('RemoveAndRefit', 'Tailgate trim — R&I', { h: 0.5 }),
    L('Paint', 'Paint — tailgate (new)', { ph: 2.1, m: 96.40 }),
    L('Paint', 'Paint — bumper rear (new)', { ph: 1.5, m: 78.20 }),
    L('Paint', 'Paint — rear panel (repair)', { ph: 1.8, m: 72.90 }),
    L('Blend', 'Blend — quarter panel O/S', { ph: 0.9, m: 38.50 }),
    L('Specialist', 'Four-wheel alignment check', { q: 1, u: 65.00 }),
    L('Specialist', 'Diagnostic scan & fault report', { q: 1, u: 85.00 }),
  ];
  // v2 (sent): the floor pan repair added by hand after the import.
  const ROWS_V2 = ROWS_V1.slice(0, 5).concat([L('Repair', 'Floor pan rear — repair', { h: 1.2, src: 'manual' })], ROWS_V1.slice(5));
  // Working: the rear panel repair time amended since the report went.
  const ROWS_WORKING = ROWS_V2.map((r) => (r.d === 'Rear panel — repair' ? { ...r, h: 3.8, src: 'amended' } : { ...r }));

  const ROWS_SUPP = [
    L('Replace', 'Bumper rear (primed)', { q: 1, u: 286.40, p: '51127268970' }),
    L('Replace', 'Tailgate', { q: 1, u: 512.30, p: '41627211266' }),
    L('Replace', 'Lamp assembly rear O/S', { q: 1, u: 148.75, p: '63217255912' }),
    L('Replace', 'Impact absorber rear', { q: 1, u: 96.20, p: '51127212155' }),
    L('Replace', 'Rear floor crossmember', { q: 1, u: 118.60, h: 1.2, p: '41117179280' }),
    L('Repair', 'Rear panel — repair', { h: 4.5 }),
    L('Repair', 'Floor pan rear — repair', { h: 1.2 }),
    L('Repair', 'Quarter panel O/S — repair', { h: 2.4 }),
    L('RemoveAndRefit', 'Rear bumper — R&I', { h: 0.8 }),
    L('RemoveAndRefit', 'Tailgate trim — R&I', { h: 0.5 }),
    L('Paint', 'Paint — tailgate (new)', { ph: 2.1, m: 96.40 }),
    L('Paint', 'Paint — bumper rear (new)', { ph: 1.5, m: 78.20 }),
    L('Paint', 'Paint — rear panel (repair)', { ph: 1.8, m: 72.90 }),
    L('Paint', 'Paint — quarter panel O/S (repair)', { ph: 1.6, m: 64.20 }),
    L('Specialist', 'Four-wheel alignment check', { q: 1, u: 65.00 }),
    L('Specialist', 'Diagnostic scan & fault report', { q: 1, u: 85.00 }),
    // Imported as received: a unit amount on a Repair line (Core's off-pattern anomaly).
    L('Repair', 'Wheel refurbishment O/S rear', { u: 95.00 }),
  ];

  const RATE = 83.28;
  const ENGINEER_VALUE = 4200;
  const AGREED_SUM = 2750;

  const header = (name, extra = {}) => ({
    name,
    rate: RATE,
    rateCard: 'Standard — £83.28/h',
    uplift: false,
    other: null,
    vatPercent: 20,
    repairerStatus: 'Registered',
    vat: { Labour: true, Parts: true, Materials: true, Specialist: true },
    vatOverridden: false,
    discounts: { Parts: 0, Materials: 0, Specialist: 0, Overall: 0 },
    ...extra,
  });

  // The Case's two specs. Spec 2 is Current: an import becomes Current when
  // it is created (FRD-25), so the earlier spec is a Draft with the sent
  // version.
  const SPECS = [
    {
      id: 'spec-1',
      route: 'AudatexPdf',
      state: 'Draft',
      current: false,
      createdAt: '28 Aug 2026 14:22',
      createdBy: 'A Patterson',
      header: header('Audatex est. 88214-01'),
      lines: ROWS_WORKING,
      versions: [
        { n: 1, kind: 'Imported', when: '28 Aug 2026 14:22', who: 'A Patterson', origin: 'Imported from Audatex', lines: ROWS_V1, rate: RATE, sent: false },
        { n: 2, kind: 'Sent', when: '28 Aug 2026 16:40', who: 'A Patterson', origin: 'Report sent', lines: ROWS_V2, rate: RATE, sent: true },
      ],
      supplementary: null,
    },
    {
      id: 'spec-2',
      route: 'AudatexPdf',
      state: 'Draft',
      current: true,
      createdAt: '12 Oct 2026 09:40',
      createdBy: 'A Patterson',
      header: header('Audatex supp. est. 88214-02'),
      lines: ROWS_SUPP,
      versions: [
        { n: 1, kind: 'Imported', when: '12 Oct 2026 09:40', who: 'A Patterson', origin: 'Imported from Audatex', lines: ROWS_SUPP, rate: RATE, sent: false },
      ],
      supplementary: null,
    },
  ];

  const ROUTE_TAG = { Manual: 'Manual', Glasses: "Glass's", AudatexPdf: 'Audatex PDF', Json: 'JSON', AiDraft: 'AI' };
  const ROUTE_WORDS = { Manual: 'entered by hand', Glasses: "from Glass's", AudatexPdf: 'from Audatex', Json: 'from a JSON estimate', AiDraft: 'drafted by AI' };
  const SOURCE_SHORT = { AudatexPdf: 'AX', Glasses: 'GL', Json: 'JSON', AiDraft: 'AI' };
  const REASONS = [
    ['estimate', 'Supplementary estimate received', 'Following receipt of a supplementary estimate'],
    ['dismantle', 'Further damage found on dismantling', 'Following dismantling of the vehicle, further damage was identified and'],
    ['inspect', 'Further inspection', 'Following a further inspection of the vehicle'],
    ['images', 'Further images received', 'Following receipt of further images'],
  ];

  const pence = (v) => Math.round(v * 100) / 100;
  const money = (v) => '£' + v.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const num = (v) => (v == null ? null : Number(v));
  const effectiveRate = (h) => pence(h.rate * (h.uplift ? 1.15 : 1));

  // EstimateTotals.Compute: the one owner of estimate money, mirrored.
  function totals(lines, h) {
    const rate = effectiveRate(h);
    let parts = 0, materials = 0, specialist = num(h.other) || 0, offPattern = 0;
    let panelH = 0, paintH = 0;
    const anomalies = [];
    lines.forEach((line, i) => {
      const quantity = line.q > 0 ? line.q : 1;
      const amount = (num(line.u) || 0) * quantity;
      materials += num(line.m) || 0;
      const position = i + 1;
      const hours = num(line.h) || 0, ph = num(line.ph) || 0;
      switch (line.t) {
        case 'Replace': parts += amount; panelH += hours; break;
        case 'Specialist':
          specialist += amount; panelH += 0;
          if (hours) anomalies.push({ position, field: 'h', text: 'Hours on a fixed-price Specialist line are retained but not priced.' });
          break;
        case 'Paint': case 'Blend':
          panelH += hours; paintH += ph;
          if (amount) { offPattern += amount; anomalies.push({ position, field: 'u', text: 'A unit amount on a line that prices no part is retained in specialist treatment.' }); }
          break;
        default:
          panelH += hours;
          if (amount) { offPattern += amount; anomalies.push({ position, field: 'u', text: 'A unit amount on a line that prices no part is retained in specialist treatment.' }); }
      }
      if (line.t !== 'Paint' && line.t !== 'Blend' && ph) anomalies.push({ position, field: 'ph', text: 'Paint hours on a line that is not Paint or Blend are retained but not priced.' });
    });
    const d = h.discounts;
    const panelLabour = panelH * rate, paintLabour = paintH * rate;
    const dParts = parts * (1 - d.Parts / 100), dMat = materials * (1 - d.Materials / 100), dSpec = (specialist + offPattern) * (1 - d.Specialist / 100);
    const overall = 1 - d.Overall / 100;
    const raw = { parts: dParts * overall, panelLabour: panelLabour * overall, paintLabour: paintLabour * overall, materials: dMat * overall, specialist: dSpec * overall, offPattern };
    const taxable = (h.vat.Parts ? raw.parts : 0) + (h.vat.Labour ? raw.panelLabour + raw.paintLabour : 0) + (h.vat.Materials ? raw.materials : 0) + (h.vat.Specialist ? raw.specialist : 0);
    const vat = taxable * h.vatPercent / 100;
    const p = { parts: pence(raw.parts), panelLabour: pence(raw.panelLabour), paintLabour: pence(raw.paintLabour), materials: pence(raw.materials), specialist: pence(raw.specialist) };
    p.net = pence(p.parts + p.panelLabour + p.paintLabour + p.materials + p.specialist);
    p.vat = pence(vat);
    p.gross = pence(p.net + p.vat);
    return { printed: p, raw, rate, hours: { panel: panelH, paint: paintH }, anomalies, offPattern: pence(offPattern) };
  }

  // RepairSpecificationScaling: one factor lowers prices, materials and the
  // rate to their floors; hours never move; bisection on the gross.
  function scaled(lines, h, factor, floors) {
    const priceFactor = Math.max(floors.price / 100, factor);
    const base = h.rate;
    const rate = pence(Math.max(Math.min(floors.rate, base), base * factor));
    const sl = lines.map((l) => ({ ...l, u: l.u != null ? pence(l.u * priceFactor) : l.u, m: l.m != null ? pence(l.m * priceFactor) : l.m, scaled: (l.u != null && priceFactor < 1) || (l.m != null && priceFactor < 1) }));
    return { lines: sl, header: { ...h, rate, uplift: false, rateCard: null }, priceFactor, atFloor: rate < base && rate <= floors.rate };
  }
  function grossAt(lines, h, factor, floors) { const s = scaled(lines, h, factor, floors); return totals(s.lines, s.header).printed.gross; }
  function scaleTo(lines, h, targetGross, floors) {
    const top = grossAt(lines, h, 1, floors), floor = grossAt(lines, h, 0, floors);
    const target = Math.min(top, Math.max(floor, targetGross));
    let lo = 0, hi = 1;
    for (let i = 0; i < 40; i++) { const mid = (lo + hi) / 2; if (grossAt(lines, h, mid, floors) > target) hi = mid; else lo = mid; }
    const r = scaled(lines, h, lo, floors);
    return { ...r, factor: lo, grossBefore: top, grossAfter: totals(r.lines, r.header).printed.gross, floor, top };
  }
  const percentOfValue = (value, gross) => Math.ceil(gross / value * 1000) / 10;

  // RepairSpecificationComparison.Compare: lines matched by description.
  const normd = (s) => String(s || '').toLowerCase().replace(/[^a-z0-9/]+/g, ' ').trim();
  const FIELDS = [['t', 'type'], ['p', 'part number'], ['q', 'quantity'], ['u', 'unit amount'], ['h', 'hours'], ['ph', 'paint hours'], ['m', 'materials']];
  function diff(fromLines, fromH, toLines, toH) {
    const ma = new Map(fromLines.map((r) => [normd(r.d), r]));
    const mb = new Map(toLines.map((r) => [normd(r.d), r]));
    const added = [], changed = [], removed = [], same = [];
    for (const b of toLines) {
      const a = ma.get(normd(b.d));
      if (!a) { added.push(b); continue; }
      const f = FIELDS.filter(([k]) => k === 'q' ? (a.q || 1) !== (b.q || 1) : (a[k] || (k === 'p' || k === 't' ? '' : 0)) !== (b[k] || (k === 'p' || k === 't' ? '' : 0))).map(([, n]) => n);
      if (f.length) changed.push({ from: a, to: b, fields: f }); else same.push({ from: a, to: b });
    }
    for (const a of fromLines) if (!mb.has(normd(a.d))) removed.push(a);
    const grossFrom = totals(fromLines, fromH).printed.gross, grossTo = totals(toLines, toH).printed.gross;
    return { added, changed, removed, same, grossFrom, grossTo, grossDelta: pence(grossTo - grossFrom) };
  }

  // RepairSpecificationComparison.SupplementaryStatement, word for word.
  const hoursOf = (l) => ((num(l.h) || 0) + (num(l.ph) || 0)).toString();
  function statement(d, reasonCode) {
    const reason = REASONS.find((r) => r[0] === reasonCode) || REASONS[0];
    let text = reason[2] + (d.added.length ? ' the following additional items are now required: ' + d.added.map((l) => l.d).join('; ') + '.' : ' the repair specification has been revised.');
    const hours = d.changed.filter((c) => c.fields.includes('hours') || c.fields.includes('paint hours'));
    if (hours.length) text += ' The repair time for ' + hours.map((c) => `${c.to.d} (${hoursOf(c.from)} h → ${hoursOf(c.to)} h)`).join('; ') + ' has been revised.';
    if (d.removed.length) text += ' ' + d.removed.map((l) => l.d).join('; ') + (d.removed.length > 1 ? ' are' : ' is') + ' no longer required.';
    text += ` The estimated repair cost has ${d.grossTo >= d.grossFrom ? 'increased' : 'reduced'} from ${money(d.grossFrom)} to ${money(d.grossTo)}.`;
    return text;
  }

  const contractSentence = (sum) => `A contract repair has been agreed for the sum of ${money(sum)} including VAT. Costs cannot increase above this figure.`;

  // A line's value, as the side-by-side Compare prints it.
  const lineValue = (l, rate) => pence(((num(l.u) || 0) * (l.q > 0 ? l.q : 1)) + ((l.t === 'Specialist' ? 0 : (num(l.h) || 0) + (num(l.ph) || 0)) * rate) + (num(l.m) || 0));

  const clone = (v) => JSON.parse(JSON.stringify(v));

  return { OPERATIONS, WORD, COLS, SPECS: clone(SPECS), RATE, ENGINEER_VALUE, AGREED_SUM, ROUTE_TAG, ROUTE_WORDS, SOURCE_SHORT, REASONS, FLOORS: { rate: 50, price: 65 }, money, pence, totals, effectiveRate, scaleTo, grossAt, percentOfValue, diff, statement, contractSentence, lineValue, clone };
})();
export default v38Model;
