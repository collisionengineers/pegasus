// Shared pieces for the v30 mockup build: the live shell rebuilt from
// src/Pegasus.Web/Pages/Shared/_Layout.cshtml and _LayoutAuth.cshtml at
// origin/dev (no working-set strip; the one Refresh partial), the Lucide
// sprite, the inlined site CSS and fonts, and small markup helpers.
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const lib = path.dirname(fileURLToPath(import.meta.url));
export const current = path.resolve(lib, '..');
export const repo = path.resolve(current, '../../../../');
const web = path.join(repo, 'src/Pegasus.Web');

export const esc = (value) => String(value)
  .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');

export const icon = (id, cls = 'icon') => `<svg class="${cls}" aria-hidden="true"><use href="#icon-${id}" /></svg>`;

// The tone table is Shared/_StatusChip.cshtml's, for the labels these
// surfaces show. Pages pass the label; the chip picks the tone.
const tones = new Map(Object.entries({
  'not ready': 'amber', review: 'navy', 'with engineer': 'navy', held: 'amber', complete: 'green', completed: 'green',
  enabled: 'navy', disabled: 'neutral', 'password change required': 'amber',
  'case created': 'green', 'creating case': 'amber', 'case not created': 'red', triage: 'green', unidentified: 'amber',
  current: 'neutral', stale: 'amber', partial: 'amber', unavailable: 'red', failed: 'red',
  'draft ready': 'amber', taken: 'navy', queued: 'amber', running: 'blue',
  manual: 'neutral', 'e-mail': 'neutral', 'provider api': 'navy', automation: 'blue',
  today: 'amber', overdue: 'red', 'no longer polled': 'neutral', linked: 'green', sent: 'green', 'report sent': 'green',
  registered: 'green', 'could not be read': 'red', received: 'neutral', processing: 'blue', uploading: 'blue',
}));
export const toneOf = (label) => {
  const key = String(label).toLowerCase().replace(/[^a-z0-9 -]/g, '').trim();
  if (tones.has(key)) return tones.get(key);
  if (/overdue$/.test(key)) return 'red';
  if (/^awaiting |preparing$/.test(key)) return 'amber';
  if (/failed/.test(key)) return 'red';
  return 'neutral';
};
export const chip = (label, tone = toneOf(label)) => `<span class="status status--${tone}">${esc(label)}</span>`;

export const refreshButton = () => `<div class="refresh-button" data-refresh-region role="status" aria-live="polite"><form method="get" data-refresh-form><button type="submit" class="btn btn--small" title="Refresh">${icon('refresh-cw', 'icon icon--spin')}<span data-refresh-label>Refresh</span></button></form></div>`;

// Shared/_FreshnessBanner.cshtml: the dot, "Current · HH:MM" and Refresh.
export function freshnessBanner({ status = 'current', clock = '09:41', actionsFirst = false } = {}) {
  const label = { loading: 'Refreshing', stale: 'Stale', partial: 'Partial', unavailable: 'Unavailable', failed: 'Failed' }[status] ?? 'Current';
  const dot = status === 'stale' || status === 'partial' ? ' partial' : status === 'failed' || status === 'unavailable' ? ' failed' : '';
  const notice = dot ? chip(label, dot === ' failed' ? 'red' : 'amber') : '';
  return `<div class="freshness${actionsFirst ? ' freshness--actions-first' : ''}"><span class="freshness-status"><span class="health-dot${dot}"></span><span><time>${label} · ${clock}</time></span>${notice}</span>${refreshButton()}</div>`;
}

export async function loadAssets() {
  const read = (relative) => readFile(path.join(repo, relative));
  const text = (relative) => readFile(path.join(repo, relative), 'utf8');
  const [regular, italic, mark128, mark256] = await Promise.all([
    read('src/Pegasus.Web/wwwroot/fonts/inter/InterVariable.woff2'),
    read('src/Pegasus.Web/wwwroot/fonts/inter/InterVariable-Italic.woff2'),
    read('src/Pegasus.Web/wwwroot/images/pegasus-mark-refined-128.png'),
    read('src/Pegasus.Web/wwwroot/images/pegasus-mark-refined-256.png'),
  ]);
  const site = (await text('src/Pegasus.Web/wwwroot/css/site.css'))
    .replace('url(../fonts/inter/InterVariable.woff2)', `url(data:font/woff2;base64,${regular.toString('base64')})`)
    .replace('url(../fonts/inter/InterVariable-Italic.woff2)', `url(data:font/woff2;base64,${italic.toString('base64')})`);
  const pageCss = [
    'src/Pegasus.Web/wwwroot/css/inbox.css',
    'src/Pegasus.Web/wwwroot/css/work-centre.css',
    'src/Pegasus.Web/wwwroot/css/admin.css',
  ];
  const pages = (await Promise.all(pageCss.map(text))).join('\n');
  // The sprite partial minus its Razor comment: ninety-six symbols.
  const sprite = (await readFile(path.join(web, 'Pages/Shared/_LucideSprite.cshtml'), 'utf8'))
    .replace(/@\*[\s\S]*?\*@/g, '').trim();
  return {
    css: `${site}\n${pages}`,
    sprite,
    mark128: `data:image/png;base64,${mark128.toString('base64')}`,
    mark256: `data:image/png;base64,${mark256.toString('base64')}`,
  };
}

const railLinks = [
  { key: 'work-centre', label: 'Work Centre', icon: 'layout-dashboard', count: null },
  { key: 'inbox', label: 'Inbox', icon: 'inbox', count: 'inbox' },
  { key: 'upload', label: 'Upload', icon: 'upload', count: null },
  { key: 'cases', label: 'Cases', icon: 'list-checks', count: 'cases' },
  { key: 'search', label: 'Search', icon: 'search', count: null },
  { key: 'operations', label: 'Operations', icon: 'activity', count: 'operations' },
];

// The authenticated shell (Shared/_Layout.cshtml): rail, utility bar, main.
// Counts are figures a page already queried; an absent one renders nothing.
export function shell({ route, content, assets, clock = '09:41', counts = { inbox: 6, cases: 17, operations: 1 }, unread = 2, user = { name: 'alex', role: 'Administrator', initials: 'A' } }) {
  const link = ({ key, label, icon: glyph, count }) => {
    const current = key === route ? ' aria-current="page"' : '';
    const figure = count && counts[count] != null
      ? `<span class="nav-count" aria-label="${counts[count]} ${count === 'operations' ? 'retryable' : 'outstanding'}">${counts[count]}</span>`
      : '';
    return `<a class="nav-link" href="#"${current}>${icon(glyph)}<span>${label}</span>${figure}</a>`;
  };
  return `<a class="skip-link" href="#main-content">Skip to main content</a>
<div class="app-shell" data-app-shell>
  <header class="app-rail">
    <a class="brand" href="#" aria-label="Pegasus home"><img src="${assets.mark128}" alt="" aria-hidden="true" /><span class="brand-copy"><strong>PEGASUS</strong><span>Case management</span></span></a>
    <nav class="primary-nav" aria-label="Primary">
      <div class="nav-label">Work</div>
      ${railLinks.map(link).join('\n      ')}
      <div class="nav-label">Manage</div>
      <a class="nav-link" href="#"${route === 'administration' ? ' aria-current="page"' : ''}>${icon('settings')}<span>Administration</span></a>
    </nav>
    <div class="rail-spacer"></div>
    <button type="button" class="rail-collapse" data-rail-toggle aria-expanded="true" aria-label="Collapse navigation" data-label-collapse="Collapse navigation" data-label-expand="Expand navigation">${icon('chevron-left')}<span>Collapse</span></button>
    <div class="rail-health"><div class="rail-health-line"><span class="health-dot"></span><span>Current · ${clock}</span></div></div>
    <div class="rail-user" role="group" aria-label="User"><span class="avatar" aria-hidden="true">${esc(user.initials)}</span><div><strong>${esc(user.name)}</strong><small>${esc(user.role)}</small></div><button type="button" class="icon-button" data-dialog-open="account-dialog" aria-label="Account menu">${icon('more-horizontal')}</button></div>
  </header>
  <div class="app-column">
    <section class="utility-bar" aria-label="Utility bar">
      <div class="utility-freshness"><span class="health-dot"></span><span>Current · ${clock}</span></div>
      <form class="utility-search" role="search" onsubmit="return false"><label class="sr-only" for="global-search">Search Pegasus</label>${icon('search')}<input id="global-search" type="search" name="query" aria-label="Search Pegasus" placeholder="Search cases, references or people" autocomplete="off" /><span class="shortcut-hint" aria-hidden="true">Ctrl K</span></form>
      <div class="utility-actions">
        <a class="btn btn--primary" href="#">${icon('plus')}<span>New case</span></a>
        <span class="bell-wrap"><button type="button" class="icon-button" data-dialog-open="notifications-dialog" aria-label="Notifications · ${unread} unread">${icon('bell')}</button>${unread > 0 ? `<span class="bell-count" aria-hidden="true">${unread}</span>` : ''}</span>
        <button type="button" class="icon-button utility-account" data-dialog-open="account-dialog" aria-label="Account menu">${icon('more-horizontal')}</button>
      </div>
    </section>
    <main id="main-content" class="app-main" tabindex="-1">
      <div class="content">
${content}
      </div>
    </main>
  </div>
</div>
<div class="dialog-backdrop" data-dialog="account-dialog" hidden>
  <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="account-dialog-title">
    <div class="dialog-head"><h2 id="account-dialog-title" tabindex="-1">Account</h2><button type="button" class="dialog-close" data-dialog-close aria-label="Close dialog">${icon('x')}</button></div>
    <div class="dialog-body"><div class="definition-list"><dl class="definition"><dt>Name</dt><dd>${esc(user.name)}</dd></dl><dl class="definition"><dt>Role</dt><dd>${esc(user.role)}</dd></dl><dl class="definition"><dt>Idle lock</dt><dd>30 minutes</dd></dl></div></div>
    <div class="dialog-foot"><button type="button" class="btn" data-dialog-close>Close</button><a class="btn" href="#">${icon('flag')}<span>Release notes</span></a><button type="button" class="btn">${icon('alert-triangle')}<span>Report a problem</span></button><a class="btn" href="#">${icon('key')}<span>Change password</span></a><form onsubmit="return false"><button type="submit" class="btn btn--danger">${icon('log-out')}<span>Sign out</span></button></form></div>
  </section>
</div>
<div class="dialog-backdrop" data-dialog="notifications-dialog" hidden>
  <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="notifications-dialog-title">
    <div class="dialog-head"><h2 id="notifications-dialog-title" tabindex="-1">Notifications</h2><button type="button" class="dialog-close" data-dialog-close aria-label="Close dialog">${icon('x')}</button></div>
    <div class="dialog-body dialog-body--list"><div class="row-list">
      <form class="row-form" onsubmit="return false"><button type="submit" class="row-button row-button--unread"><span class="title">QDOS26010 <span class="mono">BH17RZV</span></span><span class="sub">Assigned to you</span><span class="side">${chip('Unread', 'amber')}<span>09:12</span></span></button></form>
      <form class="row-form" onsubmit="return false"><button type="submit" class="row-button row-button--unread"><span class="title">QDOS25990 <span class="mono">LK21XYZ</span></span><span class="sub">Estimate draft ready</span><span class="side">${chip('Unread', 'amber')}<span>Yesterday 16:40</span></span></button></form>
    </div></div>
    <div class="dialog-foot"><button type="button" class="btn" data-dialog-close>Close</button><button type="button" class="btn">Mark all read</button></div>
  </section>
</div>
<div class="toast-region" aria-live="polite" data-toast-region></div>`;
}

// The navless frame (Shared/_LayoutAuth.cshtml).
export function authFrame({ inner, assets, cardClass = '', brand }) {
  const brandRow = brand ?? `<div class="auth-brand"><img src="${assets.mark256}" alt="" /><strong>PEGASUS</strong></div>`;
  return `<div class="external-shell">
  <main id="main-content" tabindex="-1">
    <section class="auth-card ${cardClass}">
      ${brandRow}
      ${inner}
    </section>
  </main>
</div>`;
}

// Shared/_ErrorSummary.cshtml.
export const errorSummary = (messages) => `<div class="validation-summary" role="alert" aria-live="assertive" tabindex="-1"><h2 class="validation-summary__heading">${icon('alert-circle')}<span>Please correct the following errors:</span></h2><ul class="validation-summary__list">${messages.map((m) => `<li>${esc(m)}</li>`).join('')}</ul></div>`;

export const pageHeader = ({ eyebrow, title, actions = '' }) => `<header class="page-header"><div class="page-title">${eyebrow ? `<p class="eyebrow">${esc(eyebrow)}</p>` : ''}<h1>${title}</h1></div>${actions ? `<div class="page-actions">${actions}</div>` : ''}</header>`;
