// v28 mockup shim. The only non-live script in a captured state page.
//
// A captured page is the application's own HTML, CSS and JS with no server
// behind it. This shim (1) keeps the live scripts from talking to a server that
// is not there, (2) turns navigation to a live route into navigation to the
// captured state for that route, and (3) says so plainly when a route or a post
// was not captured, instead of pretending. It changes nothing on the page.
(function () {
  'use strict';
  var routes = window.V28_ROUTES || { exact: {}, shaped: {}, transitions: {} };
  var state = window.V28_STATE || {};
  var GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/g;

  // (1) No server: background requests never settle, so nothing reports a
  // false failure, and beacons are accepted and dropped.
  window.fetch = function () { return new Promise(function () {}); };
  if (navigator.sendBeacon) {
    try { navigator.sendBeacon = function () { return true; }; } catch (e) { /* read-only in some browsers */ }
  }

  function keyOf(pathAndQuery) {
    var url = new URL(pathAndQuery, 'https://x');
    var params = [];
    url.searchParams.forEach(function (v, k) { params.push([k.toLowerCase(), v.toLowerCase()]); });
    params.sort(function (a, b) { return a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0; });
    var query = params.map(function (p) { return p[0] + '=' + p[1]; }).join('&');
    return url.pathname.replace(/\/+$/, '').toLowerCase() + (query ? '?' + query : '');
  }

  function resolve(live) {
    if (!live || live.charAt(0) !== '/') return null;
    var key = keyOf(live);
    if (routes.exact[key]) return routes.exact[key];
    var pathOnly = key.split('?')[0];
    if (routes.exact[pathOnly]) return routes.exact[pathOnly];
    var shape = pathOnly.replace(GUID, '{id}');
    return routes.shaped[shape] || null;
  }

  function say(text) {
    var region = document.querySelector('[data-toast-region]');
    var note = document.createElement('div');
    note.setAttribute('role', 'status');
    note.style.cssText = 'position:fixed;left:50%;bottom:16px;transform:translateX(-50%);z-index:99999;'
      + 'background:#1b1e20;color:#fff;font:12px/1.4 system-ui,sans-serif;padding:8px 12px;border-radius:4px;'
      + 'box-shadow:0 6px 18px rgba(0,0,0,.35);max-width:70vw';
    note.textContent = 'Mockup: ' + text;
    (region && region.parentNode ? region.parentNode : document.body).appendChild(note);
    setTimeout(function () { note.remove(); }, 3500);
  }

  function go(id) {
    var params = new URLSearchParams(window.location.search);
    var keep = params.get('embed') ? '?embed=1' : '';
    if (window.parent !== window) {
      try { window.parent.postMessage({ v28: 'state', id: id }, '*'); } catch (e) { /* standalone */ }
    }
    window.location.href = id + '.html' + keep;
  }

  function follow(live, how) {
    var id = resolve(live);
    if (id) { go(id); return; }
    say((how || 'This goes to') + ' ' + live.split('?')[0] + ', which this baseline did not capture.');
  }

  // (2) Links. Registered on window in the bubble phase so every live handler
  // has already run: a link the application handles in place (section nav,
  // viewers, dialogs) is left alone.
  window.addEventListener('click', function (event) {
    if (event.defaultPrevented || event.button !== 0) return;
    var link = event.target.closest && event.target.closest('a[href]');
    if (!link) return;
    var href = link.getAttribute('href');
    if (!href || href.charAt(0) !== '/') return;
    event.preventDefault();
    if (link.hasAttribute('download') || /\/Download(\?|$)|^\/Received\/[^/]+\/(Source|Asset|Image)/i.test(href)) {
      say('This opens or downloads a file from the server. Files are not part of this capture.');
      return;
    }
    follow(href);
  }, false);

  // Rows and controls whose live script assigns window.location from an attribute.
  function retarget(root) {
    ['data-select-url', 'data-href', 'data-url', 'data-row-href'].forEach(function (name) {
      root.querySelectorAll('[' + name + ']').forEach(function (element) {
        var live = element.getAttribute(name);
        if (!live || live.charAt(0) !== '/') return;
        var id = resolve(live);
        element.setAttribute('data-v28-live', live);
        element.setAttribute(name, id ? id + '.html' + (window.location.search.indexOf('embed') >= 0 ? '?embed=1' : '') : '#');
      });
    });
  }

  // (3) Forms. A GET form is navigation; a post is a server action.
  document.addEventListener('submit', function (event) {
    var form = event.target;
    if (!(form instanceof HTMLFormElement)) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    var submitter = event.submitter;
    var action = (submitter && submitter.getAttribute('formaction')) || form.getAttribute('action') || state.live || '/';
    var method = ((submitter && submitter.getAttribute('formmethod')) || form.getAttribute('method') || 'get').toLowerCase();
    if (method === 'get') {
      var data = new FormData(form);
      if (submitter && submitter.name) data.append(submitter.name, submitter.value);
      var url = new URL(action, 'https://x');
      data.forEach(function (value, name) { if (typeof value === 'string' && value !== '') url.searchParams.set(name, value); });
      follow(url.pathname + url.search, 'This searches');
      return;
    }
    var moves = (routes.transitions && routes.transitions[state.id]) || [];
    var target = action + ' ' + (submitter ? (submitter.name + '=' + submitter.value + ' ' + submitter.textContent) : '');
    for (var i = 0; i < moves.length; i++) {
      if (target.indexOf(moves[i].match) >= 0) { go(moves[i].to); return; }
    }
    var label = submitter ? submitter.textContent.replace(/\s+/g, ' ').trim() : 'This form';
    say('"' + label + '" posts to the server (' + action.split('?')[0] + '). The result is not part of this capture.');
  }, true);

  // Review presets, used by the family frame and the screenshot run:
  //   ?dialog=<id>   open a live dialog through its own opener
  //   ?click=<css>   click one live control after load
  //   ?scroll=<id>   bring an element to the top of the viewport
  //   ?rail=collapsed
  function presets() {
    retarget(document);
    var params = new URLSearchParams(window.location.search);
    if (params.get('rail') === 'collapsed') {
      var shell = document.querySelector('[data-app-shell]');
      if (shell) shell.classList.add('rail-collapsed');
    }
    var dialog = params.get('dialog');
    if (dialog) {
      var opener = document.querySelector('[data-dialog-open="' + dialog + '"]');
      if (opener) opener.click();
    }
    var click = params.get('click');
    if (click) {
      var control = document.querySelector(click);
      if (control) control.click();
    }
    var scroll = params.get('scroll');
    if (scroll) {
      // Prefer the record's own section link so the live script places it.
      var jump = document.querySelector('[data-section-link="' + scroll.replace(/^section-/, '') + '"]');
      var element = document.getElementById(scroll);
      if (jump) setTimeout(function () { jump.click(); }, 50);
      else if (element) setTimeout(function () { element.scrollIntoView(); }, 50);
    }
  }
  if (document.readyState === 'complete') presets();
  else window.addEventListener('load', presets);
})();
