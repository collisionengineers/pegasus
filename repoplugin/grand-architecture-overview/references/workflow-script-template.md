# Workflow script template

An annotated `Workflow` script for all five phases, and the plain-`Agent` fallback for when the
Workflow tool isn't available. Adapt — don't run blind.

## Division of labour (important)

The Workflow script has **no filesystem and no Node access** — it can't walk directories, read
files, or run `scripts/*.mjs`, and `Date.now()`/`Math.random()` throw. So:

- **Phase 0 (scout) runs INLINE in the main loop *before* you call Workflow** — via
  `scripts/scout-cluster.mjs` (Bash) or your own tools. You pass the resulting `ClusterManifest`
  into the script as `args`.
- **Phase 4 (write files) runs INLINE in the main loop *after* Workflow returns** — the script
  can't write `ARCHITECTURE-OVERVIEW.md` or the `.json` companion. The script *returns* the
  synthesised markdown + the structured data; you write them to disk.
- The **seam index** (normally `build-seam-index.mjs`) is mirrored as a **pure-JS function inside
  the script** (`buildSeamIndex` below), because the script can transform data, just not touch disk.

So the end-to-end shape is: **scout inline → run Workflow (Phases 1–3 + synthesis agent) → write
files inline**.

## The script

```javascript
export const meta = {
  name: 'grand-architecture-overview',
  description: 'Profile a cluster of projects, index seams, find & verify integration opportunities, synthesise an overview',
  phases: [
    { title: 'Profile',     detail: 'one read-only agent per project' },
    { title: 'Investigate', detail: 'one agent per seam' },
    { title: 'Verify',      detail: 'adversarial check per candidate opportunity' },
    { title: 'Synthesise',  detail: 'compose the overview markdown' },
  ],
}

// args = the ClusterManifest from the inline Phase-0 scout.
const manifest = args
// Guard: nothing to do. A cluster needs ≥2 projects; one (or zero) means scout found a single repo
// or an empty dir — say so and stop rather than fan out over nothing.
if (!manifest.projects || manifest.projects.length < 2) {
  return { error: 'not-a-cluster', message: `Found ${manifest.projects?.length || 0} project(s); a grand architecture overview needs ≥2 related projects.`, manifest }
}
const eligible = manifest.projects.filter(p => p.eligible_for_live_integration)
const archived = manifest.projects.filter(p => p.mine_as_prior_art)
// In focused mode, restrict profiling to the named projects (+ their obvious neighbours).
const toProfile = manifest.run_profile === 'focused'
  ? manifest.projects.filter(p => manifest.focus_projects.includes(p.name))
  : [...eligible, ...archived]

// Update mode: the main loop loads the prior overview JSON into `manifest.prior` before invoking.
// Re-profile only projects whose git head moved; reuse the prior profile for the unchanged ones,
// and hand the prior register to synthesis so §8 can show the delta and rejected ideas aren't re-proposed.
const prior = manifest.run_profile === 'update' ? (manifest.prior || null) : null
const priorProfiles = new Map((prior?.profiles || []).map(p => [p.name, p]))
const priorCommit = new Map((prior?.manifest?.projects || []).map(p => [p.name, p.last_commit]))
const moved = p => !priorProfiles.has(p.name) || priorCommit.get(p.name) !== p.last_commit
const toProfileNow = prior ? toProfile.filter(moved) : toProfile

// --- compact schemas (full versions in references/schemas.md; keep these in sync with them) ---
const MECHANISMS = ['shared-db-entity','api-call','event-webhook','shared-library','shared-contract',
  'sso-auth','shared-design-system','data-sync','deep-link','file-handoff','shared-service']
const PROFILE_SCHEMA = { type: 'object', required: ['name','purpose','domain','stack','lifecycle','owned_entities','interfaces_exposed','interfaces_consumed','external_systems','key_anchors'], properties: {
  name:{type:'string'}, purpose:{type:'string'}, domain:{type:'string'},
  stack:{type:'array',items:{type:'string'}}, lifecycle:{enum:['active','archive','on-hold','unknown']}, role:{type:'string'},
  owned_entities:{type:'array',items:{type:'object',required:['name','keys','defined_in'],properties:{name:{type:'string'},keys:{type:'array',items:{type:'string'}},defined_in:{type:'string'}}}},
  referenced_entities:{type:'array',items:{type:'string'}},
  interfaces_exposed:{type:'array',items:{type:'object',required:['kind','name','anchor'],properties:{kind:{type:'string'},name:{type:'string'},anchor:{type:'string'}}}},
  interfaces_consumed:{type:'array',items:{type:'object',required:['kind','target'],properties:{kind:{type:'string'},target:{type:'string'},mode:{type:'string'}}}},
  external_systems:{type:'array',items:{type:'string'}}, data_contracts:{type:'array',items:{type:'string'}},
  personas:{type:'array',items:{type:'string'}}, auth_model:{type:'string'}, existing_integrations:{type:'array',items:{type:'string'}},
  extension_points:{type:'array',items:{type:'string'}}, key_anchors:{type:'array',items:{type:'string'}},
  prior_art_notes:{type:['string','null']} } }

const OPPS_SCHEMA = { type:'object', required:['opportunities'], properties:{ opportunities:{ type:'array', items:{
  type:'object', required:['id','title','projects','mechanism','seam','anchors','smallest_viable_step','impact','effort'], properties:{
  id:{type:'string'}, title:{type:'string'}, projects:{type:'array',items:{type:'string'},minItems:2},
  direction:{enum:['producer->consumer','bidirectional','shared-resource']}, mechanism:{enum:MECHANISMS},
  seam:{type:'object',required:['type','name'],properties:{type:{enum:['shared-entity','external-system','interface-contract','producer-consumer','cross-cutting-concern']},name:{type:'string'},correlation_key:{type:'string'},data_flowing:{type:'array',items:{type:'string'}}}},
  anchors:{type:'array',minItems:1,items:{type:'object',required:['project','path','why'],properties:{project:{type:'string'},path:{type:'string'},why:{type:'string'}}}},
  smallest_viable_step:{type:'string'}, impact:{type:'object',required:['score','unlocks'],properties:{score:{type:'integer',minimum:1,maximum:5},unlocks:{type:'string'}}},
  effort:{type:'object',required:['size'],properties:{size:{enum:['S','M','L']},drivers:{type:'array',items:{type:'string'}}}},
  dependencies:{type:'array',items:{type:'string'}}, risks:{type:'array',items:{type:'string'}} } } } } }

const VERDICT_SCHEMA = { type:'object', required:['opportunity_id','verdict','confidence'], properties:{
  opportunity_id:{type:'string'}, verdict:{enum:['verified','weakened','rejected']},
  checks:{type:'object',properties:{seam_is_real:{type:'boolean'},both_ends_active:{type:'boolean'},stack_compatible:{type:'boolean'},not_duplicate:{type:'boolean'},effort_realistic:{type:'boolean'}}},
  corrected_effort:{enum:['S','M','L']}, confidence:{type:'number'}, kill_reason:{type:['string','null']}, assumptions:{type:'array',items:{type:'string'}} } }

// ---------- Phase 1: profile each project (only the moved ones in update mode) ----------
phase('Profile')
const fresh = (await pipeline(
  toProfileNow,
  (p) => agent(
    `Profile ONE project, read-only. Project "${p.name}" at ${manifest.root}/${p.path} (lifecycle: ${p.lifecycle}).
     Follow references/profiling-rubric.md. Capture owned vs referenced entities (with keys + the file each is defined in),
     interfaces exposed/consumed, external systems, auth_model, data contracts, existing_integrations, extension points,
     personas, and key file anchors.
     ${p.lifecycle === 'active' ? 'Full depth.' : 'Light capsule + prior_art_notes with a do-not-integrate-live flag.'}
     Only describe THIS project from ITS files. Every claim needs a real file anchor.`,
    { label: `profile:${p.name}`, phase: 'Profile', schema: PROFILE_SCHEMA, agentType: 'Explore' }
  )
)).filter(Boolean)
// Merge fresh profiles with reused prior profiles for unchanged projects (update mode).
const profiles = prior
  ? [...fresh, ...toProfile.filter(p => !moved(p)).map(p => priorProfiles.get(p.name)).filter(Boolean)]
  : fresh

// ---------- Phase 2a: seam index (pure JS — mirrors build-seam-index.mjs) ----------
const seams = buildSeamIndex(profiles, new Set(eligible.map(p => p.name)))
const investigable = seams.filter(s => s.weight >= 2)
log(`${profiles.length} profiles → ${seams.length} seams (${investigable.length} with weight ≥ 2)`)

// optional bounded pairwise: focus projects, or the top-K most-central projects
const centrality = {}
for (const s of seams) for (const m of s.members) centrality[m] = (centrality[m] || 0) + 1
const topK = Object.entries(centrality).sort((a,b)=>b[1]-a[1]).slice(0,4).map(([n])=>n)
const pairwiseTargets = manifest.run_profile === 'focused' ? manifest.focus_projects : topK

// ---------- Phase 2b: investigate each seam ----------
phase('Investigate')
const candidateBatches = await parallel([
  ...investigable.map(s => () => agent(
    `Investigate integration opportunities along ONE seam, per references/integration-taxonomy.md.
     Cluster root: ${manifest.root}. Seam ${s.id}: ${s.type} "${s.key}"${s.owner ? ` (owner: ${s.owner})` : ''}, members: ${s.members.join(', ')}.
     Member profiles attached: ${JSON.stringify(s.members.map(m => profiles.find(p=>p.name===m)))}.
     You may OPEN files under the cluster root to confirm a seam before proposing on it.
     Propose 2–4 CONCRETE opportunities through this seam. Each must name the mechanism, the correlation key
     (how the two sides join), cite a real file per project as an anchor, and give a smallest viable first step.
     Never propose an archived/on-hold member as a live target. Quality over quantity.`,
    { label: `seam:${s.key}`, phase: 'Investigate', schema: OPPS_SCHEMA, agentType: 'Explore' }
  )),
  // bounded pairwise to catch cross-name connections among hubs
  () => agent(
    `Look for integration opportunities BETWEEN these central projects that seam-indexing might miss because
     they model a shared concept under different names: ${pairwiseTargets.join(', ')}. Cluster root: ${manifest.root}.
     Profiles attached: ${JSON.stringify(profiles.filter(p=>pairwiseTargets.includes(p.name)))}.
     You may open files to confirm. Same rules: concrete seam, correlation key, file anchors, smallest step. Return only genuine finds.`,
    { label: 'pairwise:hubs', phase: 'Investigate', schema: OPPS_SCHEMA, agentType: 'Explore' }
  ),
])
let candidates = candidateBatches.filter(Boolean).flatMap(b => b.opportunities)

// ---------- Phase 3: adversarial verify + rank ----------
phase('Verify')
const verdicts = (await parallel(candidates.map(o => () => agent(
  `Adversarially evaluate ONE integration opportunity — try to FALSIFY it. Cluster root: ${manifest.root}.
   Opportunity: ${JSON.stringify(o)}. Profiles of its projects attached: ${JSON.stringify(profiles.filter(p=>o.projects.includes(p.name)))}.
   Check in order: (1) OPEN each cited anchor file under the cluster root — does it exist and actually evidence
   the seam (the named entity/contract/interface)? If an anchor is missing or doesn't support the claim, fail
   seam_is_real. (2) lifecycle — is either end archived/on-hold? if so REJECT. (3) is a join key named for a
   data/entity seam? if not, weaken. (4) are the stacks/hosting/auth compatible? (5) does it duplicate something
   that already exists? (6) is the effort honest? Return a Verdict. A clear rejection with a reason is a good outcome.`,
  { label: `verify:${o.id}`, phase: 'Verify', schema: VERDICT_SCHEMA, agentType: 'Explore' }
)))).filter(Boolean)

const W = { S:1.0, M:0.6, L:0.3 }
const ranked = candidates.map(o => {
  const v = verdicts.find(x => x.opportunity_id === o.id) || {}
  const status = v.verdict || 'candidate'
  const effort = v.corrected_effort || o.effort?.size || 'M'
  const conf = v.confidence ?? 0.5
  return { ...o, status, confidence: conf, assumptions: v.assumptions, kill_reason: v.kill_reason,
           rank_score: (o.impact?.score || 1) * (W[effort] || 0.6) * conf }
})
const live = ranked.filter(o => o.status !== 'rejected').sort((a,b) => b.rank_score - a.rank_score)
const rejected = ranked.filter(o => o.status === 'rejected')

// ---------- Phase 4: synthesis agent returns markdown; main loop writes the files ----------
phase('Synthesise')
const sharedInfra = seams.filter(s => s.weight >= 3)  // promote to shared-infra findings
const markdown = await agent(
  `Compose ARCHITECTURE-OVERVIEW.md EXACTLY per references/output-template.md. Inputs:
   manifest=${JSON.stringify(manifest)}
   profiles=${JSON.stringify(profiles)}
   ranked_opportunities=${JSON.stringify(live)}
   rejected=${JSON.stringify(rejected.map(r=>({id:r.id,kill_reason:r.kill_reason})))}
   shared_infra_seams=${JSON.stringify(sharedInfra)}
   ${prior ? `prior_register=${JSON.stringify(prior.opportunities?.map(o=>({id:o.id,status:o.status})) || [])}` : ''}
   Build the mermaid map (solid = existing seams, dashed = proposed opportunities, colour = lifecycle).
   Sequence the roadmap with Wave 0 = shared foundations that unblock multiple opportunities.
   Section 8 MUST state what this adds beyond ${JSON.stringify(manifest.existing_artifacts)}. Do not restate the catalogue.
   ${prior ? 'This is an UPDATE: §8 must list new / changed / resolved / newly-rejected opportunities vs prior_register, not just a static delta.' : ''}
   Return only the finished markdown.`,
  { label: 'synthesise', phase: 'Synthesise' }
)

return { markdown, manifest, profiles, seams, opportunities: ranked }

// ===== pure-JS seam index — kept identical to scripts/build-seam-index.mjs (canonical types) =====
function buildSeamIndex(profiles, eligibleNames) {
  const norm = s => String(s||'').toLowerCase().replace(/[\s_\-]/g,'').replace(/s$/,'')
  const base = f => String(f).split(/[\\/]/).pop()
  const SLUG = {'shared-entity':'entity','external-system':'extsys','interface-contract':'contract','producer-consumer':'producer-consumer','cross-cutting-concern':'concern'}
  const seams = [], seenIds = new Set()
  const add = (type, key, evidence, owner=null) => {
    const id = `seam-${SLUG[type]}-${key}`
    if (seenIds.has(id)) return
    const members = [...new Set(evidence.map(e => e.project))]
    if (members.length < 2) return
    seenIds.add(id)
    const eligible_members = members.filter(m => eligibleNames.has(m))
    seams.push({ id, type, key, members, owner, evidence, weight: eligible_members.length, eligible_members })
  }
  // shared-entity
  const ent = {}
  for (const p of profiles) {
    for (const e of (p.owned_entities||[])) { const k=norm(e.name); (ent[k] ??= {owner:null, ev:[]}); ent[k].owner=p.name; ent[k].ev.push({project:p.name, anchor:e.defined_in, role:'owner'}) }
    for (const e of (p.referenced_entities||[])) { const k=norm(typeof e==='string'?e:e.name); (ent[k] ??= {owner:null, ev:[]}).ev.push({project:p.name, anchor:'(referenced)'}) }
  }
  for (const [k,v] of Object.entries(ent)) add('shared-entity', k, v.ev, v.owner)
  // external-system
  const ext = {}
  for (const p of profiles) for (const s of (p.external_systems||[])) (ext[norm(s)] ??= []).push({project:p.name, anchor:s})
  for (const [k,ev] of Object.entries(ext)) add('external-system', k, ev)
  // interface-contract (by basename)
  const con = {}
  for (const p of profiles) for (const c of (p.data_contracts||[])) (con[base(c)] ??= []).push({project:p.name, anchor:c})
  for (const [k,ev] of Object.entries(con)) add('interface-contract', k, ev)
  // producer-consumer — full-name token match (len ≥ 4) or the interface's own name; guards nameless interfaces
  const tokenMatch = (target, name) => { const t=norm(target), n=norm(name); return !!n && n.length>=4 && t.includes(n) }
  for (const a of profiles) for (const iface of (a.interfaces_exposed||[]))
    for (const b of profiles) if (a!==b) for (const c of (b.interfaces_consumed||[]))
      if (tokenMatch(c.target, a.name) || tokenMatch(c.target, iface.name))
        add('producer-consumer', `${norm(a.name)}-to-${norm(b.name)}`, [{project:a.name,anchor:iface.anchor},{project:b.name,anchor:'(consumes)'}], a.name)
  // cross-cutting capability (no generic data-store tag — see build-seam-index.mjs for why)
  const CAP = [['pdf-rendering',/render|pdf|report/],['auth',/auth|oauth|sso|entra|identity|gateway/],['design-system',/design|theme|brand|ui.?kit|component/],['parsing',/parse|extract|document.?map|mapper/]]
  const cap = {}
  for (const p of profiles) { const hay = norm([...(p.stack||[]),p.role,...(p.extension_points||[])].join(' '))
    for (const [tag,re] of CAP) if (re.test(hay)) (cap[tag] ??= []).push({project:p.name, anchor:'(capability)'}) }
  for (const [k,ev] of Object.entries(cap)) add('cross-cutting-concern', k, ev)
  return seams
}
```

> **Before authoring:** confirm the exact `Workflow` primitive names and signatures (`agent`, `pipeline`,
> `parallel`, `phase`, `log`, the `schema`/`agentType` options, and `meta.phases`) against the Workflow
> tool's own spec — this template reflects the documented API, but "adapt, don't run blind."

## Fallback (no Workflow tool)

Same five phases, run with parallel `Agent`/`Explore` calls. Fan-out = send N tool calls in one
message; barrier = wait for all before the next phase. Use the schemas from `references/schemas.md`
verbatim in each prompt and ask for matching JSON.

1. **Scout** inline (`scripts/scout-cluster.mjs` or your tools) → manifest.
2. **Profile** — one `Explore` agent per project, all in one message. Collect the `ProjectProfile`s.
3. **Seam index** — run `node scripts/build-seam-index.mjs profiles.json` (write the profiles to a
   temp file first), or do the grouping by hand following `linkage-method.md`.
4. **Investigate** — one agent per seam (weight ≥ 2) + one bounded-pairwise agent, in one message.
5. **Verify** — one verifier per candidate, in one message. Then rank by impact × effort_weight ×
   confidence in your head or a scratch script.
6. **Synthesise & write** — you write `ARCHITECTURE-OVERVIEW.md` (per `output-template.md`) and the
   `.json` companion. No agent needed.

## Scaling notes

- **Concurrency** caps at ~10–16. For very large N, `pipeline` already queues Phase 1; in fallback,
  send profiles in batches of ~10.
- **Focused mode** profiles only the named projects and investigates only the seams between them —
  a much shorter run that returns a deep two/three-way brief, not a full audit.
- **Update mode** — the main loop reads the prior `.json` inline and attaches it as `manifest.prior`
  before invoking Workflow (the script can't read files). The script then re-profiles only projects whose
  git head moved (compares `last_commit`), reuses prior profiles for the rest, passes the prior register to
  synthesis for the §8 delta, and (via the kept rejections) avoids re-proposing dead ideas. See the
  `prior` / `toProfileNow` / `moved` branch in the script above.
