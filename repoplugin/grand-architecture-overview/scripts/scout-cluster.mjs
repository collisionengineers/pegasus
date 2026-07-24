#!/usr/bin/env node
// scout-cluster.mjs — deterministic, read-only cluster discovery.
//
//   node scout-cluster.mjs <cluster-root>   →   ClusterManifest JSON on stdout
//
// Best-effort: it finds projects, infers lifecycle/role, reads git remotes, and locates
// existing cross-project artifacts, so Phase 0 doesn't burn agent tokens on directory walking.
// It never throws — if something can't be read, it's omitted and the caller (Claude) refines.
// This is an accelerator, not an authority: review and augment the manifest before fanning out.

import { readdirSync, statSync, existsSync } from 'node:fs';
import { join, basename, relative } from 'node:path';
import { execSync } from 'node:child_process';

const root = (process.argv[2] || process.cwd()).replace(/[\\/]+$/, '');

const IGNORE = new Set([
  'node_modules', '.git', 'dist', 'build', 'bin', 'obj', '.next', 'out', 'vendor',
  '.venv', 'venv', '__pycache__', '.turbo', 'coverage', '.svelte-kit', 'target', '.idea', '.vs',
]);
const MANIFESTS = ['package.json', 'pyproject.toml', 'requirements.txt', 'go.mod', 'Cargo.toml',
  '*.csproj', '*.sln', 'wrangler.toml', 'README.md', 'CLAUDE.md', 'AGENTS.md', 'CONTEXT.md'];
const LIFECYCLE = { active: 'active', archive: 'archive', archived: 'archive',
  'on-hold': 'on-hold', 'on-hold-projects': 'on-hold', onhold: 'on-hold', 'on_hold': 'on-hold' };
const ROLE_BUCKETS = { connectors: 'connector', connector: 'connector', skills: 'skills',
  research: 'research', context: 'context', tools: 'tool', tool: 'tool', packages: 'library', libs: 'library' };

const isDir = (p) => { try { return statSync(p).isDirectory(); } catch { return false; } };
const hasGit = (p) => existsSync(join(p, '.git'));
const hasManifest = (p) => {
  let entries; try { entries = readdirSync(p); } catch { return false; }
  return entries.some((f) =>
    MANIFESTS.includes(f) ||
    (f.endsWith('.csproj')) || (f.endsWith('.sln')));
};
const sh = (cmd) => { try { return execSync(cmd, { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim(); } catch { return null; } };

// ---- collect candidate project directories ----
const gitProjects = new Set();   // dirs that are their own repo
const all = [];                  // every dir we consider a project

function walk(dir, depth) {
  if (depth > 5) return;
  let entries;
  try { entries = readdirSync(dir, { withFileTypes: true }); } catch { return; }
  for (const e of entries) {
    if (!e.isDirectory()) continue;
    if (IGNORE.has(e.name)) continue;
    if (e.name.startsWith('.') && e.name !== '.') continue;
    const full = join(dir, e.name);
    if (hasGit(full)) { gitProjects.add(full); walk(full, depth + 1); }   // recurse: nested repos
    else walk(full, depth + 1);
  }
}
walk(root, 0);

// non-git projects: manifest-bearing dirs near the top that contain no git descendant
function collectNonGit(dir, depth) {
  if (depth > 3) return;
  let entries;
  try { entries = readdirSync(dir, { withFileTypes: true }); } catch { return; }
  for (const e of entries) {
    if (!e.isDirectory() || IGNORE.has(e.name) || e.name.startsWith('.')) continue;
    const full = join(dir, e.name);
    if (gitProjects.has(full)) continue;
    let descHasGit = false;
    for (const g of gitProjects) if (g.startsWith(full + '/') || g.startsWith(full + '\\')) { descHasGit = true; break; }
    if (descHasGit) { collectNonGit(full, depth + 1); continue; }
    if (hasManifest(full)) all.push({ path: full, is_git: false });
    else collectNonGit(full, depth + 1);
  }
}
for (const g of gitProjects) all.push({ path: g, is_git: true });
collectNonGit(root, 0);

// de-dupe (a git project shouldn't also appear as non-git)
const seen = new Set();
const candidates = all.filter((c) => (seen.has(c.path) ? false : seen.add(c.path)));

// ---- enrich each project ----
function inferLifecycle(relPath) {
  for (const seg of relPath.split(/[\\/]/)) if (LIFECYCLE[seg.toLowerCase()]) return LIFECYCLE[seg.toLowerCase()];
  return 'unknown';
}
function inferRole(relPath, name) {
  for (const seg of relPath.split(/[\\/]/)) if (ROLE_BUCKETS[seg.toLowerCase()]) return ROLE_BUCKETS[seg.toLowerCase()];
  const n = name.toLowerCase();
  if (/website|web-?dev|site/.test(n)) return 'website';
  if (/connector/.test(n)) return 'connector';
  if (/skill/.test(n)) return 'skills';
  if (/research|evals?|dataset/.test(n)) return 'research';
  if (/context|kb|knowledge/.test(n)) return 'context';
  if (/lib|sdk|mapper|renderer|tool/.test(n)) return 'library';
  return 'app';
}

const projects = candidates.map((c) => {
  const name = basename(c.path);
  const rel = relative(root, c.path) || '.';
  const lifecycle = inferLifecycle(rel);
  const role = inferRole(rel, name);
  const remote = c.is_git ? (sh(`git -C "${c.path}" remote get-url origin`) || null) : null;
  const last_commit = c.is_git ? (sh(`git -C "${c.path}" log -1 --format=%cs`) || null) : null;
  const eligible = lifecycle !== 'archive' && lifecycle !== 'on-hold';
  let docCount = 0, fileCount = 0;
  try {
    for (const f of readdirSync(c.path)) { fileCount++; if (/\.(md|mdx)$/i.test(f)) docCount++; }
  } catch { /* ignore */ }
  return {
    name, path: rel.replace(/\\/g, '/'), is_git: c.is_git,
    ...(remote ? { remote: remote.replace(/^https?:\/\//, '').replace(/\.git$/, '') } : {}),
    lifecycle, role,
    eligible_for_live_integration: eligible,
    ...(eligible ? {} : { mine_as_prior_art: true }),
    size_hint: { top_level_entries: fileCount, top_level_docs: docCount },
    ...(last_commit ? { last_commit } : {}),
  };
});

// ---- shape detection ----
const rootIsGit = hasGit(root);
let shape;
if (gitProjects.size > 1) shape = 'monorepo-of-repos';
else if (rootIsGit) shape = candidates.length > 1 ? 'single-repo-multipackage' : 'single-repo';
else shape = candidates.length > 0 ? 'flat-dirs' : 'empty';

// github-org heuristic: a folder of cloned repos (root not itself a repo) whose remotes share one
// org owner. The user can't point scout at a remote org directly — they clone the org under one
// parent dir first (or `gh repo list <org> | …`), then scout sees this shape.
const orgOf = (r) => { const m = String(r || '').match(/github\.com[/:]([^/]+)\//i); return m ? m[1].toLowerCase() : null; };
const orgs = new Set(projects.map((p) => orgOf(p.remote)).filter(Boolean));
if (!rootIsGit && gitProjects.size > 1 && orgs.size === 1) shape = 'github-org';
const remote_org = orgs.size === 1 ? [...orgs][0] : null;

// ---- existing artifacts (clean paths) + context-store dirs (tracked separately) ----
const existing_artifacts = [];
const contextDirs = [];
function findArtifacts(dir, depth) {
  if (depth > 2) return;
  let entries; try { entries = readdirSync(dir, { withFileTypes: true }); } catch { return; }
  for (const e of entries) {
    if (IGNORE.has(e.name) || e.name.startsWith('.')) continue;
    const full = join(dir, e.name);
    const rel = relative(root, full).replace(/\\/g, '/');
    if (e.isFile()) {
      if (/^INDEX\.md$/i.test(e.name)) existing_artifacts.push(rel);
      if (/grand-architecture-overview\.(json|md)$/i.test(e.name)) existing_artifacts.push(rel);
      if (/constellation|architecture-overview/i.test(e.name) && e.name.endsWith('.md')) existing_artifacts.push(rel);
    } else if (e.isDirectory()) {
      if (/context/i.test(e.name)) contextDirs.push(rel);
      findArtifacts(full, depth + 1);
    }
  }
}
findArtifacts(root, 0);

const priorOverview = existing_artifacts.find((a) => /grand-architecture-overview\.json/i.test(a)) || null;

const manifest = {
  cluster_name: basename(root),
  root: root.replace(/\\/g, '/'),
  shape,
  ...(remote_org ? { remote_org } : {}),
  run_profile: 'full',          // caller overrides to 'focused' / 'update' based on the user's ask
  focus_projects: [],
  projects: projects.sort((a, b) => a.path.localeCompare(b.path)),
  existing_artifacts: [...new Set(existing_artifacts)],
  prior_overview: priorOverview,
  context_store_dir: contextDirs.sort((a, b) => a.length - b.length)[0] || null,  // shallowest context dir
};

process.stdout.write(JSON.stringify(manifest, null, 2) + '\n');
