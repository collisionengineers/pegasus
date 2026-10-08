"""Self-check and screenshot capture for the v34 Valuation proposal.

    python check-valuation-v34.py            checks, then screenshots
    python check-valuation-v34.py --no-shots checks only

Loads the mockup in headless Chromium (Playwright) and asserts: every state
renders with no script error; none of the operator's screenshot narration
and no Apply button; one Engineer's Value box and no Retail or Trade boxes;
Get valuation only on a connected card and the research card; every control
today's section has, except the two deliberate drops; every visible word is
one the application already uses, or the chosen card's word "Selected";
the figures move as the live arithmetic moves them; nothing spills sideways
at 1580, 1440 and 760. It then writes the section screenshots and
verification.json. This is evidence about the mockup, not the application.

Set PEGASUS_CHROME to a Chromium executable when Playwright's own browser
does not match the installed driver.
"""
import json
import os
import re
import sys
from datetime import date
from pathlib import Path

from playwright.sync_api import sync_playwright

CURRENT = Path(__file__).resolve().parent
REPO = CURRENT.parents[3]
SHOTS = CURRENT / 'v34-shots'
PAGE = CURRENT / 'pegasus_case_valuation_v34.html'
STATES = ['fetched', 'fetched-read', 'recorded', 'recorded-read', 'own', 'refused', 'claimant-vat',
          'pending', 'inspection', 'empty', 'empty-read']
WIDTHS = [(1580, 1000), (1440, 900), (760, 1000)]
REFUSAL = 'The valuation deductions exceed the value, so there is no figure to apply.'
NARRATION = ['click a guide to select', 'entered manually', 'tick to add', 'suggested figures', 'overwrite freely',
             'one field, two places', 'mirrors', 'Composed', 'what the report carries', 'apply sets',
             'no adjustments', 'Apply to engineer', '(PAV)', 'Use this value', 'Proposed', 'No trade figure']

ok = []
fail = []


def check(name, condition, detail=''):
    (ok if condition else fail).append(name if condition else f'{name} {detail}'.strip())


def url(state, options=''):
    return PAGE.as_uri() + f'?state={state}' + (f'&opt={options}' if options else '')


# ---- the words the application already uses -----------------------------------

def application_strings():
    found = set()
    for relative in ['src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs', 'src/Pegasus.Web/Presentation/OperatorLabels.cs',
                     'src/Pegasus.Core/Assessment/ValuationCalculations.cs', 'src/Pegasus.Core/Assessment/Valuations.cs',
                     'src/Pegasus.Web/Pages/Cases/Details.Report.cs']:
        source = (REPO / relative).read_text(encoding='utf8')
        for match in re.finditer(r'"((?:[^"\\\n]|\\.)*)"', source):
            found.add(match.group(1).replace("\\'", "'").replace('\\"', '"'))
    return found


SOURCE_NAMES = r"(Glass's|Brego|Super CAP|CAP|Cazana|AI market research)"
MONEY = r'£[\d,]+\.\d\d'
PATTERNS = [re.compile(pattern) for pattern in [
    rf'^(\+ |− )?{MONEY}$',
    r'^\d+\.\d\d$',
    rf'^Engineer\'s Value (—|{MONEY})$',
    rf'^from {SOURCE_NAMES} retail$',
    r'^(Glass\'s|Brego|Super CAP|CAP|Cazana) valuation is unavailable\. Contact an administrator or$',
    r'^\.$',
    r'^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) \d{4}$',
    r'^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) \d{4} · [\d,]+ miles · \d{1,2} (Sep|Oct) 2026$',
    r'^Researching · October 2026$',
    r'^−(10|20) %$',
    r'^—$',
    r'^Guide source (not )?disclosed( · valuation commentary)?( · unrelated damage)?$',
]]
# Synthetic data and words drawn by partials this folder does not read (the
# section head's Edit, the AI source tag, the read-mode "None" of the live
# calculation partial).
FIXTURE_WORDS = {"Glass's", 'Brego', 'Super CAP', 'CAP', 'Cazana', 'AI market research', 'AI',
                 'Tow bar', 'Decals', 'Camper conversion', 'PCO plated', 'Driving tuition',
                 'Edit', 'None', 'Collapse section', '​'}
# The chosen card's word, from the operator's screenshot (item B, 8 October 2026).
SWITCHED_WORDS = {'Selected'}

VISIBLE_TEXT = """
() => {
  const section = document.getElementById('section-valuation');
  const walker = document.createTreeWalker(section, NodeFilter.SHOW_TEXT);
  const out = [];
  while (walker.nextNode()) {
    const node = walker.currentNode;
    const text = node.textContent.replace(/\\s+/g, ' ').trim();
    if (!text || text === '\\u200b') continue;
    const element = node.parentElement;
    if (element.closest('option, script, style, template, [hidden], .sr-only')) continue;
    let hidden = false;
    for (let up = element; up && up !== section.parentElement; up = up.parentElement) {
      const style = getComputedStyle(up);
      if (style.display === 'none' || style.visibility === 'hidden') { hidden = true; break; }
    }
    if (!hidden) out.push(text);
  }
  return out;
}
"""


def invented(page, allowed):
    words = page.evaluate(VISIBLE_TEXT)
    return [word for word in words
            if word not in allowed and word not in FIXTURE_WORDS and word not in SWITCHED_WORDS
            and not any(p.match(word) for p in PATTERNS)]


def section_text(page):
    return ' '.join(page.evaluate(VISIBLE_TEXT))


def load(page, state, options=''):
    page.goto(url(state, options))
    page.wait_for_selector('#section-valuation .panel-body > *')


def saved(page):
    page.wait_for_selector('[data-lease-line]:has-text("Saved")', timeout=4000)


def engineer(page):
    return page.locator('[data-valuation-value="engineer"]').input_value()


def tag(page):
    node = page.locator('[data-out="eng-tag"] .src-tag')
    return node.text_content().strip() if node.count() else ''


def spill(page):
    return page.evaluate("""() => {
      const section = document.getElementById('section-valuation');
      const bounds = section.getBoundingClientRect();
      let worst = document.documentElement.scrollWidth - window.innerWidth;
      section.querySelectorAll('*').forEach((node) => {
        const box = node.getBoundingClientRect();
        if (box.width && getComputedStyle(node).display !== 'none' && !node.closest('.sr-only')) {
          worst = Math.max(worst, box.right - bounds.right, bounds.left - box.left);
        }
      });
      return Math.round(worst);
    }""")


def run_checks(page, errors, allowed):
    # Every state.
    for state in STATES:
        label = state
        before = len(errors)
        load(page, state)
        text = section_text(page)
        editing = not state.endswith('-read')
        check(f'{label}: renders with no script error', len(errors) == before, '; '.join(errors[before:]))
        for phrase in NARRATION:
            check(f'{label}: no "{phrase}"', phrase.lower() not in text.lower())
        check(f'{label}: no Apply button', page.locator('#section-valuation button:has-text("Apply")').count() == 0)
        check(f"{label}: one Engineer's Value cell", page.locator('[data-field="assessment.values.engineer"]').count() == 1)
        check(f'{label}: no Retail or Trade value boxes',
              page.locator('[data-field="assessment.values.retail"], [data-field="assessment.values.trade"], [data-valuation-value="retail"], [data-valuation-value="trade"]').count() == 0)
        check(f"{label}: every word is the application's", not invented(page, allowed), str(invented(page, allowed)))
        check(f'{label}: "None yet" only with no card chosen', ('None yet' in text) == (state == 'empty'))
        gets = page.locator('[data-valuation-get]')
        owners = sorted(set(gets.nth(i).evaluate("b => b.closest('[data-pick]').dataset.pick") for i in range(gets.count())))
        expected = [] if not editing else (['glasses'] if state == 'inspection' else ['ai', 'glasses'])
        check(f'{label}: Get valuation only on connected and research cards', owners == expected, str(owners))
        chosen = page.locator('.gc.sel')
        if state in ('empty', 'empty-read', 'fetched-read'):
            check(f'{label}: no card chosen', chosen.count() == 0)
        else:
            check(f'{label}: one chosen card saying Selected', chosen.count() == 1 and chosen.locator('[data-valuation-chosen-word]').text_content() == 'Selected')
        if editing:
            check(f'{label}: the approved sentence on the four unconnected cards, no Manual tag',
                  page.locator('[data-valuation-unavailable]').count() == 4 and 'Manual' not in text)

    # Coverage: every control kind today's section has, in the edit states.
    load(page, 'fetched')
    kinds = {
        'guide retail boxes': ('[data-valuation-retail]', 5),
        'guide trade boxes': ('[data-valuation-trade]', 5),
        'guide month boxes': ('[data-valuation-entry-month]', 5),
        'research month': ('[data-valuation-month]', 1),
        'preset increases': ('[data-valuation-add] [data-preset-toggle]', 7),
        'Other… rows': ('.add--custom input[type=text]', 2),
        'increase amounts': ('[data-valuation-add] input.amt', 7),
        'commercial VAT': ('#f-valuation-vat', 1),
        'condition deduction': ('#f-valuation-deduction', 1),
        'previous total loss': ('#f-valuation-ptl', 1),
        'previous total loss percentages': ('.v-ptl-switch button', 2),
        'Engineer\'s Value box': ('[data-valuation-value="engineer"]', 1),
        'report switches': ('[data-report-switch]', 3),
        'research meta': ('.gc--ai .gc-meta', 1),
        'pickable cards': ('.gc[tabindex="0"]', 5),
    }
    for name, (selector, count) in kinds.items():
        check(f'coverage: {name}', page.locator(selector).count() == count, str(page.locator(selector).count()))
    check('coverage: Report a problem', page.locator('[data-act="problem"]').count() == 4)
    load(page, 'recorded')
    check('coverage: earlier research stays a card', page.locator('.gc--ai').count() == 2)
    load(page, 'claimant-vat')
    check('claimant VAT: tick box disabled with its tag',
          page.locator('#f-valuation-vat').is_disabled() and 'Claimant is VAT registered' in section_text(page))
    load(page, 'pending')
    check('pending: Researching with the filed note', 'Researching · October 2026' in section_text(page)
          and page.locator('[data-valuation-pending-note]').count() == 1)
    load(page, 'inspection')
    check('inspection: no research month', page.locator('[data-valuation-month]').count() == 0)

    # Flows, as the live script moves the figures.
    load(page, 'fetched')
    check('fetched: CAP chosen, box holds its retail', engineer(page) == '3100.00')
    page.locator('[data-pick="cazana"] h3').click()
    check('click Cazana: box follows at once', engineer(page) == '2875.00')
    check('click Cazana: Cazana is the chosen card', page.locator('.gc.sel').get_attribute('data-pick') == 'cazana')
    check('click Cazana: basis line follows', 'from Cazana retail' in section_text(page))
    saved(page)
    check('click Cazana: the save records it (source word shown)', tag(page) == 'Cazana')
    page.locator('#f-valuation-vat').check()
    check('VAT: 20 % of retail added', engineer(page) == '3450.00')
    check('VAT: amount on its row', page.locator('[data-out="amt-vat"]').first.text_content() == '+ £575.00')
    page.locator('#f-valuation-ptl').check()
    check('previous total loss: ticked starts at 10 %', engineer(page) == '3105.00')
    page.locator('.v-ptl-switch button:has-text("20")').click()
    check('previous total loss: 20 %', engineer(page) == '2760.00')
    check('previous total loss: amount in its label line', page.locator('[data-out="amt-ptl"]').text_content() == '− £690.00')
    page.locator('#f-valuation-add-0').check()
    check('increase: Tow bar adds its figure', engineer(page) == '3060.00')
    page.locator('#f-valuation-deduction').fill('60')
    check('deduction: subtracted', engineer(page) == '3000.00')
    saved(page)
    check('save: head follows', page.locator('[data-valuation-head]').text_content().strip() == "Engineer's Value £3,000.00")
    page.locator('[data-valuation-value="engineer"]').fill('2950')
    saved(page)
    check('typed figure: the Engineer\'s own, no source word', tag(page) == '')
    page.locator('#f-valuation-add-1').check()
    check('calculator change: box follows again', engineer(page) == '3500.00')

    # The whole card is the target: name, labels, figures, boxes, empty space.
    def chosen():
        return page.locator('.gc.sel').get_attribute('data-pick')
    load(page, 'fetched')
    page.locator('[data-pick="brego"] label[for="f-valuation-brego-retail"]').click()
    check('card: a click on a label chooses the card', chosen() == 'brego' and engineer(page) == '3050.00')
    check('card: and lands in that box', page.evaluate('document.activeElement.id') == 'f-valuation-brego-retail')
    page.evaluate("window.v34box = document.getElementById('f-valuation-glasses-trade')")
    page.locator('#f-valuation-glasses-trade').click()
    check('card: a click in a box chooses the card', chosen() == 'glasses' and engineer(page) == '2950.00')
    check('card: and keeps the box focused', page.evaluate('document.activeElement.id') == 'f-valuation-glasses-trade')
    check('card: the box is not redrawn, so its caret stays put',
          page.evaluate('window.v34box.isConnected && document.activeElement === window.v34box'))
    check('card: chosen word moves with it', page.locator('[data-valuation-chosen-word]').count() == 1
          and page.locator('[data-pick="glasses"] [data-valuation-chosen-word]').count() == 1)
    page.locator('[data-pick="cazana"] .gc-month').click(position={'x': 4, 'y': 4})
    check('card: a click on the guide month line chooses the card', chosen() == 'cazana')
    page.locator('[data-pick="cap"] .gc-head h3').click()
    check('card: a click on the name chooses the card', chosen() == 'cap')
    page.locator('[data-pick="ai"] .gc-meta').click()
    check('card: a click on the research card\'s meta chooses it', chosen() == 'ai' and engineer(page) == '3195.00')
    box = page.locator('[data-pick="brego"]').bounding_box()
    page.mouse.click(box['x'] + box['width'] / 2, box['y'] + box['height'] - 4)
    check('card: a click on its padding chooses the card', chosen() == 'brego')
    page.locator('[data-pick="super-cap"] .gc-head h3').click()
    check('card: no retail says why, and is not chosen',
          chosen() == 'brego' and page.locator('[data-pick="super-cap"] [data-valuation-needs-retail]').text_content() == 'Enter the retail value on this card to use it.')
    page.locator('#f-valuation-super-cap-retail').click()
    check('card: a click into an empty card\'s box only focuses it', chosen() == 'brego'
          and page.evaluate('document.activeElement.id') == 'f-valuation-super-cap-retail')
    page.keyboard.type('3300')
    check('card: typing a retail clears the notice', page.locator('[data-valuation-needs-retail]').count() == 0)
    page.locator('[data-pick="super-cap"] .gc-head h3').click()
    check('card: then a click chooses it', chosen() == 'super-cap' and engineer(page) == '3300.00')
    page.locator('[data-pick="glasses"] [data-valuation-get]').click()
    check('card: Get valuation does not choose its card', chosen() == 'super-cap')
    page.wait_for_selector('[data-pick="glasses"] [data-valuation-get]:has-text("Get valuation")')

    load(page, 'fetched')
    page.locator('[data-pick="glasses"] [data-valuation-get]').click()
    check('Get valuation: busy word', 'Looking up…' in section_text(page))
    page.wait_for_selector('[data-pick="glasses"] [data-valuation-get]:has-text("Get valuation")')
    check('Get valuation: figures land', page.locator('#f-valuation-glasses-retail').input_value() == '2950.00')
    page.locator('[data-pick="ai"] [data-valuation-get]').click()
    check('research: card goes to Researching, earlier result kept',
          'Researching · October 2026' in section_text(page) and page.locator('.gc--ai').count() == 2)

    load(page, 'refused')
    check('refused: Core\'s sentence', REFUSAL in section_text(page))
    check('refused: box keeps the saved figure', engineer(page) == '3100.00')

    load(page, 'empty')
    page.locator('#f-valuation-brego-retail').fill('4000')
    page.locator('[data-pick="brego"] h3').click()
    check('typed card: chosen once it has a retail', engineer(page) == '4000.00')

    load(page, 'recorded')
    check('recorded: source word on the label', tag(page) == 'CAP')
    page.locator('[data-case-done]').first.click()
    page.wait_for_selector('html[data-v34-mode="read"]')
    check('Done: same section, greyed', page.locator('[data-valuation-value="engineer"]').count() == 0 and tag(page) == 'CAP')
    check('Done: report summary', 'Guide source not disclosed' in section_text(page))
    check('Done: applied increase ticked', page.locator('.add.on .add-check').count() == 1)


def run_widths(browser, heights):
    for width, height in WIDTHS:
        page = browser.new_page(viewport={'width': width, 'height': height})
        for state in STATES:
            load(page, state)
            worst = spill(page)
            check(f'{state} at {width}: nothing spills sideways', worst <= 1, str(worst))
            box = page.locator('#section-valuation').bounding_box()
            heights.setdefault(state, {})[str(width)] = round(box['height'])
        page.close()


SHOT_LIST = [
    ('fetched', '', True), ('fetched-read', '', True), ('recorded', '', True), ('recorded-read', '', True),
    ('own', '', False), ('refused', '', False), ('claimant-vat', '', False), ('pending', '', False),
    ('inspection', '', False), ('empty', '', False), ('empty-read', '', False),
]


def shoot(page, path):
    page.evaluate("window.scrollTo(0, 0); document.querySelector('.v34-mock').style.display = 'none'")
    box = page.locator('#section-valuation').bounding_box()
    page.screenshot(path=str(path), full_page=True, clip=box)


def run_shots(browser):
    SHOTS.mkdir(exist_ok=True)
    for old in SHOTS.glob('*.png'):
        old.unlink()
    written = []
    for index, (state, options, all_widths) in enumerate(SHOT_LIST, start=1):
        for width, height in (WIDTHS if all_widths else WIDTHS[:1]):
            page = browser.new_page(viewport={'width': width, 'height': height})
            load(page, state, options)
            suffix = '-' + options.replace(':', '-') if options else ''
            name = f'{index:02d}-{state}{suffix}-{width}.png'
            shoot(page, SHOTS / name)
            written.append(name)
            page.close()
    # The whole Case page around the section, once.
    page = browser.new_page(viewport={'width': 1580, 'height': 1000})
    load(page, 'fetched')
    page.evaluate("document.querySelector('.v34-mock').style.display = 'none'")
    page.screenshot(path=str(SHOTS / '00-page-fetched-1580.png'), full_page=True)
    written.append('00-page-fetched-1580.png')
    page.close()
    return written


def main():
    allowed = application_strings()
    heights = {}
    chrome = os.environ.get('PEGASUS_CHROME')
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(executable_path=chrome) if chrome else playwright.chromium.launch()
        page = browser.new_page(viewport={'width': 1580, 'height': 1000})
        errors = []
        page.on('console', lambda message: errors.append(message.text) if message.type == 'error' else None)
        page.on('pageerror', lambda error: errors.append(str(error)))
        run_checks(page, errors, allowed)
        check('no console error across the run', not errors, '; '.join(errors))
        page.close()
        run_widths(browser, heights)
        shots = [] if '--no-shots' in sys.argv else run_shots(browser)
        browser.close()
    result = {'fail': fail, 'okCount': len(ok)}
    print('RESULT ' + json.dumps(result))
    if '--no-shots' not in sys.argv:
        (SHOTS / 'verification.json').write_text(json.dumps({
            'date': date.today().isoformat(),
            'result': result,
            'sectionHeights': heights,
            'screenshots': shots,
        }, indent=2) + '\n', encoding='utf8', newline='\n')
    return 1 if fail else 0


if __name__ == '__main__':
    sys.exit(main())
