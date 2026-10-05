"""Offline evidence for the three v31 Triage Case mockups (not application evidence).

Drives every design through every preset, the main flows and keyboard
behaviour, prints RESULT {"fail":[],"okCount":N}, then captures the
screenshot set. Uses the workstation's existing Playwright.
"""
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from playwright.sync_api import sync_playwright

current = Path(__file__).resolve().parent
shots = current / 'v31-triage-shots'
shots.mkdir(exist_ok=True)
designs = ['a', 'b', 'c']
presets = ['open', 'assigned', 'awaiting', 'finding', 'completed', 'replied', 'cancelled', 'upload', 'blocked', 'unknown', 'response', 'noimages', 'many']
shot_states = ['open', 'finding', 'completed', 'cancelled', 'upload', 'blocked', 'response', 'noimages', 'many']
shot_dialogs = ['menu', 'composer', 'triage-determinations-dialog', 'case-message-m1', 'viewer']
fails, ok = [], [0]
errors, external = [], []


def check(name, cond):
    if cond:
        ok[0] += 1
    else:
        fails.append(name)


def url(design, query):
    return (current / f'pegasus_triage_case_{design}_v31.html').as_uri() + '?' + query


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

    def menu_labels():
        return page.eval_on_selector_all('[data-triage-actions] .menu-body button', 'els=>els.map(e=>e.textContent.trim())')

    def lead_label():
        return page.eval_on_selector_all('.ribbon-actions > .btn--primary', 'els=>els.map(e=>e.textContent.trim())')

    # Every design x preset: structure the operator asked for.
    for d in designs:
        for state in presets:
            for primary in ['on', 'off']:
                load(d, f'state={state}&opt=primary:{primary}')
                tag = f'{d}/{state}/{primary}'
                st = page.evaluate('window.triageMockup.state()')
                dv = page.evaluate('window.triageMockup.derived()')
                labels = menu_labels() + lead_label()
                check(f'{tag}: Actions menu present', page.locator('[data-triage-actions]').count() == 1)
                check(f'{tag}: no Open message outside Correspondence', page.evaluate("[...document.querySelectorAll('#tv-root a,#tv-root button')].filter(e=>e.textContent.trim()==='Open message'&&!e.closest('[data-correspondence]')).length") == 0)
                check(f'{tag}: no inline chaser form', page.locator('#triage-reply-to,[data-triage-reply-form]').count() == 0)
                check(f'{tag}: no Assign button in the ribbon facts', page.locator('.ribbon-facts button').count() == 0)
                check(f'{tag}: no record bar', page.locator('.record-bar').count() == 0)
                check(f'{tag}: Files has a Correspondence tab', page.locator('[data-files-tab="correspondence"]').count() == 1)
                check(f'{tag}: vehicle images iff images', (page.locator('[data-vehicle-images]').count() > 0) == (len(st['images']) > 0))
                check(f'{tag}: Cancel Triage iff mutable', ('Cancel Triage' in labels) == dv['mutable'])
                check(f'{tag}: Reopen iff settled', ('Reopen' in labels) == (not dv['mutable']))
                check(f'{tag}: Record finding iff mutable', ('Record finding' in labels) == dv['mutable'])
                check(f'{tag}: no Await information', 'Await information' not in labels)
                check(f'{tag}: no Crop or Tag controls', page.evaluate("[...document.querySelectorAll('button,select,label')].filter(e=>/^(Crop|Tag|Add a tag)$/.test(e.textContent.trim())).length") == 0)
                check(f'{tag}: Assign/Reassign iff mutable', (('Assign' in labels) or ('Reassign' in labels)) == dv['mutable'])
                check(f'{tag}: Link/Unlink iff mutable', (('Link case' in labels) or ('Unlink case' in labels)) == dv['mutable'])
                check(f'{tag}: Send chaser iff e-mail chaser and not blocked', ('Send chaser' in labels) == bool(dv['canSend'] and dv['purpose'] == 'chaser'))
                check(f'{tag}: Reply with finding iff Completed and sendable', ('Reply with finding' in labels) == bool(dv['canSend'] and dv['purpose'] == 'outcome'))
                check(f'{tag}: Complete Triage iff Finding recorded', ('Complete Triage' in labels) == (st['state'] == 'Finding recorded'))
                check(f'{tag}: Record correction iff Completed with finding', ('Record correction' in labels) == bool(st['state'] == 'Completed' and st['finding']))
                check(f'{tag}: danger item is last', page.evaluate("(()=>{const b=[...document.querySelectorAll('[data-triage-actions] .menu-body .btn--danger')];return b.length===0||b[0]===document.querySelector('[data-triage-actions] .menu-body').lastElementChild})()"))
                check(f'{tag}: primary off leaves no lead', primary == 'on' or len(lead_label()) == 0)
                check(f'{tag}: correspondence rows match e-mail source', (page.locator('[data-correspondence-row]').count() > 0) == st['email'])

    for d in designs:
        # Determinations -> Complete -> Reply with outcome through the composer.
        load(d, 'state=assigned&opt=primary:off')
        page.click('[data-triage-actions] summary')
        page.click('[data-action-key="determinations"]')
        check(f'{d}: determinations dialog opens with focus on Roadworthiness', page.evaluate("document.activeElement.id==='triage-det-road'"))
        page.select_option('#triage-det-road', 'Roadworthy')
        page.select_option('#triage-det-repair', 'Repairable')
        page.fill('#triage-det-reason', 'Panel damage only.')
        page.click('[data-dialog="triage-determinations-dialog"] button[type=submit]')
        check(f'{d}: finding recorded', page.evaluate("window.triageMockup.state().state") == 'Finding recorded')
        check(f'{d}: notice Finding recorded.', 'Finding recorded.' in page.inner_text('[data-notice]'))
        check(f'{d}: read-out shows Roadworthy', 'Roadworthy' in page.inner_text('[data-triage-readout]'))
        page.click('[data-triage-actions] summary')
        page.click('[data-action-key="complete"]')
        check(f'{d}: completed', page.evaluate("window.triageMockup.state().state") == 'Completed')
        before = page.locator('[data-correspondence-row]').count()
        page.click('[data-notice] [data-composer-open]')
        check(f'{d}: notice Reply with finding opens composer', page.locator('[data-composer]').is_visible())
        check(f'{d}: composer titled Reply with finding', page.inner_text('#mail-compose-title') == 'Reply with finding')
        for _ in range(14):
            page.keyboard.press('Tab')
            check(f'{d}: Tab stays in composer', page.evaluate("document.querySelector('[data-composer]').contains(document.activeElement)"))
        page.click('[data-composer] button[type=submit]')
        check(f'{d}: Reply sent.', 'Reply sent.' in page.inner_text('[data-notice]'))
        check(f'{d}: correspondence row added', page.locator('[data-correspondence-row]').count() == before + 1)

        # Chaser through the menu, then Escape returns focus to Actions.
        load(d, 'state=open&opt=primary:off')
        page.click('[data-triage-actions] summary')
        page.click('[data-action-key="chaser"]')
        check(f'{d}: Send chaser opens composer', page.inner_text('#mail-compose-title') == 'Send chaser')
        check(f'{d}: composer focuses To', page.evaluate("document.activeElement.id==='tv-to'"))
        page.keyboard.press('Escape')
        check(f'{d}: Escape closes composer', not page.locator('[data-composer]').is_visible())
        check(f'{d}: focus returns to Actions', page.evaluate("document.activeElement===document.querySelector('[data-triage-actions] summary')"))
        page.click('[data-triage-actions] summary')
        page.click('[data-action-key="chaser"]')
        page.click('[data-composer] button[type=submit]')
        check(f'{d}: Chaser sent.', 'Chaser sent.' in page.inner_text('[data-notice]'))

        # Assign, Link case, Cancel, Reopen.
        page.click('[data-triage-actions] summary')
        page.click('[data-action-key="assign"]')
        page.select_option('#triage-assignee', 'R. Khan')
        page.click('[data-dialog="triage-assign-dialog"] button[type=submit]')
        check(f'{d}: assigned in ribbon', page.inner_text('[data-ribbon-assignee]') == 'R. Khan')
        check(f'{d}: menu now says Reassign', 'Reassign' in menu_labels())
        page.click('[data-triage-actions] summary')
        page.click('[data-action-key="link"]')
        page.fill('#triage-link-case-id', 'QDOS26214')
        page.fill('#triage-link-case-reason', 'Same vehicle.')
        page.click('[data-dialog="triage-link-case-dialog"] button[type=submit]')
        check(f'{d}: Case linked', 'Case linked.' in page.inner_text('[data-notice]') and 'QDOS26214' in page.inner_text('.ribbon'))
        page.click('[data-triage-actions] summary')
        page.click('[data-action-key="cancel"]')
        page.fill('#triage-cancel-dialog_reason', 'Duplicate.')
        page.click('[data-dialog="triage-cancel-dialog"] button[type=submit]')
        check(f'{d}: cancelled', page.evaluate("window.triageMockup.state().state") == 'Cancelled')
        check(f'{d}: cancelled offers Reopen', 'Reopen' in menu_labels() + lead_label())

        # Message dialog from the Correspondence tab and the viewer.
        load(d, 'state=awaiting&files=correspondence&tab=files')
        page.click('[data-message-open="m2"]')
        check(f'{d}: message dialog opens', page.locator('[data-dialog="case-message-m2"]').is_visible())
        check(f'{d}: message dialog has Reply, Reply all, Forward', all(x in page.inner_text('[data-dialog="case-message-m2"] .dialog-foot') for x in ['Reply', 'Reply all', 'Forward', 'Open full message']))
        page.keyboard.press('Escape')
        check(f'{d}: focus returns to Open message', page.evaluate("document.activeElement.dataset.messageOpen==='m2'"))
        load(d, 'state=open')
        if d == 'c':
            page.click('[data-viewer-open="2"]')
        else:
            page.click('[data-vehicle-images] [data-viewer-open]')
        check(f'{d}: viewer opens', page.locator('[data-viewer]').is_visible())
        page.keyboard.press('ArrowRight')
        check(f'{d}: viewer pages on ArrowRight', page.locator('[data-viewer-count]').inner_text().startswith('2 /' if d != 'c' else '4 /'))
        page.keyboard.press('Escape')
        check(f'{d}: Escape closes viewer', not page.locator('[data-viewer]').is_visible())
        page.click('[data-triage-actions] summary')
        page.keyboard.press('Escape')
        check(f'{d}: Escape closes the Actions menu', not page.evaluate("document.querySelector('[data-triage-actions]').open"))

    # Design-specific behaviour.
    load('b', 'state=open')
    page.click('[data-goto-correspondence]')
    check('b: card link opens the Correspondence tab', page.get_attribute('[data-files-tab="correspondence"]', 'aria-selected') == 'true')
    for d in designs:
        # Record finding with both tickboxes: finding, completion, then the reply composer.
        load(d, 'state=open&opt=primary:off')
        page.click('[data-triage-actions] summary')
        page.click('[data-action-key="determinations"]')
        check(f'{d}: dialog titled Record finding', page.inner_text('#triage-determinations-dialog-title') == 'Record finding')
        page.check('#triage-det-reply')
        check(f'{d}: Reply with finding ticks Complete Triage', page.is_checked('#triage-det-complete'))
        page.uncheck('#triage-det-complete')
        check(f'{d}: unticking Complete Triage clears Reply with finding', not page.is_checked('#triage-det-reply'))
        page.check('#triage-det-reply')
        page.select_option('#triage-det-road', 'Unroadworthy')
        page.fill('#triage-det-reason', 'Structural damage.')
        page.click('[data-dialog="triage-determinations-dialog"] button[type=submit]')
        check(f'{d}: ticked Record finding completes', page.evaluate("window.triageMockup.state().state") == 'Completed')
        check(f'{d}: ticked Reply opens the composer', page.locator('[data-composer]').is_visible() and page.inner_text('#mail-compose-title') == 'Reply with finding')
        page.keyboard.press('Escape')
        load(d, 'state=upload')
        page.click('[data-triage-actions] summary')
        page.click('[data-action-key="determinations"]')
        check(f'{d}: no Reply with finding without e-mail', page.locator('#triage-det-reply').count() == 0)
    load('c', 'state=open')
    page.focus('[data-page-tab="images"]')
    page.keyboard.press('ArrowRight')
    check('c: ArrowRight moves to Files', page.get_attribute('[data-page-tab="files"]', 'aria-selected') == 'true')
    page.keyboard.press('End')
    check('c: End moves to Notes', page.get_attribute('[data-page-tab="notes"]', 'aria-selected') == 'true')
    load('c', 'state=noimages')
    check('c: no Images tab without images', page.locator('[data-page-tab="images"]').count() == 0)
    load('a', 'state=open')
    page.click('[data-pick="3"]')
    check('a: filmstrip picks the lead image', '4 / 7' in page.inner_text('.tv-count'))

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
            number = index * 3 + di + 1
            for width, height in [(1580, 1000), (1440, 900), (760, 1000)]:
                page.set_viewport_size({'width': width, 'height': height})
                load(d, f'state={state}&embed=1')
                page.evaluate('document.fonts.ready')
                page.screenshot(path=str(shots / f'{number:02}-{d}-{state}-{width}.png'))
                count += 1
    base = len(shot_states) * 3
    for index, dialog in enumerate(shot_dialogs):
        for di, d in enumerate(designs):
            number = base + index * 3 + di + 1
            page.set_viewport_size({'width': 1440, 'height': 900})
            state = 'awaiting' if dialog == 'case-message-m1' else 'open'
            load(d, f'state={state}&embed=1&dialog={dialog}')
            page.screenshot(path=str(shots / f'{number:02}-{d}-dialog-{dialog}-1440.png'))
            count += 1
    for d in designs:
        page.set_viewport_size({'width': 1580, 'height': 1000})
        load(d, 'state=finding&embed=1')
        page.screenshot(path=str(shots / f'full-{d}-1580.png'), full_page=True)
    browser.close()

record = {'recordedUtc': datetime.now(timezone.utc).isoformat(), 'scope': 'Offline Triage Case mockups only; not application evidence', 'selfcheck': result, 'screenshots': count, 'fullPage': 3, 'consoleErrors': errors, 'externalRequests': external}
(shots / 'verification.json').write_text(json.dumps(record, indent=2) + '\n', encoding='utf-8')
print(f'Saved {count} screenshots and 3 full-page captures.')
