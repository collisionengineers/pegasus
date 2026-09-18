document.addEventListener('DOMContentLoaded', function () {
  var card = document.querySelector('[data-auth-card]');
  function updateCardClass() {
    if (!card) return;
    var page = window.MOCK.getState('acc-page');
    card.className = 'auth-card' + (page === 'authorize' ? ' auth-card--wide consent-card' : '');
  }
  document.querySelectorAll('[data-mock-input="acc-page"]').forEach(function (el) {
    el.addEventListener('change', updateCardClass);
  });
  updateCardClass();
});
