// Administration lane: tab/section switching for the "ad-area" state (the
// 13 areas, matching Shared/_AdminNav.cshtml's group order) and the Logs
// page's "adLogsTab" (Action logs / Intake log). Both are plain
// window.MOCK.setState calls; visibility itself is handled generically by
// mock-engine's data-mock-show. This file only keeps aria-current in sync on
// the rail-style admin-nav (site.js does the same on the live _AdminNav) and
// shows a toast for every state-changing form, since nothing in this static
// capture is wired to a backend.

(function () {
  'use strict';

  // Fallback defaults for the sub-area toggles that only have a "set" hook
  // (data-ad-set on Edit/Cancel/tab controls in the body) and no matching
  // [data-mock-input] control in administration.strip.html: Accounts'
  // Glass's-login demo state, Contacts' list/edit panel and its own
  // viewing/editing toggle, Configuration's viewing/editing toggle,
  // Valuation presets' inline row edit toggle, the intake log drawer, and
  // whether Automation & AI is composed. Without an explicit default each
  // reads undefined and every data-mock-show branch for that key stays
  // hidden, which would blank out the Contacts and Automation & AI sections
  // on load. mock-engine's own readControls()/applyQueryOverrides() already
  // populated state for any key a strip control DOES carry, so this only
  // fills genuine gaps and never overrides a real control's value.
  var adDefaults = {
    'adGlass': 'notset',
    'adGlassEditing': '0',
    'adContactsView': 'list',
    'adContactEdit': '0',
    'adConfigEdit': '0',
    'adPresetEdit': '0',
    'adLogsDrawer': 'closed',
    'adAutomationComposed': '1'
  };
  Object.keys(adDefaults).forEach(function (key) {
    if (window.MOCK.state[key] === undefined) { window.MOCK.state[key] = adDefaults[key]; }
  });

  function applyAdAreaCurrent() {
    var area = window.MOCK.getState('ad-area') || 'hub';
    document.querySelectorAll('[data-ad-area-link]').forEach(function (el) {
      if (el.getAttribute('data-ad-area-link') === area) {
        el.setAttribute('aria-current', 'page');
      } else {
        el.removeAttribute('aria-current');
      }
    });
  }

  function applyLogsTabCurrent() {
    var tab = window.MOCK.getState('adLogsTab') || 'action';
    document.querySelectorAll('[data-ad-logs-tab]').forEach(function (el) {
      if (el.getAttribute('data-ad-logs-tab') === tab) {
        el.setAttribute('aria-current', 'page');
      } else {
        el.removeAttribute('aria-current');
      }
    });
  }

  // Generic "set one state key on click" hook: any element carrying
  // data-ad-set="key:value" (the admin-nav links, the hub's admin-cards, the
  // Logs tabs) sets that key instead of navigating.
  document.addEventListener('click', function (e) {
    var setter = e.target.closest('[data-ad-set]');
    if (!setter) { return; }
    e.preventDefault();
    var pair = setter.getAttribute('data-ad-set').split(':');
    window.MOCK.setState(pair[0], pair[1]);
    applyAdAreaCurrent();
    applyLogsTabCurrent();
  });

  // Every state-changing form in this lane carries data-mock-action="demo"
  // (mock-engine already prevents the actual submit); this shows the
  // required "Demo" toast rather than letting the dialog silently look like
  // it succeeded. Filter/search forms use data-mock-action="demo-filter"
  // instead and stay silent — they are inert controls, not mutations.
  document.addEventListener('submit', function (e) {
    var form = e.target.closest('[data-mock-action="demo"]');
    if (form) {
      window.MOCK.showToast(form.getAttribute('data-demo-message') || 'Demo: not wired in this offline capture');
    }
  });
  document.addEventListener('click', function (e) {
    var button = e.target.closest('button[data-mock-action="demo"]');
    if (button && !button.closest('form')) {
      window.MOCK.showToast(button.getAttribute('data-demo-message') || 'Demo: not wired in this offline capture');
    }
  });

  // Mirrors wwwroot/js/accounts.js's role→sign-off eligibility toggle inside
  // the account settings dialog: Administrator/Engineer can hold sign-off
  // fields, User cannot. The live page's lease/heartbeat/take-over
  // concurrency is not modelled here (see administration/accounts Notes).
  document.addEventListener('change', function (e) {
    if (!e.target.matches('[data-account-role]')) { return; }
    var allowed = e.target.value === 'Administrator' || e.target.value === 'Engineer';
    var dialog = e.target.closest('.dialog');
    if (!dialog) { return; }
    dialog.querySelectorAll('[data-account-signoff-field] input, [data-account-signoff-field] select, [data-account-signoff]').forEach(function (el) {
      el.disabled = !allowed;
    });
  });

  document.addEventListener('DOMContentLoaded', function () {
    applyAdAreaCurrent();
    applyLogsTabCurrent();
  });
})();
