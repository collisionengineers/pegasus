// Diagnostic: what the live Case page offers right now (edit state, field names).
//   HOST_INFO=<host.json> node probe.mjs [path]
import { readFileSync } from 'node:fs';
import { launch, sleep } from './cdp.mjs';

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
const info = JSON.parse(readFileSync(process.env.HOST_INFO, 'utf8'));
const page = await launch();
await page.goto(`${info.url}${process.argv[2] || `/Cases/${info.caseId}`}`, 2500);
const report = await page.eval(`(() => ({
  title: document.title,
  h1: (document.querySelector('h1') || {}).textContent,
  editing: !!document.querySelector('input[name="editLeaseToken"]'),
  claim: [...document.querySelectorAll('form')].map((f) => f.getAttribute('action') || '').filter((a) => /Lease|Edit/i.test(a)),
  buttons: [...document.querySelectorAll('.ribbon-actions button, .ribbon-actions a, [data-case-ribbon-actions] button')].map((b) => b.textContent.replace(/\\s+/g, ' ').trim()).slice(0, 12),
  names: [...new Set([...document.querySelectorAll('[name]')].map((e) => e.getAttribute('name')))].slice(0, 60),
  notices: [...document.querySelectorAll('.notice, .stale-bar')].map((e) => e.textContent.replace(/\\s+/g, ' ').trim()).slice(0, 5),
}))()`);
console.log(JSON.stringify(report, null, 1));
await page.close();
