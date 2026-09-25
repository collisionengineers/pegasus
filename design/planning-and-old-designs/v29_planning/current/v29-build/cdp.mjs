// Minimal headless-Chromium driver over the DevTools protocol, no npm packages.
import { spawn } from 'node:child_process';
import { join } from 'node:path';
import { tmpdir } from 'node:os';

export const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

export async function launch({ width = 1440, height = 900 } = {}) {
  const chrome = process.env.CHROME
    || join(process.env.LOCALAPPDATA, 'ms-playwright', 'chromium-1234', 'chrome-win64', 'chrome.exe');
  const port = 9300 + Math.floor(Math.random() * 600);
  const proc = spawn(chrome, [
    '--headless=new', '--ignore-certificate-errors', '--allow-file-access-from-files',
    '--hide-scrollbars', '--disable-gpu', '--no-sandbox',
    `--user-data-dir=${join(tmpdir(), 'v29-capture-' + process.pid + '-' + Date.now())}`,
    `--remote-debugging-port=${port}`, 'about:blank',
  ], { stdio: 'ignore' });

  let target;
  for (let i = 0; i < 200 && !target; i++) {
    await sleep(200);
    try {
      const list = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
      target = list.find((t) => t.type === 'page');
    } catch { /* not up yet */ }
  }
  if (!target) { proc.kill(); throw new Error('Chromium did not start'); }

  const ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve) => (ws.onopen = resolve));
  let seq = 0;
  const pending = new Map();
  const errors = [];
  ws.onmessage = (message) => {
    const msg = JSON.parse(message.data);
    if (msg.id && pending.has(msg.id)) { pending.get(msg.id)(msg); pending.delete(msg.id); return; }
    if (msg.method === 'Runtime.exceptionThrown') {
      errors.push('EXC ' + (msg.params.exceptionDetails.exception?.description || msg.params.exceptionDetails.text));
    }
    if (msg.method === 'Runtime.consoleAPICalled' && msg.params.type === 'error') {
      errors.push('CONSOLE ' + msg.params.args.map((a) => a.value ?? a.description).join(' '));
    }
  };
  const send = (method, params = {}) => new Promise((resolve) => {
    const id = ++seq; pending.set(id, resolve); ws.send(JSON.stringify({ id, method, params }));
  });
  await send('Page.enable');
  await send('Runtime.enable');
  await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });

  const page = {
    errors,
    send,
    async viewport(w, h) {
      await send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
    },
    async goto(url, settle = 1200) {
      const nav = await send('Page.navigate', { url });
      if (nav.error || nav.result?.errorText) throw new Error(`navigate ${url}: ${JSON.stringify(nav.error || nav.result.errorText)}`);
      await sleep(settle);
    },
    async eval(expression) {
      const res = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
      if (res.result?.exceptionDetails) {
        throw new Error('eval failed: ' + (res.result.exceptionDetails.exception?.description || res.result.exceptionDetails.text));
      }
      return res.result?.result?.value;
    },
    async screenshot() {
      const res = await send('Page.captureScreenshot', { format: 'png' });
      return Buffer.from(res.result.data, 'base64');
    },
    // Browser.close ends the whole process tree. proc.kill() alone leaves the
    // renderer and GPU children running on Windows, one set per launch.
    async close() {
      try { await Promise.race([send('Browser.close'), sleep(3000)]); } catch { /* already gone */ }
      try { ws.close(); } catch { /* closed */ }
      try { proc.kill(); } catch { /* exited */ }
      if (process.platform === 'win32' && proc.pid) {
        try { spawn('taskkill', ['/PID', String(proc.pid), '/T', '/F'], { stdio: 'ignore' }); } catch { /* exited */ }
      }
    },
  };
  return page;
}
