"""Creates the extra synthetic Cases the v36 walk needs on the running host.

    python make-cases-v36.py [--base https://localhost:5240]

Creates an Inspection and Audit Case through the Create page, prints its
id, and (when the Actions menu offers it) assigns the Engineer and creates
its Audit so the Views card renders. Synthetic data only.
"""
import json
import os
import re
import sys

from playwright.sync_api import sync_playwright


def main():
    args = sys.argv[1:]
    base = args[args.index('--base') + 1] if '--base' in args else 'https://localhost:5240'
    out = {}
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=os.environ.get('PEGASUS_CHROME') or None)
        context = browser.new_context(viewport={'width': 1580, 'height': 1000}, ignore_https_errors=True)
        page = context.new_page()
        page.goto(f'{base}/Cases/Create'); page.wait_for_load_state('networkidle')
        page.fill('#PrincipalCode', 'QDOS')
        page.select_option('#CaseType', 'InspectionAndAudit')
        page.fill('#ClaimantName', 'Audit Claimant')
        page.fill('#ClaimNumber', 'AUD-77')
        page.fill('#VehicleRegistration', 'AU36DIT')
        page.fill('#VehicleMake', 'Vauxhall')
        page.fill('#VehicleModel', 'Astra')
        page.click('form button[type=submit]:has-text("Create")')
        page.wait_for_load_state('networkidle'); page.wait_for_timeout(800)
        out['inspectionAuditUrl'] = page.url
        m = re.search(r'/Cases/([0-9a-f-]{36})', page.url)
        out['inspectionAuditId'] = m.group(1) if m else None
        out['state'] = page.evaluate("document.querySelector('.ribbon-chips .status')?.textContent.trim()")
        out['menu'] = page.evaluate("[...document.querySelectorAll('.ribbon-actions .menu-body button, .ribbon-actions .menu-body a')].map(e => ({t: e.textContent.replace(/\\s+/g,' ').trim(), disabled: e.disabled, title: e.closest('.menu-gated')?.title}))")
        browser.close()
    print(json.dumps(out, indent=1))


if __name__ == '__main__':
    main()
