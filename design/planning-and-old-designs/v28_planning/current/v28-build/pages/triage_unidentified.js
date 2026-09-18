document.addEventListener('DOMContentLoaded', function () {
  if (location.hash === '#unidentified') { window.MOCK.setState('tu-area', 'unidentified'); }
  document.addEventListener('click', function (e) {
    var setArea = e.target.closest('[data-mock-action="tu-set-area"]');
    if (setArea) {
      window.MOCK.setState('tu-area', setArea.getAttribute('data-tu-area'));
      return;
    }
    if (e.target.closest('[data-mock-action="tu-goto-awaiting"]')) {
      window.MOCK.showToast('Demo: Awaiting instruction is the image_intake lane');
      return;
    }
    if (e.target.closest('[data-mock-action="tu-open-triage-detail"]')) {
      window.MOCK.setState('tu-triage-view', 'detail');
      return;
    }
    if (e.target.closest('[data-mock-action="tu-back-triage-list"]')) {
      window.MOCK.setState('tu-triage-view', 'list');
      return;
    }
    if (e.target.closest('[data-mock-action="tu-open-unidentified-detail"]')) {
      window.MOCK.setState('tu-unident-view', 'detail');
      return;
    }
    if (e.target.closest('[data-mock-action="tu-back-unidentified-list"]')) {
      window.MOCK.setState('tu-unident-view', 'list');
      return;
    }
    var triageRow = e.target.closest('[data-mock-action="tu-select-triage-row"]');
    if (triageRow) {
      document.querySelectorAll('[data-mock-action="tu-select-triage-row"]').forEach(function (r) {
        var row = r.closest('tr');
        if (row) row.setAttribute('aria-selected', 'false');
      });
      var row2 = triageRow.closest('tr');
      if (row2) row2.setAttribute('aria-selected', 'true');
      return;
    }
    var unidentifiedRow = e.target.closest('[data-mock-action="tu-select-unidentified-row"]');
    if (unidentifiedRow) {
      document.querySelectorAll('[data-mock-action="tu-select-unidentified-row"]').forEach(function (r) {
        var row = r.closest('tr');
        if (row) row.setAttribute('aria-selected', 'false');
      });
      var row3 = unidentifiedRow.closest('tr');
      if (row3) row3.setAttribute('aria-selected', 'true');
      return;
    }
  });
});
