document.addEventListener('DOMContentLoaded', function () {
  document.addEventListener('click', function (e) {
    if (e.target.closest('[data-mock-action="ii-open-detail"]')) {
      window.MOCK.setState('ii-view', 'detail');
      return;
    }
    if (e.target.closest('[data-mock-action="ii-back-to-list"]')) {
      window.MOCK.setState('ii-view', 'list');
      return;
    }
    var row = e.target.closest('[data-mock-action="ii-select-row"]');
    if (row) {
      document.querySelectorAll('[data-mock-action="ii-select-row"]').forEach(function (r) {
        var tr = r.closest('tr');
        if (tr) tr.setAttribute('aria-selected', 'false');
      });
      var current = row.closest('tr');
      if (current) current.setAttribute('aria-selected', 'true');
      return;
    }
  });
});
