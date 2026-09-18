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
    ['green', /^(case created|linked|linked to case|e-mail linked|reply linked|complete|completed|sent|report sent|delivered|saved|stored|document stored|approved|accepted|applied|resolved|registered|vehicle images registered|connected|configured|succeeded|success|roadworthy|passed)$/],
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
    replaceText(document.body, /\bProvider chasing for update\b/g, 'Principal chasing for update');
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

    var layer = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    layer.setAttribute('class', 'damage-marks'); layer.setAttribute('data-damage-marks', '');
    svg.appendChild(layer);
    var hint = document.createElement('p');
    hint.className = 'v28-damage-hint'; hint.setAttribute('data-damage-hint', '');
    hint.innerHTML = '<span>Press and drag on the vehicle to size the damaged area · drag a disc to move it</span><strong data-damage-readout></strong>';
    hint.hidden = !editable;
    svg.parentNode.insertBefore(hint, svg.nextSibling);

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
      list.addEventListener('click', function (event) {
        var button = event.target.closest('[data-mark-remove]');
        if (button) { marks.splice(+button.getAttribute('data-mark-remove'), 1); render(); }
      });
    }
    render();
  }

  var PROPOSALS = [['P1', p1Logo], ['P2', p2Tones], ['P3', p3Provider], ['P4', p4Stepper], ['P5', p5Damage],
    ['P6', function () { p6AccessDenied(); p6UpdatedOnce(); p6DatesStayWhole(); p6Dates(); p6HubIcons(); }]];
  function run() {
    PROPOSALS.forEach(function (proposal) {
      if (!on(proposal[0])) return;
      try { proposal[1](); } catch (error) { console.error('v28 proposal ' + proposal[0] + ' failed: ' + error.message); }
    });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run); else run();
})();
