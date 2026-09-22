// One full-height screenshot of a state, for reviewing long pages.
//   node fullshot.mjs <state-id> [width] [out.png]
import { writeFileSync } from 'node:fs';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { launch, sleep } from './cdp.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const id = process.argv[2];
const width = Number(process.argv[3] || 1440);
const out = process.argv[4] || join(here, '..', 'v28-shots', `full-${id}-${width}.png`);
const page = await launch({ width, height: 900 });
await page.goto(pathToFileURL(resolve(here, '..', 'states', id + '.html')).href, 900);
// The record scrolls inside its own column, so measure the tallest scroller.
const height = await page.eval(`Math.max(document.documentElement.scrollHeight,
  ...[...document.querySelectorAll('main, .app-main, .workspace-main')].map((e) => e.scrollHeight + 260))`);
await page.viewport(width, Math.min(height, 12000));
await sleep(600);
writeFileSync(out, await page.screenshot());
await page.close();
console.log(out, height);
