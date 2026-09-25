// Sign in and the rest of the navless family, read from
// Pages/Account/SignIn.cshtml, PasswordChange.cshtml (forced) and
// AccessDenied.cshtml. Baseline is the live markup; the proposal layer adds
// the brand-row, title and show-password switches (items H to K).
import { authFrame, esc, icon } from './shared.mjs';

const field = ({ id, label, type = 'text', value = '', autocomplete, autofocus = false, reveal = false }) => {
  const input = `<input id="${id}" name="${id}" type="${type}" value="${esc(value)}"${autocomplete ? ` autocomplete="${autocomplete}"` : ''}${autofocus ? ' autofocus' : ''} />`;
  const control = reveal
    ? `<div class="p30-pw">${input}<button type="button" class="icon-button p30-reveal" data-reveal aria-controls="${id}" aria-pressed="false" aria-label="Show password">${icon('eye')}</button></div>`
    : input;
  return `<div class="field"><label for="${id}">${label}</label>${control}<span class="field-error" data-valmsg-for="${id}"></span></div>`;
};

export const signin = {
  key: 'signin',
  file: 'pegasus_signin_v30.html',
  title: 'Sign in',
  frame: 'auth',
  route: null,
  states: [
    { id: 'default', label: 'Sign in', expect: ['form input[type="password"]'] },
    { id: 'error', label: 'Sign in · invalid credentials', expect: ['.validation-summary li'] },
    { id: 'signed-out', label: 'Signed out', expect: ['h1.success-text'] },
    { id: 'forced-password', label: 'Set a new password (forced)', expect: ['#ConfirmPassword'] },
    { id: 'access-denied', label: 'Access denied', expect: ['[role="alert"] h1'] },
  ],
  layers: ['baseline', 'proposal'],
  opts: [
    { key: 'brand', label: 'Brand row', values: ['current', 'compact'], item: 'H' },
    { key: 'title', label: 'Title', values: ['current', 'short'], item: 'I' },
    { key: 'reveal', label: 'Show password', values: ['off', 'on'], item: 'J' },
  ],
  render(state, layer, assets) {
    const proposal = layer === 'proposal';
    const brand = proposal
      ? `<div class="auth-brand p30-brand"><img src="${assets.mark256}" alt="" /><span class="p30-brand-copy"><strong>PEGASUS</strong><span class="p30-brand-sub">Case management</span></span></div>`
      : undefined;
    const title = proposal
      ? '<h1><span class="p30-title-long">Sign in to Pegasus</span><span class="p30-title-short">Sign in</span></h1>'
      : '<h1>Sign in to Pegasus</h1>';
    const signInForm = (userName) => `<form method="post" onsubmit="return false">
  <input name="ReturnUrl" type="hidden" value="/" />
  ${field({ id: 'UserName', label: 'Username', value: userName, autocomplete: 'username', autofocus: true })}
  ${field({ id: 'Password', label: 'Password', type: 'password', autocomplete: 'current-password', reveal: proposal })}
  <button type="submit" class="btn btn--primary">Sign in</button>
</form>`;
    let inner;
    switch (state) {
      case 'error':
        inner = `${title}
<div class="validation-summary mt-1 validation-summary-errors" role="alert" data-valmsg-summary="true"><ul><li>The username or password is incorrect. If your access has changed, contact an administrator.</li></ul></div>
${signInForm('alex')}`;
        break;
      case 'signed-out':
        inner = `<h1 class="cluster success-text">${icon('check')}<span>You are signed out</span></h1>
<div class="validation-summary mt-1 validation-summary-valid" role="alert" data-valmsg-summary="true"><ul><li style="display:none"></li></ul></div>
${signInForm('')}`;
        break;
      case 'forced-password':
        inner = `<h1>Set a new password before continuing</h1>
${proposal ? '' : '<p>You cannot use Pegasus until the password issued to you is replaced. Choose a new password and confirm it.</p>'}
<section><div>
  <div class="validation-summary mb-2 validation-summary-valid" role="alert"><ul><li style="display:none"></li></ul></div>
  <form method="post" class="stack" onsubmit="return false">
    <input name="OperationKey" type="hidden" value="" />
    ${field({ id: 'NewPassword', label: 'New password', type: 'password', autocomplete: 'new-password', reveal: proposal })}
    ${field({ id: 'ConfirmPassword', label: 'Confirm new password', type: 'password', autocomplete: 'new-password' })}
    <div class="button-row"><button type="submit" class="btn btn--primary">Change password</button></div>
  </form>
</div></section>`;
        break;
      case 'access-denied':
        inner = `<div role="alert">
  <p class="eyebrow">Administration</p>
  <h1>Access denied</h1>
  <p>Administration is available to Administrators only.</p>
  <div class="button-row"><a class="btn btn--primary" href="#">Return to Work Centre</a></div>
</div>`;
        break;
      default:
        inner = `${title}
<div class="validation-summary mt-1 validation-summary-valid" role="alert" data-valmsg-summary="true"><ul><li style="display:none"></li></ul></div>
${signInForm('alex')}`;
    }
    return authFrame({ inner, assets, brand });
  },
};
