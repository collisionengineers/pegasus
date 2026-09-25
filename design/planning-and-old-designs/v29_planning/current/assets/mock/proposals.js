// v29 proposals layer: Case referencing and structure (PR 803 + issue 814).
// Everything proposed sits here, applied over the captured live pages at
// load, so the capture stays the faithful baseline and every change can be
// compared against it.
//
//   (default)            all proposals on
//   ?proposals=off       the baseline exactly as captured
//   ?skip=P4,P6          all on except the named ones
//   ?stage=sent|audit    Inspection + Audit Case: Inspection report sent (before
//                        Create audit) or Audit created (default)
//   ?view=audit|inspection   which view of an Inspection + Audit Case (default audit)
//   ?dialog=create-audit|triage   open that proposed dialog on load
//   ?type=triage         Create case with Triage chosen
//   ?opt=key:value,...   undecided choices (see OPTIONS)
//   ?opt=auditview:X     item AA, how the Audit and Inspection views are shown:
//                        strip (1, default) | ribbon (2) | sectionrow (3) | aside (4) | compare (5)
//
// Second pass, 23 September 2026: the views replace the working-set strip (the
// open-records tabs), not the Scroll/Tabs switch; a Triage Case changes only as
// its prior requirements say; the approved wording shows by default; Search
// lists an Audit as its own entry.
//
// Proposal ids P1..P13 are the ones v29-notes.md and the sign-off list use.
(function () {
  'use strict';
  var params = new URLSearchParams(window.location.search);
  if (params.get('proposals') === 'off') return;
  var skipped = (params.get('skip') || '').split(',').map(function (s) { return s.trim().toUpperCase(); });
  var on = function (id) { return skipped.indexOf(id) < 0; };
  var $ = function (selector, root) { return (root || document).querySelector(selector); };
  var $$ = function (selector, root) { return Array.prototype.slice.call((root || document).querySelectorAll(selector)); };
  var state = window.V29_STATE || {};
  var stage = params.get('stage') === 'sent' ? 'sent' : 'audit';
  var view = params.get('view') === 'inspection' ? 'inspection' : 'audit';
  if (/(^|,)auditview:compare(,|$)/.test(params.get('opt') || '')) view = 'audit';

  // Undecided choices, each a strip variable with its proposed default.
  var OPTIONS = {
    auditref: 'none',      // none | audit | both   the ribbon also names a.{Case/PO} (item C); the Audit view tab carries it
    rolabel: 'on',         // on | none             the approved label on read-only Inspection heads (item B)
    singleview: 'none',    // none | tab            a Case with one view shows no strip, or its one view tab (item A)
    principal: 'off',      // off | on              the live Open the Triage, or with a Principal pick (item S)
    metric: 'end',         // end | afterheld       where the Triages metric sits (item Q)
    auditview: 'strip'     // strip | ribbon | sectionrow | aside | compare   how the views are shown (item AA)
  };
  (params.get('opt') || '').split(',').forEach(function (pair) {
    var parts = pair.split(':');
    if (parts.length === 2 && Object.prototype.hasOwnProperty.call(OPTIONS, parts[0])) OPTIONS[parts[0]] = parts[1];
  });

  // Fixture facts the proposals need that the captured page does not carry.
  // The Triage Case takes the next number in QDOS's shared 2031 sequence after
  // the two captured Cases (QDOS31001, a.QDOS31002). The sent time is the
  // fixture's clock.
  var TRIAGE_REFERENCE = 't.QDOS31003';
  var LIVE_TRIAGE_REFERENCE = /\bT-00001\b/g;
  var SENT_AT = '06 May 2031 11:30';

  document.documentElement.setAttribute('data-v29-proposals', 'on');

  function mark(element, id) { element.setAttribute('data-v29-proposal', id); return element; }
  function removed(element, id) { if (element) { element.setAttribute('data-v29-removed', id); element.hidden = true; } }

  // Builds an element from a tag, attributes and children (strings are text).
  function el(tag, attrs, children) {
    var node = document.createElement(tag);
    Object.keys(attrs || {}).forEach(function (name) { node.setAttribute(name, attrs[name]); });
    (children || []).forEach(function (child) {
      node.appendChild(typeof child === 'string' ? document.createTextNode(child) : child);
    });
    return node;
  }
  function icon(name) {
    var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('class', 'icon'); svg.setAttribute('aria-hidden', 'true');
    var use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
    use.setAttribute('href', '#icon-' + name);
    svg.appendChild(use);
    return svg;
  }

  // Text nodes only, so no markup or attribute is disturbed.
  function replaceText(root, pattern, replacement) {
    var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null);
    var node; var nodes = [];
    while ((node = walker.nextNode())) nodes.push(node);
    nodes.forEach(function (text) {
      var parent = text.parentNode && text.parentNode.nodeName;
      if (parent === 'SCRIPT' || parent === 'STYLE') return;
      pattern.lastIndex = 0;
      if (pattern.test(text.nodeValue)) {
        pattern.lastIndex = 0;
        text.nodeValue = text.nodeValue.replace(pattern, function () { return replacement; });
      }
    });
  }

  // A link to this same state page with some presets changed.
  function presetHref(changes) {
    var next = new URLSearchParams(window.location.search);
    Object.keys(changes).forEach(function (key) {
      if (changes[key] === null) next.delete(key); else next.set(key, changes[key]);
    });
    next.delete('dialog');
    var query = next.toString();
    return window.location.pathname.replace(/^.*\//, '') + (query ? '?' + query : '');
  }

  // Newly added dialogs and their openers bind through the live site.js binders.
  function addDialog(dialog) {
    var host = el('div', { 'data-v29-dialog-host': '' }, [dialog]);
    document.body.appendChild(host);
    bindLive(host);
  }
  // Opens a proposed dialog the way its opener would, never by navigating.
  function openWhenBound(opener) {
    setTimeout(function () {
      if (opener.dataset.dialogOpenBound === 'true') opener.click();
      else console.error('v29: dialog opener was not bound by the live binders');
    }, 150);
  }
  function bindLive(root) {
    (window.pegasusMountBinders || []).forEach(function (binder) {
      try { binder(root); } catch (error) { console.error('v29 binder: ' + error.message); }
    });
  }

  var isInspectionAndAudit = function () {
    var chip = $('[data-case-type-chip]');
    return !!chip && /Inspection \+ Audit/.test(chip.textContent);
  };
  var caseReference = function () {
    var heading = $('.ribbon-ref .ribbon-value');
    return heading ? heading.textContent.trim() : '';
  };
  var auditReference = function () { return 'a.' + caseReference(); };
  var auditCreated = function () { return isInspectionAndAudit() && stage === 'audit'; };
  var inspectionView = function () { return auditCreated() && view === 'inspection'; };

  // ---- P1 · Views replace the working-set strip (item A) -----------------
  // The open-records strip under the utility bar goes from every page. On an
  // Inspection + Audit Case whose Audit exists, the same strip carries the
  // Case's views instead: Inspection and Audit, in the working-set tab's own
  // markup, each naming the reference its report carries. Scroll/Tabs stays.
  function viewTab(key, label, reference, active) {
    var wrapper = el('div', { 'class': 'workspace-tab' + (active ? ' is-active' : ''), 'data-v29-view': key });
    var link = el('a', { 'class': 'workspace-tab-link', href: presetHref({ view: key }) }, [
      icon('folder'), el('span', { 'class': 'ref' }, [label]), el('span', { 'class': 'reg' }, [reference])
    ]);
    if (active) link.setAttribute('aria-current', 'page');
    wrapper.appendChild(link);
    return wrapper;
  }
  // The two views as links to this page, the current one marked.
  function viewLink(key, label) {
    var link = el('a', { href: presetHref({ view: key }), 'data-v29-view': key }, [label]);
    if (key === view) link.setAttribute('aria-current', 'page');
    return link;
  }
  function viewSwitch(ariaLabel) {
    return el('div', { 'class': 'layout-switch', role: 'group', 'aria-label': ariaLabel, 'data-v29-view-switch': '' },
      [viewLink('inspection', 'Inspection'), viewLink('audit', 'Audit')]);
  }

  // Option 2 · the ribbon carries the switch, as a ribbon item after the reference.
  function viewsInRibbon() {
    var reference = $('.ribbon-ref');
    if (!reference) return;
    var item = el('div', { 'class': 'ribbon-item', 'data-v29-ribbon-views': '' }, [
      el('div', { 'class': 'ribbon-label' }, ['View']),
      el('div', { 'class': 'ribbon-value' }, [viewSwitch('Case view')])
    ]);
    // The live ribbon shares its width equally (flex 1 1 0); the switch keeps its own width.
    item.style.flex = '0 0 auto';
    reference.parentNode.insertBefore(mark(item, 'P1'), reference.nextSibling);
  }

  // Option 3 · the section row carries the switch, before Refresh; Scroll/Tabs stays at the end.
  function viewsInSectionRow() {
    var tools = $('.section-row .section-tools');
    if (!tools) return;
    tools.insertBefore(mark(viewSwitch('Case view'), 'P1'), tools.firstChild);
  }

  // Option 4 · the aside's first card lists the two views with their report state.
  function viewsInAside() {
    var aside = $('[data-case-aside]');
    if (!aside) return;
    function row(key, label, reference, chip, tone) {
      var name = key === view
        ? el('span', { 'class': 'v29-view-current', 'aria-current': 'page' }, [label + ' · ' + reference])
        : el('a', { href: presetHref({ view: key }), 'data-v29-view': key }, [label + ' · ' + reference]);
      return el('div', { 'class': 'next-row', 'data-v29-view-row': key }, [name, el('span', { 'class': 'status status--plain ' + tone }, [chip])]);
    }
    var card = el('section', { 'class': 'panel context-card', 'data-v29-views-card': '' }, [
      el('div', { 'class': 'panel-head' }, [el('h2', {}, ['Views'])]),
      el('div', { 'class': 'panel-body stack' }, [
        row('inspection', 'Inspection', caseReference(), 'Sent', 'status--green'),
        row('audit', 'Audit', auditReference(), 'With Engineer', 'status--blue')
      ])
    ]);
    aside.insertBefore(mark(card, 'P1'), aside.firstChild);
  }

  // Option 5 · no switch: the page is the Audit, and a value the Audit changed
  // shows the Inspection's value under it. Two changes are illustrated.
  function compareInPlace() {
    var record = $('[data-case-record]');
    if (record) record.setAttribute('data-v29-view', 'compare');
    var changes = [
      ['settlement.repair_delays', '3'],
      ['narrative.engineers_comments', 'Synthetic audit comment: rear impact confirmed; repair delays revised.']
    ];
    changes.forEach(function (change) {
      var cell = $('.fc[data-field="' + change[0] + '"]');
      var value = cell ? $('.fv', cell) : null;
      if (!value) return;
      var before = value.textContent.trim();
      value.textContent = change[1];
      cell.appendChild(mark(el('div', { 'class': 'v29-was', 'data-v29-was': '' }, [
        el('span', { 'class': 'lbl' }, ['Inspection']), el('span', {}, [before])
      ]), 'P1'));
      var section = cell.closest('.record-section');
      var head = section ? $('.panel-head', section) : null;
      if (head && !$('[data-v29-changed]', head)) {
        var title = $('h2', head);
        title.parentNode.insertBefore(mark(el('span', { 'class': 'status status--plain', 'data-v29-changed': '' }, ['Changed from Inspection']), 'P1'), title.nextSibling);
      }
    });
    // With no Inspection view, the sent report line keeps its facts and drops its link.
    setTimeout(function () {
      var link = $('[data-v29-inspection-report] > .btn');
      if (link) link.remove();
    }, 0);
  }

  function p1ViewTabs() {
    var strip = $('[data-working-set]');
    if (strip) removed(strip, 'P1');
    document.body.classList.remove('has-working-set');
    var record = $('[data-case-record]');
    if (!record || !strip) return;
    if (auditCreated() && OPTIONS.auditview !== 'strip') {
      record.setAttribute('data-v29-view', view);
      record.setAttribute('data-v29-auditview', OPTIONS.auditview);
      if (OPTIONS.auditview === 'ribbon') viewsInRibbon();
      else if (OPTIONS.auditview === 'sectionrow') viewsInSectionRow();
      else if (OPTIONS.auditview === 'aside') viewsInAside();
      else if (OPTIONS.auditview === 'compare') compareInPlace();
      return;
    }
    var tabs = [];
    if (auditCreated()) {
      tabs.push(viewTab('inspection', 'Inspection', caseReference(), view === 'inspection'));
      tabs.push(viewTab('audit', 'Audit', auditReference(), view === 'audit'));
    } else if (OPTIONS.singleview === 'tab') {
      // A standalone Audit's one view is its Audit; every other Case's is its Inspection.
      var chip = ($('[data-case-type-chip]') || { textContent: '' }).textContent.trim();
      var single = chip === 'Audit' ? 'Audit' : 'Inspection';
      tabs.push(viewTab(single.toLowerCase(), single, caseReference(), true));
    }
    if (!tabs.length) return;
    var views = el('nav', { 'class': 'workspace-tabs', 'aria-label': 'Case views', 'data-v29-view-tabs': '' }, tabs);
    strip.parentNode.insertBefore(mark(views, 'P1'), strip);
    document.body.classList.add('has-working-set');
    record.setAttribute('data-v29-view', auditCreated() ? view : 'single');
  }

  // ---- P2 · The ribbon names the Audit's reference (item C) ---------------
  // The heading stays the Case/PO. Once the Audit exists the ribbon carries
  // a.{Case/PO}, the Audit report's reference, beside it.
  function p2AuditReference() {
    if (!auditCreated() || OPTIONS.auditref === 'none') return;
    if (OPTIONS.auditref === 'audit' && view !== 'audit') return;
    var reference = $('.ribbon-ref');
    if (!reference) return;
    var item = el('div', { 'class': 'ribbon-item', 'data-v29-audit-reference': '' }, [
      el('div', { 'class': 'ribbon-label' }, ['Audit reference']),
      el('div', { 'class': 'ribbon-value mono' }, [el('span', {}, [auditReference()])])
    ]);
    reference.parentNode.insertBefore(mark(item, 'P2'), reference.nextSibling);
  }

  // ---- P3 · The Inspection view is read-only (item B) ---------------------
  // After Create audit the Inspection's copy and its sent report never change.
  // Its view offers no Edit anywhere; the Audit view owns editing.
  function p3InspectionReadOnly() {
    if (!inspectionView()) return;
    $$('[data-case-ribbon-actions] form[data-case-edit-form]').forEach(function (form) { removed(form, 'P3'); });
    $$('[data-section-edit]').forEach(function (button) {
      var form = button.closest('form');
      var head = button.closest('.panel-actions');
      removed(form || button, 'P3');
      if (OPTIONS.rolabel === 'on' && head) {
        head.insertBefore(mark(el('span', { 'class': 'gated' }, [el('span', {}, ['Read-only · Audit created'])]), 'P3'), head.firstChild);
      }
    });
  }

  // ---- P4 · The Report section of each view (items F, G, X) ---------------
  // One report per work. The Audit view's report carries a.{Case/PO}; the
  // Inspection's sent report is summarised there with a link to its own view.
  // The Inspection view shows that sent report and nothing to generate.
  function p4Report() {
    if (!isInspectionAndAudit()) return;
    var card = $('[data-report-preview-card]');
    if (!card) return;
    var status = $('[data-report-status]', card);
    var sentLine = caseReference() + ' · Sent ' + SENT_AT;
    function asSent(target) {
      if (!target) return;
      target.textContent = '';
      target.appendChild(el('span', {}, [sentLine]));
    }
    if (stage === 'sent' || inspectionView()) {
      asSent(status);
      mark(card, 'P4');
      removed($('[data-report-not-ready]'), 'P4');
      if (inspectionView()) {
        removed($('[data-report-gate]'), 'P4');
      }
      return;
    }
    // Audit view: the Audit report card carries its reference.
    if (status) {
      status.insertBefore(el('span', { 'class': 'mono', 'data-v29-report-reference': '' }, [auditReference() + ' · ']), status.firstChild);
      mark(card, 'P4');
    }
    // The Inspection's sent report: one line, and the way to it.
    var summary = el('div', { 'class': 'pv', 'data-v29-inspection-report': '' }, [
      el('div', { 'class': 'pv-thumb', 'aria-hidden': 'true' }),
      el('div', { 'class': 'pv-meta' }, [
        el('div', { 'class': 'pt' }, [($('[data-report-title]', card) || { textContent: '' }).textContent]),
        el('div', { 'class': 'ps' }, [el('span', {}, [sentLine])])
      ]),
      el('a', { 'class': 'btn btn--small', href: presetHref({ view: 'inspection' }) }, ['Inspection view'])
    ]);
    card.parentNode.insertBefore(mark(summary, 'P4'), card);
  }

  // ---- P5 · Create audit once the Inspection report is sent (items D, E, F)
  // The Actions item sits where the live one does. The dialog keeps the live
  // markup and states the facts: the Case, the Audit reference, the Engineer.
  function p5CreateAudit() {
    if (!isInspectionAndAudit() || stage !== 'sent') return;
    var menu = $('[data-case-ribbon-actions] details.menu .menu-body');
    if (!menu) return; // The Actions menu is offered inside an edit session.
    var engineer = ($$('.ribbon-item')[3] || { textContent: '' }).textContent.replace(/^\s*Engineer\s*/i, '').trim();
    var button = el('button', { type: 'button', 'class': 'btn', 'data-dialog-open': 'case-create-audit-dialog', 'data-create-audit': '' }, [icon('plus'), el('span', {}, ['Create audit'])]);
    var correct = $$('button', menu).filter(function (b) { return /Correct principal/.test(b.textContent); })[0];
    if (correct) correct.parentNode.insertBefore(mark(button, 'P5'), correct.nextSibling); else menu.appendChild(mark(button, 'P5'));

    var dialog = el('div', { id: 'case-create-audit-dialog', 'class': 'dialog-backdrop', 'data-dialog': 'case-create-audit-dialog', hidden: '' }, [
      el('section', { 'class': 'dialog dialog--compact', role: 'dialog', 'aria-modal': 'true', 'aria-labelledby': 'case-create-audit-dialog-title' }, [
        el('div', { 'class': 'dialog-head' }, [
          el('h2', { id: 'case-create-audit-dialog-title', tabindex: '-1' }, ['Create audit']),
          el('button', { type: 'button', 'class': 'dialog-close', 'data-dialog-close': '', 'aria-label': 'Close dialog' }, [icon('x')])
        ]),
        el('form', { method: 'post', action: '#' }, [
          el('div', { 'class': 'dialog-body stack' }, [
            el('dl', { 'class': 'audit-facts' }, [
              el('dt', {}, ['Case']), el('dd', { 'class': 'mono' }, [caseReference()]),
              el('dt', {}, ['Audit reference']), el('dd', { 'class': 'mono', 'data-audit-reference': '' }, [auditReference()]),
              el('dt', {}, ['Engineer']), el('dd', {}, [engineer || 'Unassigned'])
            ])
          ]),
          el('div', { 'class': 'dialog-foot' }, [
            el('button', { type: 'button', 'class': 'btn', 'data-dialog-close': '', 'data-dialog-dismiss': '' }, ['Cancel']),
            el('a', { 'class': 'btn btn--primary', href: presetHref({ stage: 'audit', view: 'audit' }), 'data-dialog-initial-focus': '' }, [icon('plus'), el('span', {}, ['Create audit'])])
          ])
        ])
      ])
    ]);
    addDialog(mark(dialog, 'P5'));
    bindLive(menu);
    if (params.get('menu') === 'actions') { var actionsMenu = button.closest('details'); if (actionsMenu) actionsMenu.open = true; }
    if (params.get('dialog') === 'create-audit') {
      var details = button.closest('details'); if (details) details.open = true;
      openWhenBound(button);
    }
  }

  // ---- P6 · Files names the Audit folder (item G) -------------------------
  // The Audit's report files go in the a.{Case/PO} Box folder under the Case
  // folder. Files says so beside the Case folder's own custody chip.
  function p6AuditFolder() {
    if (!auditCreated()) return;
    var chip = $('[data-custody-chip]');
    if (!chip) return;
    var audit = el('span', { 'class': 'status status--amber status--plain', 'data-v29-audit-folder': '' }, ['Box audit folder: preparing']);
    chip.parentNode.insertBefore(mark(audit, 'P6'), chip.nextSibling);
  }

  // ---- P7 · A Triage Case gains the Case's Files (item P) -----------------
  // Operator, 23 September: Triage Cases change only as their prior
  // requirements say. Those add the established Case Files upload path (PR 803
  // plan); the t. Case/PO and /Cases/{id} are P9. Everything else is live.
  function p7TriageFiles() {
    if (state.id !== 'triage-record') return;
    var notes = $$('section.panel').filter(function (panel) { var h = $('h2', panel); return h && h.textContent.trim() === 'Notes'; })[0];
    if (!notes) return;
    var files = el('section', { 'class': 'panel section-gap', id: 'section-files', 'aria-labelledby': 'triage-files-title' }, [
      el('div', { 'class': 'panel-head' }, [
        el('h2', { id: 'triage-files-title' }, ['Files']),
        el('span', { 'class': 'status status--amber status--plain' }, ['Box case folder: preparing']),
        el('div', { 'class': 'panel-actions' }, [
          el('a', { 'class': 'btn btn--small btn--primary', href: '/Upload' }, [icon('upload'), el('span', {}, ['Add evidence'])])
        ])
      ]),
      el('div', { 'class': 'panel-body stack' }, [
        el('h3', { 'class': 'tab-panel-title' }, ['Documents']),
        el('p', { 'class': 'muted' }, ['No documents on this Case.'])
      ])
    ]);
    notes.parentNode.insertBefore(mark(files, 'P7'), notes);
  }

  // ---- P8 · The Work Centre counts Triages (item Q) -----------------------
  function p8TriagesMetric() {
    var strip = $('.wc-metrics');
    if (!strip) return;
    var triageCount = 1; // The fixture's one Open Triage.
    var metric = el('a', { 'class': 'metric', 'data-value': 'triage', href: '/Cases?tab=triage' }, [
      el('span', { 'class': 'metric-label' }, [el('span', {}, ['Triages'])]),
      el('span', { 'class': 'metric-value' }, [String(triageCount)])
    ]);
    strip.classList.remove('metric-strip--4');
    strip.classList.add('metric-strip--5');
    var held = $('.metric[data-value="held"]', strip);
    if (OPTIONS.metric === 'afterheld' && held) strip.insertBefore(mark(metric, 'P8'), held.nextSibling);
    else strip.appendChild(mark(metric, 'P8'));
  }

  // ---- P9 · A Triage is a Case: t. Case/PO, /Cases/{id} (items W, U) -------
  // Every surface names the Triage by its Case/PO and opens it at /Cases/{id};
  // /Triage and /Triage/{id} are gone.
  function p9TriageIdentity() {
    replaceText(document.body, LIVE_TRIAGE_REFERENCE, TRIAGE_REFERENCE);
    var routes = window.V29_ROUTES;
    $$('a[href^="/Triage/"]').forEach(function (link) {
      var id = link.getAttribute('href').replace(/^\/Triage\//, '').replace(/[?#].*$/, '');
      if (!/^[0-9a-f-]{36}$/i.test(id)) return;
      link.setAttribute('href', '/Cases/' + id);
      mark(link, 'P9');
      if (routes && routes.exact) routes.exact['/cases/' + id.toLowerCase()] = 'triage-record';
    });
  }

  // ---- P10 · Triage sits in the Workflow group of Cases (item Q) ----------
  function p10Rail() {
    var triage = $('button.scope-button[value="triage"]');
    var query = $('button.scope-button[value="query"]');
    if (!triage || !query) return;
    query.parentNode.insertBefore(mark(triage, 'P10'), query.nextSibling);
  }

  // ---- P11 · Create case offers Triage (item R) ---------------------------
  // Triage needs only the Principal and the registration.
  function p11CreateTriage() {
    var select = $('#CaseType');
    if (!select) return;
    select.appendChild(mark(el('option', { value: 'Triage' }, ['Triage']), 'P11'));
    var keep = ['PrincipalCode', 'CaseType', 'VehicleRegistration'];
    function apply() {
      var triage = select.value === 'Triage';
      $$('form .field').forEach(function (field) {
        var control = $('input, select, textarea', field);
        if (!control || !control.id || keep.indexOf(control.id) >= 0) return;
        if (!field.closest('main')) return;
        field.hidden = triage;
        if (triage) { if (control.required) { control.setAttribute('data-v29-required', ''); control.required = false; } }
        else if (control.hasAttribute('data-v29-required')) control.required = true;
      });
    }
    select.addEventListener('change', apply);
    if (params.get('type') === 'triage') { select.value = 'Triage'; apply(); }
  }

  // ---- P12 · Open the Triage names its Principal (item S) -----------------
  // The live action and dialog, drawn because the captured item does not
  // qualify for it, with the Principal a Triage Case now needs.
  function p12OpenTriage() {
    var close = $('[data-unidentified-action="close"]');
    if (!close) return;
    var link = el('a', { 'class': 'btn', href: '?dialog=triage', 'data-dialog-open': 'unidentified-triage-dialog', 'data-unidentified-action': 'triage' }, [icon('clipboard-list'), el('span', {}, ['Open the Triage'])]);
    close.parentNode.insertBefore(mark(link, 'P12'), close);
    var fields = [];
    if (OPTIONS.principal === 'on') {
      fields.push(el('div', { 'class': 'field', 'data-v29-triage-principal': '' }, [
        el('label', { 'class': 'req', 'for': 'unidentified-triage-principal' }, ['Principal']),
        el('select', { id: 'unidentified-triage-principal', name: 'principalCode', required: '' }, [
          el('option', { value: '' }, ['Choose']), el('option', { value: 'QDOS' }, ['QDOS'])
        ])
      ]));
    }
    fields.push(el('div', { 'class': 'field' }, [
      el('label', { 'class': 'req', 'for': 'unidentified-triage-vrm' }, ['Vehicle registration']),
      el('input', { id: 'unidentified-triage-vrm', 'class': 'mono', name: 'vehicleRegistration', maxlength: '20', required: '', 'data-dialog-initial-focus': '' })
    ]));
    var dialog = el('div', { id: 'unidentified-triage-dialog', 'class': 'dialog-backdrop', 'data-dialog': 'unidentified-triage-dialog', hidden: '' }, [
      el('section', { 'class': 'dialog', role: 'dialog', 'aria-modal': 'true', 'aria-labelledby': 'unidentified-triage-title' }, [
        el('div', { 'class': 'dialog-head' }, [
          el('h2', { id: 'unidentified-triage-title', tabindex: '-1' }, ['Open the Triage']),
          el('button', { type: 'button', 'class': 'dialog-close', 'data-dialog-close': '', 'aria-label': 'Close dialog' }, [icon('x')])
        ]),
        el('form', { method: 'post', action: '#', id: 'unidentified-triage-form', 'class': 'dialog-body stack' }, fields),
        el('div', { 'class': 'dialog-foot' }, [
          el('button', { type: 'button', 'class': 'btn', 'data-dialog-close': '' }, ['Cancel']),
          el('button', { type: 'submit', form: 'unidentified-triage-form', 'class': 'btn btn--primary' }, ['Open the Triage'])
        ])
      ])
    ]);
    addDialog(mark(dialog, 'P12'));
    bindLive(close.parentNode);
    if (params.get('dialog') === 'triage') openWhenBound(link);
  }

  // ---- P13 · Search finds the Triage Case (item W) ------------------------
  function p13SearchTriage() {
    if (state.id !== 'search-triage') return;
    var empty = $$('.pane').map(function (pane) { return pane; }).filter(function (pane) {
      var h = $('h2', pane); return h && h.textContent.trim() === 'Case results';
    })[0];
    if (!empty) return;
    var list = $('[data-row-list]', empty);
    var count = $('.pane-head .muted', empty);
    if (!list) return;
    var triageId = (window.V29_ROUTES && Object.keys(window.V29_ROUTES.exact).filter(function (key) {
      return window.V29_ROUTES.exact[key] === 'triage-record' && /^\/triage\//.test(key);
    })[0] || '').replace(/^\/triage\//, '');
    var table = el('div', { 'class': 'table-wrap' }, [
      el('table', {}, [
        el('caption', { 'class': 'sr-only' }, ['Case search results']),
        el('thead', {}, [el('tr', {}, ['Case/PO', 'Vehicle', 'Claimant', 'Principal', 'State', 'Editing'].map(function (h) { return el('th', { scope: 'col' }, [h]); }))]),
        el('tbody', {}, [el('tr', {}, [
          el('td', {}, [el('a', { 'class': 'table-row-link', href: '/Cases/' + triageId }, [TRIAGE_REFERENCE])]),
          el('td', {}, ['TR32AGE']),
          el('td', {}, ['Not recorded']),
          el('td', {}, ['QDOS']),
          el('td', {}, [el('span', { 'class': 'status status--navy' }, ['Open'])]),
          el('td', {})
        ])])
      ])
    ]);
    list.textContent = '';
    list.appendChild(mark(table, 'P13'));
    if (count) count.textContent = '1 result';
    if (window.V29_ROUTES && triageId) window.V29_ROUTES.exact['/cases/' + triageId] = 'triage-record';
  }

  // ---- P14 · Search lists an Audit as its own entry (item J) --------------
  // Operator, 23 September: an Inspection + Audit Case whose Audit exists
  // surfaces twice, QDOS26001 and a.QDOS26001, both going to the same Case.
  function p14SearchAuditEntry() {
    if (state.id !== 'search-results') return;
    var rows = $$('.pane table tbody tr');
    var inspection = rows.filter(function (row) {
      var link = $('td .table-row-link', row);
      return link && /^[A-Z]+\d+$/.test(link.textContent.trim());
    })[0];
    if (!inspection) return;
    var link = $('td .table-row-link', inspection);
    var reference = link.textContent.trim();
    var href = link.getAttribute('href');
    var audit = inspection.cloneNode(true);
    audit.setAttribute('aria-selected', 'false');
    var auditLink = $('td .table-row-link', audit);
    auditLink.textContent = 'a.' + reference;
    auditLink.setAttribute('href', href + '?view=audit');
    link.setAttribute('href', href + '?view=inspection');
    var template = $('template', audit);
    if (template) {
      var heading = template.content.querySelector('h2');
      if (heading) heading.textContent = heading.textContent.replace(reference, 'a.' + reference);
    }
    inspection.parentNode.insertBefore(mark(audit, 'P14'), inspection.nextSibling);
    var pane = inspection.closest('.pane');
    var count = pane ? $('.pane-head .muted', pane) : null;
    if (count) count.textContent = rows.length + 1 + ' results';
  }

  var PROPOSALS = [
    ['P1', p1ViewTabs], ['P2', p2AuditReference], ['P3', p3InspectionReadOnly], ['P4', p4Report],
    ['P5', p5CreateAudit], ['P6', p6AuditFolder], ['P7', p7TriageFiles], ['P8', p8TriagesMetric],
    ['P9', p9TriageIdentity], ['P10', p10Rail], ['P11', p11CreateTriage], ['P12', p12OpenTriage],
    ['P13', p13SearchTriage], ['P14', p14SearchAuditEntry]
  ];
  function run() {
    PROPOSALS.forEach(function (proposal) {
      if (!on(proposal[0])) return;
      try { proposal[1](); } catch (error) { console.error('v29 proposal ' + proposal[0] + ' failed: ' + error.message); }
    });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run); else run();
})();
