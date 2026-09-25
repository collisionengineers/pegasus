"""Focused offline Work Centre evidence. Uses existing workstation Playwright."""
import json
from datetime import datetime, timezone
from pathlib import Path
from playwright.sync_api import sync_playwright

current = Path(__file__).resolve().parent
shots = current / 'v30-work-centre-shots'
shots.mkdir(exist_ok=True)
states = ['default', 'mine', 'filtered', 'empty', 'stale', 'partial', 'unavailable', 'assign', 'conflict', 'new-cases', 'ai-jobs', 'quiet']
errors, external = [], []
with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=['--allow-file-access-from-files'])
    context = browser.new_context(viewport={'width':1580,'height':1000}, device_scale_factor=1)
    def block(route):
        external.append(route.request.url)
        route.abort()
    context.route('http://**', block)
    context.route('https://**', block)
    page = context.new_page()
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.on('console', lambda message: errors.append(message.text) if message.type == 'error' else None)
    page.goto((current/'v30-work-centre-selfcheck.html').as_uri())
    page.wait_for_function('window.selfcheckResult !== undefined', timeout=120000)
    result = page.evaluate('window.selfcheckResult')
    print('RESULT '+json.dumps(result), flush=True)
    if result['fail']:
        raise SystemExit('Self-check failed; no evidence set claimed.')
    count = 0
    for index, state in enumerate(states):
        for design_index, design in enumerate(['a','b','c']):
            number = index * 3 + design_index + 1
            for width,height in [(1580,1000),(1440,900),(760,1000)]:
                page.set_viewport_size({'width':width,'height':height})
                page.goto((current/f'pegasus_work_centre_{design}_v30.html').as_uri()+f'?state={state}&embed=1')
                page.wait_for_function('window.mockupReady===true')
                page.evaluate('document.fonts.ready')
                page.screenshot(path=str(shots/f'{number:02}-{design}-{state}-{width}.png'))
                count += 1
            print(f'Captured {design.upper()} / {state}',flush=True)
    for design in ['a','b','c']:
        page.set_viewport_size({'width':1580,'height':1000})
        page.goto((current/f'pegasus_work_centre_{design}_v30.html').as_uri()+'?state=default&embed=1')
        page.evaluate('document.fonts.ready')
        page.screenshot(path=str(shots/f'full-{design}-1580.png'),full_page=True)
        page.locator('[data-select="r3"]').focus()
        page.keyboard.press('Enter')
        if design=='c':
            assert page.locator('#wd-drawer').evaluate('el=>el.open')
            page.keyboard.press('Escape')
            assert page.locator('[data-select="r3"]').evaluate('el=>el===document.activeElement')
            page.keyboard.press('Enter')
        page.locator('[data-assign="r3"]').click()
        assert page.locator('#wd-engineer').evaluate('el=>el===document.activeElement')
        for _ in range(9):
            page.keyboard.press('Tab')
            assert page.locator('#wd-assignment').evaluate('el=>el.contains(document.activeElement)')
        page.keyboard.press('Escape')
        assert not page.locator('#wd-assignment').evaluate('el=>el.open')
        assert page.locator('[data-assign="r3"]').evaluate('el=>el===document.activeElement')
        if design=='c': page.keyboard.press('Escape')
        if design=='b':
            page.locator('#tab-attention').focus()
            page.keyboard.press('ArrowRight')
            assert page.locator('#tab-new-cases').get_attribute('aria-selected')=='true'
            page.keyboard.press('End')
            assert page.locator('#tab-ai-jobs').get_attribute('aria-selected')=='true'
    browser.close()

record = {'recordedUtc':datetime.now(timezone.utc).isoformat(),'scope':'Offline Work Centre mockups only; not application evidence','selfcheck':result,'stateScreenshots':count,'fullPageScreenshots':3,'keyboardDesigns':['a','b','c'],'consoleErrors':errors,'externalRequests':external}
(shots/'verification.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8')
assert not errors,errors
assert not external,external
print(f'Saved {count} state screenshots and 3 full-page captures. Keyboard and dialog checks passed; no console errors or external requests.')
