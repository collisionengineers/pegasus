// Case record (v28 baseline). Local interaction for the as-is capture of
// Details.cshtml + Shared/_Case*.cshtml. The shared mock engine (mock-engine.js,
// concatenated before this file) owns dialog open/close, data-mock-show visibility
// and the generic toast-on-submit for any form[data-mock-action="toast"]; this file
// owns only Case-record-specific behaviour: the page-wide edit session's read/edit
// geometry switch, per-section lock/availability, the Actions-menu state already
// driven by data-mock-show in the body, section fold/unfold, the Scroll/Tabs
// display switch, the image viewer, and the Damage Plan clicker.

(function () {
  'use strict';

  var ENGINEER_SECTIONS = ['damage', 'valuation', 'estimate', 'settlement', 'report'];
  var SECTION_KEYS = ['overview', 'inspection', 'vehicle', 'damage', 'valuation', 'estimate', 'settlement', 'report', 'files', 'notes'];
  var POST_REPORT_READONLY_STATES = ['completed', 'query'];
  // Simplified capture rule (see page README Notes): the live AssessmentCanOpen
  // seam is not modelled fact-for-fact; Engineer sections are treated as
  // assessable once a Case has reached an Engineer or a closed state.
  var ASSESSABLE_STATES = ['review', 'engineerprep', 'engineerpost', 'closedcancelled', 'closedrejected', 'closederror', 'closedunlinked'];

  var STAGE_LABEL = {
    notready: 'Not ready',
    held: 'Held',
    review: 'Review',
    engineerprep: 'With Engineer',
    engineerpost: 'With Engineer',
    completed: 'Completed',
    query: 'Query',
    closedcancelled: 'Closed · Provider cancelled',
    closedrejected: 'Closed · Collision Engineers rejected',
    closederror: 'Closed · Created in error',
    closedunlinked: 'Closed · E-mail unlinked'
  };

  var STAGE_STEP = {
    notready: 0, held: 0, review: 1, engineerprep: 2, engineerpost: 2,
    completed: 3, query: 3,
    closedcancelled: 0, closedrejected: 0, closederror: 0, closedunlinked: 0
  };

  function frameState() {
    var s = window.MOCK.state;
    var crstate = s.crstate || 'review';
    var credit = s.credit || 'off';
    var archived = s.crarchived === 'yes';
    var isEditing = credit === 'on';
    var colleague = credit === 'colleague';
    var isPostReportReadOnly = POST_REPORT_READONLY_STATES.indexOf(crstate) !== -1;
    var canEditCaseData = isEditing && !isPostReportReadOnly && !archived;
    var canEditEngineering = canEditCaseData && ASSESSABLE_STATES.indexOf(crstate) !== -1;
    return {
      crstate: crstate, isEditing: isEditing, colleague: colleague, archived: archived,
      canEditCaseData: canEditCaseData, canEditEngineering: canEditEngineering,
      isPostReportReadOnly: isPostReportReadOnly
    };
  }

  function sectionIsEditable(key, frame) {
    return ENGINEER_SECTIONS.indexOf(key) !== -1 ? frame.canEditEngineering : frame.canEditCaseData;
  }

  // The v25 decision 2 geometry: .is-editing on the record swaps every .fc's
  // .fv/.fi (site.css `.is-editing .fc:not(.ro) .fv/.fi`); .is-locked on a
  // section reopens the read state within an open session
  // (`.is-editing .is-locked .fc .fv/.fi`), matching SectionIsEditable(key).
  function updateFrame() {
    var frame = frameState();
    var record = document.getElementById('case-record');
    if (!record) return;
    record.classList.toggle('is-editing', frame.isEditing);

    SECTION_KEYS.forEach(function (key) {
      var section = document.getElementById('section-' + key);
      if (!section) return;
      var editable = sectionIsEditable(key, frame);
      var locked = frame.isEditing && !editable;
      section.classList.toggle('is-locked', locked);

      var availEl = section.querySelector('[data-section-availability]');
      var editBtn = section.querySelector('[data-section-edit]');
      var availText = null;
      if (frame.colleague) {
        availText = 'Priya Anand is editing';
      } else if (locked && frame.isPostReportReadOnly) {
        availText = 'Return the Case to the Engineer to edit';
      }
      if (availEl) {
        availEl.hidden = !availText;
        var span = availEl.querySelector('span:last-child');
        if (span) span.textContent = availText || '';
      }
      if (editBtn && key !== 'files' && key !== 'notes') {
        editBtn.hidden = frame.isEditing || frame.colleague || frame.isPostReportReadOnly || frame.archived;
      }
      if (key === 'damage') {
        section.setAttribute('data-damage-editable', editable ? 'true' : 'false');
      }
    });

    // Ribbon: the state chip, the Edit Case / Enable return label, the stepper.
    var chip = document.querySelector('[data-cr-state-chip]');
    if (chip) {
      var text = STAGE_LABEL[frame.crstate] || frame.crstate;
      if (frame.crstate === 'held') text = 'Held · review on 24 Sep';
      chip.textContent = text;
      chip.className = 'status' + (frame.crstate === 'review' ? ' status--navy'
        : frame.crstate.indexOf('closed') === 0 ? ' status--red'
        : frame.crstate === 'held' || frame.crstate === 'notready' ? ' status--amber'
        : frame.crstate === 'completed' ? ' status--green'
        : ' status--blue');
    }
    var echo = document.querySelector('[data-cr-state-chip-echo]');
    if (echo) echo.textContent = chip ? chip.textContent : '';
    var editLabel = document.querySelector('[data-cr-edit-label]');
    if (editLabel) editLabel.textContent = frame.isPostReportReadOnly ? 'Enable return' : 'Edit Case';

    var stepper = document.querySelector('[data-cr-stepper]');
    if (stepper) {
      var step = STAGE_STEP[frame.crstate] || 0;
      Array.prototype.forEach.call(stepper.querySelectorAll('li'), function (li) {
        var index = Number(li.getAttribute('data-step'));
        li.classList.toggle('done', index < step);
        li.classList.toggle('now', index === step);
      });
    }

    // Next action: mirrors DetailsModel.NextAction's ordering (AI drafts first,
    // then the next permitted lifecycle step) for the states this capture models.
    var nextLabel = document.querySelector('[data-cr-next-label]');
    var nextLink = document.querySelector('[data-cr-next-link]');
    if (nextLabel && nextLink) {
      var next = { label: 'None', section: 'notes', text: 'Notes' };
      if (frame.crstate === 'review') next = { label: 'Hand to Engineer', section: 'overview', text: 'Overview' };
      else if (frame.crstate === 'notready' || frame.crstate === 'held') next = { label: STAGE_LABEL[frame.crstate], section: 'overview', text: 'Overview' };
      else if (window.MOCK.state.crreport === 'none') next = { label: 'Generate report', section: 'report', text: 'Report' };
      else if (frame.crstate === 'engineerprep') next = { label: 'Mark report sent', section: 'report', text: 'Report' };
      else if (frame.crstate === 'engineerpost') next = { label: 'Mark completed', section: 'overview', text: 'Overview' };
      else if (window.MOCK.state.crreport === 'generated') next = { label: 'Prepare delivery', section: 'report', text: 'Report' };
      nextLabel.textContent = next.label;
      nextLink.textContent = next.text;
      nextLink.setAttribute('href', '#section-' + next.section);
    }
  }

  // ---- Section fold/unfold (site.css `.panel.is-collapsed>.panel-body`) ----
  document.addEventListener('click', function (e) {
    var toggle = e.target.closest('[data-collapse-toggle]');
    if (toggle) {
      var panel = toggle.closest('.panel');
      if (!panel) return;
      var collapsed = panel.classList.toggle('is-collapsed');
      toggle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
      return;
    }

    // Edit Case / section-head Edit: open the page-wide edit session.
    if (e.target.closest('[data-cr-edit]') || e.target.closest('[data-section-edit]')) {
      window.MOCK.setState('credit', 'on');
      return;
    }
    if (e.target.closest('[data-cr-cancel-edit]')) {
      window.MOCK.setState('credit', 'off');
      return;
    }
    if (e.target.closest('[data-cr-save]')) {
      window.MOCK.setState('credit', 'off');
      window.MOCK.setState('crtoast', 'save');
      window.MOCK.showToast('Case data saved');
      return;
    }
    if (e.target.closest('[data-cr-back-to-record]')) {
      e.preventDefault();
      window.MOCK.setState('crview', 'record');
      return;
    }

    // Scroll / Tabs display switch (docs/design README: Scroll is always the
    // default; a Tabs choice lasts only the browser session here).
    var layoutBtn = e.target.closest('[data-cr-layout]');
    if (layoutBtn) {
      var mode = layoutBtn.getAttribute('data-cr-layout');
      window.MOCK.setState('crscroll', mode);
      applyLayout(mode);
      return;
    }

    // Section-nav links: in Tabs mode these switch the active section instead
    // of scrolling (site.js's own contract for `data-section-link`).
    var navLink = e.target.closest('[data-section-link]');
    if (navLink && document.getElementById('case-record').getAttribute('data-layout') === 'tabs') {
      e.preventDefault();
      activateTab(navLink.getAttribute('data-section-link'));
    }
  });

  function applyLayout(mode) {
    var record = document.getElementById('case-record');
    if (!record) return;
    record.setAttribute('data-layout', mode);
    document.querySelectorAll('[data-case-layout-switch] [data-cr-layout]').forEach(function (btn) {
      btn.setAttribute('aria-pressed', btn.getAttribute('data-cr-layout') === mode ? 'true' : 'false');
    });
    if (mode === 'tabs' && !document.querySelector('.workspace-main .record-section.is-active')) {
      activateTab('overview');
    }
  }

  function activateTab(key) {
    document.querySelectorAll('.workspace-main .record-section').forEach(function (section) {
      section.classList.toggle('is-active', section.getAttribute('data-section') === key);
    });
    document.querySelectorAll('[data-section-link]').forEach(function (link) {
      link.setAttribute('aria-current', link.getAttribute('data-section-link') === key ? 'true' : 'false');
    });
  }

  // ---- Damage Plan clicker: click a zone to cycle its severity, matching
  // the live diagram's recorded-zones list, marker numbers and derived
  // location/severity cells (Shared/_CaseDamage.cshtml). ----
  // The real codes and ranks (AssessmentVocabulary.DamageSeverities); the
  // live diagram colours a zone via `.is-damaged[data-sev="<code>"]` in
  // case-workspace.css, so this reuses that pairing rather than inventing
  // a class per severity.
  var DAMAGE_SEVERITIES = ['none', 'light', 'light_to_moderate', 'moderate', 'moderate_to_heavy', 'heavy'];
  var DAMAGE_SEVERITY_LABEL = {
    light: 'Light', light_to_moderate: 'Light to moderate', moderate: 'Moderate',
    moderate_to_heavy: 'Moderate to heavy', heavy: 'Heavy'
  };
  var damageImpacts = { nearside_rear_door: 'moderate' };

  function zoneName(el) {
    var title = el.querySelector('title');
    return title ? title.textContent : el.getAttribute('data-damage-zone');
  }

  function renderDamage() {
    var section = document.getElementById('section-damage');
    if (!section) return;
    var svg = section.querySelector('.damage-diagram');
    var list = section.querySelector('[data-damage-impact-list]');
    var countEl = section.querySelector('[data-damage-count]');
    var locationEl = section.querySelector('[data-damage-location]');
    var severityEl = section.querySelector('[data-damage-severity]');
    var codes = Object.keys(damageImpacts);

    if (svg) {
      Array.prototype.forEach.call(svg.querySelectorAll('[data-damage-zone]'), function (zone) {
        var code = zone.getAttribute('data-damage-zone');
        var severity = damageImpacts[code];
        zone.classList.toggle('is-damaged', !!severity);
        if (severity) zone.setAttribute('data-sev', severity);
        else zone.removeAttribute('data-sev');
      });
    }

    if (list) {
      list.innerHTML = '';
      if (codes.length === 0) {
        var empty = document.createElement('li');
        empty.className = 'muted';
        empty.setAttribute('data-damage-empty', '');
        empty.textContent = 'No damage recorded.';
        list.appendChild(empty);
      }
      codes.forEach(function (code, index) {
        var zoneEl = svg ? svg.querySelector('[data-damage-zone="' + code + '"]') : null;
        var name = zoneEl ? zoneName(zoneEl) : code;
        var li = document.createElement('li');
        li.className = 'impact-row';
        li.setAttribute('data-damage-row', code);
        li.innerHTML = '<span class="zc"><i class="zn">' + (index + 1) + '</i>' + name + '</span>' +
          '<span class="fc"><div class="fv">' + (DAMAGE_SEVERITY_LABEL[damageImpacts[code]] || 'Light') + '</div></span>' +
          '<span class="fc"><div class="fv empty">No note</div></span>' +
          '<button type="button" class="del" data-damage-row-remove aria-label="Remove ' + name + '">×</button>';
        list.appendChild(li);
      });
    }

    if (countEl) countEl.textContent = String(codes.length);
    if (locationEl) {
      locationEl.textContent = codes.length === 0 ? 'Not recorded'
        : codes.length === 1 ? (svg && svg.querySelector('[data-damage-zone="' + codes[0] + '"]') ? zoneName(svg.querySelector('[data-damage-zone="' + codes[0] + '"]')) : codes[0])
        : 'Multiple';
    }
    if (severityEl) {
      var worst = codes.reduce(function (acc, code) {
        var rank = DAMAGE_SEVERITIES.indexOf(damageImpacts[code]);
        return rank > acc.rank ? { rank: rank, code: code } : acc;
      }, { rank: -1, code: null });
      severityEl.textContent = worst.code ? DAMAGE_SEVERITY_LABEL[damageImpacts[worst.code]] : 'Not recorded';
    }
  }

  document.addEventListener('click', function (e) {
    var zone = e.target.closest('[data-damage-zone]');
    if (!zone || !zone.closest('#section-damage')) return;
    if (document.getElementById('section-damage').getAttribute('data-damage-editable') !== 'true') return;
    var code = zone.getAttribute('data-damage-zone');
    var current = damageImpacts[code];
    var idx = current ? DAMAGE_SEVERITIES.indexOf(current) : 0;
    idx = (idx + 1) % DAMAGE_SEVERITIES.length;
    if (DAMAGE_SEVERITIES[idx] === 'none') {
      delete damageImpacts[code];
    } else {
      damageImpacts[code] = DAMAGE_SEVERITIES[idx];
    }
    renderDamage();
  });
  document.addEventListener('click', function (e) {
    var remove = e.target.closest('[data-damage-row-remove]');
    if (!remove) return;
    var row = remove.closest('[data-damage-row]');
    if (row) { delete damageImpacts[row.getAttribute('data-damage-row')]; renderDamage(); }
  });

  // ---- Full-screen image viewer: opens from any [data-evidence-item] inside a
  // [data-evidence-set], per Shared/_CaseViewer.cshtml. Rotate/zoom/crop are
  // visual-only in this capture. ----
  var viewerZoom = 1;
  function openViewer(trigger) {
    var viewer = document.querySelector('[data-case-viewer]');
    if (!viewer) return;
    var name = trigger.getAttribute('data-file-name') || 'image.jpg';
    var nameEl = viewer.querySelector('[data-viewer-name]');
    if (nameEl) nameEl.textContent = name;
    viewer.hidden = false;
    var closeBtn = viewer.querySelector('[data-viewer-close]');
    if (closeBtn) closeBtn.focus();
  }
  document.addEventListener('click', function (e) {
    var item = e.target.closest('[data-evidence-item]');
    if (item && item.closest('[data-evidence-set]')) {
      e.preventDefault();
      openViewer(item);
      return;
    }
    if (e.target.closest('[data-viewer-close]')) {
      var viewer = e.target.closest('[data-case-viewer]') || document.querySelector('[data-case-viewer]');
      if (viewer) viewer.hidden = true;
      return;
    }
    if (e.target.closest('[data-viewer-zoom]')) {
      viewerZoom = viewerZoom >= 2 ? 1 : viewerZoom + 0.5;
      var label = document.querySelector('[data-viewer-zoom-label]');
      if (label) label.textContent = viewerZoom === 1 ? 'Zoom' : Math.round(viewerZoom * 100) + '%';
      var img = document.querySelector('[data-viewer-image]');
      if (img) img.style.transform = 'scale(' + viewerZoom + ')';
      return;
    }
    if (e.target.closest('[data-viewer-rotate]')) {
      var stage = document.querySelector('[data-viewer-image]');
      if (stage) {
        var current = Number(stage.getAttribute('data-rotation') || '0');
        current = (current + 90) % 360;
        stage.setAttribute('data-rotation', String(current));
        stage.style.transform = 'rotate(' + current + 'deg)';
      }
      return;
    }
    // Crop, on the viewer stage or a tile's own Crop button: enters the crop
    // tool state (view tools hide, crop tools show) — the drag/handle geometry
    // itself is out of scope for this static capture (noted in Notes).
    if (e.target.closest('[data-viewer-crop]') || e.target.closest('[data-preparation-crop]')) {
      var v = document.querySelector('[data-case-viewer]');
      if (v) {
        v.hidden = false;
        var view = v.querySelector('[data-viewer-view-tools]');
        var crop = v.querySelector('[data-viewer-crop-tools]');
        if (view) view.hidden = true;
        if (crop) crop.hidden = false;
      }
      return;
    }
    if (e.target.closest('[data-viewer-crop-save]') || e.target.closest('[data-viewer-crop-cancel]')) {
      var v2 = document.querySelector('[data-case-viewer]');
      if (v2) {
        var view2 = v2.querySelector('[data-viewer-view-tools]');
        var crop2 = v2.querySelector('[data-viewer-crop-tools]');
        if (view2) view2.hidden = false;
        if (crop2) crop2.hidden = true;
      }
      if (e.target.closest('[data-viewer-crop-save]')) window.MOCK.showToast('Demo: crop is staged into the one Case form, not wired here');
    }
  });

  // ---- Estimate "Send to AI" range readout (case-workspace.js's own small
  // behaviour: the percentage output and the derived target amount). ----
  document.addEventListener('input', function (e) {
    if (e.target.matches('[data-estimate-range]')) {
      var output = document.getElementById('ai-target-value');
      if (output) output.textContent = e.target.value + '%';
      var amountEl = document.getElementById('ai-target-amount');
      if (amountEl) {
        var base = 8450;
        amountEl.textContent = '£' + Math.round(base * (Number(e.target.value) / 100)).toLocaleString('en-GB') + '.00';
      }
    }
  });

  // ---- Files section tabs (Documents / Images / Correspondence), the strip
  // Shared/_CaseFiles.cshtml enables with script; every panel still renders
  // without it, so this only toggles which one shows. ----
  document.addEventListener('click', function (e) {
    var tab = e.target.closest('[data-file-tab]');
    if (!tab) return;
    var key = tab.getAttribute('data-file-tab');
    var wrap = tab.closest('[data-file-tabs-wrap]');
    if (!wrap) return;
    wrap.querySelectorAll('[data-file-tab]').forEach(function (btn) {
      btn.setAttribute('aria-selected', btn === tab ? 'true' : 'false');
    });
    wrap.querySelectorAll('[data-file-tab-panel]').forEach(function (panel) {
      panel.hidden = panel.getAttribute('data-file-tab-panel') !== key;
    });
  });

  // ---- VAT reset (estimate discount/VAT bar) ----
  document.addEventListener('click', function (e) {
    if (e.target.closest('[data-vat-reset]')) {
      window.MOCK.showToast('Demo: resets VAT categories to the repairer VAT status');
    }
  });

  // A standalone control (not inside its own data-mock-action="toast" form)
  // carrying data-mock-action="toast" — e.g. Renew editing, Refresh, a
  // valuation card's Get valuation, Accept a proposal. Submit buttons inside
  // a toast-carrying form are left to the submit listener below so the
  // toast fires once, not twice.
  document.addEventListener('click', function (e) {
    var el = e.target.closest('[data-mock-action="toast"]');
    if (!el || el.tagName === 'FORM') return;
    // A submit control inside its own toast-carrying form is left to the
    // submit listener below, so the toast fires once, not twice; a
    // type="button" control (Add To recipient, Get valuation, …) is not a
    // submit and needs handling here even when it sits inside such a form.
    if (el.getAttribute('type') === 'submit') return;
    if (el.tagName === 'A') e.preventDefault();
    window.MOCK.showToast('Demo: this action is not wired in the mockup');
  });

  // Every posted form in this capture carries data-mock-action="toast" so
  // mock-engine.js's generic submit listener already prevents the real POST;
  // this adds the one thing it does not do — tell the operator the action
  // is not wired here, and close the dialog it opened from, if any.
  document.addEventListener('submit', function (e) {
    var form = e.target.closest('form[data-mock-action="toast"]');
    if (!form) return;
    window.MOCK.showToast('Demo: this action is not wired in the mockup');
    var dialog = form.closest('.dialog-backdrop');
    if (dialog) dialog.hidden = true;
  });

  // ---- Keyboard contract (docs/design README): Ctrl S saves only while
  // editing; ArrowUp/ArrowDown move through a row list. F5's "re-query, never
  // a browser reload" and the Scroll fallback's ?section= are page-navigation
  // behaviours this single static file cannot reproduce — noted in the page
  // README rather than faked here. ----
  document.addEventListener('keydown', function (e) {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
      var record = document.getElementById('case-record');
      if (record && record.classList.contains('is-editing')) {
        e.preventDefault();
        window.MOCK.setState('credit', 'off');
        window.MOCK.showToast('Case data saved');
      }
    }
    if (e.key === 'ArrowUp' || e.key === 'ArrowDown') {
      var list = e.target.closest('.impact-list, .doc-list, [data-image-grid], .notes-timeline');
      if (!list) return;
      var rows = Array.prototype.slice.call(list.children).filter(function (el) { return el.tagName === 'LI' || el.tagName === 'DIV'; });
      var focusable = rows.map(function (row) { return row.querySelector('a,button,input,select,textarea'); }).filter(Boolean);
      var current = focusable.indexOf(document.activeElement);
      if (current === -1) return;
      e.preventDefault();
      var next = e.key === 'ArrowDown' ? Math.min(current + 1, focusable.length - 1) : Math.max(current - 1, 0);
      focusable[next].focus();
    }
  });

  document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[data-mock-input]').forEach(function (el) {
      el.addEventListener('change', updateFrame);
    });
    updateFrame();
    applyLayout((window.MOCK.state.crscroll) || 'scroll');
    renderDamage();
  });
})();
