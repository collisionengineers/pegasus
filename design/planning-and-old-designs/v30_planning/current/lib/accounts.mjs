// Administration → Staff accounts (Pages/Administration/Accounts/Index.cshtml
// and Shared/_AdminNav.cshtml at origin/dev). Baseline is the live markup; the
// proposal layer carries items U to Z for the settings dialog.
import { chip, errorSummary, esc, icon, pageHeader } from './shared.mjs';

const accounts = [
  { id: 'alex', role: 'Administrator', signOff: 'Yes · default', glass: 'alex.ce', enabled: true, self: true, printedName: 'alex', qualifications: 'none', isSignOff: true, isDefault: true, signature: true },
  { id: 'sarah', role: 'Engineer', signOff: 'Yes · qualifications missing', glass: null, enabled: true, printedName: 'Sarah Whitfield', qualifications: '', isSignOff: true, isDefault: false, signature: true },
  { id: 'priya', role: 'Engineer', signOff: 'Yes', glass: 'priya.k', enabled: true, printedName: 'Priya Kaur', qualifications: 'BEng (Hons), MIMI', isSignOff: true, isDefault: false, signature: true },
  { id: 'tom', role: 'User', signOff: 'No', glass: null, enabled: false, mustChange: true, printedName: '', qualifications: '', isSignOff: false, isDefault: false, signature: false },
];

const adminNav = `<nav class="admin-nav panel" aria-label="Administration areas">
  <div class="admin-nav__group" role="group" aria-labelledby="admin-nav-people"><span class="admin-nav__label" id="admin-nav-people">People and access</span>
    <a href="#" aria-current="page">${icon('user')}<span>Staff accounts &amp; roles</span></a>
    <a href="#">${icon('shield')}<span>Contacts</span></a></div>
  <div class="admin-nav__group" role="group" aria-labelledby="admin-nav-configuration"><span class="admin-nav__label" id="admin-nav-configuration">Configuration</span>
    <a href="#">${icon('settings')}<span>Workflow configuration</span></a>
    <a href="#">${icon('mail')}<span>Mail settings</span></a>
    <a href="#">${icon('pound-sterling')}<span>Valuation presets</span></a></div>
  <div class="admin-nav__group" role="group" aria-labelledby="admin-nav-oversight"><span class="admin-nav__label" id="admin-nav-oversight">Operations and oversight</span>
    <a href="#">${icon('heart-pulse')}<span>Service health</span></a>
    <a href="#">${icon('scroll-text')}<span>Logs</span></a>
    <a href="#">${icon('bar-chart')}<span>Reports</span></a>
    <a href="#">${icon('flag')}<span>Release notes</span></a>
    <a href="#">${icon('alert-triangle')}<span>Problem reports</span></a>
    <a href="#">${icon('sparkles')}<span>AI jobs</span></a></div>
</nav>`;

const table = () => `<div class="table-wrap no-border"><table>
  <caption class="sr-only">Staff accounts</caption>
  <thead><tr><th scope="col">Username</th><th scope="col">Role</th><th scope="col">Sign-off</th><th scope="col">Glass's</th><th scope="col">State</th><th scope="col"><span class="sr-only">Settings</span></th></tr></thead>
  <tbody>${accounts.map((a) => `<tr>
    <td class="mono">${a.id}</td><td>${a.role}</td><td>${esc(a.signOff)}</td>
    <td data-account-glass="${a.id}"><span class="cluster"><span class="${a.glass ? 'mono' : 'muted'}">${a.glass ?? 'Not configured'}</span><a class="link-button" href="#">Manage login</a></span></td>
    <td>${chip(a.enabled ? 'Enabled' : 'Disabled')}${a.mustChange ? chip('Password change required', 'amber') : ''}</td>
    <td class="right"><span class="cluster"><a class="btn btn--small" href="?state=settings-${a.self ? 'self' : a.enabled ? 'other' : 'disabled'}">Settings</a></span></td>
  </tr>`).join('')}</tbody>
</table></div>`;

const createDialog = (open) => `<div class="dialog-backdrop" data-dialog="create-account-dialog"${open ? ' data-dialog-open-on-load="true"' : ' hidden'}>
  <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="create-account-title">
    <div class="dialog-head"><h2 id="create-account-title" tabindex="-1" data-dialog-initial-focus>Create staff account</h2><button type="button" class="dialog-close" data-dialog-close aria-label="Close">${icon('x')}</button></div>
    <form method="post" onsubmit="return false">
      <div class="dialog-body stack">
        <div class="field"><label class="req" for="create-username">Username</label><input id="create-username" name="userName" required maxlength="64" autocomplete="off" /></div>
        <div class="field"><label class="req" for="create-password">Temporary password</label><input id="create-password" name="temporaryPassword" type="password" required minlength="12" maxlength="128" autocomplete="new-password" /></div>
      </div>
      <div class="dialog-foot"><button type="button" class="btn" data-dialog-close>Cancel</button><button type="submit" class="btn btn--primary">Create staff account</button></div>
    </form>
  </section>
</div>`;

function settingsDialog(account, { proposal, error, deleteOpen }) {
  const id = `settings-${account.id}`;
  const roleOptions = ['Administrator', 'Engineer', 'User'].map((r) => `<option value="${r}"${r === account.role ? ' selected' : ''}>${r}</option>`).join('');
  const roleControl = account.self
    ? (proposal
      ? `<div class="p30-value" id="${id}-role" aria-readonly="true">${account.role}</div><input type="hidden" name="role" value="${account.role}" />`
      : `<select id="${id}-role" name="role" data-account-role disabled>${roleOptions}</select><input type="hidden" name="role" value="${account.role}" />`)
    : `<select id="${id}-role" name="role" data-account-role>${roleOptions}</select>`;
  const signOffField = (inner, extra = '') => `<div class="field${extra}" data-account-signoff-field${account.isSignOff ? '' : ' hidden'}>${inner}</div>`;
  const fields = `<div class="account-signoff-fields${proposal ? ' p30-fields' : ''}">
  <div class="field"><label for="${id}-role">Role</label>${roleControl}</div>
  <div class="field"><label for="${id}-signoff">Sign-off Engineer</label><select id="${id}-signoff" name="isSignOffEngineer" data-account-signoff><option value="false"${account.isSignOff ? '' : ' selected'}>No</option><option value="true"${account.isSignOff ? ' selected' : ''}>Yes</option></select></div>
  ${signOffField(`<label for="${id}-name">Printed name</label><input id="${id}-name" name="printedName" value="${esc(error ? '' : account.printedName)}" maxlength="120" />`)}
  ${signOffField(`<label for="${id}-qualifications">Qualifications</label><input id="${id}-qualifications" name="qualifications" value="${esc(account.qualifications)}" maxlength="240" />`)}
</div>`;
  const glassLine = `<section class="account-settings-section" aria-labelledby="${id}-glass-title"><div class="settings-line"><div><strong id="${id}-glass-title">Glass's repair estimates</strong><span class="meta">Manage this account's login.</span></div><a class="btn" href="#" data-account-glass-login>Manage login</a></div></section>`;
  const signature = proposal
    ? `<section class="account-settings-section" aria-labelledby="${id}-signature-title" data-account-signoff-field${account.isSignOff ? '' : ' hidden'}><div class="settings-line"><div><strong id="${id}-signature-title">Signature image</strong><span class="meta" data-signature-state>${account.signature ? 'On file' : 'Not on file'}</span></div><label class="btn" for="${id}-signature">${account.signature ? 'Replace signature' : 'Upload signature'}</label><input id="${id}-signature" name="signature" type="file" accept="image/png" class="sr-only" /></div></section>`
    : signOffField(`<label for="${id}-signature">${account.signature ? 'Replace signature' : 'Signature image'}</label><input id="${id}-signature" name="signature" type="file" accept="image/png" />`);
  const defaultChoice = `<label class="choice" for="${id}-default" data-account-signoff-field${account.isSignOff ? '' : ' hidden'}><input id="${id}-default" name="isDefaultSignOffEngineer" type="checkbox" value="true"${account.isDefault ? ' checked' : ''} data-account-default /><span>Default sign-off Engineer</span></label>`;
  const adverse = account.enabled ? '<button type="submit" class="btn btn--danger">Disable</button>' : '<button type="submit" class="btn">Enable</button>';
  const routine = account.enabled ? '<form method="post" onsubmit="return false"><button type="submit" class="btn">Force logout</button></form><form method="post" onsubmit="return false"><button type="submit" class="btn">Reset password</button></form>' : '';
  const deleteButton = `<button type="button" class="btn btn--danger" data-native-dialog-open="${id}-delete">Delete</button>`;
  const head = proposal
    ? `<div class="dialog-head"><div class="cluster p30-dialog-title"><h2 id="${id}-title" tabindex="-1" data-dialog-initial-focus>${account.id}</h2>${chip(account.enabled ? 'Enabled' : 'Disabled')}${account.mustChange ? chip('Password change required', 'amber') : ''}</div><button type="button" class="dialog-close" data-dialog-close data-account-settings-cancel aria-label="Close">${icon('x')}</button></div>`
    : `<div class="dialog-head"><h2 id="${id}-title" tabindex="-1" data-dialog-initial-focus>${account.id}</h2><button type="button" class="dialog-close" data-dialog-close data-account-settings-cancel aria-label="Close">${icon('x')}</button></div>`;
  const facts = proposal ? '' : `<dl class="fact-grid account-settings-facts"><div class="fact"><dt>Username</dt><dd class="mono">${account.id}</dd></div><div class="fact"><dt>State</dt><dd>${account.enabled ? 'Enabled' : 'Disabled'}</dd></div></dl>`;
  const body = `<div class="dialog-body stack">
  ${error ? errorSummary(['Enter the printed name for the Sign-off Engineer.']) : ''}
  ${facts}
  ${fields}
  ${proposal ? `<div class="p30-settings-list">${glassLine}${signature}</div>` : `${glassLine}${signature}`}
  ${defaultChoice}
</div>`;
  const foot = proposal
    ? `<div class="dialog-foot p30-foot">${account.self ? '' : `<div class="left"><form method="post" onsubmit="return false">${adverse}</form>${routine}<span class="p30-foot-gap" aria-hidden="true"></span>${deleteButton}</div>`}<button type="button" class="btn" data-dialog-close data-account-settings-cancel>Cancel</button><button type="submit" class="btn btn--primary">Save settings</button></div>`
    : `<div class="dialog-foot"><button type="button" class="btn" data-dialog-close data-account-settings-cancel>Cancel</button><button type="submit" class="btn btn--primary">Save settings</button></div>`;
  const secondFoot = !proposal && !account.self
    ? `<div class="dialog-foot"><div class="left"><form method="post" onsubmit="return false">${adverse}</form>${deleteButton}</div>${routine}</div>`
    : '';
  const deleteDialog = account.self ? '' : `<dialog id="${id}-delete" aria-labelledby="${id}-delete-title"${deleteOpen ? ' data-native-open' : ''}>
  <form method="dialog" class="stack"><h2 id="${id}-delete-title" tabindex="-1">Delete ${account.id}</h2><div class="button-row"><button class="btn" type="submit" value="cancel">Cancel</button><button type="submit" class="btn btn--danger" value="delete">Delete</button></div></form>
</dialog>`;
  return `<div class="dialog-backdrop" data-dialog="${id}" data-dialog-open-on-load="true">
  <section class="dialog dialog--wide account-settings-dialog" role="dialog" aria-modal="true" aria-labelledby="${id}-title">
    ${head}
    <form method="post" enctype="multipart/form-data" data-account-settings onsubmit="return false">
      ${body}
      ${foot}
    </form>
    ${secondFoot}
  </section>
</div>
${deleteDialog}`;
}

export const accountsSurface = {
  key: 'accounts',
  file: 'pegasus_admin_accounts_v30.html',
  title: 'Administration · Staff accounts',
  frame: 'shell',
  route: 'administration',
  states: [
    { id: 'list', label: 'Staff accounts list', expect: ['.admin-nav a[aria-current="page"]', 'table tbody tr'], forbid: ['.account-settings-dialog'] },
    { id: 'settings-self', label: 'Settings · own account (alex)', expect: ['[data-dialog="settings-alex"]:not([hidden]) .account-settings-dialog'] },
    { id: 'settings-other', label: 'Settings · another enabled account', expect: ['[data-dialog="settings-sarah"]:not([hidden])', '.dialog-foot .left'] },
    { id: 'settings-disabled', label: 'Settings · a disabled account', expect: ['[data-dialog="settings-tom"]:not([hidden])'] },
    { id: 'error', label: 'Settings · validation error', expect: ['.account-settings-dialog .validation-summary'] },
    { id: 'create', label: 'Create staff account', expect: ['[data-dialog="create-account-dialog"]:not([hidden])'] },
    { id: 'temporary-password', label: 'Temporary password revealed', expect: ['output.mono'] },
    { id: 'delete', label: 'Delete confirmation', expect: ['dialog[open]'] },
  ],
  layers: ['baseline', 'proposal'],
  opts: [],
  render(state, layer) {
    const proposal = layer === 'proposal';
    const dialogFor = { 'settings-self': 'alex', 'settings-other': 'sarah', 'settings-disabled': 'tom', error: 'sarah', delete: 'sarah' }[state];
    const account = dialogFor ? accounts.find((a) => a.id === dialogFor) : null;
    const temporary = state === 'temporary-password';
    const password = 'Vr7!kq2LmP9s';
    const temporaryBaseline = temporary && !proposal
      ? `<section class="panel" aria-labelledby="temporary-password-title"><div class="panel-head"><h2 id="temporary-password-title">Temporary password</h2></div><div class="panel-body stack"><p>Give this to the account holder. It is shown only for this response and must be changed at first sign-in.</p><output class="mono">${password}</output></div></section>`
      : '';
    const temporaryProposal = temporary && proposal
      ? `<div class="notice notice--success" role="status" data-temporary-password>${icon('check')}<span><strong>Temporary password for sarah</strong> · <output class="mono">${password}</output><br />Give this to the account holder. It is shown only for this response and must be changed at first sign-in.</span><button type="button" class="dismiss" data-dismiss aria-label="Dismiss">${icon('x')}</button></div>`
      : '';
    return `${pageHeader({ eyebrow: 'Administration', title: 'Staff accounts &amp; roles' })}
<div class="admin-layout">
  ${adminNav}
  <div class="stack">
    ${temporaryProposal}
    <section class="panel" aria-labelledby="staff-accounts-title">
      <div class="panel-head"><div><h2 id="staff-accounts-title">Staff accounts</h2><p class="panel-title-meta">4 accounts</p></div><button type="button" class="btn btn--primary" data-dialog-open="create-account-dialog">${icon('plus')}<span>Create staff account</span></button></div>
      ${table()}
    </section>
  </div>
</div>
${temporaryBaseline}
${createDialog(state === 'create')}
${account ? settingsDialog(account, { proposal, error: state === 'error', deleteOpen: state === 'delete' }) : ''}`;
  },
};
