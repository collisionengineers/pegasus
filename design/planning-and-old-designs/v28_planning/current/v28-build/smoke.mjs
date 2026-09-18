// Quick console-error smoke test for one built v28 page. Usage:
//   node smoke.mjs pegasus_work_centre_v28.html "role=Engineer&railcollapsed=1"
import { spawn } from 'node:child_process';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { tmpdir } from 'node:os';

const here = dirname(fileURLToPath(import.meta.url));
const chrome = process.env.CHROME || `${process.env.LOCALAPPDATA}\\ms-playwright\\chromium-1234\\chrome-win64\\chrome.exe`;
const file = process.argv[2];
const query = process.argv[3] || '';
const url = pathToFileURL(resolve(here, '..', file)).href + (query ? `?${query}` : '');
const port = 9300 + Math.floor(Math.random() * 600);
const udir = join(tmpdir(), `pw-profile-${process.pid}-${Date.now()}`);
const proc = spawn(chrome, ['--headless=new', '--allow-file-access-from-files', '--disable-gpu', '--no-sandbox', `--user-data-dir=${udir}`, `--remote-debugging-port=${port}`, '--window-size=1580,1000', 'about:blank'], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let target;
for (let i = 0; i < 50 && !target; i++) { await sleep(200); try { const list = await (await fetch(`http://127.0.0.1:${port}/json`)).json(); target = list.find((t) => t.type === 'page'); } catch { /* starting */ } }
if (!target) { proc.kill(); throw new Error('Chromium did not start'); }
const ws = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((r) => (ws.onopen = r));
let seq = 0; const pending = new Map(); const errors = [];
ws.onmessage = (m) => { const msg = JSON.parse(m.data); if (msg.id && pending.has(msg.id)) { pending.get(msg.id)(msg); pending.delete(msg.id); } else if (msg.method === 'Runtime.exceptionThrown') errors.push('EXC ' + (msg.params.exceptionDetails.exception?.description || msg.params.exceptionDetails.text)); else if (msg.method === 'Runtime.consoleAPICalled' && msg.params.type === 'error') errors.push('CONSOLE ' + msg.params.args.map((a) => a.value ?? a.description).join(' ')); };
const send = (method, params = {}) => new Promise((r) => { const id = ++seq; pending.set(id, r); ws.send(JSON.stringify({ id, method, params })); });
await send('Runtime.enable'); await send('Page.enable');
await send('Page.navigate', { url });
await sleep(1200);
const { result } = await send('Runtime.evaluate', { expression: 'JSON.stringify({title: document.title, h1: (document.querySelector("h1")||{}).textContent, mockLoaded: !!window.MOCK, state: window.MOCK && window.MOCK.state})', returnByValue: true });
ws.close(); proc.kill();
console.log('PAGE', result.result.value);
console.log('ERRORS', JSON.stringify(errors));
process.exit(errors.length ? 1 : 0);
