// Inbox (Pages/Mail/Index.cshtml at origin/dev). Baseline is the live
// markup with a synthetic fixture; the proposal layer carries items L to P.
import { chip, esc, freshnessBanner, icon, pageHeader } from './shared.mjs';

const mailbox = 'instructions@ce-mailbox.example';
const rows = [
  { id: 1, sender: 'claims@principal-a.example', subject: 'New instruction – BH17RZV – J Morgan', excerpt: 'Please find attached our instruction for the above vehicle. Inspection at the claimant’s address; images to follow from the repairer.', date: '25 Sep 2026', time: '08:52', unread: true, outcome: 'Case created', classification: 'New instruction', caseRef: 'QDOS26010', attachments: 2, matches: 'Subject; Body' },
  { id: 2, sender: 'workshop@repairer.example', subject: 'RE: QDOS25984 estimate query', excerpt: 'Thanks for the estimate. Two lines need clarifying before we can start – see below.', date: '25 Sep 2026', time: '08:31', unread: true, outcome: 'Case created', classification: 'In-progress case', caseRef: 'QDOS25984', attachments: 1 },
  { id: 3, sender: 'a.taylor@example.com', subject: 'Photos of damage', excerpt: 'Hi, here are the photos you asked for. Let me know if you need anything else.', date: '24 Sep 2026', time: '17:06', unread: false, outcome: 'Unidentified', classification: 'Unclassified', destination: 'Unidentified', attachments: 5, matches: 'Body' },
  { id: 4, sender: 'fleet@principal-b.example', subject: 'Pre-instruction: MA59BDY availability', excerpt: 'Can you confirm an Engineer is available next week for an inspection in Reading?', date: '24 Sep 2026', time: '15:40', unread: false, outcome: 'Triage', classification: 'Pre-instruction', destination: 'Triage' },
  { id: 5, sender: 'claims@principal-a.example', subject: 'Instruction – LK21XYZ', excerpt: 'New instruction attached.', date: '24 Sep 2026', time: '14:12', unread: false, outcome: 'Creating case', classification: 'New instruction', attachments: 1 },
  { id: 6, sender: 'claims@principal-c.example', subject: 'Update request – QDOS26003', excerpt: 'Please provide an update on the above claim.', date: '24 Sep 2026', time: '11:58', unread: false, outcome: 'Case created', classification: 'In-progress case', caseRef: 'QDOS26003' },
  { id: 7, sender: 'claims@principal-a.example', subject: 'Instruction', excerpt: 'Please see attached.', date: '23 Sep 2026', time: '16:20', unread: false, outcome: 'Case not created', classification: 'New instruction', attachments: 1, forwarder: 'office@ce-mailbox.example' },
  { id: 8, sender: 'r.khan@ce-mailbox.example', subject: 'FW: Report sent – QDOS25990', excerpt: 'For the file.', date: '23 Sep 2026', time: '09:05', unread: false, outcome: 'Case created', classification: 'Post-report', caseRef: 'QDOS25990' },
];
const dismissedRows = [
  { id: 21, sender: 'newsletter@parts-supplier.example', subject: 'September offers', excerpt: 'This month’s trade prices…', date: '22 Sep 2026', time: '07:30', unread: false, outcome: 'Classified', classification: 'Not client related' },
  { id: 22, sender: 'accounts@principal-c.example', subject: 'Remittance advice 4471', excerpt: 'Payment made today.', date: '19 Sep 2026', time: '12:02', unread: false, outcome: 'Classified', classification: 'Billing', attachments: 1 },
];
const scopes = [
  { label: 'All incoming', icon: 'inbox', count: 23 },
  { label: 'Receiving work', icon: 'download', count: 9 },
  { label: 'Case updates', icon: 'reply', count: 7 },
  { label: 'Pre-instructions', icon: 'clock', count: 2 },
  { label: 'Unidentified', icon: 'search', count: 3 },
  { label: 'Sent Items', icon: 'send', count: 0 },
  { label: 'Dismissed', icon: 'x', count: 2 },
];

const attachmentsText = (n) => n ? `${n} attachment${n === 1 ? '' : 's'}` : null;

function scopeList(pressed, empty) {
  return `<div class="scope-list" data-mail-scopes>${scopes.map((s) => `<form method="get" onsubmit="return false"><button type="submit" class="scope-button" aria-pressed="${s.label === pressed ? 'true' : 'false'}"><span class="scope-visual-icon">${icon(s.icon)}</span><span>${s.label}</span><span class="tabular">${empty ? 0 : s.count}</span></button></form>`).join('')}</div>`;
}

function filterForm({ proposal, dismissed, folder = 'Inbox', search = '' }) {
  const selects = `<div class="field"><label for="mailbox-filter">Mailbox</label><select id="mailbox-filter" name="mailbox"><option value="" selected>All Mailboxes</option><option>${mailbox}</option><option>office@ce-mailbox.example</option></select></div>
${dismissed ? '' : `<div class="field"><label for="folder-filter">Folder</label><select id="folder-filter" name="folder"><option value=""${folder === 'Inbox' ? ' selected' : ''}>Inbox</option><option value="sent"${folder === 'Sent' ? ' selected' : ''}>Sent Items</option><option value="deleted"${folder === 'Deleted' ? ' selected' : ''}>Deleted Items</option></select></div>`}
<div class="field"><label for="queue-filter">Category</label><select id="queue-filter" name="queue"${folder === 'Deleted' ? ' disabled' : ''}><option value="" selected>All categories</option><optgroup label="Destinations"><option>Receiving work</option><option>Queries</option><option>Triage</option><option>Unidentified</option><option>Other</option></optgroup><optgroup label="Categories"><option>General</option><option>Billing</option><option>Not client related</option><option>Internal CC</option></optgroup></select></div>`;
  const input = `<input id="mail-search" type="search" name="search" value="${esc(search)}" maxlength="200" placeholder="Subject, sender, reference" />`;
  if (proposal) {
    return `<form method="get" class="inbox-filter p30-filter" aria-label="Mail view" onsubmit="return false">${selects}
<div class="field inbox-filter__search"><label for="mail-search">Search</label><div class="p30-searchrow">${input}<button class="btn btn--dark" type="submit">Search</button></div></div></form>`;
  }
  return `<form method="get" class="inbox-filter" aria-label="Mail view" onsubmit="return false">${selects}
<div class="field inbox-filter__search"><label for="mail-search">Search</label>${input}</div>
<button class="btn btn--dark" type="submit">Search</button></form>`;
}

function messageRow(item, { proposal, selected, dismissed, state }) {
  const unread = item.unread ? ' unread' : '';
  const isSelected = selected === item.id;
  const attachments = attachmentsText(item.attachments);
  const attachmentsMarkup = attachments
    ? (proposal ? `<span class="p30-att" title="${attachments}">${icon('paperclip')}<span>${item.attachments}</span><span class="sr-only"> ${attachments.split(' ')[1]}</span></span>` : esc(attachments))
    : null;
  const caseLine = item.caseRef
    ? `<span class="row-meta"><a href="#">${item.caseRef}</a>${attachmentsMarkup ? ` · ${attachmentsMarkup}` : ''}</span>`
    : attachmentsMarkup ? `<span class="row-meta">${attachmentsMarkup}</span>` : '';
  const destination = !item.caseRef && item.destination ? ` · ${item.destination}` : '';
  const action = dismissed
    ? `<button type="submit" class="icon-button" title="Restore" aria-label="Restore ${esc(item.subject)}">${icon('undo')}</button>`
    : `<button type="submit" class="icon-button" title="Dismiss" aria-label="Dismiss ${esc(item.subject)}">${icon('x')}</button>`;
  return `<div class="row-button inbox-row${unread}" data-mail-preview-row data-mail-row="${item.id}">
  <span class="row-top"><span class="row-title">${item.unread ? '<span class="unread-indicator" aria-hidden="true"></span><span class="sr-only">Unread</span>' : ''}${esc(item.sender)}</span><span class="row-time"><time>${item.date}</time><time>${item.time}</time></span></span>
  <a class="row-title" href="?state=${state}&amp;selected=${item.id}" data-mail-preview-trigger aria-controls="mail-quick-preview" aria-expanded="${isSelected ? 'true' : 'false'}"${isSelected ? ' aria-current="true"' : ''} title="${esc(item.subject)}">${esc(item.subject)}</a>
  ${item.excerpt ? `<span class="row-excerpt">${esc(item.excerpt)}</span>` : ''}
  <span class="row-top"><span class="cluster">${chip(item.outcome)}<span class="row-meta">${esc(item.classification)}${destination}</span></span>${caseLine}</span>
  ${item.forwarder ? `<span class="row-meta">Forwarded by ${esc(item.forwarder)}</span>` : ''}
  ${item.matches && state === 'search' ? `<span class="row-meta">Matched in: ${esc(item.matches)}</span>` : ''}
  <form method="post" class="inbox-row-action" data-mail-row-action="${dismissed ? 'restore' : 'dismiss'}" onsubmit="return false">${action}</form>
</div>`;
}

function deletedRow(item) {
  return `<div class="row-button">
  <span class="row-top"><span class="row-title">${esc(item.sender)}</span><span class="row-time"><time>${item.date}</time><time>${item.time}</time></span></span>
  <span class="row-title">${esc(item.subject)}</span>
  <span class="row-meta">Matched in: Subject</span>
  <span class="row-top"><span class="row-meta">${mailbox}</span><span class="row-meta">${attachmentsText(item.attachments ?? 0) ?? '0 attachments'}</span></span>
</div>`;
}

function preview(item, { proposal }) {
  const attachments = item.attachments
    ? Array.from({ length: item.attachments }, (_, i) => i === 0 ? `Instruction ${item.caseRef ?? 'BH17RZV'}.pdf` : `IMG_44${70 + i}.jpg`)
    : [];
  const list = proposal
    ? `<ul class="accepted-list p30-att-list" data-mail-preview-attachments>${attachments.length ? attachments.map((a) => `<li>${icon('paperclip')}<span>${esc(a)}</span></li>`).join('') : '<li class="p30-att-none">No attachments</li>'}</ul>`
    : `<ul class="accepted-list" data-mail-preview-attachments>${attachments.length ? attachments.map((a) => `<li>${esc(a)}</li>`).join('') : '<li>No attachments</li>'}</ul>`;
  return `<div class="pane">
  <div class="pane-head"><h2>Message preview</h2></div>
  <div class="pane-body pane-scroll" tabindex="0">
    <aside id="mail-quick-preview" class="mail-preview" data-mail-preview aria-label="${esc(item.subject)}">
      <p data-mail-preview-status aria-live="polite" hidden></p>
      <div data-mail-preview-facts>
        <div class="mail-header"><div class="cluster cluster--between"><div><div class="mail-subject" data-mail-preview-subject>${esc(item.subject)}</div><div class="mail-route">From <span data-mail-preview-sender>${esc(item.sender)}</span> · <time data-mail-preview-received>${item.date} ${item.time}</time> · ${mailbox}</div></div>${chip(item.outcome)}</div></div>
        <div class="mail-body"><p data-mail-preview-excerpt>${esc(item.excerpt)}</p></div>
        <div class="section-gap">${list}</div>
        <div class="section-gap fact-grid">
          <dl class="fact"><dt>Classification</dt><dd data-mail-preview-classification>${esc(item.classification)}</dd></dl>
          <dl class="fact"><dt>Case association</dt><dd data-mail-preview-association>${item.caseRef ?? 'No case'}</dd></dl>
          <dl class="fact"><dt>Folder</dt><dd>Inbox</dd></dl>
          <dl class="fact"><dt>Search match</dt><dd>—</dd></dl>
        </div>
        <div class="button-row section-gap" data-mail-preview-actions>
          <a class="btn btn--dark" href="#">${icon('external-link')}<span>Open full message</span></a>
          ${item.caseRef ? `<a class="btn" href="#">${icon('folder')}<span>Open linked Case</span></a>` : ''}
        </div>
      </div>
    </aside>
  </div>
</div>`;
}

export const inbox = {
  key: 'inbox',
  file: 'pegasus_inbox_v30.html',
  title: 'Inbox',
  frame: 'shell',
  route: 'inbox',
  states: [
    { id: 'default', label: 'Messages with a selected row (three panes)', expect: ['.pane-layout--3', '.row-title[aria-current="true"]', '#mail-quick-preview'] },
    { id: 'list', label: 'Messages, nothing selected (two panes)', expect: ['.pane-layout--2'], forbid: ['#mail-quick-preview'] },
    { id: 'empty', label: 'No mail', expect: ['.empty'] },
    { id: 'stale', label: 'Freshness stale', expect: ['.freshness .status--amber'] },
    { id: 'dismissed', label: 'Dismissed scope', expect: ['[data-mail-row-action="restore"]'], forbid: ['#folder-filter'] },
    { id: 'deleted', label: 'Deleted Items search', expect: ['#queue-filter[disabled]', '.notice'] },
    { id: 'search', label: 'Search with matches', expect: ['.row-meta'] },
  ],
  layers: ['baseline', 'proposal'],
  opts: [],
  render(state, layer) {
    const proposal = layer === 'proposal';
    const selected = state === 'default' ? 1 : null;
    const dismissed = state === 'dismissed';
    const deleted = state === 'deleted';
    const search = state === 'search' ? 'BH17RZV' : deleted ? 'QDOS25984' : '';
    const visible = state === 'empty' ? [] : dismissed ? dismissedRows : state === 'search' ? rows.filter((r) => r.matches) : rows;
    const total = state === 'empty' ? 0 : dismissed ? 2 : state === 'search' ? 2 : deleted ? 2 : 23;
    const pressed = dismissed ? 'Dismissed' : 'All incoming';
    const header = pageHeader({
      eyebrow: 'Emails',
      title: 'Inbox',
      actions: `<div class="page-action-group"><a class="btn" href="#" data-mail-compose-open aria-controls="mail-compose-host" aria-expanded="false">${icon('send')}<span>Compose</span></a>${freshnessBanner({ status: state === 'stale' ? 'stale' : 'current', actionsFirst: true })}</div>`,
    });
    let body;
    if (deleted) {
      body = `<div class="notice" role="status">${icon('info')}<span>This read-only search checked the 100 newest Deleted Items in the approved scope. Older items were not scanned.</span></div>
${deletedRow({ sender: 'workshop@repairer.example', subject: 'RE: QDOS25984 estimate query', date: '25 Sep 2026', time: '08:31', attachments: 1 })}
${deletedRow({ sender: 'accounts@repairer.example', subject: 'QDOS25984 invoice', date: '20 Sep 2026', time: '10:14', attachments: 1 })}`;
    } else if (visible.length === 0) {
      body = `<div class="empty"><p>${proposal ? 'No mail has been received.' : 'Nothing here yet. Messages are kept from the point this screen started keeping them; anything received before that was processed but is not shown here.'}</p></div>`;
    } else {
      body = visible.map((item) => messageRow(item, { proposal, selected, dismissed, state })).join('\n');
    }
    const sort = deleted ? '' : proposal
      ? `<a class="btn btn--small sort-toggle" href="#" aria-label="Received order: newest first; activate for oldest first">${icon('arrow-up-down')}<span>Received</span></a>`
      : '<a class="btn btn--small sort-toggle" href="#" aria-label="Received order: newest first; activate for oldest first">Received ↓</a>';
    const pagination = total > 8
      ? `<div class="pagination"><span>Page 1 of 3</span><div class="button-row"><a class="btn btn--small" href="#">Next</a></div></div>`
      : '';
    const content = `${header}
<div id="mail-compose-host" class="mail-compose-backdrop" data-mail-compose-host hidden></div>
<section class="pane-layout ${selected ? 'pane-layout--3' : 'pane-layout--2'} inbox-layout" data-mail-preview-workspace aria-label="Emails">
  <aside class="pane"><div class="pane-body pane-scroll">${scopeList(pressed, state === 'empty')}</div></aside>
  <div class="pane">
    ${filterForm({ proposal, dismissed, folder: deleted ? 'Deleted' : 'Inbox', search })}
    <div class="pane-head inbox-messages-head"><div class="inbox-message-title"><h2>Messages</h2></div><span class="meta">${total} message${total === 1 ? '' : 's'}</span>${sort}</div>
    <div class="pane-body pane-scroll" tabindex="0">
${body}
    </div>
    ${pagination}
  </div>
  ${selected ? preview(rows.find((r) => r.id === selected), { proposal }) : ''}
</section>`;
    return content;
  },
};
