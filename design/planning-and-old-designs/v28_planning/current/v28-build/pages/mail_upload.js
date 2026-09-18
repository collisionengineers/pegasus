// mail_upload lane: Mail (Inbox/Message/Compose) + Upload/UploadStatus/UploadGroupStatus.
// Demo-only page script; concatenated after mock-engine.js by build.py.

(function () {
  'use strict';

  // The live pane-layout class (2 vs 3 columns) is chosen server-side by
  // whether a message is selected; the generic data-mock-show hide/show
  // cannot flip that class, so this lane's own script does it.
  function updatePreviewLayout() {
    var section = document.querySelector('[data-mail-preview-workspace]');
    if (!section) return;
    var open = window.MOCK.getState('mu-preview') !== 'closed';
    section.classList.toggle('pane-layout--3', open);
    section.classList.toggle('pane-layout--2', !open);
  }

  // The Inbox nav link and the Upload nav link both route to this one file
  // (src/Pegasus.Web/Pages/Shared/_Layout.cshtml gives Upload its own link
  // with a #upload hash); mirror that here since data-nav is fixed per file.
  function applyHashArea() {
    if (location.hash === '#upload') {
      window.MOCK.setState('mu-area', 'upload');
    } else if (location.hash === '#compose') {
      window.MOCK.setState('mu-area', 'mail');
      window.MOCK.setState('mu-mail-view', 'compose');
    }
  }

  document.addEventListener('DOMContentLoaded', function () {
    applyHashArea();
    updatePreviewLayout();
    document.querySelectorAll('[data-mock-input="mu-preview"]').forEach(function (el) {
      el.addEventListener('change', updatePreviewLayout);
    });

    // Compose is a dialog over the Inbox list on the live page
    // (data-mail-compose-open); here it is one more mu-mail-view value, so
    // the trigger just switches the state instead of opening a host panel.
    document.addEventListener('click', function (e) {
      var composeOpen = e.target.closest('[data-mail-compose-open]');
      if (composeOpen) {
        e.preventDefault();
        window.MOCK.setState('mu-mail-view', 'compose');
      }
      var composeClose = e.target.closest('[data-mail-compose-close]');
      if (composeClose) {
        e.preventDefault();
        window.MOCK.setState('mu-mail-view', 'list');
      }
      // Row/preview links to the full message (asp-page="/Mail/Message" live).
      var openMessage = e.target.closest('[data-mock-action="open-message"]');
      if (openMessage) {
        e.preventDefault();
        window.MOCK.setState('mu-mail-view', 'message');
      }
    });

    // "New category name"/"Why no existing category fits" only appear once
    // "Other received/sent classification" is chosen (site.js's own toggle;
    // not part of the shared mock engine, so this lane supplies it).
    document.addEventListener('change', function (e) {
      var select = e.target.closest('[data-other-toggle]');
      if (!select) return;
      var isOther = select.value === 'other-received' || select.value === 'other-sent';
      select.closest('form').querySelectorAll('[data-other-field]').forEach(function (field) {
        field.hidden = !isOther;
      });
    });

    // GET/POST forms transcribed from the live pages (filters, dismiss,
    // correspondence, link/unlink, discard, attach) are not wired to a
    // backend here; block navigation so the static file never reloads with a
    // half-formed query string, which would silently reset every mu-* state.
    document.addEventListener('submit', function (e) {
      var form = e.target;
      if (form.hasAttribute('data-mock-action') || form.closest('[data-mock-action]')) return; // engine already handles this
      if (form.matches('form')) { e.preventDefault(); }
    });
  });
})();
