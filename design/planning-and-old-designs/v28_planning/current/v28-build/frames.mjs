// Writes the nine family files. A family file is only a frame: a state picker
// (demo control, not product UI) around an iframe that shows one captured state
// page. It contains no Pegasus markup of its own, so it cannot drift from live.
//   node frames.mjs
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const current = resolve(here, '..');
const manifest = JSON.parse(readFileSync(join(here, 'manifest.json'), 'utf8'));
const captured = JSON.parse(readFileSync(join(here, 'captured.json'), 'utf8'));
const presets = [
  ...JSON.parse(readFileSync(join(here, 'shots.json'), 'utf8')),
  // Proposal presets carry a query of their own; a plain proposal shot of a state adds nothing to the picker.
  ...JSON.parse(readFileSync(join(here, 'proposal-shots.json'), 'utf8')).filter((shot, index, all) => shot.query && !JSON.parse(readFileSync(join(here, 'shots.json'), 'utf8')).some((b) => b.state === shot.state && b.query === shot.query)).map((shot) => ({ ...shot, label: 'Proposal: ' + shot.shows.split(';')[0] })),
];

const escape = (text) => String(text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');

for (const family of manifest.families) {
  const states = captured.filter((state) => state.family === family.key);
  const options = states.map((state) => {
    const own = presets.filter((preset) => preset.state === state.id);
    return `<option value="${escape(state.id)}" data-live="${escape(state.live)}" data-note="${escape(state.note || '')}">${escape(state.label)}</option>`
      + own.map((preset) => `<option value="${escape(state.id)}" data-query="${escape(preset.query)}" data-live="${escape(state.live)}" data-note="Preset: ${escape(preset.query)}">&nbsp;&nbsp;&nbsp;${escape(preset.label || preset.name)}</option>`).join('');
  }).join('\n        ');
  const others = manifest.families.map((other) =>
    `<option value="${escape(other.file)}"${other.key === family.key ? ' selected' : ''}>${escape(other.title)}</option>`).join('');

  const html = `<!DOCTYPE html>
<!--
  v28 baseline, ${family.title}. Captured from ${manifest.source}; the operator's proposals sit over it as a switchable layer.
  This file is a frame around captured state pages (states/*.html), which are the
  application's own server-rendered HTML with the live CSS and JS. The bar at the
  top is demo control, not product UI. Temporary design review artifact: not
  application code, not design authority, not implementation evidence.
-->
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<title>${escape(family.title)} · Pegasus v28 baseline</title>
<style>
  html, body { height: 100%; margin: 0; background: #2a2f32; font: 12px/1.4 system-ui, -apple-system, "Segoe UI", sans-serif; color: #e7e9ea; }
  body { display: grid; grid-template-rows: auto 1fr; }
  .bar { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 14px; padding: 6px 10px; background: #1b1e20; border-bottom: 1px solid #33383b; }
  .bar strong { font-weight: 600; }
  .bar label { display: inline-flex; align-items: center; gap: 6px; color: #9aa0a3; }
  .bar select, .bar button, .bar a.button { font: inherit; background: #2a2f32; color: #e7e9ea; border: 1px solid #454b4f; border-radius: 3px; padding: 3px 8px; text-decoration: none; cursor: pointer; }
  .bar select { max-width: 46ch; }
  .bar button[aria-pressed="true"] { background: #e7e9ea; color: #1b1e20; }
  .bar .live { font-family: ui-monospace, Consolas, monospace; color: #c9cdd0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 60ch; }
  .bar .note { color: #9aa0a3; font-style: italic; }
  .stage { display: flex; justify-content: center; overflow: auto; min-height: 0; }
  iframe { border: 0; background: #fff; height: 100%; width: 100%; flex: none; }
</style>
</head>
<body>
  <div class="bar" role="group" aria-label="Mockup controls (demo control, not product UI)">
    <strong>v28 baseline</strong>
    <label>Area <select id="family">${others}</select></label>
    <label>State <select id="state">
        ${options}
    </select></label>
    <span role="group" aria-label="Width">
      <button type="button" data-width="" aria-pressed="true">Fit</button>
      <button type="button" data-width="1580" aria-pressed="false">1580</button>
      <button type="button" data-width="1440" aria-pressed="false">1440</button>
      <button type="button" data-width="760" aria-pressed="false">760</button>
    </span>
    <span role="group" aria-label="Baseline or proposals">
      <button type="button" data-layer="on" aria-pressed="true">Proposals</button>
      <button type="button" data-layer="off" aria-pressed="false">Baseline</button>
    </span>
    <a class="button" id="alone" href="#" target="_blank" rel="noopener">Open state alone</a>
    <span class="live" id="live" title="The live route this state was captured from"></span>
    <span class="note" id="note"></span>
  </div>
  <div class="stage"><iframe id="view" title="${escape(family.title)}"></iframe></div>
<script src="assets/mock/routes.js"></script>
<script>
(function () {
  var family = ${JSON.stringify(family.key)};
  var select = document.getElementById('state');
  var view = document.getElementById('view');
  var owner = {};
  var baseline = false;
  (window.V28_ROUTES.families || []).forEach(function (f) { f.states.forEach(function (s) { owner[s.id] = f; }); });

  function show(option, push) {
    var id = option.value;
    var query = option.getAttribute('data-query') || '';
    var layer = baseline ? '&proposals=off' : '';
    view.src = 'states/' + id + '.html?embed=1' + layer + (query ? '&' + query : '');
    document.getElementById('alone').href = 'states/' + id + '.html?' + (baseline ? 'proposals=off' : 'proposals=on') + (query ? '&' + query : '');
    document.getElementById('live').textContent = option.getAttribute('data-live') || '';
    document.getElementById('note').textContent = option.getAttribute('data-note') || '';
    if (push) history.replaceState(null, '', '#' + id + (query ? '?' + query : ''));
  }
  function find(id, query) {
    for (var i = 0; i < select.options.length; i++) {
      var o = select.options[i];
      if (o.value === id && (o.getAttribute('data-query') || '') === (query || '')) return o;
    }
    return null;
  }
  select.addEventListener('change', function () { show(select.options[select.selectedIndex], true); });
  document.getElementById('family').addEventListener('change', function (event) { window.location.href = event.target.value; });
  document.querySelectorAll('[data-width]').forEach(function (button) {
    button.addEventListener('click', function () {
      document.querySelectorAll('[data-width]').forEach(function (b) { b.setAttribute('aria-pressed', b === button ? 'true' : 'false'); });
      var width = button.getAttribute('data-width');
      view.style.width = width ? width + 'px' : '100%';
    });
  });
  document.querySelectorAll('[data-layer]').forEach(function (button) {
    button.addEventListener('click', function () {
      baseline = button.getAttribute('data-layer') === 'off';
      document.querySelectorAll('[data-layer]').forEach(function (b) { b.setAttribute('aria-pressed', b === button ? 'true' : 'false'); });
      show(select.options[select.selectedIndex], false);
    });
  });
  // A captured page navigating to another captured state tells the frame.
  window.addEventListener('message', function (event) {
    if (!event.data || event.data.v28 !== 'state') return;
    var id = event.data.id;
    var target = owner[id];
    if (target && target.key !== family) { window.location.href = target.file + '#' + id; return; }
    var option = find(id, '');
    if (option) { select.value = id; select.selectedIndex = option.index; show(option, true); }
  });
  var hash = decodeURIComponent(window.location.hash.replace(/^#/, ''));
  var parts = hash.split('?');
  var start = (hash && find(parts[0], parts[1] || '')) || select.options[0];
  select.selectedIndex = start.index;
  show(start, false);
})();
</script>
</body>
</html>
`;
  writeFileSync(join(current, family.file), html);
  console.log('frame', family.file, states.length, 'states');
}
