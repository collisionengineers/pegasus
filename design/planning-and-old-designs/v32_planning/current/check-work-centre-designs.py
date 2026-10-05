"""Offline evidence for the five v32 Work Centre mockups (not application evidence).

Drives every design through every preset, the Dismiss, Assign, Complete and
keyboard flows, the copy allow-list and the 760px geometry, prints
RESULT {"fail":[],"okCount":N}, then captures the screenshot set. Uses the
workstation's existing Playwright. `--no-shots` stops after the RESULT line.
"""
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from playwright.sync_api import sync_playwright

current = Path(__file__).resolve().parent
shots = current / 'v32-work-centre-shots'
shots.mkdir(exist_ok=True)
designs = ['a', 'b', 'c', 'd', 'e']
presets = ['default', 'mine', 'filtered', 'find', 'selected', 'dismissed', 'empty-office', 'empty-mine', 'attention-unavailable', 'ai-unavailable', 'stats-unavailable', 'all-empty', 'assign', 'new-cases', 'ai-jobs']
shot_states = ['default', 'mine', 'filtered', 'selected', 'dismissed', 'empty-office', 'attention-unavailable', 'all-empty']
widths = [(1580, 1000), (1440, 900), (760, 1000)]
fails, ok = [], [0]
errors, external = [], []
banned = ['intake', 'queue', 'lease', 'provider', 'bounded', 'projection']
fixtures_text = '\n'.join(line for line in (current / 'lib' / 'work-centre-fixtures.mjs').read_text(encoding='utf-8').split('\n') if not line.lstrip().startswith('//'))

# Copy allow-list: every visible text node must be a live label, a fixture
# value or one of the counted / timed patterns. Anything else is invented copy.
allowed_exact = set(re.findall(r"'((?:[^'\\]|\\.)*)'", fixtures_text))
allowed_exact |= {
    'Work Centre', 'Office-wide work', 'Create Case', 'Refresh', 'Refreshing…', 'Refreshing', 'Needs attention', 'Office', 'Mine',
    'Find in Needs attention', 'Find in this work', 'Clear filters', 'Nothing needs attention', 'No work matches these filters.', 'No work to show.',
    'Next action', 'Record / detail', 'Owner', 'Due', 'Received', 'Assign to me', 'Assign Engineer', 'Assign', 'Reassign', 'Choose an Engineer', 'Engineer',
    'Not recorded', 'Work Centre is unavailable. Refresh to run the live queues again.', 'New cases', 'Changed by automation', 'Since you last looked',
    'No Case was created in the last 7 days', 'New cases are unavailable.', 'AI jobs', 'No AI job is waiting', 'AI jobs are unavailable.', 'No reason recorded',
    'Open Case', 'No Engineer', 'No owner', 'Previous', 'Next', 'Complete job', 'Review estimate', 'Open query', 'Review', 'Dismiss', 'Cancel', 'Close',
    'Reference', 'Registration', 'Claimant', 'Principal', 'Vehicle', 'Source', 'Sender', 'State', 'Missing', 'Chase', 'Image reference', 'Last 7 days',
    'Not ready', 'Held', 'Unidentified', 'Triages', 'Case', 'Unassigned', 'Vehicle images paired', 'Triage', 'AI draft', 'Overdue', 'Due today', 'Later',
    'Page 1 of 1 · earliest due first', 'Page 1 of 1 · newest first', 'Today', 'This week', 'Activity', 'Activity is unavailable.', 'Kind', 'Mockup destination',
    'Action', 'Route', 'Taken', 'Queued', 'Failed', 'Draft ready', 'Shown in the strip above', 'Arrived', 'Waiting', 'Out', 'With AI', 'Detail', 'Arrival',
    'Job', 'Instruction', 'Record', 'Started', 'Note', 'New since you last looked', 'AI jobs in progress', 'My AI jobs', 'New case', 'AI job', '—', '·', '',
    # Shell (rail, utility bar, account and notifications dialogs) from the live layout.
    'PEGASUS', 'Case management', 'Work', 'Manage', 'Inbox', 'Upload', 'Cases', 'Search', 'Administration', 'Collapse', 'Skip to main content', 'Ctrl K',
    'alex', 'Administrator', 'Engineer', 'User', 'Account', 'Name', 'Role', 'Idle lock', '30 minutes', 'Release notes', 'Report a problem', 'Change password', 'Sign out',
    'Notifications', 'Unread', 'Mark all read', 'A', 'Search Pegasus', 'Current', 'QDOS26010', 'BH17RZV', 'Assigned to you', '09:12', 'QDOS25990', 'LK21XYZ', 'Estimate draft ready', 'Yesterday 16:40',
}
allowed_patterns = [
    re.compile(r'^(Overdue|Due today|Later|New since you last looked|AI jobs in progress) \((\d+|—)\)$'),
    re.compile(r'^\d+ (item|items|row|rows)$'), re.compile(r'^Last 7 days · \d+ (row|rows)$'), re.compile(r'^\d+ draft ready · \d+ failed$'),
    re.compile(r'^(· )?Started by [A-Za-z. ]+( ·)?$'), re.compile(r'^(· )?(Today|This week)$'), re.compile(r'^(Taken until|Lease expires) \d\d:\d\d$'), re.compile(r'^(Updated|Current ·) \d\d:\d\d$'),
    re.compile(r'^\d+$'), re.compile(r'^\d\d [A-Z][a-z]{2} \d{4} \d\d:\d\d$'), re.compile(r'^[A-Z][A-Za-z ]+ \(\d+\)$'), re.compile(r'^Dismiss .+$'),
    re.compile(r'^Assign Engineer · .+$'), re.compile(r'^New cases \(\d+\)$'), re.compile(r'^Current · \d\d:\d\d$'), re.compile(r'^\d+ outstanding$'),
    re.compile(r'^[A-Za-z0-9 .·\-\u2019]+ · (Today|This week)( \d+)?( · This week \d+)?$'),
]
allowed_exact = {v.replace('\\u2019', '\u2019').replace('\\u00b7', '\u00b7') for v in allowed_exact}
subject_parts = set()
for value in list(allowed_exact):
    for part in value.split(' · '):
        subject_parts.add(part.strip())
allowed_exact |= subject_parts


def check(name, cond):
    if cond:
        ok[0] += 1
    else:
        fails.append(name)


def url(design, query):
    return (current / f'pegasus_work_centre_{design}_v32.html').as_uri() + '?' + query


with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=['--allow-file-access-from-files'])
    context = browser.new_context(viewport={'width': 1580, 'height': 1000}, device_scale_factor=1)

    def block(route):
        external.append(route.request.url)
        route.abort()

    context.route('http://**', block)
    context.route('https://**', block)
    page = context.new_page()
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.on('console', lambda m: errors.append(m.text) if m.type == 'error' else None)

    def load(design, query):
        page.goto(url(design, query))
        page.wait_for_function('window.mockupReady===true')

    def texts():
        return page.evaluate("""() => { const out = []; const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT); let node; while ((node = walker.nextNode())) { const t = node.textContent.replace(/\\s+/g, ' ').trim(); if (!t) continue; const el = node.parentElement; if (el.closest('.wc-mock, script, style')) continue; out.push(t); } return out; }""")

    def visible_dismiss(row):
        return row.evaluate("""r => { if (r.closest('[hidden]')) return 'ok'; const b = [...r.querySelectorAll('[data-dismiss]')]; if (b.length !== 1) return 'count ' + b.length; const el = b[0]; const cs = getComputedStyle(el); if (el.hidden || el.offsetParent === null || cs.visibility === 'hidden' || Number(cs.opacity) < 1) return 'hidden'; const controls = [...r.querySelectorAll('a,button')].filter(c => !c.closest('[data-wc-detail]')); if (controls[controls.length - 1] !== el) return 'not last'; const ref = r.getAttribute('data-wc-ref'); if (el.getAttribute('aria-label') !== 'Dismiss ' + ref) return 'label ' + el.getAttribute('aria-label'); return 'ok'; }""")

    def strip_rows():
        return page.evaluate("""() => [...document.querySelectorAll('[data-wc-record]')].filter(r => !r.closest('[hidden]'))""")

    try:
        for d in designs:
            for state in presets:
                load(d, f'state={state}')
                tag = f'{d}/{state}'
                st = page.evaluate('window.workCentreMockup.state()')
                check(f'{tag}: one h1 Work Centre', page.eval_on_selector_all('h1', 'els=>els.map(e=>e.textContent.trim())') == ['Work Centre'])
                check(f'{tag}: one Create Case primary in the header', page.eval_on_selector_all('.page-actions .btn--primary', 'els=>els.map(e=>e.textContent.trim())') == ['Create Case'])
                check(f'{tag}: no utility New case', page.locator('.utility-actions a.btn').count() == 0)
                check(f'{tag}: no Operations rail link', 'Operations' not in page.inner_text('.primary-nav'))
                # Dismiss on every row, in every list.
                rows = page.locator('[data-wc-record]')
                verdicts = [visible_dismiss(rows.nth(i)) for i in range(rows.count())]
                check(f'{tag}: every row ends in one visible Dismiss ({verdicts})', all(v == 'ok' for v in verdicts))
                check(f'{tag}: no Dismiss inside the open row', page.locator('[data-wc-detail] [data-dismiss]').count() == 0)
                if state == 'selected':
                    check(f'{tag}: the open row shows facts and actions', page.locator('[data-wc-detail] .fact').count() == 6 and page.locator('[data-wc-detail] [data-wc-actions] a,[data-wc-detail] [data-wc-actions] button').count() >= 1)
                # Metrics.
                if state not in ('attention-unavailable',):
                    hrefs = dict(page.eval_on_selector_all('[data-metric]', 'els=>els.map(e=>[e.getAttribute("data-metric"),e.getAttribute("href")])'))
                    check(f'{tag}: five metrics link to their Cases tab', hrefs == {k: f'/Cases?tab={k}' for k in ['not_ready', 'review', 'held', 'unidentified', 'triage']})
                    values = dict(page.eval_on_selector_all('[data-metric]', 'els=>els.map(e=>[e.getAttribute("data-metric"),(e.querySelector(".metric-value")||e.querySelector("strong")).textContent.trim()])'))
                    check(f'{tag}: metric figures', values == dict(zip(['not_ready', 'review', 'held', 'unidentified', 'triage'], ['0'] * 5 if state == 'all-empty' else ['7', '4', '2', '3', '2'])))
                else:
                    check(f'{tag}: unavailable attention renders no metric figure', page.locator('[data-metric]').count() == 0 and '0' not in page.eval_on_selector_all('.metric-value', 'els=>els.map(e=>e.textContent)'))
                # Activity.
                if state == 'stats-unavailable':
                    check(f'{tag}: activity unavailable shows the notice and no digits', page.locator('[data-wc-activity] .notice').count() >= 1 and page.locator('[data-figure]').count() == 0)
                else:
                    check(f'{tag}: activity section present', page.locator('[data-wc-activity]').count() >= 1)
                    text = page.evaluate("document.querySelector('#wc-root').textContent")
                    check(f'{tag}: activity figures present', all(x in text for x in ['Sent to Engineer', 'Reports sent', 'Completed', '17', '11', '9']))
                    check(f'{tag}: New cases and E-mails figures present', all(x in text for x in ['E-mails received', '18', 'New cases']))
                # One clock.
                updated = re.findall(r'Updated \d\d:\d\d', page.inner_text('#wc-root'))
                check(f'{tag}: one Updated clock in the header', len(updated) == 1)
                # Copy and vocabulary.
                lower = page.evaluate("document.querySelector('#wc-root').textContent").replace('Work Centre is unavailable. Refresh to run the live queues again.', '').replace('Queued', '').lower()
                check(f'{tag}: no banned word', not any(re.search(r'\b' + w + r'\b', lower) for w in banned))
                def known(t):
                    if t in allowed_exact or any(p.match(t) for p in allowed_patterns):
                        return True
                    parts = [x.strip() for x in t.split(' · ')]
                    return len(parts) > 1 and all(x in allowed_exact or any(p.match(x) for p in allowed_patterns) for x in parts)
                unknown = [t for t in texts() if not known(t)]
                check(f'{tag}: every visible text is a label or a fixture value ({unknown[:6]})', not unknown)
                check(f'{tag}: every status chip carries text', all(page.eval_on_selector_all('.status', 'els=>els.map(e=>e.textContent.trim().length>0)')))
                # Empty states.
                if state == 'all-empty':
                    check(f'{tag}: all empty reads No work to show. with activity still drawn', page.locator('[data-wc-nothing]').count() == 1 and page.locator('[data-wc-activity]').count() >= 1)
                    check(f'{tag}: all empty draws no list', page.locator('[data-wc-record]').count() == 0)
                else:
                    check(f'{tag}: No work to show. only when all empty', page.locator('[data-wc-nothing]').count() == 0)
                if state == 'empty-office':
                    check(f'{tag}: empty office draws no attention row and says Nothing needs attention or omits the section', page.locator('[data-wc-row]').count() == 0 and (d == 'd' or 'Nothing needs attention' in page.evaluate("document.querySelector('#wc-root').textContent") or page.locator('[data-wc-section="attention"]').count() == 0))
                    check(f'{tag}: empty office keeps New cases and AI jobs', page.locator('[data-wc-arrival]').count() == {'b': 2, 'd': 2}.get(d, 7) and page.locator('[data-wc-job]').count() == {'d': 3, 'e': 3}.get(d, 5))
                if state == 'empty-mine':
                    check(f'{tag}: empty Mine keeps the scope switch', page.locator('[data-wc-scope-link="office"]').count() == 1 and page.locator('[data-wc-row]').count() == 0 and (d == 'd' or 'Nothing needs attention' in page.evaluate("document.querySelector('#wc-root').textContent")))
                if state == 'mine':
                    check(f'{tag}: Mine lists seven rows', page.evaluate('window.workCentreMockup.visible().length') == 7)
                if state == 'filtered':
                    if d in ('b', 'd'):
                        check(f'{tag}: Kind select holds Held', page.eval_on_selector('[data-wc-kind-select]', 'e=>e.value') == 'held' and page.evaluate('window.workCentreMockup.visible().length') == 2)
                    else:
                        check(f'{tag}: two chips on and counts over the whole scope', page.locator('[data-wc-kind][aria-current="true"]').count() == 2 and page.inner_text('[data-wc-kind="case"] .n') == '2' and page.evaluate('window.workCentreMockup.visible().length') == 4)
                    check(f'{tag}: Clear filters offered', page.locator('[data-wc-clear]').count() == 1)
                if state == 'find':
                    check(f'{tag}: Find BH17RZV leaves one row', page.evaluate('window.workCentreMockup.visible()') == ['r5'])
                if state == 'dismissed':
                    check(f'{tag}: QDOS26203 left every list', page.locator('[data-wc-record="QDOS26203"]').count() == 0 and page.locator('[data-wc-record]').count() > 0)
                    check(f'{tag}: no notice after a dismissal', page.locator('.notice--success, .toast').count() == 0)
                if state == 'attention-unavailable':
                    check(f'{tag}: unavailable attention keeps its notice', 'Work Centre is unavailable' in page.inner_text('#wc-root'))
                    if d in ('a',):
                        check(f'{tag}: tab kept with a dash', page.inner_text('[data-wc-tab-link="attention"] .tab-count') == '—')
                if state == 'ai-unavailable':
                    check(f'{tag}: AI jobs unavailable notice', 'AI jobs are unavailable.' in page.evaluate("document.querySelector('#wc-root').textContent"))
                    if d == 'a':
                        check(f'{tag}: AI tab kept with a dash', page.inner_text('[data-wc-tab-link="ai-jobs"] .tab-count') == '—')
                if state == 'assign':
                    check(f'{tag}: assign dialog open with focus on the Engineer select', page.locator('#wc-assign-dialog:not([hidden])').count() == 1 and page.evaluate("document.activeElement.id==='wc-assign-engineer'"))
                if state == 'new-cases' and d == 'a':
                    check(f'{tag}: New cases tab selected', page.get_attribute('[data-wc-tab-link="new-cases"]', 'aria-selected') == 'true')

            # Option switches.
            load(d, 'state=default&opt=dismiss:text')
            check(f'{d}: text Dismiss variant names every row', all(t.startswith('Dismiss') for t in page.eval_on_selector_all('[data-dismiss]', 'els=>els.filter(e=>e.offsetParent!==null).map(e=>e.textContent.trim())')) and page.locator('[data-dismiss]:visible').count() == page.locator('[data-wc-record]:visible').count())
            load(d, 'state=default&opt=stats:strip')
            check(f'{d}: strip placement draws the five cells under the counts', page.locator('.wc-activity--strip [data-figure]').count() == 5)
            load(d, 'state=default&opt=clock:section')
            check(f'{d}: per-section clocks draw more than one Updated', len(re.findall(r'Updated \d\d:\d\d', page.evaluate("document.querySelector('#wc-root').textContent"))) > 1)
            load(d, 'state=default&opt=taken:lease')
            check(f'{d}: live wording switch shows Lease expires', 'Lease expires 09:55' in page.evaluate("document.querySelector('#wc-root').textContent"))
            load(d, 'state=default&role=Engineer')
            check(f'{d}: Engineer sees no Administration link', 'Administration' not in page.inner_text('.primary-nav'))
            if d == 'b':
                check('b: an Engineer opens on Mine', page.evaluate("window.workCentreMockup.state().scope") == 'mine')
                load(d, 'state=default&role=Administrator')
                check('b: an Administrator opens on Office', page.evaluate("window.workCentreMockup.state().scope") == 'office')

            # Dismiss flow: the record's rows leave every list; focus lands on the next row; counts update; no notice.
            load(d, 'state=default')
            before_rows = page.locator('[data-wc-record]').count()
            before_target = page.locator('[data-wc-record="QDOS26203"]').count()
            before_metrics = page.eval_on_selector_all('[data-metric]', 'els=>els.map(e=>(e.querySelector(".metric-value")||e.querySelector("strong")).textContent.trim())')
            count_meta = page.inner_text('[data-wc-count]') if page.locator('[data-wc-count]').count() else None
            page.click('[data-wc-record="QDOS26203"] [data-dismiss]:visible')
            check(f'{d}: dismissing QDOS26203 removes every row of the record', before_target >= 1 and page.locator('[data-wc-record="QDOS26203"]').count() == 0 and page.locator('[data-wc-record]').count() == before_rows - before_target)
            check(f'{d}: focus moved to a row or heading in the page', page.evaluate("document.activeElement !== document.body && document.querySelector('#wc-root').contains(document.activeElement)"))
            check(f'{d}: metrics unchanged by a dismissal', page.eval_on_selector_all('[data-metric]', 'els=>els.map(e=>(e.querySelector(".metric-value")||e.querySelector("strong")).textContent.trim())') == before_metrics)
            if count_meta:
                check(f'{d}: Needs attention count fell by one', page.inner_text('[data-wc-count]') != count_meta)
            check(f'{d}: no notice, no undo', page.locator('.notice--success, [data-undo]').count() == 0)
            page.click('[data-wc-record="j4"] [data-dismiss]:visible')
            check(f'{d}: dismissing the job removes its AI draft row and its job row', page.locator('[data-wc-record="j4"]').count() == 0 and page.locator('[data-wc-row="r7"]').count() == 0)
            # The last row of a list hands focus to the list's heading.
            load(d, 'state=ai-jobs')
            for record in ['j3', 'j5', 'j2', 'j1', 'j4']:
                control = page.locator(f'[data-wc-record="{record}"] [data-dismiss]:visible')
                if control.count():
                    control.first.click()
            check(f'{d}: the last job dismissed hands focus to a heading or row (' + str(page.locator('[data-wc-job]').count()) + ' jobs, focus ' + page.evaluate("document.activeElement.tagName + '#' + (document.activeElement.dataset.focus || document.activeElement.id || '')") + ')', page.locator('[data-wc-job]').count() == 0 and page.evaluate("document.activeElement !== document.body && document.querySelector('#wc-root').contains(document.activeElement)"))

            # Assign Engineer dialog: focus, containment, Escape, assignment.
            load(d, 'state=selected')
            page.click('[data-assign="r5"]')
            check(f'{d}: Assign Engineer opens its dialog on the select', page.evaluate("document.activeElement.id==='wc-assign-engineer'"))
            inside = True
            for _ in range(8):
                page.keyboard.press('Tab')
                inside = inside and page.evaluate("document.querySelector('#wc-assign-dialog').contains(document.activeElement)")
            check(f'{d}: Tab stays inside the dialog', inside)
            page.keyboard.press('Escape')
            check(f'{d}: Escape closes and returns focus to the opener', page.locator('#wc-assign-dialog:not([hidden])').count() == 0 and page.evaluate("document.activeElement?.dataset?.assign==='r5'"))
            page.click('[data-assign="r5"]')
            page.select_option('#wc-assign-engineer', 'R. Khan')
            page.click('#wc-assign-dialog .btn--primary')
            check(f'{d}: assigned row becomes Review Case owned by R. Khan', page.get_attribute('[data-wc-row="r5"]', 'data-wc-row-kind') == 'review' and 'R. Khan' in page.inner_text('[data-wc-row="r5"]'))
            load(d, 'state=mine')
            if page.locator('[data-take="r6"]').count() == 0:
                page.click('[data-select="r6"]')
            page.click('[data-take="r6"]')
            check(f'{d}: Assign to me takes the Triage', 'alex' in page.inner_text('[data-wc-row="r6"]'))
            # Complete job.
            load(d, 'state=ai-jobs')
            if page.locator('[data-complete="j4"]:visible').count() == 0:
                page.click('[data-select="r7"]')
            page.click('[data-complete="j4"]:visible')
            check(f'{d}: Complete job removes the job and its draft row', page.locator('[data-wc-record="j4"]').count() == 0)
            # Refresh keeps filters and selection.
            load(d, 'state=filtered&selected=r4')
            page.click('[data-refresh]')
            page.wait_for_selector('[data-wc-refresh-outcome-label]:has-text("Updated 09:42")')
            check(f'{d}: Refresh keeps filters and the open row', page.locator('[data-wc-detail]').count() == 1 and page.evaluate('window.workCentreMockup.state().kinds.length') >= 1)
            # Scope switch.
            load(d, 'state=default')
            page.click('[data-wc-scope-link="mine"]')
            check(f'{d}: Mine switch narrows to seven', page.evaluate('window.workCentreMockup.visible().length') == 7 and page.get_attribute('[data-wc-scope-link="mine"]', 'aria-current') == 'true')
            # Find.
            load(d, 'state=default')
            page.fill('[data-wc-search]', 'U14')
            check(f'{d}: Find narrows as you type and keeps focus', page.evaluate('window.workCentreMockup.visible().length') == 2 and page.evaluate("document.activeElement.id==='wc-search'"))
            # Tabs (A only).
            if d == 'a':
                load(d, 'state=default')
                page.focus('[data-wc-tab-link="attention"]')
                page.keyboard.press('ArrowRight')
                check('a: ArrowRight moves to New cases', page.get_attribute('[data-wc-tab-link="new-cases"]', 'aria-selected') == 'true')
                page.keyboard.press('End')
                check('a: End moves to AI jobs', page.get_attribute('[data-wc-tab-link="ai-jobs"]', 'aria-selected') == 'true')
            if d == 'd':
                load(d, 'state=default')
                page.click('[data-group-toggle="overdue"]')
                check('d: a group collapses', page.locator('[data-wc-row="r1"]').count() == 0 and page.get_attribute('[data-group-toggle="overdue"]', 'aria-expanded') == 'false')
                page.select_option('[data-wc-kind-select]', 'new-case')
                check('d: Kind New case shows only arrivals', page.locator('[data-wc-arrival]').count() == 2 and page.locator('[data-wc-row]').count() == 0)
            if d == 'e':
                load(d, 'state=default')
                check('e: four lanes', page.locator('.wc-lane').count() == 4)
                check('e: Waiting holds the chases, holds, images, Unidentified and Triage', page.locator('[data-lane="waiting"] [data-wc-row]').count() == 9)
                check('e: Review holds Review, Unassigned and AI drafts', page.locator('[data-lane="review"] [data-wc-row]').count() == 5)
            # Geometry at 760.
            page.set_viewport_size({'width': 760, 'height': 1000})
            load(d, 'state=default&embed=1')
            check(f'{d}: no horizontal page overflow at 760', page.evaluate('document.documentElement.scrollWidth <= 760'))
            check(f'{d}: Dismiss controls are at least 32px', all(h >= 32 for h in page.eval_on_selector_all('[data-dismiss]', 'els=>els.filter(e=>e.offsetParent!==null).map(e=>e.getBoundingClientRect().height)')))
            check(f'{d}: rows are at least 40px', all(h >= 40 for h in page.eval_on_selector_all('[data-wc-record]', 'els=>els.filter(e=>e.offsetParent!==null).map(e=>e.getBoundingClientRect().height)')))
            page.set_viewport_size({'width': 1580, 'height': 1000})

        # The live baseline documents the three treatments issue 1017 describes.
        load('live', 'state=default')
        check('live: attention rows carry no Dismiss', page.locator('[data-wc-row] [data-dismiss]').count() == 0)
        load('live', 'state=selected')
        check('live: Dismiss only inside the open row', page.locator('[data-wc-detail] [data-dismiss]').count() == 1)
        load('live', 'state=new-cases')
        check('live: New cases rows carry an icon Dismiss', page.locator('[data-wc-arrival] [data-dismiss].btn--icon').count() == 7)
        load('live', 'state=ai-jobs')
        check('live: AI job cards carry a text Dismiss', page.locator('[data-wc-job] [data-dismiss]').count() == 5 and all(t == 'Dismiss' for t in page.eval_on_selector_all('[data-wc-job] [data-dismiss]', 'els=>els.map(e=>e.textContent.trim())')))

    except Exception as exc:
        fails.append(f'exception during the design flows: {exc!r}')
    check('no console errors', not errors)
    check('no external requests', not external)
    result = {'fail': fails, 'okCount': ok[0]}
    print('RESULT ' + json.dumps(result), flush=True)
    if fails or '--no-shots' in sys.argv:
        browser.close()
        raise SystemExit(1 if fails else 0)

    count = 0
    for index, state in enumerate(shot_states):
        for di, d in enumerate(designs):
            number = index * 5 + di + 1
            for width, height in widths:
                page.set_viewport_size({'width': width, 'height': height})
                load(d, f'state={state}&embed=1')
                page.evaluate('document.fonts.ready')
                page.screenshot(path=str(shots / f'{number:02}-{d}-{state}-{width}.png'))
                count += 1
    for width, height in widths:
        page.set_viewport_size({'width': width, 'height': height})
        load('live', 'state=default&embed=1')
        page.screenshot(path=str(shots / f'00-live-default-{width}.png'))
        count += 1
    base = len(shot_states) * 5
    for di, d in enumerate(designs):
        page.set_viewport_size({'width': 1440, 'height': 900})
        load(d, 'state=assign&embed=1')
        page.screenshot(path=str(shots / f'{base + di + 1:02}-{d}-dialog-assign-1440.png'))
        count += 1
    for d in designs + ['live']:
        page.set_viewport_size({'width': 1580, 'height': 1000})
        load(d, 'state=selected&embed=1')
        page.screenshot(path=str(shots / f'full-{d}-1580.png'), full_page=True)
    browser.close()

record = {'recordedUtc': datetime.now(timezone.utc).isoformat(), 'scope': 'Offline Work Centre mockups only; not application evidence', 'selfcheck': result, 'screenshots': count, 'fullPage': 6, 'consoleErrors': errors, 'externalRequests': external}
(shots / 'verification.json').write_text(json.dumps(record, indent=2) + '\n', encoding='utf-8')
print(f'Saved {count} screenshots and 6 full-page captures.')
