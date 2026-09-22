// v28 proposals layer. Everything the operator has asked to change sits here,
// applied over the captured pages at load, so the capture itself stays the
// faithful baseline and every change can be compared against it.
//
//   (default)          all proposals on
//   ?proposals=off     the baseline exactly as captured
//   ?skip=P4,P6        all on except the named ones
//
// Each proposal has a letter-number id that the working log and the notes use.
(function () {
  'use strict';
  var params = new URLSearchParams(window.location.search);
  if (params.get('proposals') === 'off') return;
  var skipped = (params.get('skip') || '').split(',').map(function (s) { return s.trim().toUpperCase(); });
  var on = function (id) { return skipped.indexOf(id) < 0; };
  var $$ = function (selector, root) { return Array.prototype.slice.call((root || document).querySelectorAll(selector)); };
  var mock = (function () {
    var script = document.currentScript;
    return script ? script.src.replace(/[^/]*$/, '') : '../assets/mock/';
  })();

  document.documentElement.setAttribute('data-v28-proposals', 'on');

  // Text nodes only, so no markup or attribute is disturbed.
  function replaceText(root, pattern, replacement) {
    var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null);
    var node; var nodes = [];
    while ((node = walker.nextNode())) nodes.push(node);
    nodes.forEach(function (text) {
      var parent = text.parentNode && text.parentNode.nodeName;
      if (parent === 'SCRIPT' || parent === 'STYLE' || parent === 'TEXTAREA') return;
      if (pattern.test(text.nodeValue)) text.nodeValue = text.nodeValue.replace(pattern, replacement);
    });
  }

  // ---- P1 · The refined mark replaces the lockup -------------------------
  function p1Logo() {
    $$('.brand img, .auth-brand img').forEach(function (img) {
      img.setAttribute('data-v28-live-src', img.getAttribute('src'));
      img.src = mock + (img.closest('.auth-brand') ? 'pegasus-mark-refined-256.png' : 'pegasus-mark-refined-128.png');
    });
  }

  // ---- P2 · Status colour by meaning -------------------------------------
  // A chip's tone follows what its words mean: green for an outcome that
  // succeeded, red for one that did not, amber for waiting on something,
  // navy for in hand, neutral for settled with nothing to say. Labels are
  // matched whole, lower-cased; the first rule that matches wins.
  var TONES = [
    ['red', /(^| )(failed|failure|could not be read|not created|unavailable|denied|rejected|refused|blocked|error|overdue|lost|conflict|unroadworthy)( |$)|storage failed|lookup failed/],
    ['green', /^(case created|linked|linked to case|e-mail linked|reply linked|complete|completed|sent|report sent|delivered|saved|stored|document stored|approved|accepted|applied|resolved|registered|vehicle images registered|connected|configured|succeeded|success|roadworthy|passed|active|enabled)$/],
    ['amber', /^(query|creating case|not yet processed|awaiting .*|pending|draft|not configured|password change required|overridden|today|chase due)$|: preparing$/],
    ['neutral', /^(cancelled|closed|archived|dismissed|created in error|staff-closed|disabled|no recorded activity)$|^closed /]
  ];
  var TONE_CLASSES = ['status--navy', 'status--amber', 'status--red', 'status--green', 'status--blue', 'status--neutral'];
  function p2Tones() {
    $$('.status').forEach(function (chip) {
      var label = (chip.textContent || '').replace(/\s+/g, ' ').trim().toLowerCase();
      if (!label) return;
      for (var i = 0; i < TONES.length; i++) {
        if (!TONES[i][1].test(label)) continue;
        var wanted = 'status--' + TONES[i][0];
        if (chip.classList.contains(wanted)) return;
        var had = TONE_CLASSES.filter(function (name) { return chip.classList.contains(name); })[0] || 'none';
        TONE_CLASSES.forEach(function (name) { chip.classList.remove(name); });
        chip.classList.add(wanted);
        chip.setAttribute('data-v28-live-tone', had.replace('status--', ''));
        return;
      }
    });
  }

  // ---- P3 · "Provider" never appears on the front end --------------------
  function p3Provider() {
    replaceText(document.body, /\bProvider cancelled\b/g, 'Cancelled');
    replaceText(document.body, /\bProvider cancellation\b/g, 'Cancelled');
    // One category where there were two (operator, 18 September).
    $$('option').forEach(function (option) {
      if (/Client chasing for update/.test(option.textContent)) { option.setAttribute('data-v28-removed', 'P3'); option.hidden = true; option.disabled = true; }
    });
    replaceText(document.body, /\b(Provider|Principal|Client) chasing for update\b/g, 'Update Request');
    replaceText(document.body, /\bWork Provider\b/g, 'Principal');
    replaceText(document.body, /\bProviders\b/g, 'Principals');
    replaceText(document.body, /\bProvider\b/g, 'Principal');
    replaceText(document.body, /\bproviders\b/g, 'principals');
    replaceText(document.body, /\bprovider\b/g, 'principal');
  }

  // ---- P4 · The lifecycle strip on Overview goes -------------------------
  function p4Stepper() {
    $$('#section-overview .stepper, .record-section .stepper').forEach(function (strip) {
      strip.setAttribute('data-v28-removed', 'P4');
      strip.hidden = true;
    });
  }

  // ---- P6 · Issues D to H of the baseline notes --------------------------
  // D: Access denied joins the error family in the navless frame.
  function p6AccessDenied() {
    if (!/^access-denied/.test((window.V28_STATE || {}).id || '')) return;
    var shell = document.querySelector('[data-app-shell]');
    var heading = document.querySelector('main h1');
    var sentence = document.querySelector('main .empty-state, main .panel-body p');
    if (!shell || !heading) return;
    var frame = document.createElement('div');
    frame.className = 'external-shell';
    frame.setAttribute('data-v28-proposal', 'P6-D');
    frame.innerHTML = '<main id="main-content" tabindex="-1"><section class="auth-card">'
      + '<div class="auth-brand"><img src="' + mock + 'pegasus-mark-refined-256.png" alt="" /><strong>PEGASUS</strong></div>'
      + '<h1></h1><p></p><div class="button-row"><a class="btn" href="/">Return to Work Centre</a></div>'
      + '</section></main>';
    frame.querySelector('h1').textContent = heading.textContent.trim();
    frame.querySelector('p').textContent = sentence ? sentence.textContent.trim() : '';
    shell.parentNode.insertBefore(frame, shell);
    shell.hidden = true;
    $$('.workspace-tabs').forEach(function (strip) { strip.hidden = true; });
  }
  // E: the Work Centre says when it was updated once, in the page header.
  function p6UpdatedOnce() {
    if (!document.querySelector('[data-wc-updated]')) return;
    $$('[data-wc-freshness]').forEach(function (line) {
      if (line.closest('.panel-head, .pane-head')) return; // a section's own refresh time stays
      if (/^\s*Updated\b/.test(line.textContent)) { line.setAttribute('data-v28-removed', 'P6-E'); line.hidden = true; }
    });
  }
  // F: a date in the Cases table never breaks across lines.
  function p6DatesStayWhole() {
    $$('.cases-table td').forEach(function (cell) {
      if (cell.children.length === 0 && /^\s*\d{1,2} [A-Z][a-z]{2,3} \d{4}( \d{2}:\d{2})?\s*$/.test(cell.textContent)) {
        cell.classList.add('nowrap'); cell.setAttribute('data-v28-proposal', 'P6-F');
      }
    });
  }
  // G: one month abbreviation everywhere.
  function p6Dates() { replaceText(document.body, /\bSept\b/g, 'Sep'); }
  // H: the Administration hub uses the same icon as the Administration nav.
  function p6HubIcons() {
    var nav = {};
    $$('.admin-nav a[href]').forEach(function (link) {
      var use = link.querySelector('use');
      if (use) nav[link.getAttribute('href')] = use.getAttribute('href');
    });
    $$('.admin-content a[href], .admin-layout .panel a[href]').forEach(function (card) {
      if (card.closest('.admin-nav')) return;
      var use = card.querySelector('use'); var wanted = nav[card.getAttribute('href')];
      if (use && wanted && use.getAttribute('href') !== wanted) {
        use.setAttribute('data-v28-live-icon', use.getAttribute('href'));
        use.setAttribute('href', wanted);
      }
    });
  }

  // ---- P5 · Damage: areas, not panels ------------------------------------
  // v27 variant C with the eight areas of v27 section 12. Press and drag on
  // the vehicle to size a disc; the areas under it are derived and named.
  var AREAS = { front: 'Front', left_front: 'LH Front', left_rear: 'LH Rear', left_side: 'LH Side', rear: 'Rear', right_front: 'RH Front', right_rear: 'RH Rear', right_side: 'RH Side' };
  var AREA_ORDER = ['front', 'left_front', 'left_rear', 'left_side', 'rear', 'right_front', 'right_rear', 'right_side'];
  var SEVERITIES = [['light', 'Light'], ['light_to_moderate', 'Light to moderate'], ['moderate', 'Moderate'], ['moderate_to_heavy', 'Moderate to heavy'], ['heavy', 'Heavy']];
  var SEVERITY_RANK = SEVERITIES.map(function (s) { return s[0]; });
  // Where each live zone sits, so recorded zones open as discs in the same place.
  var ZONE_CENTRES = {
    front_centre: [120, 40], front_left_corner: [62, 52], front_right_corner: [178, 52], bonnet: [120, 86], windscreen: [120, 134],
    left_front_wing: [58, 110], right_front_wing: [182, 110], left_front_door: [54, 196], right_front_door: [186, 196],
    left_rear_door: [54, 262], right_rear_door: [186, 262], roof: [120, 226], left_quarter: [58, 330], right_quarter: [182, 330],
    rear_screen: [120, 318], tailgate: [120, 372], rear_left_corner: [62, 396], rear_right_corner: [178, 396], rear_centre: [120, 408],
    wheel_left_front: [36, 112], wheel_right_front: [204, 112], wheel_left_rear: [36, 324], wheel_right_rear: [204, 324]
  };

  function p5Damage() {
    var section = document.getElementById('section-damage');
    var svg = section && section.querySelector('svg.damage-diagram');
    var list = section && section.querySelector('[data-damage-impact-list]');
    if (!svg || !list) return;
    var editable = !!list.querySelector('[data-damage-row-severity]') || !!document.querySelector('input[name="editLeaseToken"]');
    section.setAttribute('data-v28-damage', editable ? 'edit' : 'read');
    list.setAttribute('aria-label', 'Recorded areas');
    replaceText(section, /\bRecorded zones\b/g, 'Recorded areas');

    var marks = $$('[data-damage-row]', list).map(function (row, index) {
      var centre = ZONE_CENTRES[row.getAttribute('data-damage-row')] || [120, 220];
      var select = row.querySelector('[data-damage-row-severity]');
      var severity = select ? select.value : ((row.querySelector('.fv') || {}).textContent || 'Moderate').trim().toLowerCase().replace(/ /g, '_');
      var note = row.querySelector('[data-damage-row-note]');
      var noteText = note ? note.value : '';
      if (!note) { var cell = row.querySelectorAll('.fc .fv')[1]; if (cell && !cell.classList.contains('empty')) noteText = cell.textContent.trim(); }
      return { id: 'm' + (index + 1), x: centre[0], y: centre[1], r: 22, severity: severity, note: noteText };
    });
    // Two recorded zones side by side are one damaged area in this model.
    if (marks.length === 2 && Math.hypot(marks[0].x - marks[1].x, marks[0].y - marks[1].y) < 80) {
      marks = [{ id: 'm1', x: Math.round((marks[0].x + marks[1].x) / 2), y: Math.round((marks[0].y + marks[1].y) / 2), r: 40, severity: marks[0].severity, note: marks[0].note }];
    }
    var sequence = marks.length + 1;
    // What the record held when the page opened; Reset returns to it.
    var recorded = JSON.parse(JSON.stringify(marks));

    var layer = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    layer.setAttribute('class', 'damage-marks'); layer.setAttribute('data-damage-marks', '');
    svg.appendChild(layer);
    var hint = document.createElement('p');
    hint.className = 'v28-damage-hint'; hint.setAttribute('data-damage-hint', '');
    hint.innerHTML = '<span>Press and drag on the vehicle to size the damaged area · drag a disc to move it</span><strong data-damage-readout></strong>';
    hint.hidden = !editable;
    var reset = document.createElement('button');
    reset.type = 'button'; reset.className = 'btn btn--small v28-damage-reset'; reset.setAttribute('data-damage-reset', '');
    reset.innerHTML = '<svg class="icon" aria-hidden="true"><use href="#icon-refresh-cw" /></svg><span>Reset</span>';
    reset.hidden = !editable;
    svg.parentNode.insertBefore(hint, svg.nextSibling);
    hint.parentNode.insertBefore(reset, hint.nextSibling);

    function svgPoint(event) {
      var point = svg.createSVGPoint(); point.x = event.clientX; point.y = event.clientY;
      var mapped = point.matrixTransform(svg.getScreenCTM().inverse());
      return [Math.round(mapped.x), Math.round(mapped.y)];
    }
    function onVehicle(x, y) {
      var point = svg.createSVGPoint(); point.x = x; point.y = y;
      var body = svg.querySelector('.dv-body');
      if (body && body.isPointInFill(point)) return true;
      return $$('.dv-wheel, .dv-mirror', svg).some(function (rect) {
        var rx = +rect.getAttribute('x'), ry = +rect.getAttribute('y');
        return x >= rx && x <= rx + +rect.getAttribute('width') && y >= ry && y <= ry + +rect.getAttribute('height');
      });
    }
    // Front and rear thirds, the sides between; left, centre, right.
    function areaAt(x, y) {
      if (!onVehicle(x, y)) return null;
      var band = y < 150 ? 'front' : y > 300 ? 'rear' : 'side';
      var lateral = x < 100 ? 'left' : x > 140 ? 'right' : 'centre';
      if (band === 'side') return (lateral === 'centre' ? (x < 120 ? 'left' : 'right') : lateral) + '_side';
      return lateral === 'centre' ? band : lateral + '_' + band;
    }
    function markAreas(mark) {
      var points = [[mark.x, mark.y]];
      for (var a = 0; a < 8; a++) points.push([mark.x + Math.round(mark.r * Math.cos(a * Math.PI / 4)), mark.y + Math.round(mark.r * Math.sin(a * Math.PI / 4))]);
      var areas = [];
      points.forEach(function (p) { var area = areaAt(p[0], p[1]); if (area && areas.indexOf(area) < 0) areas.push(area); });
      return areas.sort(function (a, b) { return AREA_ORDER.indexOf(a) - AREA_ORDER.indexOf(b); });
    }
    var word = function (code) { var found = SEVERITIES.filter(function (s) { return s[0] === code; })[0]; return found ? found[1] : code; };
    var escape = function (text) { return String(text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;'); };

    function render() {
      var guides = editable ? '<g class="dm-guides" aria-hidden="true"><path d="M42 150 H198 M42 300 H198 M100 16 V418 M140 16 V418" /></g>' : '';
      layer.innerHTML = guides + marks.map(function (mark, index) {
        return '<g class="dm dm-area" data-mark="' + mark.id + '" data-sev="' + mark.severity + '">'
          + '<circle class="area sev" cx="' + mark.x + '" cy="' + mark.y + '" r="' + mark.r + '" />'
          + '<circle class="n" r="8" cx="' + mark.x + '" cy="' + mark.y + '" />'
          + '<text x="' + mark.x + '" y="' + (mark.y + 3.2) + '" text-anchor="middle">' + (index + 1) + '</text></g>';
      }).join('');
      list.innerHTML = !marks.length ? '<li class="muted" data-damage-empty>No damage recorded.</li>' : marks.map(function (mark, index) {
        var where = markAreas(mark).map(function (area) { return AREAS[area]; }).join(', ') || 'Off the vehicle';
        var options = SEVERITIES.map(function (s) { return '<option value="' + s[0] + '"' + (s[0] === mark.severity ? ' selected' : '') + '>' + s[1] + '</option>'; }).join('');
        return '<li class="impact-row" data-damage-row="' + mark.id + '"><span class="zc"><i class="zn">' + (index + 1) + '</i>' + where + '</span>'
          + '<span class="fc">' + (editable ? '<label class="sr-only" for="v28-severity-' + index + '">Severity</label>' : '') + '<div class="fv">' + word(mark.severity) + '</div>'
          + (editable ? '<select id="v28-severity-' + index + '" class="fi" data-mark-severity="' + index + '">' + options + '</select>' : '') + '</span>'
          + '<span class="fc">' + (editable ? '<label class="sr-only" for="v28-note-' + index + '">Note</label>' : '') + '<div class="fv' + (mark.note ? '' : ' empty') + '">' + escape(mark.note || 'No note') + '</div>'
          + (editable ? '<input id="v28-note-' + index + '" class="fi" maxlength="200" value="' + escape(mark.note) + '" placeholder="Note" data-mark-note="' + index + '" />' : '') + '</span>'
          + (editable ? '<button type="button" class="del" data-mark-remove="' + index + '" aria-label="Remove area ' + (index + 1) + '">×</button>' : '') + '</li>';
      }).join('');
      var all = []; var worst = -1;
      marks.forEach(function (mark) {
        markAreas(mark).forEach(function (area) { if (all.indexOf(area) < 0) all.push(area); });
        worst = Math.max(worst, SEVERITY_RANK.indexOf(mark.severity));
      });
      all.sort(function (a, b) { return AREA_ORDER.indexOf(a) - AREA_ORDER.indexOf(b); });
      var names = all.map(function (area) { return AREAS[area]; });
      var set = function (selector, text) {
        var cell = section.querySelector(selector); if (!cell) return;
        cell.textContent = text; cell.classList.toggle('empty', text === 'Not recorded');
      };
      set('[data-damage-location]', !names.length ? 'Not recorded' : names.length === 1 ? names[0] : 'Multiple · ' + names.join(', '));
      set('[data-damage-severity]', worst < 0 ? 'Not recorded' : word(SEVERITY_RANK[worst]));
      set('[data-damage-count]', String(marks.length));
      var none = $$('p, div', section).filter(function (el) { return el.children.length === 0 && /^No damage recorded\.$/.test(el.textContent.trim()) && !el.closest('[data-damage-impact-list]'); })[0];
      if (none) none.hidden = marks.length > 0;
    }

    if (editable) {
      var drawing = null;
      // The live panel clicker must not also fire.
      ['click', 'keydown'].forEach(function (type) {
        svg.addEventListener(type, function (event) { event.stopPropagation(); if (type === 'click') event.preventDefault(); }, true);
      });
      svg.addEventListener('pointerdown', function (event) {
        event.stopPropagation();
        var p = svgPoint(event); var hit = event.target.closest && event.target.closest('[data-mark]');
        if (hit) {
          var moved = marks.filter(function (mark) { return mark.id === hit.getAttribute('data-mark'); })[0];
          drawing = { move: moved, ox: moved.x - p[0], oy: moved.y - p[1] };
        } else {
          if (!onVehicle(p[0], p[1])) return;
          drawing = { mark: { id: 'm' + (sequence++), x: p[0], y: p[1], r: 12, severity: 'moderate', note: '' } };
          marks.push(drawing.mark);
        }
        event.preventDefault(); svg.setPointerCapture(event.pointerId); render();
      }, true);
      svg.addEventListener('pointermove', function (event) {
        var p = svgPoint(event);
        if (!drawing) { var area = areaAt(p[0], p[1]); hint.querySelector('[data-damage-readout]').textContent = area ? AREAS[area] : ''; return; }
        if (drawing.move) { drawing.move.x = p[0] + drawing.ox; drawing.move.y = p[1] + drawing.oy; }
        else drawing.mark.r = Math.max(10, Math.round(Math.hypot(p[0] - drawing.mark.x, p[1] - drawing.mark.y)));
        render();
      }, true);
      var finish = function () { if (drawing) { drawing = null; render(); } };
      svg.addEventListener('pointerup', finish, true);
      svg.addEventListener('pointercancel', finish, true);
      svg.addEventListener('pointerleave', function () { hint.querySelector('[data-damage-readout]').textContent = ''; });
      list.addEventListener('change', function (event) {
        var index = event.target.getAttribute('data-mark-severity');
        if (index !== null) { marks[+index].severity = event.target.value; render(); }
      });
      list.addEventListener('input', function (event) {
        var index = event.target.getAttribute('data-mark-note');
        if (index !== null) marks[+index].note = event.target.value;
      });
      reset.addEventListener('click', function () {
        marks = JSON.parse(JSON.stringify(recorded)); sequence = marks.length + 1; render();
      });
      list.addEventListener('click', function (event) {
        var button = event.target.closest('[data-mark-remove]');
        if (button) { marks.splice(+button.getAttribute('data-mark-remove'), 1); render(); }
      });
    }
    render();
  }


  // ---- P7 · No Lifecycle actions container on Overview -------------------
  // Return to Review is already an item of the ribbon's Actions menu; the
  // second copy in its own panel goes.
  function p7Lifecycle() {
    $$('[data-lifecycle-actions]').forEach(function (panel) {
      panel.setAttribute('data-v28-removed', 'P7'); panel.hidden = true;
    });
  }

  // ---- P8 · Valuation cards -----------------------------------------------
  // Cazana is drawn like Glass's, Brego and Super CAP: an entry card while
  // editing, nothing at all in read until a guide is recorded. Get valuation
  // is a real button at the bottom centre of each card.
  function p8Valuation() {
    var section = document.getElementById('section-valuation');
    if (!section) return;
    var seam = section.querySelector('[data-valuation-seam]');
    var template = section.querySelector('.valuation-card.entry[data-valuation-entry="brego"]')
      || section.querySelector('.valuation-card.entry:not([data-valuation-entry="glasses"])');
    if (seam && template) {
      var card = template.cloneNode(true);
      card.setAttribute('data-valuation-entry', 'cazana');
      card.setAttribute('data-valuation-card', 'cazana');
      card.setAttribute('data-valuation-source-card', 'Cazana');
      card.setAttribute('data-v28-proposal', 'P8');
      card.setAttribute('action', (card.getAttribute('action') || '').replace(/Brego/g, 'Cazana'));
      var title = card.querySelector('h3 span'); if (title) title.textContent = 'Cazana';
      $$('[name="source"]', card).forEach(function (input) { input.value = 'Cazana'; });
      $$('[data-valuation-source]', card).forEach(function (button) {
        button.setAttribute('data-valuation-source', 'cazana');
        button.setAttribute('formaction', (button.getAttribute('formaction') || '').replace(/Brego/g, 'Cazana'));
      });
      $$('[data-valuation-save]', card).forEach(function (button) { button.setAttribute('data-valuation-save', 'cazana'); });
      $$('[data-valuation-retail], [data-valuation-trade]', card).forEach(function (input) { input.value = ''; });
      $$('label', card).forEach(function (label) { label.removeAttribute('for'); });
      seam.parentNode.insertBefore(card, seam);
    }
    if (seam) { seam.setAttribute('data-v28-removed', 'P8'); seam.hidden = true; }
    $$('.valuation-card.entry', section).forEach(function (entry) {
      var get = entry.querySelector('h3 [data-valuation-source]');
      var actions = entry.querySelector('.entry-actions');
      if (!get || !actions) return;
      get.classList.remove('btn--ghost');
      get.setAttribute('data-v28-proposal', 'P8');
      actions.classList.add('v28-entry-actions');
      actions.insertBefore(get, actions.firstChild);
    });
  }

  // ---- P9 · Estimate: Print Estimate and Compare under More --------------
  function p9Estimate() {
    var section = document.getElementById('section-estimate');
    if (!section) return;
    var print = section.querySelector('[data-estimate-actions] a[data-document-preview]');
    var more = section.querySelector('[data-estimate-more] .menu-body');
    if (print && !more) {
      // Live renders More only when it has something to hold.
      var tools = section.querySelector('.panel-head [data-estimate-expand]');
      if (!tools) return;
      var menu = document.createElement('details');
      menu.className = 'menu'; menu.setAttribute('data-menu', ''); menu.setAttribute('data-estimate-more', ''); menu.setAttribute('data-v28-proposal', 'P9');
      menu.innerHTML = '<summary class="btn btn--small"><span>More</span><svg class="icon" aria-hidden="true"><use href="#icon-chevron-down" /></svg></summary><div class="menu-body"></div>';
      tools.parentNode.insertBefore(menu, tools);
      more = menu.querySelector('.menu-body');
    }
    if (!print || !more) return;
    var holder = print.parentNode;
    print.classList.remove('btn--small');
    print.setAttribute('data-v28-proposal', 'P9');
    print.setAttribute('data-file-name', 'Print Estimate');
    var label = print.querySelector('span'); if (label) label.textContent = 'Print Estimate';
    var icon = print.querySelector('use'); if (icon) icon.setAttribute('href', '#icon-file-text');
    var compare = more.querySelector('[data-estimate-compare]');
    more.insertBefore(print, compare || null);
    if (holder && !holder.querySelector('a, button')) { holder.setAttribute('data-v28-removed', 'P9'); holder.hidden = true; }
  }


  // ---- P8b · Get valuation on a source with no connection ----------------
  // It answers with an error that says to contact an administrator. Glass's
  // has a connection, so its button is left to the application.
  function p8GetValuationError() {
    $$('#section-valuation .valuation-card.entry').forEach(function (card) {
      if (card.getAttribute('data-valuation-entry') === 'glasses') return;
      var button = card.querySelector('[data-valuation-source]');
      if (!button) return;
      button.addEventListener('click', function (event) {
        event.preventDefault(); event.stopPropagation();
        var old = card.querySelector('[data-v28-valuation-error]'); if (old) old.remove();
        var notice = document.createElement('div');
        notice.className = 'notice notice--danger'; notice.setAttribute('role', 'alert'); notice.setAttribute('data-v28-valuation-error', '');
        notice.innerHTML = '<svg class="icon" aria-hidden="true"><use href="#icon-alert-circle" /></svg><span>Error. Contact an administrator.</span>';
        card.insertBefore(notice, card.querySelector('.entry-actions'));
      }, true);
    });
  }

  // ---- P9b · Compare shows greyed out until a Case has two estimates -----
  function p9CompareDisabled() {
    var body = document.querySelector('#section-estimate [data-estimate-more] .menu-body');
    if (!body || body.querySelector('[data-estimate-compare]')) return;
    var button = document.createElement('button');
    button.type = 'button'; button.className = 'btn'; button.disabled = true;
    button.setAttribute('data-estimate-compare', ''); button.setAttribute('data-v28-proposal', 'P9');
    button.innerHTML = '<svg class="icon" aria-hidden="true"><use href="#icon-list" /></svg><span>Compare</span>';
    body.appendChild(button);
  }

  // ---- P10 · The "Use estimate · ..." lock chip goes ----------------------
  // Use estimate is simply available: a missing repairer VAT status does not
  // block it (operator, 18 September: the gate "seems like nonsense").
  function p10UseEstimateChip() {
    $$('[data-estimate-use-condition]').forEach(function (chip) {
      var button = document.createElement('button');
      button.type = 'submit'; button.className = 'btn btn--small'; button.setAttribute('formaction', '/Cases/Details?handler=UseEstimate'); button.setAttribute('formmethod', 'post');
      button.setAttribute('data-v28-proposal', 'P10');
      button.innerHTML = '<svg class="icon" aria-hidden="true"><use href="#icon-check" /></svg><span>Use estimate</span>';
      var useForm = document.createElement('form'); useForm.setAttribute('method', 'post');
      useForm.setAttribute('action', '/Cases/Details?handler=UseEstimate'); useForm.style.display = 'contents';
      useForm.appendChild(button); chip.parentNode.insertBefore(useForm, chip);
      chip.setAttribute('data-v28-removed', 'P10'); chip.hidden = true;
    });
  }

  // ---- P11 · Upload received, reworked ------------------------------------
  // Same content and the same words. What was uploaded comes first and is
  // legible; the decision sits beside it; discarding is last and folded away.
  function p11UploadReceived() {
    var decision = document.getElementById('group-decision-title');
    var files = document.getElementById('group-status-title');
    if (!decision || !files) return;
    var decisionPanel = decision.closest('.panel');
    var filesPanel = files.closest('.panel');
    var discardTitle = document.getElementById('group-discard-title');
    var discardPanel = discardTitle && discardTitle.closest('.panel');
    var stack = filesPanel.parentNode;

    var layout = document.createElement('div');
    layout.className = 'v28-upload'; layout.setAttribute('data-v28-proposal', 'P11');
    var side = document.createElement('div'); side.className = 'stack v28-upload__side';
    stack.insertBefore(layout, stack.firstChild);
    layout.appendChild(filesPanel); layout.appendChild(side);
    side.appendChild(decisionPanel);

    // Files: a count in the head, proper thumbnails, the outcome beside each.
    var rows = $$('.file-row', filesPanel);
    var head = filesPanel.querySelector('.panel-head');
    if (head && rows.length) {
      var meta = document.createElement('span'); meta.className = 'meta';
      meta.textContent = rows.length + (rows.length === 1 ? ' file' : ' files');
      head.appendChild(meta);
    }
    filesPanel.classList.add('v28-upload__files');

    // Decision: compact fields, the three ways out on one row.
    decisionPanel.classList.add('v28-upload__decision');
    var register = decisionPanel.querySelector('form[action*="RegisterGroup"]');
    var cancel = $$('a.btn', decisionPanel).filter(function (a) { return a.textContent.trim() === 'Cancel'; })[0];
    var attach = decisionPanel.querySelector('details.upload-attach');
    if (register && cancel) {
      var row = register.querySelector('.button-row');
      var emptied = cancel.parentNode;
      row.appendChild(cancel);
      if (emptied && !emptied.children.length) emptied.remove();
    }
    // The attach form stays a sibling of the register form: forms cannot nest.
    if (attach) attach.classList.add('v28-upload__attach');
    var reg = document.getElementById('group-registration');
    if (reg) reg.classList.add('mono');

    // Discard: last, closed until asked for, and visibly destructive.
    if (discardPanel) {
      var fold = document.createElement('details');
      fold.className = 'panel v28-upload__discard';
      var summaryEl = document.createElement('summary'); summaryEl.className = 'panel-head';
      summaryEl.appendChild(discardPanel.querySelector('.panel-head h2'));
      // The live summary.panel-head rule supplies the plus and minus marker.
      fold.appendChild(summaryEl);
      fold.appendChild(discardPanel.querySelector('.panel-body'));
      var box = fold.querySelector('#group-discard-confirmation');
      var label = fold.querySelector('label[for="group-discard-confirmation"]');
      if (box && label) { label.classList.add('choice'); label.insertBefore(box, label.firstChild); label.insertBefore(document.createTextNode(' '), box.nextSibling); }
      var go = fold.querySelector('button[type="submit"]');
      if (go) { go.classList.remove('btn--ghost'); go.classList.add('btn--danger'); }
      side.appendChild(fold);
      discardPanel.remove();
    }
  }

  var PROPOSALS = [['P1', p1Logo], ['P2', p2Tones], ['P3', p3Provider], ['P4', p4Stepper], ['P5', p5Damage],
    ['P6', function () { p6AccessDenied(); p6UpdatedOnce(); p6DatesStayWhole(); p6Dates(); p6HubIcons(); }],
    ['P7', p7Lifecycle], ['P8', function () { p8Valuation(); p8GetValuationError(); }],
    ['P9', function () { p9Estimate(); p9CompareDisabled(); }], ['P10', p10UseEstimateChip], ['P11', p11UploadReceived]];
  function run() {
    PROPOSALS.forEach(function (proposal) {
      if (!on(proposal[0])) return;
      try { proposal[1](); } catch (error) { console.error('v28 proposal ' + proposal[0] + ' failed: ' + error.message); }
    });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run); else run();
})();
