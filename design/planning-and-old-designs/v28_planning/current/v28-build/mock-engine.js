// v28 baseline — shared mock engine. Demo-only: drives the "Mockup controls"
// strip, dialogs and per-route state switches. Not shipped, not live site.js.
// Lane pages register their own state keys/values via data-mock-show and add
// page JS after this block (build.py concatenates PAGE_JS below this file).
(function () {
  'use strict';
  var state = {};
  var defaults = {};

  function qs() { return new URLSearchParams(location.search); }

  function readControls() {
    document.querySelectorAll('[data-mock-input]').forEach(function (el) {
      var key = el.getAttribute('data-mock-input');
      var value = el.type === 'checkbox' ? (el.checked ? '1' : '0') : el.value;
      defaults[key] = value;
    });
  }

  function applyQueryOverrides() {
    var params = qs();
    document.querySelectorAll('[data-mock-input]').forEach(function (el) {
      var key = el.getAttribute('data-mock-input');
      if (params.has(key)) {
        var v = params.get(key);
        if (el.type === 'checkbox') { el.checked = v === '1' || v === 'true'; }
        else { el.value = v; }
      }
    });
    if (params.has('dialog')) { state._openOnLoad = params.get('dialog'); }
    if (params.has('stripCollapsed')) { document.querySelector('[data-mockup-strip]').classList.add('is-collapsed'); }
  }

  function collectState() {
    document.querySelectorAll('[data-mock-input]').forEach(function (el) {
      var key = el.getAttribute('data-mock-input');
      state[key] = el.type === 'checkbox' ? (el.checked ? '1' : '0') : el.value;
    });
  }

  function render() {
    // Role gates admin-only nav/controls.
    var isAdmin = state.role === 'Administrator';
    document.querySelectorAll('[data-admin-only]').forEach(function (el) { el.hidden = !isAdmin; });
    document.querySelector('[data-shell-role]') && (document.querySelector('[data-shell-role]').textContent = state.role || 'Administrator');
    var nameByRole = { Administrator: 'Sam Whitlock', Engineer: 'Priya Anand', User: 'Priya Anand' };
    var initialsByRole = { Administrator: 'SW', Engineer: 'PA', User: 'PA' };
    document.querySelectorAll('[data-shell-username]').forEach(function (el) { el.textContent = nameByRole[state.role] || 'Sam Whitlock'; });
    document.querySelectorAll('[data-shell-initials]').forEach(function (el) { el.textContent = initialsByRole[state.role] || 'SW'; });

    // Freshness drives the shell health text/dot and any page freshness banner.
    var freshLabel = { current: 'Current', partial: 'Partial', stale: 'Stale', unavailable: 'Unavailable' }[state.fresh] || 'Current';
    var clock = '09:41';
    document.querySelectorAll('[data-shell-health-text]').forEach(function (el) { el.textContent = freshLabel + ' · ' + clock; });
    document.querySelectorAll('[data-shell-health-dot]').forEach(function (el) {
      el.classList.remove('partial', 'failed');
      if (state.fresh === 'partial' || state.fresh === 'stale') el.classList.add('partial');
      if (state.fresh === 'unavailable') el.classList.add('failed');
    });

    // Page-level Unavailable notice (docs/design/README "genuine runtime
    // failure renders the designed failure state").
    var notice = document.querySelector('[data-shell-unavailable-notice]');
    if (notice) notice.hidden = state.pageunavailable !== '1';

    // Rail collapse.
    var shell = document.querySelector('[data-app-shell]');
    if (shell) shell.classList.toggle('rail-collapsed', state.railcollapsed === '1');
    var railBtn = document.querySelector('[data-rail-toggle]');
    if (railBtn) railBtn.setAttribute('aria-expanded', state.railcollapsed === '1' ? 'false' : 'true');

    // Generic state-driven visibility: data-mock-show="key:value,key2:value2" (AND).
    document.querySelectorAll('[data-mock-show]').forEach(function (el) {
      var pairs = el.getAttribute('data-mock-show').split(',');
      var visible = pairs.every(function (pair) {
        var parts = pair.split(':');
        var key = parts[0].trim();
        var wanted = parts[1] ? parts[1].trim().split('|') : [];
        return wanted.indexOf(state[key]) !== -1;
      });
      el.hidden = !visible;
    });

    document.title = document.title.replace(/ \(state: .*\)$/, '') ;
  }

  window.MOCK = {
    state: state,
    setState: function (key, value) {
      state[key] = value;
      var el = document.querySelector('[data-mock-input="' + key + '"]');
      if (el) { if (el.type === 'checkbox') el.checked = value === '1' || value === 'true'; else el.value = value; }
      render();
    },
    getState: function (key) { return state[key]; },
    render: render,
    showToast: function (message) {
      var region = document.querySelector('[data-toast-region]');
      if (!region) return;
      var toast = document.createElement('div');
      toast.className = 'toast';
      toast.textContent = message;
      region.appendChild(toast);
      setTimeout(function () { toast.remove(); }, 4000);
    }
  };

  // ---- Dialogs (mirrors site.js data-dialog-open/close conventions) ----
  function openDialog(id) {
    var d = document.querySelector('.dialog-backdrop[data-dialog="' + id + '"]');
    if (!d) return;
    d.hidden = false;
    var focusable = d.querySelector('input, select, textarea, button, [tabindex]');
    if (focusable) focusable.focus();
  }
  function closeDialog(el) {
    var d = el.closest('.dialog-backdrop');
    if (d) d.hidden = true;
  }
  document.addEventListener('click', function (e) {
    var openTrigger = e.target.closest('[data-dialog-open]');
    if (openTrigger) { openDialog(openTrigger.getAttribute('data-dialog-open')); return; }
    var stripTrigger = e.target.closest('[data-mock-action="open-dialog"]');
    if (stripTrigger) { openDialog(stripTrigger.getAttribute('data-dialog-target')); return; }
    var closeTrigger = e.target.closest('[data-dialog-close]');
    if (closeTrigger) { closeDialog(closeTrigger); return; }
    if (e.target.classList && e.target.classList.contains('dialog-backdrop')) { e.target.hidden = true; return; }
    var action = e.target.closest('[data-mock-action]');
    if (action) {
      var name = action.getAttribute('data-mock-action');
      switch (name) {
        case 'strip-toggle':
          document.querySelector('[data-mockup-strip]').classList.toggle('is-collapsed');
          break;
        case 'reset-strip':
          location.search = '';
          break;
        case 'go-work-centre':
          location.href = 'pegasus_work_centre_v28.html';
          break;
        case 'sign-out':
          window.MOCK.showToast('Demo: sign-out is not wired in the mockup');
          closeDialog(action);
          break;
        case 'mark-all-read':
          document.querySelectorAll('[data-notification-list] .row-button--unread').forEach(function (b) { b.classList.remove('row-button--unread'); b.querySelector('.status--navy') && b.querySelector('.status--navy').remove(); });
          break;
        case 'notification-open':
          e.preventDefault();
          window.MOCK.showToast('Demo: opens the notification’s place');
          break;
        default:
          if (e.target.closest('form[data-mock-action]')) { e.preventDefault(); }
          break;
      }
    }
  });
  document.addEventListener('submit', function (e) {
    if (e.target.closest('[data-mock-action]') || e.target.hasAttribute('data-mock-action')) { e.preventDefault(); }
  });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
      document.querySelectorAll('.dialog-backdrop').forEach(function (d) { if (!d.hidden) d.hidden = true; });
    }
  });

  function markCurrentNav() {
    var nav = document.body.getAttribute('data-nav');
    document.querySelectorAll('[data-nav-link]').forEach(function (el) {
      if (el.getAttribute('data-nav-link') === nav) el.setAttribute('aria-current', 'page');
      else el.removeAttribute('aria-current');
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    markCurrentNav();
    readControls();
    applyQueryOverrides();
    collectState();
    document.querySelectorAll('[data-mock-input]').forEach(function (el) {
      el.addEventListener('change', function () { collectState(); render(); });
    });
    render();
    if (state._openOnLoad) openDialog(state._openOnLoad);
  });
})();
