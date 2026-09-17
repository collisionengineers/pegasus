// Sprint 1609 dispatch: implement the verified sprint tasks with Codex, verify each branch on this
// host behind one serialized slot, review with a separate thread, and deliver as PRs.
//
// Run through the codex-bridge native runner (see README.md). This file is ordinary Node code and
// runs with this process's permissions: git, dotnet, pwsh and gh are spawned directly.
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { TASKS, CASE_FEATURE_CLASSES } from "./tasks.mjs";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const CO_AUTHOR = "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>";
const PR_FOOTER = "🤖 Generated with [Claude Code](https://claude.com/claude-code)";
const STAGES = ["pending", "prepared", "implemented", "verified", "reviewed", "delivered"];
// Every Codex turn goes through the shared loopback app-server (`codex-bridge.mjs server start`):
// three private servers cold-starting their MCP servers at once overran the 30 s initialize handshake.
const VIA = process.env.SPRINT_CODEX_VIA ?? "stdio";
// Persisted end states: a task in one of these is not re-run by a later invocation. A task left
// out by `only`/`skip`, a dry run, or an unmet dependency is never persisted as done.
const TERMINAL = new Set(["delivered", "blocked", "needs-human"]);

export default async ({ codex, log, args: rawArgs, runDir, writeFile }) => {
  const args = {
    push: true, only: [], skip: [], dryRun: false, waitForMergeMinutes: 180, maxFixRounds: 3, maxReviewRounds: 2,
    minFreeGb: 10, buildTimeoutMinutes: 40, testTimeoutMinutes: 90, ...rawArgs
  };
  const main = path.resolve(args.mainCheckout);
  const wtRoot = path.resolve(args.worktreeRoot);
  const stateDir = path.join(main, "artifacts", "sprint-1609");
  fs.mkdirSync(stateDir, { recursive: true });
  for (const d of ["verify", "trx", "pr"]) fs.mkdirSync(path.join(runDir, d), { recursive: true });

  // ---------------------------------------------------------------- process helpers
  const baseEnv = {
    ...process.env,
    MSBUILDDISABLENODEREUSE: "1", DOTNET_CLI_USE_MSBUILD_SERVER: "0", DOTNET_CLI_TELEMETRY_OPTOUT: "1",
    DOTNET_NOLOGO: "1", GH_PROMPT_DISABLED: "1", GIT_TERMINAL_PROMPT: "0"
  };
  /** Small-output command: captured stdout, throws on failure unless ok:true. */
  function cap(cmd, argv, { cwd = main, env = {}, ok = false, timeoutMs = 120000, raw = false } = {}) {
    const r = spawnSync(cmd, argv, { cwd, env: { ...baseEnv, ...env }, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], windowsHide: true, timeout: timeoutMs, maxBuffer: 64 * 1024 * 1024 });
    if (r.error) throw new Error(`${cmd} ${argv.join(" ")}: ${r.error.message}`);
    if (r.status !== 0 && !ok) throw new Error(`${cmd} ${argv.join(" ")} failed (${r.status}): ${(r.stderr || r.stdout || "").trim().slice(-2000)}`);
    // raw keeps leading whitespace: `git status --porcelain` lines start with a status column.
    return { status: r.status, out: raw ? (r.stdout ?? "") : (r.stdout ?? "").trim(), err: (r.stderr ?? "").trim() };
  }
  /** Porcelain status paths, with the two-column status prefix stripped correctly. */
  const porcelainPaths = (cwd) => cap("git", ["status", "--porcelain", "--untracked-files=all"], { cwd, raw: true }).out
    .split(/\r?\n/).filter((l) => l.length > 3).map((l) => l.slice(3).trim().replace(/^"|"$/g, ""));
  /** Long command: stdout+stderr appended to a log file, never buffered. status null = timeout. */
  function longRun(cmd, argv, { cwd, env = {}, logFile, timeoutMs }) {
    const fd = fs.openSync(logFile, "a");
    fs.writeSync(fd, `\n=== ${new Date().toISOString()} ${cmd} ${argv.join(" ")}\n=== cwd ${cwd}\n`);
    let r;
    try { r = spawnSync(cmd, argv, { cwd, env: { ...baseEnv, ...env }, stdio: ["ignore", fd, fd], windowsHide: true, timeout: timeoutMs, killSignal: "SIGKILL" }); }
    finally { fs.closeSync(fd); }
    if (r.error && r.error.code !== "ETIMEDOUT") throw new Error(`${cmd}: ${r.error.message}`);
    const status = r.status === null ? null : r.status;
    fs.appendFileSync(logFile, `=== exit ${status === null ? "TIMEOUT" : status}\n`);
    return status;
  }
  const git = (cwd, ...argv) => cap("git", argv, { cwd });
  const gitOk = (cwd, ...argv) => cap("git", argv, { cwd, ok: true });
  const gh = (argv, opts = {}) => cap("gh", argv, { cwd: main, timeoutMs: 180000, ...opts });
  const pwsh = (cwd, file, argv, logFile, timeoutMs = 20 * 60000) => longRun("pwsh", ["-NoProfile", "-NonInteractive", "-File", file, ...argv], { cwd, logFile, timeoutMs });
  const tail = (file, n = 150) => { try { const l = fs.readFileSync(file, "utf8").split(/\r?\n/); return l.slice(-n).join("\n"); } catch { return "(no log)"; } };
  const now = () => new Date().toISOString();
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  const rel = (p) => p.replace(/\\/g, "/");

  // ---------------------------------------------------------------- state
  const stateFile = path.join(stateDir, "state.json");
  const state = fs.existsSync(stateFile) ? JSON.parse(fs.readFileSync(stateFile, "utf8")) : { runs: [], tasks: {} };
  if (!args.dryRun) state.runs.push({ runDir, startedAt: now() });
  const saveState = () => {
    const tmp = `${stateFile}.tmp`;
    fs.writeFileSync(tmp, JSON.stringify(state, null, 2), "utf8");
    fs.renameSync(tmp, stateFile);
    try { writeFile("state.json", state); } catch { /* mirror only */ }
  };
  const ts = (id) => (state.tasks[id] ??= { id, state: "pending", verifyRound: 0, reviewRound: 0, history: [] });
  const setState = (id, next, extra = {}) => {
    const t = ts(id);
    Object.assign(t, extra, { state: next, updatedAt: now() });
    t.history.push({ at: t.updatedAt, state: next, ...(extra.lastError ? { error: String(extra.lastError).slice(0, 500) } : {}) });
    saveState();
    log(`[${id}] -> ${next}${extra.lastError ? `: ${String(extra.lastError).slice(0, 200)}` : ""}`);
  };

  // stage events for dependencies
  const reached = new Map(); // `${id}:${stage}` -> Promise resolvers
  const eventFor = (key) => { if (!reached.has(key)) { let resolve; const p = new Promise((r) => { resolve = r; }); reached.set(key, { p, resolve }); } return reached.get(key); };
  const signal = (id, stage) => eventFor(`${id}:${stage}`).resolve(ts(id).state);
  const signalTerminal = (id) => { for (const s of [...STAGES, "terminal"]) signal(id, s); };
  const waitFor = (id, stage) => eventFor(`${id}:${stage}`).p;

  // ---------------------------------------------------------------- host slot (one build/test at a time)
  const slotFile = path.join(stateDir, "host-slot.json");
  const ledger = fs.existsSync(slotFile) ? JSON.parse(fs.readFileSync(slotFile, "utf8")) : { host: os.hostname(), entries: [] };
  const ledgerWrite = (entry) => { ledger.entries.push({ at: now(), pid: process.pid, ...entry }); fs.writeFileSync(slotFile, JSON.stringify(ledger, null, 2), "utf8"); };
  let slotChain = Promise.resolve();
  function listProcesses() {
    const script = "Get-CimInstance Win32_Process -Filter \"Name='testhost.exe' OR Name='testhost.x86.exe' OR Name='dotnet.exe' OR Name='VBCSCompiler.exe' OR Name='MSBuild.exe'\" | Select-Object ProcessId,Name,CommandLine,ExecutablePath | ConvertTo-Json -Compress";
    const r = cap("pwsh", ["-NoProfile", "-NonInteractive", "-Command", script], { ok: true });
    if (!r.out) return [];
    try { const v = JSON.parse(r.out); return Array.isArray(v) ? v : [v]; } catch { return []; }
  }
  async function waitForForeignTestHosts(id) {
    const deadline = Date.now() + 30 * 60000;
    for (;;) {
      const hosts = listProcesses().filter((p) => /testhost/i.test(p.Name ?? "") || /dotnet\.exe$/i.test(p.Name ?? "") && /\sdotnet(\.dll)?\s+test\s/i.test(p.CommandLine ?? ""));
      if (!hosts.length) return;
      if (Date.now() > deadline) throw new Error(`host slot refused: foreign test processes still running: ${hosts.map((h) => `${h.Name}#${h.ProcessId}`).join(", ")}`);
      log(`[${id}] waiting for ${hosts.length} foreign test process(es) to finish before taking the host slot`);
      await sleep(30000);
    }
  }
  function sweepWorktreeProcesses(dir) {
    const needle = rel(dir).toLowerCase();
    const mine = listProcesses().filter((p) => rel(`${p.CommandLine ?? ""} ${p.ExecutablePath ?? ""}`).toLowerCase().includes(needle));
    for (const p of mine) { cap("pwsh", ["-NoProfile", "-NonInteractive", "-Command", `Stop-Process -Id ${p.ProcessId} -Force -ErrorAction SilentlyContinue`], { ok: true }); }
    return mine.length;
  }
  function withHostSlot(id, dir, fn) {
    const run = slotChain.then(async () => {
      await waitForForeignTestHosts(id);
      ledgerWrite({ state: "owner", task: id, worktree: dir });
      try { return await fn(); }
      finally {
        cap("dotnet", ["build-server", "shutdown"], { cwd: dir, ok: true, timeoutMs: 120000 });
        const swept = sweepWorktreeProcesses(dir);
        ledgerWrite({ state: "idle", task: id, worktree: dir, swept });
      }
    });
    slotChain = run.catch(() => {});
    return run;
  }

  // ---------------------------------------------------------------- docs-gate merge wait (Phase A -> B)
  let mergeWait = null;
  function waitForDocsGateMerge() {
    if (mergeWait) return mergeWait;
    mergeWait = (async () => {
      const u1 = ts("U1");
      if (args.dryRun) return true;
      if (state.docsGateMerged) return true;
      const deadline = Date.now() + args.waitForMergeMinutes * 60000;
      for (;;) {
        const num = u1.prNumber ?? state.docsGatePr;
        if (num) {
          const r = gh(["pr", "view", String(num), "--json", "state,mergedAt"], { ok: true });
          try { const v = JSON.parse(r.out); if (v.state === "MERGED") { state.docsGateMerged = true; saveState(); git(main, "fetch", "origin"); log("docs-gate PR merged; origin/dev refreshed"); return true; } } catch { /* retry */ }
        } else if (TERMINAL.has(u1.state) && u1.state !== "delivered") {
          // U1 did not deliver; check whether dev already has the fix (someone else may have done it)
          if (devHasLinkFix()) { state.docsGateMerged = true; saveState(); return true; }
        }
        if (Date.now() > deadline) return false;
        await sleep(60000);
      }
    })();
    return mergeWait;
  }
  function devHasLinkFix() {
    git(main, "fetch", "origin");
    const r = gitOk(main, "show", "origin/dev:design/planning-and-old-designs/v27_planning/current/v27-notes.md");
    return r.status === 0 && !r.out.includes(".claude/skills/razor-html-mockup-creation");
  }

  // ---------------------------------------------------------------- worktrees
  function freeGb() { try { const s = fs.statfsSync(main); return (s.bavail * s.bsize) / 1024 ** 3; } catch { return Infinity; } }
  function worktreeList() {
    const out = git(main, "worktree", "list", "--porcelain").out.split(/\r?\n/);
    const items = []; let cur = null;
    for (const line of out) {
      if (line.startsWith("worktree ")) { cur = { dir: line.slice(9), branch: null }; items.push(cur); }
      else if (line.startsWith("branch ") && cur) cur.branch = line.slice(7).replace("refs/heads/", "");
    }
    return items;
  }
  function prepareWorktree(task) {
    const dir = path.join(wtRoot, task.dir);
    const existing = worktreeList().find((w) => rel(path.resolve(w.dir)).toLowerCase() === rel(dir).toLowerCase());
    if (existing) {
      if (existing.branch !== task.branch) throw new Error(`worktree ${dir} is on ${existing.branch}, expected ${task.branch}`);
      return dir;
    }
    if (task.existingDir) throw new Error(`expected existing worktree ${dir} for ${task.branch}`);
    if (freeGb() < args.minFreeGb) throw new Error(`refusing to add a worktree with ${freeGb().toFixed(1)} GB free (< ${args.minFreeGb})`);
    if (worktreeList().some((w) => w.branch === task.branch)) throw new Error(`branch ${task.branch} is already checked out elsewhere`);
    const localBranch = gitOk(main, "rev-parse", "--verify", "--quiet", `refs/heads/${task.branch}`).status === 0;
    if (localBranch) {
      git(main, "worktree", "add", dir, task.branch);
      if (task.baseRef && task.baseRef.startsWith("origin/") && git(dir, "rev-parse", "HEAD").out !== git(dir, "rev-parse", task.baseRef).out) {
        const ff = gitOk(dir, "merge", "--ff-only", task.baseRef);
        if (ff.status !== 0) throw new Error(`local ${task.branch} cannot fast-forward to ${task.baseRef}; reconcile it first`);
        log(`[${task.id}] fast-forwarded stale local ${task.branch} to ${task.baseRef}`);
      }
    } else {
      git(main, "worktree", "add", "-b", task.branch, dir, task.baseRef);
      gitOk(dir, "branch", "--unset-upstream");
    }
    return dir;
  }
  function assertLongPaths() {
    const r = cap("pwsh", ["-NoProfile", "-NonInteractive", "-Command", "(Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\FileSystem').LongPathsEnabled"], { ok: true });
    if (r.out.trim() !== "1") throw new Error("LongPathsEnabled is not 1; MSBuild output under long worktree paths will fail");
  }

  // ---------------------------------------------------------------- Codex turns
  const preamble = (task, dir) => {
    const verifier = [];
    if (task.build) verifier.push("dotnet build Pegasus.slnx -c Release");
    for (const t of task.tests ?? []) verifier.push(`dotnet test ${t.project} -c Release --no-build${t.filter ? ` --filter "${t.filter}"` : ""}`);
    for (const s of task.preTestScripts ?? []) verifier.push(`pwsh ${s.args.join(" ")}`);
    if (task.docs) verifier.push("pwsh scripts/Test-DocumentationLinks.ps1", `pwsh scripts/Test-MarkdownPlacement.ps1 -Base ${task.placementBase ?? "origin/dev"} -Head HEAD`);
    return `Sprint 1609 dispatch, task ${task.id}. You are the pegasus-implementer for one bounded change in the git worktree ${rel(dir)} on branch ${task.branch}. The primary (this dispatcher) owns assignment, verification, integration and delivery.

Host-slot rule (AGENTS.md): you do NOT hold the host slot. The sprint verifier, a separate serialized process, runs the build, tests and documentation scripts after your turn. Do not run dotnet build, dotnet test, dotnet restore, any scripts/*.ps1 verification script, a browser, packaging or cloud commands. Record each check you would have run in "checks" with evidence_ref null and the exact command in "reason". The verifier will run exactly:
${verifier.map((v) => `  - ${v}`).join("\n")}

Rules: do not commit; do not modify anything under 1609sprint/, corpus/, reference-evidence/ or artifacts/; do not change unrelated files; read AGENTS.md, docs/index.md, CONTEXT.md and the owning FRD before editing; keep every existing assertion, theory row and skip condition unless the task says otherwise; write no new operator-facing copy beyond what the task quotes (necessary copy is operator-approved). Reference documents named below with absolute paths are read-only inputs outside this worktree.

If the task cannot be completed as specified, stop and report outcome "blocked" with the question; do not improvise a different scope.

`;
  };
  // A failed app-server handshake is host load (a Release build saturating the machine), not the
  // task: retry with backoff instead of blocking the task. Anything else fails through normally.
  const HANDSHAKE = /initialize response|ECONNREFUSED|readyz|server is not running/i;
  // Two-way exclusion between Release builds and app-server handshakes: a handshake that overlaps a
  // build times out, so a build waits for handshakes in flight (at most 60 s) and a handshake waits
  // for the build. Both checks and flag updates are synchronous, so there is no window between them.
  let buildingNow = false;
  let handshakes = 0;
  async function codexRetrying(options, label) {
    const attempts = args.handshakeRetries ?? 6;
    for (let attempt = 1; ; attempt++) {
      if (buildingNow) { log(`${label}: waiting for the verifier's build to finish before starting the app-server`); while (buildingNow) await sleep(15000); }
      handshakes += 1;
      setTimeout(() => { handshakes -= 1; }, 60000).unref?.();
      const env = await codex({ ...options, label: attempt === 1 ? label : `${label}:retry${attempt}` });
      if (!(env.status === "failed" && HANDSHAKE.test(String(env.error ?? ""))) || attempt >= attempts) return env;
      log(`${label}: app-server handshake failed (${String(env.error).slice(0, 80)}); retry ${attempt}/${attempts - 1} in 90 s`);
      await sleep(90000);
    }
  }
  async function implementTurn(task, dir, extraTask = "") {
    const brief = fs.readFileSync(path.join(HERE, task.brief), "utf8");
    const task_ = preamble(task, dir) + brief + (extraTask ? `\n\n${extraTask}` : "");
    const env = await codexRetrying({ preset: "implement", task: task_, cwd: dir, via: VIA }, `${task.id}:implement`);
    return checkTurn(task, env);
  }
  async function fixTurn(task, dir, threadId, message) {
    const env = await codexRetrying({ preset: "implement", threadId, cwd: dir, task: message, via: VIA }, `${task.id}:fix`);
    return checkTurn(task, env);
  }
  function checkTurn(task, env) {
    const info = { status: env.status, outcome: env.outcome ?? null, threadId: env.thread_id, files: env.files_changed ?? [], questions: env.questions ?? [], summary: typeof env.summary === "string" ? env.summary : env.summary?.summary ?? "", result_file: env.result_file ?? env.out_file ?? null };
    // Only the executed command text counts. The task text names the verifier's commands, and a
    // command's captured output can quote "dotnet test" (a skill file did), so neither is scanned.
    // Scripts count only when invoked (-File, &, ./, pwsh ...), not when named as a path to rg/git/Get-Content.
    const forbidden = /(^|[\s"'\\/])dotnet(\.exe)?["']?\s+(build|test|restore)\b|(?:-File\s+|&\s*|pwsh(?:\.exe)?\s+|powershell(?:\.exe)?\s+|\.[\\/]|^\s*)["']?(?:\.?[\\/])?scripts[\\/](?:Test-DocumentationLinks|Test-MarkdownPlacement|Test-TestShard|Invoke-TestShard)\.ps1/im;
    const ran = [];
    try {
      for (const line of fs.readFileSync(env.events_file, "utf8").split(/\r?\n/)) {
        if (!line.includes("commandExecution")) continue;
        let item; try { item = JSON.parse(line)?.params?.item; } catch { continue; }
        if (item?.type !== "commandExecution") continue;
        const cmd = Array.isArray(item.command) ? item.command.join(" ") : String(item.command ?? "");
        if (forbidden.test(cmd)) ran.push(cmd.slice(0, 160));
      }
    } catch { /* no events file */ }
    info.ranDotnet = ran.length > 0;
    info.forbiddenCommands = ran;
    return info;
  }
  const wip = (task, dir, n) => {
    if (gitOk(dir, "status", "--porcelain").out === "") return git(dir, "rev-parse", "HEAD").out;
    git(dir, "add", "-A");
    git(dir, "commit", "-q", "-m", `wip(${task.id}): round ${n}`);
    return git(dir, "rev-parse", "HEAD").out;
  };

  // ---------------------------------------------------------------- verification
  function parseCounts(logFile) {
    const text = fs.readFileSync(logFile, "utf8");
    const m = [...text.matchAll(/(Passed|Failed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)/g)];
    return m.map((x) => ({ result: x[1], failed: +x[2], passed: +x[3], skipped: +x[4], total: +x[5] }));
  }
  function discoveryCompare(dir, logFile) {
    const listing = cap("dotnet", ["test", "tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj", "-c", "Release", "--no-build", "--filter", "Category!=Corpus", "--list-tests"], { cwd: dir, ok: true, timeoutMs: 15 * 60000 });
    fs.appendFileSync(logFile, `\n=== discovery listing (exit ${listing.status})\n${listing.out}\n${listing.err}\n`);
    const names = listing.out.split(/\r?\n/).map((l) => l.trim()).filter((l) => l.startsWith("Pegasus.IntegrationTests."));
    const baseline = fs.readFileSync(args.baselineListing, "utf8").split(/\r?\n/).map((l) => l.trim()).filter(Boolean);
    const classRe = /^Pegasus\.IntegrationTests\.(\w+)\.(.+)$/;
    const count = (arr) => { const m = new Map(); for (const k of arr) m.set(k, (m.get(k) ?? 0) + 1); return m; };
    const split = (arr, isCase) => { const cases = [], others = []; for (const n of arr) { const m = n.match(classRe); if (m && isCase(m[1])) cases.push(m[2]); else others.push(n); } return { cases, others }; };
    const b = split(baseline, (c) => c === "CaseDetailsWebTests");
    const n = split(names, (c) => CASE_FEATURE_CLASSES.includes(c));
    const diff = (x, y) => { const cx = count(x), cy = count(y), out = []; for (const [k, v] of cx) if ((cy.get(k) ?? 0) !== v) out.push(`baseline x${v} / new x${cy.get(k) ?? 0}: ${k}`); for (const [k, v] of cy) if (!cx.has(k)) out.push(`baseline x0 / new x${v}: ${k}`); return out; };
    const problems = [];
    // Case rows: exact multiset of method+arguments (the relocation must lose or duplicate nothing).
    const caseDiff = diff(b.cases, n.cases); if (caseDiff.length) problems.push(`Case rows differ (${caseDiff.length}):\n${caseDiff.slice(0, 40).join("\n")}`);
    // Other tests: by method name only. Theories fed from the local corpus enumerate a different
    // row count here than on the corpus-less CI runner that produced the baseline (one placeholder
    // row there, one row per corpus file here), which is an environment difference, not a change.
    const method = (name) => name.replace(/\(.*$/s, "");
    const bo = new Set(b.others.map(method)), no = new Set(n.others.map(method));
    const otherDiff = [...[...bo].filter((m) => !no.has(m)).map((m) => `missing: ${m}`), ...[...no].filter((m) => !bo.has(m)).map((m) => `new: ${m}`)];
    if (otherDiff.length) problems.push(`non-Case test methods differ (${otherDiff.length}):\n${otherDiff.slice(0, 40).join("\n")}`);
    fs.appendFileSync(logFile, `\n=== discovery compare: ${problems.length ? "FAIL" : "PASS"} (rows: baseline ${baseline.length}, new ${names.length}; Case rows baseline ${b.cases.length} new ${n.cases.length}; non-Case methods baseline ${bo.size} new ${no.size})\n${problems.join("\n")}\n`);
    return { ok: !problems.length, summary: `Case rows ${n.cases.length}/${b.cases.length} preserved exactly; non-Case methods ${no.size}/${bo.size}; total rows ${names.length} vs baseline ${baseline.length} (corpus theories enumerate locally)`, problems };
  }
  async function verify(task, dir, { docs = true } = {}) {
    const t = ts(task.id);
    const round = ++t.verifyRound; saveState();
    const logFile = path.join(runDir, "verify", `${task.id}-r${round}.log`);
    const evidence = [];
    const fail = (step, note = "") => ({ ok: false, step, logFile, evidence, tail: tail(logFile), note });
    return withHostSlot(task.id, dir, async () => {
      if (task.build) {
        if (handshakes > 0) { log(`[${task.id}] build waits for ${handshakes} app-server handshake(s) in flight`); while (handshakes > 0) await sleep(5000); }
        buildingNow = true;
        let s;
        try { s = longRun("dotnet", ["build", "Pegasus.slnx", "-c", "Release"], { cwd: dir, logFile, timeoutMs: args.buildTimeoutMinutes * 60000 }); }
        finally { buildingNow = false; }
        if (s !== 0) return fail("build", s === null ? "timeout" : "");
        evidence.push("`dotnet build Pegasus.slnx -c Release`: passed");
      }
      for (const s of task.preTestScripts ?? []) {
        const st = pwsh(dir, s.args[0], s.args.slice(1), logFile);
        if (st !== 0) return fail(s.name);
        evidence.push(`\`pwsh ${s.args.join(" ")}\`: passed`);
      }
      if (task.discoveryCheck) {
        const d = discoveryCompare(dir, logFile);
        if (!d.ok) return fail("discovery", d.problems.join("\n").slice(0, 3000));
        evidence.push(`Discovery preserved: ${d.summary}`);
      }
      for (const [k, test] of (task.tests ?? []).entries()) {
        const env = {};
        for (const [key, v] of Object.entries(test.env ?? {})) env[key] = v === "__REFERENCE_PACK__" ? args.referencePack : v;
        const trx = path.join(runDir, "trx", `${task.id}-r${round}-${k}.trx`);
        const argv = ["test", test.project, "-c", "Release", "--no-build"];
        if (test.filter) argv.push("--filter", test.filter);
        argv.push("--logger", `trx;LogFileName=${trx}`);
        if (/Core\.Tests/.test(test.project)) argv.push("--", "xUnit.MaxParallelThreads=1");
        const s = longRun("dotnet", argv, { cwd: dir, env, logFile, timeoutMs: args.testTimeoutMinutes * 60000 });
        const counts = parseCounts(logFile).at(-1);
        const label = `\`dotnet test ${path.basename(path.dirname(test.project))}${test.filter ? ` --filter "${test.filter.length > 90 ? test.filter.slice(0, 87) + "..." : test.filter}"` : ""}\``;
        if (s !== 0) return fail(`test:${k}`, counts ? `failed ${counts.failed} / passed ${counts.passed} / total ${counts.total}` : (s === null ? "timeout" : "non-zero exit"));
        evidence.push(`${label}: passed${counts ? ` (${counts.passed} passed, ${counts.skipped} skipped, ${counts.total} total)` : ""}`);
      }
      if (docs && task.docs) {
        let st = pwsh(dir, "scripts/Test-DocumentationLinks.ps1", [], logFile);
        if (st !== 0) return fail("docs-links");
        st = pwsh(dir, "scripts/Test-MarkdownPlacement.ps1", ["-Base", task.placementBase ?? "origin/dev", "-Head", "HEAD"], logFile);
        if (st !== 0) return fail("docs-placement");
        evidence.push("Documentation links and Markdown placement: passed");
      }
      return { ok: true, logFile, evidence };
    });
  }

  // ---------------------------------------------------------------- review
  async function review(task, dir, base, evidence = []) {
    const brief = fs.readFileSync(path.join(HERE, task.brief), "utf8");
    const acceptance = (brief.split(/^## Acceptance/m)[1] ?? "").split(/^## /m)[0].trim();
    const ran = evidence.length ? `\n\nThe sprint verifier has ALREADY run these at the current HEAD (${git(dir, "rev-parse", "--short", "HEAD").out}) and they passed; treat them as the execution evidence and do not withhold "ship" because you cannot run tests yourself (your session is read-only by design):\n${evidence.map((e) => `- ${e}`).join("\n")}` : "";
    const env = await codexRetrying({
      preset: "review", schema: "review", cwd: dir, via: VIA,
      review: { custom: `Review the changes on this branch relative to ${base} (run: git diff ${base}...HEAD). They implement task ${task.id} of sprint 1609. Report only real, confirmed problems in the code: bugs, contract violations against the acceptance list below, missing tests the task asked for. Do not report style. Verdict "ship" when you confirm no such problem; "needs-fixes" with findings when you do; "blocked" only if the diff itself cannot be read.${ran}\n\nAcceptance list:\n${acceptance || "(see the task brief at " + rel(path.join(HERE, task.brief)) + ")"}` }
    }, `${task.id}:review:${ts(task.id).reviewRound + 1}`);
    const o = env.status === "completed" && !env.schema_parse_error && env.summary && typeof env.summary === "object" ? env.summary : null;
    return { verdict: o?.verdict ?? "blocked", findings: o?.findings ?? [], raw: env.status };
  }

  // ---------------------------------------------------------------- delivery
  function prBody(task, summaryLines, evidence, notes = []) {
    return [
      "## Summary", "", ...summaryLines.map((s) => `- ${s}`), "",
      "## Source", "", `Sprint 1609 handover (\`1609sprint/\`), claims verified against dev on 16 Sep 2026; task ${task.id} of the dispatch run.`, "",
      "## Verification (local, serialized host slot)", "", ...evidence.map((e) => `- ${e}`), "- Full-suite evidence: this PR's CI.", "",
      "## Merge order", "", task.mergeOrder ?? "Independent.", "",
      ...(notes.length ? ["## Notes", "", ...notes.map((n) => `- ${n}`), ""] : []),
      PR_FOOTER, ""
    ].join("\n");
  }
  function commitSquashed(task, dir, squashBase, summary, evidence) {
    if (gitOk(dir, "diff", "--quiet", squashBase, "HEAD").status === 0 && gitOk(dir, "status", "--porcelain").out === "") return null;
    if (gitOk(dir, "rev-list", "--merges", `${squashBase}..HEAD`).out) throw new Error("refusing to squash across a merge commit");
    git(dir, "reset", "--soft", squashBase);
    git(dir, "add", "-A");
    const msg = [task.commitTitle, "", summary, "", "Verification:", ...evidence.map((e) => `- ${e.replace(/`/g, "")}`), "", CO_AUTHOR, ""].join("\n");
    const f = path.join(runDir, "pr", `${task.id}-commit.txt`); fs.writeFileSync(f, msg, "utf8");
    git(dir, "commit", "-q", "-F", f);
    return git(dir, "rev-parse", "HEAD").out;
  }
  function pushAndPr(task, dir, body, commentOnly = false) {
    const t = ts(task.id);
    if (!args.push) { t.pushedSha = null; return { pr: null, note: "push disabled" }; }
    const r = gitOk(dir, "push", "-u", "origin", task.branch);
    if (r.status !== 0) throw new Error(`push rejected for ${task.branch} (never forced): ${r.err.slice(-500)}`);
    t.pushedSha = git(dir, "rev-parse", "HEAD").out;
    const bodyFile = path.join(runDir, "pr", `${task.id}-body.md`); fs.writeFileSync(bodyFile, body, "utf8");
    if (task.pr?.number) {
      gh(["pr", "comment", String(task.pr.number), "--body-file", bodyFile]);
      if (task.pr.refreshBody) {
        const cur = JSON.parse(gh(["pr", "view", String(task.pr.number), "--json", "body"]).out).body ?? "";
        const fixed = cur.replace(/is pending; no local full-suite rerun duplicates it\./, "completed with two SQL shard timeouts (run 35083151345); the six-shard remediation below addresses that outcome.");
        const nb = `${fixed}\n\n## Six-shard CI remediation (16 Sep)\n\n${body}`;
        const nbf = path.join(runDir, "pr", `${task.id}-newbody.md`); fs.writeFileSync(nbf, nb, "utf8");
        gh(["pr", "edit", String(task.pr.number), "--body-file", nbf]);
      }
      t.prNumber = task.pr.number;
      return { pr: task.pr.number, url: `https://github.com/collisionengineers/pegasus/pull/${task.pr.number}` };
    }
    const existing = JSON.parse(gh(["pr", "list", "--head", task.branch, "--state", "open", "--json", "number,url"]).out);
    if (existing.length) { gh(["pr", "comment", String(existing[0].number), "--body-file", bodyFile]); t.prNumber = existing[0].number; return { pr: existing[0].number, url: existing[0].url }; }
    if (commentOnly) return { pr: null, note: "nothing to open" };
    const out = gh(["pr", "create", "--base", task.pr.base, "--head", task.branch, "--title", task.pr.title, "--body-file", bodyFile]).out;
    const num = Number((out.match(/\/pull\/(\d+)/) ?? [])[1]);
    t.prNumber = num || null;
    return { pr: t.prNumber, url: out.trim() };
  }

  // ---------------------------------------------------------------- task drivers
  async function runDeterministicU1(task) {
    const t = ts(task.id);
    if (TERMINAL.has(t.state)) return;
    if (args.dryRun) { log("[U1] dry run: would branch task/sprint-docs-gate from origin/dev, edit the v27-notes links, the placement regex and its test list, verify with the three docs scripts, push and open the PR"); signalTerminal("U1"); return; }
    const dir = prepareWorktree(task);
    const squashBase = git(dir, "rev-parse", "HEAD").out;
    setState("U1", "prepared", { worktreeDir: dir, branch: task.branch, baseSha: squashBase, squashBase });
    const edit = (file, from, to, all = false) => {
      const p = path.join(dir, file); const s = fs.readFileSync(p, "utf8");
      let f = from, t = to;
      if (!s.includes(f)) { f = from.replace(/\n/g, "\r\n"); t = to.replace(/\n/g, "\r\n"); }
      if (s.includes(t)) return; // already applied by an earlier run
      if (!s.includes(f)) throw new Error(`U1: expected text not found in ${file}`);
      // split/join and a function replacer: a plain replacement string would expand `$'` and `$&`.
      fs.writeFileSync(p, all ? s.split(f).join(t) : s.replace(f, () => t), "utf8");
    };
    edit("design/planning-and-old-designs/v27_planning/current/v27-notes.md", "../../../../.claude/skills/razor-html-mockup-creation/", "../../../../.agents/skills/razor-html-mockup-creation/", true);
    edit("scripts/Test-MarkdownPlacement.ps1", "|design/planning-and-old-designs)/.+\\.md$'", "|design/planning-and-old-designs|1609sprint)/.+\\.md$'");
    edit("scripts/Test-MarkdownPlacement.ps1", "    $normalized = $Path.Replace('\\', '/') -replace '^\\./', ''\n", "    $normalized = $Path.Replace('\\', '/') -replace '^\\./', ''\n    # 1609sprint/ is a temporary, operator-requested sprint handover root (16 September 2026).\n");
    edit("scripts/Test-TestMarkdownPlacement.ps1", "        'workspaces/document-extraction/docs/new.md'\n    )", "        'workspaces/document-extraction/docs/new.md',\n        '1609sprint/new.md'\n    )");
    wip(task, dir, 1);
    setState("U1", "implemented", { headSha: git(dir, "rev-parse", "HEAD").out });
    const logFile = path.join(runDir, "verify", "U1-r1.log");
    const v = await withHostSlot("U1", dir, async () => {
      if (pwsh(dir, "scripts/Test-TestMarkdownPlacement.ps1", [], logFile) !== 0) return { ok: false, step: "placement-regression" };
      if (pwsh(dir, "scripts/Test-DocumentationLinks.ps1", [], logFile) !== 0) return { ok: false, step: "docs-links" };
      if (pwsh(dir, "scripts/Test-MarkdownPlacement.ps1", ["-Base", "origin/dev", "-Head", "HEAD"], logFile) !== 0) return { ok: false, step: "docs-placement" };
      return { ok: true };
    });
    if (!v.ok) { setState("U1", "needs-human", { lastError: `${v.step} failed; see ${logFile}` }); signalTerminal("U1"); return; }
    const evidence = ["`pwsh scripts/Test-TestMarkdownPlacement.ps1`: passed", "`pwsh scripts/Test-DocumentationLinks.ps1`: passed", "`pwsh scripts/Test-MarkdownPlacement.ps1 -Base origin/dev -Head HEAD`: passed"];
    setState("U1", "verified");
    const summary = "Two links in the v27 mockup notes pointed at .claude/skills; the skill lives under .agents/skills. Admits 1609sprint/ to the Markdown placement allow-list as a temporary sprint exception, with the regression test updated.";
    commitSquashed(task, dir, squashBase, summary, evidence);
    const pr = pushAndPr(task, dir, prBody(task, [summary], evidence, ["Until this merges, every open PR's documentation lane fails on dev's copy of v27-notes.md, which skips their unit and SQL lanes."]));
    state.docsGatePr = pr.pr; saveState();
    setState("U1", "delivered", { prNumber: pr.pr, prUrl: pr.url }); signalTerminal("U1");
  }

  async function runClose(task) {
    const t = ts(task.id);
    if (TERMINAL.has(t.state)) return;
    if (args.dryRun || !args.push) { log(`[${task.id}] ${args.dryRun ? "dry run" : "push disabled"}: would close PR ${task.close.number} with a comment`); signalTerminal(task.id); return; }
    const cur = JSON.parse(gh(["pr", "view", String(task.close.number), "--json", "state"]).out);
    if (cur.state === "OPEN") gh(["pr", "close", String(task.close.number), "--comment", task.close.comment]);
    setState(task.id, "delivered", { prNumber: task.close.number }); signalTerminal(task.id);
  }

  async function runCodexTask(task) {
    const t = ts(task.id);
    if (TERMINAL.has(t.state)) { signalTerminal(task.id); return; }
    for (const dep of task.dependsOn ?? []) {
      const s = await waitFor(dep, "terminal");
      if (s !== "delivered" && !args.dryRun) { log(`[${task.id}] not run: dependency ${dep} ended ${s}`); signalTerminal(task.id); return; }
    }
    if (args.dryRun) {
      const verifier = [task.build ? "dotnet build Pegasus.slnx -c Release" : null, ...(task.preTestScripts ?? []).map((s) => `pwsh ${s.args.join(" ")}`), task.discoveryCheck ? "discovery compare vs baseline" : null, ...(task.tests ?? []).map((t) => `dotnet test ${t.project} -c Release --no-build${t.filter ? ` --filter "${t.filter}"` : ""}`), task.docs ? "docs links + placement" : null].filter(Boolean);
      log(`[${task.id}] dry run: worktree ${path.join(wtRoot, task.dir)} on ${task.branch}${task.baseRef ? ` from ${task.baseRef}` : " (existing)"}${task.dependsOn?.length ? `; after ${task.dependsOn.join(",")}` : ""}${task.verifyFirst ? "; verify-first" : ""}${task.reviewFirst ? "; review-first" : ""}${task.mergeDevFirst ? "; merges origin/dev after the docs gate" : task.baseRef === "origin/dev" ? "; rebases onto origin/dev after the docs gate" : ""}; brief ${task.brief}; verifier: ${verifier.join(" | ")}; delivery: ${task.pr?.number ? `push + comment on #${task.pr.number}${task.pr.refreshBody ? " + body refresh" : ""}` : `new PR "${task.pr?.title}" -> ${task.pr?.base}`}`);
      signalTerminal(task.id); return;
    }
    try {
      // prepare
      let dir = t.worktreeDir;
      if (t.state === "pending" || !dir || !fs.existsSync(dir)) {
        dir = prepareWorktree(task);
        if (task.expectedDelta) {
          const pending = porcelainPaths(dir);
          const expected = new Set(task.expectedDelta);
          const unexpected = pending.filter((p) => !expected.has(p));
          if (unexpected.length) throw new Error(`unexpected pending paths in ${task.dir}: ${unexpected.join(", ")}`);
          if (pending.length) { git(dir, "add", "-A"); git(dir, "commit", "-q", "-m", `${task.deltaCommitTitle}\n\n${CO_AUTHOR}`); log(`[${task.id}] committed the pending ${pending.length}-path delta`); }
          ts(task.id).deltaSha = git(dir, "rev-parse", "HEAD").out;
        }
        if (task.mergeBranchFirst) {
          git(main, "fetch", "origin");
          const m = gitOk(dir, "merge", "--no-edit", task.mergeBranchFirst);
          if (m.status !== 0) { gitOk(dir, "merge", "--abort"); throw new Error(`merge of ${task.mergeBranchFirst} conflicted: ${m.out.slice(-400)}`); }
        }
        setState(task.id, "prepared", { worktreeDir: dir, branch: task.branch, baseSha: git(dir, "rev-parse", "HEAD").out, squashBase: git(dir, "rev-parse", "HEAD").out });
      }
      const summaries = [];
      let evidence = [];
      let threadId = t.threadId ?? null;
      let round = 0;

      // verify-first tasks: reproduce before touching anything
      let baseline = null;
      if (task.verifyFirst && t.state === "prepared") {
        baseline = await verify(task, dir, { docs: false });
        if (baseline.ok) {
          log(`[${task.id}] already green at ${git(dir, "rev-parse", "--short", "HEAD").out}; no change needed`);
          setState(task.id, "verified", { note: "already green" });
          const body = prBody(task, [`Reproduced locally at ${git(dir, "rev-parse", "HEAD").out}: the previously failing tests pass. No source change; CI re-run needed once the docs-gate PR is on dev.`], baseline.evidence);
          const pr = pushAndPr(task, dir, body, true);
          setState(task.id, "delivered", { prNumber: pr.pr, prUrl: pr.url }); signalTerminal(task.id); return;
        }
      }

      // implement (+ U2's review-first pass)
      if (!["implemented", "verified", "reviewed"].includes(t.state)) {
        // A turn interrupted by an earlier run may have left partial edits: start clean. Only for
        // worktrees this script created from a base ref; existing PR worktrees are never reset.
        if (!task.existingDir && porcelainPaths(dir).length) { git(dir, "reset", "--hard", "-q"); git(dir, "clean", "-fdq"); log(`[${task.id}] discarded partial edits from an interrupted turn`); }
        let extra = "";
        if (baseline && !baseline.ok) extra = `The verifier reproduced the failure at HEAD before any change. Failed step: ${baseline.step}${baseline.note ? ` (${baseline.note})` : ""}. Log tail:\n\n\`\`\`\n${baseline.tail}\n\`\`\``;
        if (task.reviewFirst) {
          const r = await review(task, dir, ts(task.id).deltaSha ? `${ts(task.id).deltaSha}~1` : "origin/dev");
          const list = r.findings.map((f) => `- ${f.file}:${f.line} [${f.severity}] ${f.title}: ${f.fix ?? f.detail ?? ""}`).join("\n");
          extra += `\n\nAn independent read-only review of the split (verdict: ${r.verdict}) reported:\n${list || "(no findings)"}\nAddress every real finding as part of this task.`;
        }
        const r = await implementTurn(task, dir, extra);
        threadId = r.threadId;
        if (r.summary) summaries.push(r.summary);
        if (r.ranDotnet) { setState(task.id, "needs-human", { threadId, lastError: "implement turn ran dotnet build/test or a verification script despite the host-slot rule; results quarantined" }); signalTerminal(task.id); return; }
        if (r.status !== "completed" || r.outcome === "blocked" || r.questions.length) {
          setState(task.id, r.questions.length ? "needs-human" : "blocked", { threadId, questions: r.questions, lastError: `implement ended ${r.status}/${r.outcome}` }); signalTerminal(task.id); return;
        }
        wip(task, dir, ++round);
        setState(task.id, "implemented", { threadId, headSha: git(dir, "rev-parse", "HEAD").out, summaries });
      }

      // Edits left by a fix turn that an earlier run interrupted: commit them as a WIP round so the
      // rebase below sees a clean tree and the next verify judges the tree as it actually is.
      if (porcelainPaths(dir).length) { wip(task, dir, ++round); log(`[${task.id}] committed edits left by an interrupted turn`); }

      // docs-gate: trees that will contain v27-notes.md wait for the fix on origin/dev
      const needsDevMerge = Boolean(task.mergeDevFirst) || task.baseRef === "origin/dev";
      if (needsDevMerge) {
        const merged = await waitForDocsGateMerge();
        if (!merged) { setState(task.id, "needs-human", { threadId, lastError: "docs-gate PR not merged within the wait window; re-run to continue" }); signalTerminal(task.id); return; }
        if (task.mergeDevFirst) {
          // Squash the pre-merge work first: a later reset --soft must never cross a merge commit.
          const sq = ts(task.id).squashBase;
          if (git(dir, "rev-parse", "HEAD").out !== sq) { git(dir, "reset", "--soft", sq); git(dir, "add", "-A"); git(dir, "commit", "-q", "-m", `${task.commitTitle}\n\n${summaries.filter(Boolean).join(" ") || "Review corrections."}\n\n${CO_AUTHOR}`); }
          const m = gitOk(dir, "merge", "--no-edit", "origin/dev");
          if (m.status !== 0) {
            const conflicts = git(dir, "diff", "--name-only", "--diff-filter=U").out.split(/\r?\n/).filter(Boolean);
            if (conflicts.length === 1 && conflicts[0] === ".codex/PLAN.md") { git(dir, "checkout", "--ours", "--", ".codex/PLAN.md"); git(dir, "add", ".codex/PLAN.md"); git(dir, "commit", "-q", "--no-edit"); log(`[${task.id}] merged origin/dev, kept the branch's .codex/PLAN.md`); }
            else { gitOk(dir, "merge", "--abort"); setState(task.id, "needs-human", { threadId, lastError: `origin/dev merge conflicts: ${conflicts.join(", ")}` }); signalTerminal(task.id); return; }
          }
          ts(task.id).squashBase = git(dir, "rev-parse", "HEAD").out; saveState();
        } else if (task.baseRef === "origin/dev") {
          const rb = gitOk(dir, "rebase", "origin/dev");
          if (rb.status !== 0) { gitOk(dir, "rebase", "--abort"); setState(task.id, "needs-human", { threadId, lastError: "rebase onto refreshed origin/dev conflicted" }); signalTerminal(task.id); return; }
          ts(task.id).squashBase = git(main, "rev-parse", "origin/dev").out; saveState();
        }
      }

      // verify / fix loop (skipped on resume when this tree was already verified at the same HEAD)
      let v = null;
      const alreadyVerified = ["verified", "reviewed"].includes(t.state) && t.headSha === git(dir, "rev-parse", "HEAD").out && Array.isArray(t.evidence);
      if (alreadyVerified) { v = { ok: true, evidence: t.evidence }; log(`[${task.id}] verified at ${t.headSha.slice(0, 9)} in an earlier run; skipping re-verification`); }
      for (let fix = 0; !alreadyVerified && fix <= args.maxFixRounds; fix++) {
        v = await verify(task, dir);
        if (v.ok) break;
        if (fix === args.maxFixRounds) { setState(task.id, "needs-human", { threadId, lastError: `verification still failing at ${v.step} after ${fix} fix rounds; see ${v.logFile}` }); signalTerminal(task.id); return; }
        const r = await fixTurn(task, dir, threadId, `The sprint verifier ran your change and it failed at step "${v.step}"${v.note ? ` (${v.note})` : ""}. Fix the cause without weakening any assertion, then respond with the task-result JSON. The host-slot rule still applies: do not run dotnet build/test yourself. Log tail:\n\n\`\`\`\n${v.tail}\n\`\`\``);
        if (r.summary) summaries.push(r.summary);
        if (r.ranDotnet) { setState(task.id, "needs-human", { threadId, lastError: "fix turn ran dotnet despite the rule; quarantined" }); signalTerminal(task.id); return; }
        if (r.status !== "completed" || r.outcome === "blocked" || r.questions.length) { setState(task.id, "needs-human", { threadId, questions: r.questions, lastError: `fix turn ended ${r.status}/${r.outcome}` }); signalTerminal(task.id); return; }
        wip(task, dir, ++round);
      }
      evidence = v.evidence;
      setState(task.id, "verified", { headSha: git(dir, "rev-parse", "HEAD").out, summaries, evidence });

      // review / fix loop
      // Review only this task's own work (the commits since its squash base), never the whole PR branch.
      const reviewBase = ts(task.id).squashBase;
      for (let rr = 0; ; rr++) {
        ts(task.id).reviewRound = rr + 1; saveState();
        const rv = await review(task, dir, reviewBase, evidence);
        log(`[${task.id}] review ${rr + 1}: ${rv.verdict} (${rv.findings.length} findings)`);
        if (rv.verdict === "ship") break;
        if (!rv.findings.length || rr >= args.maxReviewRounds) {
          if (task.deliverOnReviewExhaustion && rv.findings.length) {
            // Operator-chosen closure for a large change: deliver the verified tree and list the
            // reviewer's remaining findings in the PR body as open items instead of looping further.
            ts(task.id).openFindings = rv.findings; saveState();
            log(`[${task.id}] delivering with ${rv.findings.length} open review finding(s) listed in the PR body`);
            break;
          }
          setState(task.id, "needs-human", { threadId, lastError: `review verdict ${rv.verdict} with ${rv.findings.length} findings after ${rr + 1} rounds`, findings: rv.findings }); signalTerminal(task.id); return;
        }
        const issues = rv.findings.map((f) => `- ${f.file}:${f.line} [${f.severity}] ${f.title}: ${f.fix ?? f.detail ?? ""}`).join("\n");
        const r = await fixTurn(task, dir, threadId, `An independent reviewer found these problems in your change. Fix each real one (say why if one is not real), keep every assertion, then respond with the task-result JSON. The host-slot rule still applies.\n${issues}`);
        if (r.summary) summaries.push(r.summary);
        if (r.ranDotnet) { setState(task.id, "needs-human", { threadId, lastError: "fix turn ran dotnet despite the rule; quarantined" }); signalTerminal(task.id); return; }
        wip(task, dir, ++round);
        // Re-verify after review fixes with the same bounded fix loop as the first verification.
        for (let fix = 0; ; fix++) {
          v = await verify(task, dir);
          if (v.ok) break;
          if (fix >= args.maxFixRounds) { setState(task.id, "needs-human", { threadId, lastError: `re-verify after review still failing at ${v.step} after ${fix} fix rounds; see ${v.logFile}` }); signalTerminal(task.id); return; }
          const rf = await fixTurn(task, dir, threadId, `The sprint verifier ran your review fixes and they failed at step "${v.step}"${v.note ? ` (${v.note})` : ""}. Fix the cause without weakening any assertion, then respond with the task-result JSON. The host-slot rule still applies. Log tail:\n\n\`\`\`\n${v.tail}\n\`\`\``);
          if (rf.summary) summaries.push(rf.summary);
          if (rf.ranDotnet || rf.status !== "completed" || rf.outcome === "blocked" || rf.questions.length) { setState(task.id, "needs-human", { threadId, questions: rf.questions, lastError: `post-review fix turn ended ${rf.status}/${rf.outcome}${rf.ranDotnet ? " (ran dotnet)" : ""}` }); signalTerminal(task.id); return; }
          wip(task, dir, ++round);
        }
        evidence = v.evidence;
      }
      setState(task.id, "reviewed");

      // deliver
      const summary = summaries.filter(Boolean).join(" ") || task.commitTitle;
      const sha = commitSquashed(task, dir, ts(task.id).squashBase, summary, evidence);
      const notes = task.id === "U2" ? [splitMapNote()] : [];
      for (const f of ts(task.id).openFindings ?? []) notes.push(`Open review finding (${f.severity}) at \`${f.file}:${f.line}\`: ${f.title}. Suggested fix: ${f.fix ?? f.detail ?? ""}`);
      const pr = pushAndPr(task, dir, prBody(task, summaries.filter(Boolean).length ? summaries : [summary], evidence, notes));
      setState(task.id, "delivered", { headSha: sha ?? git(dir, "rev-parse", "HEAD").out, prNumber: pr.pr, prUrl: pr.url });
    } catch (error) {
      setState(task.id, "blocked", { lastError: error?.message ?? String(error) });
    } finally { signalTerminal(task.id); }
  }
  function splitMapNote() {
    try {
      const map = JSON.parse(fs.readFileSync(args.splitMap, "utf8"));
      const rows = map.map((m) => `| ${m.method} | ${m.originalFile} | ${m.newFile} | ${m.preserved ? "yes" : "NO"} |`);
      return `Portable copy of the method map (${map.length} methods; body/attribute hashes in the private map):\n\n<details><summary>method → owner</summary>\n\n| Method | From | To | Preserved |\n| --- | --- | --- | --- |\n${rows.join("\n")}\n\n</details>`;
    } catch { return "Split map not readable at " + args.splitMap; }
  }

  // ---------------------------------------------------------------- run
  if (!args.dryRun) { assertLongPaths(); git(main, "fetch", "origin"); }
  const selected = TASKS.filter((t) => (!args.only.length || args.only.includes(t.id)) && !args.skip.includes(t.id));
  for (const t of TASKS) if (!selected.includes(t)) { ts(t.id); signalTerminal(t.id); }
  log(`tasks: ${selected.map((t) => t.id).join(", ")}${args.dryRun ? " (dry run)" : ""}`);
  await Promise.all(selected.map((task) => {
    if (task.kind === "deterministic") return runDeterministicU1(task).catch((e) => { setState(task.id, "blocked", { lastError: e.message }); signalTerminal(task.id); });
    if (task.kind === "gh") return runClose(task).catch((e) => { setState(task.id, "blocked", { lastError: e.message }); signalTerminal(task.id); });
    return runCodexTask(task);
  }));
  const result = {
    tasks: Object.fromEntries(Object.values(state.tasks).map((t) => [t.id, { state: t.state, pr: t.prUrl ?? t.prNumber ?? null, error: t.lastError ?? null, questions: t.questions ?? [], findings: t.findings ?? [] }])),
    docsGatePr: state.docsGatePr ?? null, docsGateMerged: Boolean(state.docsGateMerged),
    notDispatched: ["case-record-v27 (gate 0 sign-offs)", "frontend H1/H2/H4/H5", "dead-code deletion tranche", "bootstrap verification account (kept until go-live)", "docs-generation A5 wording and order 3-4 packages", "guardrails gate exception / CI lint / screenshots", "PR 764 live performance acceptance", "release + intake wipe"]
  };
  writeFile("summary.json", result);
  return result;
};
