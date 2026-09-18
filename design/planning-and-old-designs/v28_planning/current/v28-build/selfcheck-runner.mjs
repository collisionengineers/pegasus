// Runs v28-selfcheck.html in the Playwright Chromium build over the DevTools
// protocol and prints its RESULT line. Usage: node selfcheck-runner.mjs
import { spawn } from 'node:child_process';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { tmpdir } from 'node:os';

const here = dirname(fileURLToPath(import.meta.url));
const chrome = process.env.CHROME || `${process.env.LOCALAPPDATA}\\ms-playwright\\chromium-1234\\chrome-win64\\chrome.exe`;
const page = pathToFileURL(resolve(here, '..', 'v28-selfcheck.html')).href;
const port = 9300 + Math.floor(Math.random() * 600);
const udir = join(tmpdir(), `pw-profile-${process.pid}-${Date.now()}`);
const proc = spawn(chrome, ['--headless=new', '--allow-file-access-from-files', '--disable-gpu', '--no-sandbox', `--user-data-dir=${udir}`, `--remote-debugging-port=${port}`, '--window-size=1600,1100', 'about:blank'], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let target;
for (let i = 0; i < 50 && !target; i++) { await sleep(200); try { const list = await (await fetch(`http://127.0.0.1:${port}/json`)).json(); target = list.find((t) => t.type === 'page'); } catch { /* starting */ } }
if (!target) { proc.kill(); throw new Error('Chromium did not start'); }
const ws = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((r) => (ws.onopen = r));
let seq = 0; const pending = new Map(); const errors = [];
ws.onmessage = (m) => { const msg = JSON.parse(m.data); if (msg.id && pending.has(msg.id)) { pending.get(msg.id)(msg); pending.delete(msg.id); } else if (msg.method === 'Runtime.exceptionThrown') errors.push(msg.params.exceptionDetails.exception?.description || msg.params.exceptionDetails.text); };
const send = (method, params = {}) => new Promise((r) => { const id = ++seq; pending.set(id, r); ws.send(JSON.stringify({ id, method, params })); });
await send('Runtime.enable'); await send('Page.enable');
await send('Page.navigate', { url: page });
let text = '';
for (let i = 0; i < 240; i++) {
  await sleep(500);
  const reply = await send('Runtime.evaluate', { expression: "(document.getElementById('out') || {}).textContent || ''", returnByValue: true });
  text = (reply.result && reply.result.result && reply.result.result.value) || ''; if (text.startsWith('RESULT')) break;
}
ws.close(); proc.kill();
console.log(text);
if (errors.length) console.log('HARNESS ERRORS ' + JSON.stringify(errors));
const parsed = text.startsWith('RESULT') ? JSON.parse(text.slice(7)) : { fail: ['no result'] };
process.exit(parsed.fail.length || errors.length ? 1 : 0);
