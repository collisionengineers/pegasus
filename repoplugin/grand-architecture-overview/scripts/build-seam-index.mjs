#!/usr/bin/env node
// build-seam-index.mjs — deterministic ProjectProfile[] → Seam[]. No LLM, no tokens.
//
//   node build-seam-index.mjs <profiles.json> [eligible-names.json]
//     profiles.json      : array of ProjectProfile objects (Phase 1 output)
//     eligible-names.json: optional array of project names eligible for live integration
//                          (defaults to every project whose lifecycle is active/unknown)
//   → Seam[] JSON on stdout, sorted by weight desc.
//
// This is the anti-O(n²) core: it groups projects by what they SHARE (entity / external system /
// contract / producer→consumer / capability) so Phase 2 investigates seams, not every pair.
// The Workflow script inlines an identical `buildSeamIndex` function; this CLI is for fallback mode.

import { readFileSync } from 'node:fs';

const profilesPath = process.argv[2];
if (!profilesPath) { console.error('usage: node build-seam-index.mjs <profiles.json> [eligible-names.json]'); process.exit(1); }

const profiles = JSON.parse(readFileSync(profilesPath, 'utf8'));
const eligibleNames = process.argv[3]
  ? new Set(JSON.parse(readFileSync(process.argv[3], 'utf8')))
  : new Set(profiles.filter((p) => ['active', 'unknown', undefined].includes(p.lifecycle)).map((p) => p.name));

const norm = (s) => String(s || '').toLowerCase().replace(/[\s_\-]/g, '').replace(/s$/, '');
const base = (f) => String(f).split(/[\\/]/).pop();

// Canonical seam `type` values match references/schemas.md exactly. SLUG keeps the id short/readable.
const SLUG = {
  'shared-entity': 'entity', 'external-system': 'extsys', 'interface-contract': 'contract',
  'producer-consumer': 'producer-consumer', 'cross-cutting-concern': 'concern',
};
const seams = [];
const seenIds = new Set();
function add(type, key, evidence, owner = null) {
  const id = `seam-${SLUG[type]}-${key}`;
  if (seenIds.has(id)) return;              // de-dupe (e.g. two exposed interfaces matching the same consumer)
  const members = [...new Set(evidence.map((e) => e.project))];
  if (members.length < 2) return;
  seenIds.add(id);
  const eligible_members = members.filter((m) => eligibleNames.has(m));
  seams.push({ id, type, key, members, owner, evidence, weight: eligible_members.length, eligible_members });
}

// shared-entity: bucket owned + referenced entity names (normalised)
const ent = {};
for (const p of profiles) {
  for (const e of (p.owned_entities || [])) {
    const k = norm(e.name);
    (ent[k] ??= { owner: null, ev: [] });
    ent[k].owner = p.name;
    ent[k].ev.push({ project: p.name, anchor: e.defined_in, role: 'owner' });
  }
  for (const e of (p.referenced_entities || [])) {
    const k = norm(typeof e === 'string' ? e : e.name);
    (ent[k] ??= { owner: null, ev: [] }).ev.push({ project: p.name, anchor: '(referenced)' });
  }
}
for (const [k, v] of Object.entries(ent)) add('shared-entity', k, v.ev, v.owner);

// external-system: bucket external_systems strings
const ext = {};
for (const p of profiles) for (const s of (p.external_systems || [])) (ext[norm(s)] ??= []).push({ project: p.name, anchor: s });
for (const [k, ev] of Object.entries(ext)) add('external-system', k, ev);

// interface-contract: bucket data_contracts by basename
const con = {};
for (const p of profiles) for (const c of (p.data_contracts || [])) (con[base(c)] ??= []).push({ project: p.name, anchor: c });
for (const [k, ev] of Object.entries(con)) add('interface-contract', k, ev);

// producer-consumer: A exposes an interface B consumes. Match on the full project name (token, len ≥ 4
// to avoid short-name false hits like "ui"/"api") or the interface's own name when present.
const tokenMatch = (target, name) => {
  const t = norm(target), n = norm(name);
  return !!n && n.length >= 4 && t.includes(n);
};
for (const a of profiles) for (const iface of (a.interfaces_exposed || []))
  for (const b of profiles) if (a !== b) for (const c of (b.interfaces_consumed || []))
    if (tokenMatch(c.target, a.name) || tokenMatch(c.target, iface.name))
      add('producer-consumer', `${norm(a.name)}-to-${norm(b.name)}`,
        [{ project: a.name, anchor: iface.anchor }, { project: b.name, anchor: '(consumes)' }], a.name);

// cross-cutting capability: keyword map over stack / role / extension_points.
// NB: deliberately no generic "data-store" tag — almost every project has a database, so it would
// manufacture one near-universal low-signal seam. Only capabilities worth consolidating belong here.
const CAP = [
  ['pdf-rendering', /render|pdf|report/],
  ['auth', /auth|oauth|sso|entra|identity|gateway/],
  ['design-system', /design|theme|brand|ui.?kit|component/],
  ['parsing', /parse|extract|document.?map|mapper/],
];
const cap = {};
for (const p of profiles) {
  const hay = norm([...(p.stack || []), p.role, ...(p.extension_points || [])].join(' '));
  for (const [tag, re] of CAP) if (re.test(hay)) (cap[tag] ??= []).push({ project: p.name, anchor: '(capability)' });
}
for (const [k, ev] of Object.entries(cap)) add('cross-cutting-concern', k, ev);

seams.sort((a, b) => b.weight - a.weight || b.members.length - a.members.length);
process.stdout.write(JSON.stringify(seams, null, 2) + '\n');
