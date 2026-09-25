"""Offline browser evidence for the five Upload proposals; no application build or tests."""
import json
from datetime import datetime, timezone
from pathlib import Path
from playwright.sync_api import sync_playwright

base = Path(__file__).resolve().parent
shots = base / 'v30-upload-new-shots'
shots.mkdir(exist_ok=True)
states = ['ready', 'select', 'chosen', 'uploading', 'processing', 'multiple', 'no-match',
          'search-error', 'mixed', 'unreadable', 'upload-error', 'conflict', 'single',
          'document', 'duplicate', 'attached', 'discarded', 'incomplete', 'confirm', 'discard']
fail, errors, external = [], [], []
ok_count = 0
capture_count = 0

def check(condition, label):
    global ok_count
    if condition:
        ok_count += 1
    else:
        fail.append(label)

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=['--allow-file-access-from-files'])
    context = browser.new_context(viewport={'width': 1440, 'height': 900}, device_scale_factor=1)
    def block(route):
        external.append(route.request.url)
        route.abort()
    context.route('http://**', block)
    context.route('https://**', block)
    page = context.new_page()
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.on('console', lambda msg: errors.append(msg.text) if msg.type == 'error' else None)

    def go(design, state='ready', embed=True):
        page.goto((base / f'pegasus_upload_{design}_new_v30.html').as_uri()
                  + f'?state={state}' + ('&embed=1' if embed else ''))
        page.wait_for_function('() => window.mockupReady === true')
        page.evaluate('document.fonts.ready')

    def reset(state):
        page.evaluate('(s) => window.uploadMockup.setState(s)', state)

    for state_index, state in enumerate(states):
        for design_index, design in enumerate('abcde'):
            number = state_index * 5 + design_index + 1
            for width, height in [(1580, 1000), (1440, 900), (760, 1000)]:
                page.set_viewport_size({'width': width, 'height': height})
                go(design, state)
                label = f'{design}/{state}/{width}'
                geometry = page.evaluate('''() => {
                  const root=document.querySelector('.ud');
                  const ids=[...document.querySelectorAll('[id]')].map(e=>e.id);
                  const uses=[...document.querySelectorAll('use')].filter(e=>!document.querySelector(e.getAttribute('href')));
                  const photo=document.querySelector('.ud-photo');
                  const dialog=document.querySelector('#ud-dialog');
                  const r=dialog.open?dialog.getBoundingClientRect():null;
                  return {overflow:root.scrollWidth>root.clientWidth+1 || document.documentElement.scrollWidth>innerWidth+1,
                    unique:ids.length===new Set(ids).size, missingIcons:uses.length,
                    image:!photo || getComputedStyle(photo,'::before').backgroundImage.startsWith('url("data:image/png'),
                    dialog:!r || (r.left>=0&&r.right<=innerWidth&&r.top>=0&&r.bottom<=innerHeight),
                    headings:document.querySelectorAll('h1').length, errors:window.mockupErrors.length};
                }''')
                check(not geometry['overflow'], label + ': no horizontal overflow')
                check(geometry['unique'], label + ': unique IDs')
                check(geometry['missingIcons'] == 0, label + ': all Lucide icons exist')
                check(geometry['image'], label + ': embedded photographs render')
                check(geometry['dialog'], label + ': dialog fits viewport')
                check(geometry['headings'] == 1, label + ': one page heading')
                check(geometry['errors'] == 0, label + ': no runtime errors')
                expected = 0 if state == 'select' else 1 if state in ('single', 'document') else 11
                check(len(page.evaluate('window.uploadMockup.files')) == expected, label + ': complete file roster')
                if state in ('ready', 'multiple', 'no-match', 'single', 'document'):
                    check(page.evaluate('window.uploadMockup.selected') is None, label + ': no automatic Case choice')
                if state in ('uploading', 'processing', 'incomplete', 'unreadable', 'attached', 'discarded', 'duplicate', 'conflict'):
                    check(page.locator('[data-case]').count() == 0, label + ': no unsupported association action')
                if state == 'uploading':
                    check(page.locator('.ud-file-state').all_text_contents() == ['Uploading'] * 11, label + ': all files in flight together')
                if state == 'mixed':
                    check(page.locator('.ud-file-error').count() == 1, label + ': individual failure identified')
                page.screenshot(path=str(shots / f'{number:02}-{design}-{state}-{width}.png'))
                capture_count += 1
            print(f'Captured {design.upper()} / {state}', flush=True)

    page.set_viewport_size({'width':1440,'height':900})
    for design in 'abcde':
        go(design)
        page.locator('[data-case="QDOS26010"]').click()
        page.locator('[data-action="review"]').click()
        check(page.locator('#ud-dialog').evaluate('e=>e.open'), design + ': review opens')
        for fact in ['QDOS26010','PO-48271','BH17RZV','J. Morgan','Northbridge Insurance','Review','11 original files']:
            check(fact in page.locator('#ud-dialog').inner_text(), design + ': confirmation includes ' + fact)
        check(page.locator('[data-action="close"]').evaluate('e=>e===document.activeElement'), design + ': safe initial focus')
        for _ in range(7):
            page.keyboard.press('Tab')
            check(page.locator('#ud-dialog').evaluate('e=>e.contains(document.activeElement)'), design + ': Tab remains in dialog')
        page.keyboard.press('Escape')
        check(page.locator('[data-action="review"]').evaluate('e=>e===document.activeElement'), design + ': Escape restores focus')
        check(page.evaluate('window.uploadMockup.state') == 'ready', design + ': cancel changes no outcome')

        reset('multiple')
        page.locator('[data-case="QDOS25984"]').click()
        page.locator('[data-action="review"]').click()
        page.locator('[data-action="confirm"]').click()
        check('QDOS25984' in page.locator('.ud-final-destination').inner_text(), design + ': chosen second Case preserved')
        check('A. Taylor' in page.locator('.ud-final-destination').inner_text(), design + ': chosen claimant preserved')
        check(page.locator('[data-case]').count() == 0, design + ': no second association after success')

        reset('no-match')
        for query, ref in [('QDOS25802','QDOS25802'),('26018','QDOS26018'),('PO-47908','QDOS25984')]:
            page.locator('#ud-case-query').fill(query)
            page.locator('#ud-search-form').get_by_role('button',name='Search').click()
            check(page.locator(f'.ud-search-results [data-case="{ref}"]').count() == 1, design + ': lookup ' + query)
        page.locator('#ud-case-query').fill('unknown999')
        page.locator('#ud-search-form').get_by_role('button',name='Search').click()
        check('No Cases or Triage items match' in page.locator('.ud-search-results').inner_text(), design + ': no search matches')
        reset('search-error')
        check('unavailable' in page.locator('.ud-search-results').inner_text(), design + ': search failure distinct from zero')
        page.locator('[data-action="retry-search"]').click()
        check('unavailable' not in page.locator('.ud-search-results').inner_text(), design + ': search recovery')

        reset('ready')
        page.locator('.ud-mock').evaluate('e=>{e.hidden=false;e.open=true}')
        page.locator('[data-conflict]').check()
        page.locator('.ud-mock').evaluate('e=>e.open=false')
        page.locator('[data-case="QDOS26010"]').click()
        page.locator('[data-action="review"]').click()
        page.locator('[data-action="confirm"]').click()
        check(page.evaluate('window.uploadMockup.state') == 'conflict', design + ': stale confirmation fails honestly')
        check(page.locator('[data-case]').count() == 0, design + ': refresh required after conflict')
        page.locator('[data-action="refresh"]').click()
        check(page.evaluate('window.uploadMockup.selected') is None, design + ': refresh requires new explicit choice')
        page.locator('[data-conflict]').evaluate('e=>e.checked=false')

        reset('mixed')
        page.locator('[data-case="QDOS26010"]').click()
        page.locator('[data-action="review"]').click()
        check('1 file could not be read' in page.locator('#ud-dialog').inner_text(), design + ': unreadable member disclosed before confirmation')
        page.locator('[data-action="confirm"]').click()
        check('Added · unreadable' in page.locator('.ud').text_content(), design + ': retained unreadable outcome after addition')

        reset('ready')
        page.locator('[data-action="discard"]').click()
        page.locator('[data-action="confirm-discard"]').click()
        check(page.locator('#ud-discard-error').is_visible(), design + ': discard needs checkbox')
        page.locator('#ud-discard-check').check()
        page.locator('[data-action="confirm-discard"]').click()
        check(page.evaluate('window.uploadMockup.state') == 'discarded', design + ': discard closes upload')
        check('retained' in page.locator('.ud').text_content(), design + ': retained custody explained')

        reset('chosen')
        page.locator('[data-remove="f10"]').click()
        check(len(page.evaluate('window.uploadMockup.files')) == 10, design + ': selection removal')
        page.locator('[data-action="upload"]').click()
        page.wait_for_function('() => window.uploadMockup.state === "ready"')
        check(len(page.evaluate('window.uploadMockup.files')) == 10, design + ': exact roster survives simulated transfer')
        page.locator('[data-preview="0"]').first.click() if design not in 'be' else None
        if design == 'b':
            page.locator('.ud-review-files summary').click()
            page.locator('[data-preview="0"]').first.click()
        if design == 'e':
            page.locator('[data-inspect="3"]').click()
            check(page.locator('[data-inspect="3"]').get_attribute('aria-pressed') == 'true', design + ': inspector selection')
            page.locator('.ud-inspect-caption [data-preview]').click()
        check(page.locator('#ud-dialog').evaluate('e=>e.open'), design + ': file preview opens')
        before = page.locator('#ud-dialog-title').inner_text()
        page.locator('[data-action="next"]').click()
        check(before != page.locator('#ud-dialog-title').inner_text(), design + ': next file preview')
        page.keyboard.press('Escape')

        reset('select')
        page.locator('#ud-file-input').set_input_files({'name':'unsupported.exe','mimeType':'application/octet-stream','buffer':b'bad'})
        check('not supported' in page.locator('.ud-alert').inner_text(), design + ': unsupported file validation')
        check(len(page.evaluate('window.uploadMockup.files')) == 0, design + ': invalid files excluded')
        page.locator('#ud-file-input').set_input_files({'name':'instruction.pdf','mimeType':'application/pdf','buffer':b'%PDF-1.4 sample offline fixture'})
        check(len(page.evaluate('window.uploadMockup.files')) == 1, design + ': real file selection')
        page.locator('[data-action="upload"]').click()
        check('Upload unavailable in this preview' in page.locator('#ud-dialog-title').inner_text(), design + ': actual local file not falsely processed')
        page.keyboard.press('Escape')
        page.locator('[data-action="clear"]').click()
        check(len(page.evaluate('window.uploadMockup.files')) == 0, design + ': Clear selection')
        print('Interactions passed: '+design.upper(), flush=True)

    for design in 'abcde':
        page.set_viewport_size({'width':1580,'height':1000})
        go(design)
        page.screenshot(path=str(shots / f'full-{design}-1580.png'), full_page=True)
        page.locator('[data-rail-toggle]').click()
        check(page.locator('.app-rail').evaluate('e=>Math.round(e.getBoundingClientRect().width)') == 64, design + ': shell collapse')
        page.set_viewport_size({'width':390,'height':844})
        check(page.evaluate('document.documentElement.scrollWidth<=innerWidth'), design + ': extra 390px overflow check')
    browser.close()

check(not errors, 'No browser console or runtime errors: '+str(errors))
check(not external, 'No external requests: '+str(external))
result={'fail':fail,'okCount':ok_count}
evidence={'date':datetime.now(timezone.utc).isoformat(),'source':'32dabfc59','result':result,
          'captures':capture_count,'fullPageCaptures':5,'widths':[1580,1440,760],
          'states':states,'designs':list('abcde'),'consoleErrors':errors,'externalRequests':external,
          'scope':'Offline mockup evidence only; no application build, tests, or live data.'}
(shots/'selfcheck-result.json').write_text(json.dumps(evidence,indent=2)+'\n',encoding='utf-8')
print('RESULT '+json.dumps(result),flush=True)
raise SystemExit(1 if fail else 0)
