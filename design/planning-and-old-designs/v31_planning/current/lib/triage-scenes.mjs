// Synthetic vehicle photographs for the v31 Triage mockups. Every image is a
// generated SVG scene (no photograph, no corpus material): a car-park ground,
// a hatchback drawn from one of seven views, and a damage mark where the view
// shows one. The registration is the synthetic MA59 BDY fixture.

const W = 1200;
const H = 800;

const palettes = {
  silver: ['#c9ced3', '#9aa2aa', '#6e767e'],
  blue: ['#3c6fb4', '#2b5289', '#1d3a63'],
  red: ['#b8403a', '#8c2c27', '#5f1d1a'],
};

const ground = (sky, tarmac) => `
  <defs>
    <linearGradient id="sky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${sky[0]}"/><stop offset="1" stop-color="${sky[1]}"/></linearGradient>
    <linearGradient id="tar" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${tarmac[0]}"/><stop offset="1" stop-color="${tarmac[1]}"/></linearGradient>
    <radialGradient id="shade" cx=".5" cy=".5" r=".5"><stop offset="0" stop-color="#000" stop-opacity=".45"/><stop offset="1" stop-color="#000" stop-opacity="0"/></radialGradient>
    <filter id="grain"><feTurbulence type="fractalNoise" baseFrequency=".9" numOctaves="2" seed="4"/><feColorMatrix values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 .08 0"/><feComposite in2="SourceGraphic" operator="in"/></filter>
  </defs>
  <rect width="${W}" height="${H}" fill="url(#sky)"/>
  <rect y="430" width="${W}" height="${H - 430}" fill="url(#tar)"/>
  <rect x="0" y="420" width="${W}" height="14" fill="#7d8186"/>
  <g stroke="#e9e3c8" stroke-width="6" opacity=".55"><line x1="90" y1="800" x2="230" y2="470"/><line x1="1110" y1="800" x2="970" y2="470"/></g>`;

// A three-quarter-free side profile: body, glasshouse, wheels.
function side(colour, flip, damage) {
  const [body, mid, dark] = palettes[colour];
  const t = flip ? `translate(${W} 0) scale(-1 1)` : '';
  return `<g transform="${t}">
    <ellipse cx="600" cy="640" rx="430" ry="40" fill="url(#shade)"/>
    <path d="M180 590 L190 500 Q205 460 260 450 L380 440 L470 360 Q500 340 560 338 L800 338 Q850 340 880 370 L960 440 L1010 455 Q1040 465 1040 500 L1035 590 Z" fill="${body}" stroke="${dark}" stroke-width="4"/>
    <path d="M400 440 L480 368 Q500 352 545 352 L640 352 L640 440 Z" fill="#2c3540" opacity=".9"/>
    <path d="M660 352 L790 352 Q830 354 850 376 L905 440 L660 440 Z" fill="#2c3540" opacity=".9"/>
    <line x1="650" y1="352" x2="650" y2="585" stroke="${dark}" stroke-width="3"/>
    <line x1="190" y1="520" x2="1035" y2="520" stroke="${mid}" stroke-width="3"/>
    <rect x="560" y="470" width="44" height="8" rx="3" fill="${dark}"/><rect x="780" y="470" width="44" height="8" rx="3" fill="${dark}"/>
    <path d="M1010 470 L1040 480 L1038 505 L1005 500 Z" fill="#f4d58d"/>
    <path d="M188 505 L215 500 L212 530 L186 532 Z" fill="#c0392b"/>
    ${[330, 870].map((x) => `<circle cx="${x}" cy="590" r="74" fill="#1b1e22"/><circle cx="${x}" cy="590" r="44" fill="#8d949b"/><circle cx="${x}" cy="590" r="12" fill="#3a3f45"/>`).join('')}
    ${damage ? `<g><path d="M720 470 q30 -14 70 4 q40 18 82 2 l12 40 q-50 22 -92 6 q-38 -14 -64 2 z" fill="${dark}" opacity=".55"/>
      <path d="M716 488 l40 6 m4 8 l52 -2 m-80 18 l70 4 m8 -30 l46 10" stroke="#f5f5f5" stroke-width="3" opacity=".75" stroke-linecap="round"/>
      <ellipse cx="800" cy="500" rx="64" ry="26" fill="none" stroke="#fff" stroke-width="2" opacity=".35"/></g>` : ''}
  </g>`;
}

function front(colour, rear, damage) {
  const [body, mid, dark] = palettes[colour];
  const lamp = rear ? '#c0392b' : '#f4d58d';
  return `<g>
    <ellipse cx="600" cy="660" rx="360" ry="38" fill="url(#shade)"/>
    <path d="M300 640 L300 470 Q310 420 360 400 L420 300 Q440 270 500 266 L700 266 Q760 270 780 300 L840 400 Q890 420 900 470 L900 640 Z" fill="${body}" stroke="${dark}" stroke-width="4"/>
    <path d="M435 400 L480 305 Q495 288 520 288 L680 288 Q705 288 720 305 L765 400 Z" fill="#2c3540" opacity=".9"/>
    <rect x="330" y="440" width="120" height="42" rx="10" fill="${lamp}"/><rect x="750" y="440" width="120" height="42" rx="10" fill="${lamp}"/>
    <rect x="470" y="${rear ? 520 : 500}" width="260" height="${rear ? 20 : 60}" rx="8" fill="${dark}" opacity="${rear ? .5 : .85}"/>
    <rect x="500" y="560" width="200" height="46" rx="4" fill="${rear ? '#f2c531' : '#f7f7f2'}" stroke="#222" stroke-width="3"/>
    <text x="600" y="595" text-anchor="middle" font-family="Arial, sans-serif" font-weight="700" font-size="34" fill="#111">MA59 BDY</text>
    <rect x="300" y="620" width="600" height="20" fill="${mid}"/>
    <rect x="320" y="636" width="70" height="44" rx="8" fill="#1b1e22"/><rect x="810" y="636" width="70" height="44" rx="8" fill="#1b1e22"/>
    ${damage ? `<g><path d="M760 470 q50 -10 110 20 l20 80 q-60 30 -120 6 z" fill="${dark}" opacity=".6"/>
      <path d="M770 500 l90 20 m-80 10 l70 30 m-60 -60 l40 -10" stroke="#f5f5f5" stroke-width="3" opacity=".7" stroke-linecap="round"/>
      <path d="M840 448 l26 -18 l14 30" fill="none" stroke="#111" stroke-width="4" opacity=".6"/></g>` : ''}
  </g>`;
}

function closeUp(colour) {
  const [body, mid, dark] = palettes[colour];
  return `<rect width="${W}" height="${H}" fill="${body}"/>
    <rect width="${W}" height="${H}" filter="url(#grain)"/>
    <path d="M0 520 Q600 470 1200 540 L1200 800 L0 800 Z" fill="${mid}"/>
    <path d="M0 515 Q600 465 1200 535" stroke="#fff" stroke-width="5" opacity=".35" fill="none"/>
    <path d="M330 300 q160 -60 360 10 q140 50 230 10 l30 220 q-170 70 -330 20 q-170 -50 -300 10 z" fill="${dark}" opacity=".5"/>
    <g stroke="#f6f6f6" stroke-linecap="round" opacity=".8"><path d="M360 360 l260 30" stroke-width="5"/><path d="M380 420 l310 10" stroke-width="4"/><path d="M420 470 l240 40" stroke-width="3"/><path d="M640 330 l210 60" stroke-width="4"/></g>
    <ellipse cx="610" cy="420" rx="270" ry="120" fill="none" stroke="#fff" stroke-width="3" opacity=".3"/>
    <circle cx="1040" cy="190" r="70" fill="#fff" opacity=".18"/>`;
}

function odometer() {
  return `<rect width="${W}" height="${H}" fill="#14171b"/>
    <circle cx="600" cy="420" r="290" fill="#1d2228" stroke="#2f363e" stroke-width="10"/>
    ${Array.from({ length: 13 }, (_, i) => { const a = (-210 + i * 20) * Math.PI / 180; return `<line x1="${600 + 250 * Math.cos(a)}" y1="${420 + 250 * Math.sin(a)}" x2="${600 + 275 * Math.cos(a)}" y2="${420 + 275 * Math.sin(a)}" stroke="#d9dde2" stroke-width="5"/>`; }).join('')}
    <line x1="600" y1="420" x2="420" y2="300" stroke="#e8553f" stroke-width="9" stroke-linecap="round"/>
    <circle cx="600" cy="420" r="22" fill="#3a4048"/>
    <rect x="455" y="520" width="290" height="70" rx="6" fill="#0c0e10" stroke="#3a4048" stroke-width="3"/>
    <text x="600" y="570" text-anchor="middle" font-family="Consolas, monospace" font-size="46" fill="#9fe3b4">064 218</text>
    <text x="600" y="625" text-anchor="middle" font-family="Arial, sans-serif" font-size="22" fill="#7d858e">miles</text>`;
}

const views = {
  'front-left': (c, d) => side(c, true, d),
  'rear-right': (c, d) => side(c, false, d),
  nearside: (c, d) => side(c, true, d),
  offside: (c, d) => side(c, false, d),
  front: (c, d) => front(c, false, d),
  rear: (c, d) => front(c, true, d),
  'close-up': (c) => closeUp(c),
  odometer: () => odometer(),
};

export function scene({ view, colour = 'silver', damage = false, light = 'day' }) {
  const sky = light === 'dusk' ? ['#4a5a78', '#c99b7a'] : ['#b9cde0', '#e8eef3'];
  const tarmac = light === 'dusk' ? ['#4b4d52', '#2e3034'] : ['#6d7076', '#4c4f54'];
  const inner = view === 'close-up' || view === 'odometer' ? views[view](colour, damage) : ground(sky, tarmac) + views[view](colour, damage);
  const defs = view === 'close-up' ? '<defs><filter id="grain"><feTurbulence type="fractalNoise" baseFrequency=".9" numOctaves="2" seed="7"/><feColorMatrix values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 .1 0"/><feComposite in2="SourceGraphic" operator="in"/></filter></defs>' : '';
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}" width="${W}" height="${H}">${defs}${inner}</svg>`;
  return `data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}`;
}
