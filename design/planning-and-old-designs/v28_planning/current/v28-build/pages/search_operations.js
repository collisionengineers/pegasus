document.addEventListener('DOMContentLoaded', function () {
  if (location.hash === '#operations') { window.MOCK.setState('so-area', 'operations'); }
  document.addEventListener('click', function (e) {
    var row = e.target.closest('[data-mock-action="select-row"]');
    if (row) {
      document.querySelectorAll('[data-mock-action="select-row"]').forEach(function (r) { r.setAttribute('aria-selected', 'false'); });
      row.setAttribute('aria-selected', 'true');
      window.MOCK.showToast('Demo: swaps the Selected Case preview for this row');
    }
  });
});
