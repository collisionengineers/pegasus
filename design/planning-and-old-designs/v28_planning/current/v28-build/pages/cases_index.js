// Cases index (v28 baseline). The workflow rail's nine scopes are static
// buttons here (no navigation postback in the live app to reuse for an
// offline file), so a click sets the shared mock state instead of submitting
// the rail's <form method="get">. The shell's "New case" link and the
// utility-bar button both point at #create, which this page reveals as the
// manual Create Case flow (see cases_index.strip.html "View").
(function () {
  function setActiveTab(value) {
    document.querySelectorAll('[data-ci-tab]').forEach(function (button) {
      button.setAttribute('aria-pressed', button.getAttribute('data-ci-tab') === value ? 'true' : 'false');
    });
  }

  document.addEventListener('click', function (event) {
    var tabButton = event.target.closest('[data-ci-tab]');
    if (tabButton) {
      var value = tabButton.getAttribute('data-ci-tab');
      window.MOCK.setState('citab', value);
      setActiveTab(value);
      return;
    }

    var viewLink = event.target.closest('[data-ci-view]');
    if (viewLink) {
      event.preventDefault();
      window.MOCK.setState('ciview', viewLink.getAttribute('data-ci-view'));
      history.replaceState(null, '', location.pathname);
    }
  });

  document.addEventListener('DOMContentLoaded', function () {
    if (location.hash === '#create') {
      window.MOCK.setState('ciview', 'create');
    }
    setActiveTab(window.MOCK.getState('citab') || 'not_ready');
  });
})();
