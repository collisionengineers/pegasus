// Screenshot driver for the v28 mockups: headless Chromium (the Playwright
// build) over the DevTools protocol, no npm packages. Usage:
//   node shoot.mjs <shots.json>
// shots.json: [{ "name": "01-work-centre", "file": "pegasus_work_centre_v28.html",
//                "query": "role=Engineer", "width": 1580, "height": 1000 }]
// Every page load also reports console errors and uncaught exceptions; the
// run fails if any occur. Screenshots always pass stripCollapsed=1 so the
// demo strip does not sit over content.
import { spawn } from 'node:child_process';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { tmpdir } from 'node:os';

const here = dirname(fileURLToPath(import.meta.url));
const chrome = process.env.CHROME || `${process.env.LOCALAPPDATA}\\ms-playwright\\chromium-1234\\chrome-win64\\chrome.exe`;
const outDir = resolve(here, '..', 'v28-shots');
mkdirSync(outDir, { recursive: true });
const shots = JSON.parse(readFileSync(process.argv[2] || resolve(here, 'shots.json'), 'utf8'));
const port = 9300 + Math.floor(Math.random() * 600);
const udir = join(tmpdir(), `pw-profile-${process.pid}-${Date.now()}`);

const proc = spawn(chrome, ['--headless=new', '--allow-file-access-from-files', '--hide-scrollbars', '--disable-gpu', '--no-sandbox', `--user-data-dir=${udir}`, `--remote-debugging-port=${port}`, '--window-size=1580,1000', 'about:blank'], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let target;
for (let i = 0; i < 50 && !target; i++) {
  await sleep(200);
  try { const list = await (await fetch(`http://127.0.0.1:${port}/json`)).json(); target = list.find((t) => t.type === 'page'); } catch { /* not up yet */ }
}
if (!target) { proc.kill(); throw new Error('Chromium did not start'); }
const ws = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((r) => (ws.onopen = r));
let seq = 0; const pending = new Map(); const errors = [];
ws.onmessage = (m) => {
  const msg = JSON.parse(m.data);
  if (msg.id && pending.has(msg.id)) { pending.get(msg.id)(msg); pending.delete(msg.id); return; }
  if (msg.method === 'Runtime.exceptionThrown') errors.push('EXC ' + (msg.params.exceptionDetails.exception?.description || msg.params.exceptionDetails.text));
  if (msg.method === 'Runtime.consoleAPICalled' && msg.params.type === 'error') errors.push('CONSOLE ' + msg.params.args.map((a) => a.value ?? a.description).join(' '));
  if (msg.method === 'Log.entryAdded' && msg.params.entry.level === 'error') errors.push('LOG ' + msg.params.entry.text + ' ' + (msg.params.entry.url || ''));
};
const send = (method, params = {}) => new Promise((r) => { const id = ++seq; pending.set(id, r); ws.send(JSON.stringify({ id, method, params })); });
await send('Runtime.enable'); await send('Log.enable'); await send('Page.enable');

const results = [];
for (const shot of shots) {
  const width = shot.width || 1580, height = shot.height || 1000;
  await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });
  const before = errors.length;
  const q = shot.query || '';
  const url = pathToFileURL(resolve(here, '..', shot.file)).href;
  await send('Page.navigate', { url: `${url}?stripCollapsed=1&${q}` });
  await sleep(900);
  if (shot.eval) await send('Runtime.evaluate', { expression: shot.eval, awaitPromise: true });
  await sleep(300);
  let clip;
  if (shot.full) {
    const { result } = await send('Runtime.evaluate', { expression: 'document.documentElement.scrollHeight', returnByValue: true });
    await send('Emulation.setDeviceMetricsOverride', { width, height: result.value, deviceScaleFactor: 1, mobile: false });
    await sleep(300);
  }
  const { result: png } = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: !!shot.full, clip });
  writeFileSync(resolve(outDir, `${shot.name}.png`), Buffer.from(png.data, 'base64'));
  const shotErrors = errors.slice(before);
  results.push({ name: shot.name, errors: shotErrors });
  console.log(`${shotErrors.length ? 'FAIL' : 'ok  '} ${shot.name} (${width}x${shot.full ? 'full' : height})${shotErrors.length ? ' ' + shotErrors.join(' | ') : ''}`);
}
ws.close(); proc.kill();
const failed = results.filter((r) => r.errors.length);
console.log(`\n${results.length - failed.length}/${results.length} shots clean`);
process.exit(failed.length ? 1 : 0);
