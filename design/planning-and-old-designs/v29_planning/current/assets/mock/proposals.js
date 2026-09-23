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

  // Undecided choices, each a strip variable with its proposed default.
  var OPTIONS = {
    auditref: 'audit',     // audit | both | none   where the ribbon shows a.{Case/PO} (item C)
    rolabel: 'none',       // none | on             an availability label on read-only Inspection heads (item B)
    triageedit: 'case',    // case | triage         "Edit Case" or the live "Edit Triage" on a Triage Case (item P)
    principal: 'on',       // on | off              the Principal pick in Open the Triage (item S)
    metric: 'end'          // end | afterheld       where the Triages metric sits (item Q)
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

  var isCasePage = function () { return !!$('[data-case-record]'); };
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

  // ---- P1 · Views replace the Scroll/Tabs switch (item A) -----------------
  // Every Case page loses Scroll/Tabs and always scrolls. An Inspection + Audit
  // Case whose Audit exists gets a view switch in the same place and the same
  // control vocabulary: Inspection | Audit, server-rendered links (?view=).
  function p1Views() {
    var record = $('[data-case-record]');
    if (!record) return;
    record.setAttribute('data-layout', 'scroll');
    var layoutSwitch = $('[data-case-layout-switch]');
    if (!layoutSwitch) return;
    if (!auditCreated()) { removed(layoutSwitch, 'P1'); return; }
    var views = el('div', { 'class': 'layout-switch', role: 'group', 'aria-label': 'Case view', 'data-v29-view-switch': '' });
    [['inspection', 'Inspection'], ['audit', 'Audit']].forEach(function (entry) {
      var link = el('a', { href: presetHref({ view: entry[0] }), 'data-v29-view': entry[0] }, [entry[1]]);
      if (entry[0] === view) link.setAttribute('aria-current', 'page');
      views.appendChild(link);
    });
    layoutSwitch.parentNode.replaceChild(mark(views, 'P1'), layoutSwitch);
    record.setAttribute('data-v29-view', view);
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

  // ---- P7 · A Triage Case uses the Case frame (items P, T) ----------------
  // /Cases/{id} renders a Triage Case: the Case ribbon (t. Case/PO, Triage
  // state and type chips, Edit Case, one Actions menu), the section row with
  // one view, and the live Triage panels as its sections. Set principal goes.
  function p7TriageCase() {
    if (state.id !== 'triage-record') return;
    var main = $('#main-content');
    var header = $('.page-header', main);
    var liveRibbon = $('.triage-ribbon', main);
    var recordBar = $('.triage-record-actions', main);
    var body = $('.record-body', main);
    if (!main || !liveRibbon || !body) return;

    var css = el('link', { rel: 'stylesheet', href: '../assets/css/case-workspace.css' });
    document.head.appendChild(css);

    var registration = '';
    var opened = '';
    var assignee = '';
    $$('.ribbon-item', liveRibbon).forEach(function (item) {
      var label = ($('.ribbon-label', item) || { textContent: '' }).textContent.trim();
      var value = ($('.ribbon-value', item) || { textContent: '' }).textContent.trim();
      if (label === 'Registration') registration = value;
      if (label === 'Opened') opened = value;
      if (label === 'Assignee') assignee = value;
    });
    var stateChip = $('.ribbon-chips .status', liveRibbon) || el('span', { 'class': 'status status--navy' }, ['Open']);
    var principal = '';
    $$('.triage-source-panel dl.definition').forEach(function (definition) {
      var term = $('dt', definition);
      var value = $('dd', definition);
      if (!term || !value) return;
      if (term.textContent.trim() === 'Principal') principal = value.textContent.trim();
      // The Triage's reference is now its Case/PO, labelled as the Case card labels it.
      if (term.textContent.trim() === 'Triage reference') { term.textContent = 'Our ref'; mark(term, 'P7'); }
    });

    function ribbonItem(label, value, mono) {
      return el('div', { 'class': 'ribbon-item' }, [
        el('div', { 'class': 'ribbon-label' }, [label]),
        el('div', { 'class': 'ribbon-value' + (mono ? ' mono' : '') }, [el('span', {}, [value])])
      ]);
    }

    // Ribbon actions: Edit (the Triage edit scope, unchanged) and one Actions menu.
    var editForm = recordBar ? $('form[action*="handler=Edit"]', recordBar) : null;
    if (editForm) {
      var editButton = $('button', editForm);
      editButton.className = 'btn btn--dark';
      editButton.textContent = '';
      editButton.appendChild(icon('pencil'));
      editButton.appendChild(el('span', {}, [OPTIONS.triageedit === 'triage' ? 'Edit Triage' : 'Edit Case']));
    }
    var menuBody = el('div', { 'class': 'menu-body menu-body--end' });
    var assignMe = recordBar ? $('form[data-triage-assign-to-me]', recordBar) : null;
    if (assignMe) { $('button', assignMe).className = 'btn'; menuBody.appendChild(assignMe); }
    var assignEngineer = recordBar ? $('[data-dialog-open="triage-assign-dialog"]', recordBar) : null;
    if (assignEngineer) menuBody.appendChild(assignEngineer);
    var actions = el('details', { 'class': 'menu', 'data-menu': '' }, [
      el('summary', { 'class': 'btn' }, [el('span', {}, ['Actions']), icon('chevron-down')]),
      menuBody
    ]);
    var ribbonActions = el('div', { 'class': 'ribbon-actions' }, [editForm || el('span'), actions]);

    var ribbon = el('div', { 'class': 'ribbon', 'aria-label': 'Case identity' }, [
      el('div', { 'class': 'ribbon-facts' }, [
        el('div', { 'class': 'ribbon-item ribbon-ref' }, [
          el('div', { 'class': 'ribbon-label' }, ['Case workspace · ' + registration]),
          el('h1', { 'class': 'ribbon-value' }, [TRIAGE_REFERENCE])
        ]),
        ribbonItem('Principal', principal),
        ribbonItem('Assignee', assignee || 'Unassigned'),
        ribbonItem('Opened', opened),
        el('div', { 'class': 'ribbon-chips', 'aria-label': 'State' }, [
          stateChip,
          el('span', { 'class': 'status status--navy status--plain', 'data-case-type-chip': '' }, ['Triage'])
        ])
      ]),
      ribbonActions
    ]);

    // Sections: the live panels, in the order the work runs, as record sections.
    var panels = {
      triage: $('.triage-resolution-panel', body),
      source: $('.triage-source-panel', body),
      notes: $$('section.panel', main).filter(function (panel) { var h = $('h2', panel); return h && h.textContent.trim() === 'Notes'; })[0]
    };
    var extra = $$('section.panel', body).filter(function (panel) {
      return panel !== panels.triage && panel !== panels.source && panel !== panels.notes;
    });
    // The live order: site.css puts the Source panel first (.triage-source-panel{order:-1}).
    var order = [['source', 'Source', 'file-text', panels.source], ['triage', 'Determinations', 'clipboard-list', panels.triage]];
    extra.forEach(function (panel, index) {
      var h = $('h2', panel);
      order.push(['extra-' + index, h ? h.textContent.trim() : 'Section', 'mail', panel]);
    });

    // Files: the Case's Files section, with upload (standard Case custody).
    var files = el('section', { 'class': 'record-section panel', id: 'section-files', 'data-section': 'files', 'aria-labelledby': 'section-files-title' }, [
      el('div', { 'class': 'panel-head' }, [
        el('h2', { id: 'section-files-title' }, ['Files']),
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
    order.push(['files', 'Files', 'folder', files]);
    order.push(['notes', 'Notes', 'history', panels.notes]);

    var nav = el('nav', { 'class': 'section-nav', 'aria-label': 'Case sections' });
    var workspaceMain = el('div', { 'class': 'workspace-main', id: 'case-main' });
    order.forEach(function (entry, index) {
      var panel = entry[3];
      if (!panel) return;
      panel.classList.add('record-section');
      if (panel.id !== 'section-' + entry[0]) panel.id = 'section-' + entry[0];
      workspaceMain.appendChild(panel);
      var link = el('a', { 'class': 'section-link', href: '#section-' + entry[0], 'aria-current': index === 0 ? 'true' : 'false' }, [icon(entry[2]), el('span', {}, [entry[1]])]);
      nav.appendChild(link);
    });
    var refresh = header ? $('form[data-refresh-form]', header) : null;
    var tools = el('div', { 'class': 'section-tools' }, refresh ? [refresh] : []);
    if (refresh) {
      var refreshButton = $('button', refresh);
      refreshButton.className = 'btn btn--icon btn--small';
      refreshButton.textContent = '';
      refreshButton.setAttribute('aria-label', 'Refresh');
      refreshButton.appendChild(icon('refresh-cw'));
    }

    var record = el('article', { 'class': 'record case-record', 'data-case-record': '', 'data-layout': 'scroll', 'data-v29-triage-case': '' }, [
      el('div', { 'class': 'sticky-block', 'data-sticky-block': '' }, [ribbon, el('div', { 'class': 'section-row' }, [nav, tools])]),
      el('div', { 'class': 'workspace workspace--single' }, [workspaceMain])
    ]);

    var content = $('.content', main) || main;
    content.insertBefore(mark(record, 'P7'), content.firstChild);
    // Set principal goes: the Principal is a Case's identity (item T).
    $$('[data-dialog-open="triage-principal-dialog"]', record).forEach(function (button) { removed(button, 'P7'); });
    [header, liveRibbon, recordBar, body].forEach(function (node) { if (node && node.parentNode) node.parentNode.removeChild(node); });
    bindLive(record);
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
    if (state.id === 'triage-record') {
      var main = $('#main-content');
      if (main) {
        var live = state.live || '';
        main.setAttribute('data-record-href', live.replace(/^\/Triage\//, '/Cases/'));
        main.setAttribute('data-record-kind', 'case');
      }
    }
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

  var PROPOSALS = [
    ['P1', p1Views], ['P2', p2AuditReference], ['P3', p3InspectionReadOnly], ['P4', p4Report],
    ['P5', p5CreateAudit], ['P6', p6AuditFolder], ['P7', p7TriageCase], ['P8', p8TriagesMetric],
    ['P9', p9TriageIdentity], ['P10', p10Rail], ['P11', p11CreateTriage], ['P12', p12OpenTriage],
    ['P13', p13SearchTriage]
  ];
  function run() {
    PROPOSALS.forEach(function (proposal) {
      if (!on(proposal[0])) return;
      try { proposal[1](); } catch (error) { console.error('v29 proposal ' + proposal[0] + ' failed: ' + error.message); }
    });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run); else run();
})();
