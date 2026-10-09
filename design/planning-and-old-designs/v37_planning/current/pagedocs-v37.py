"""Write the v37 page folder READMEs, with the screenshot table read from v37-shots/."""
import re
from pathlib import Path

current = Path(__file__).resolve().parent
pages = current.parent / 'pages'
page = pages / 'administration-reports'
names = {'live': 'Today', 'a': 'A', 'b': 'B', 'c': 'C'}
states = {
    'default': 'Populated period', 'proposals': 'Every proposal on', 'invalid': 'From after To',
    'principal-unavailable': 'Reports by Principal unavailable', 'person': 'Person r.khan', 'empty': 'Nothing in the period',
    'report-principals': 'Report: Reports by Principal', 'report-months': 'Report: By month', 'report-turnaround': 'Report: Turnaround',
    'report-caselist': 'Report: Case list', 'principal-pch': 'PCH chosen', 'work-columns': 'Inspection and Audit as three columns (item D, live)',
    'preset': 'Case list preset chosen', 'busy': 'Download running', 'done': 'Download finished',
    'full': 'Whole page (Today as live; designs with every proposal on)',
}
groups = {}
for shot in sorted((current / 'v37-shots').glob('*.png')):
    m = re.match(r'^(\d+)-([a-z]+)-(.+)-(\d+)\.png$', shot.name)
    groups.setdefault((m[1], m[2], m[3]), []).append((m[4], shot.name))
rows = []
for (number, design, state), files in sorted(groups.items()):
    files.sort(key=lambda f: -int(f[0]))
    links = ', '.join(f'[{width}](../../current/v37-shots/{name})' for width, name in files)
    rows.append(f'| {number} | {names[design]} | {states[state]} | {links} |')


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8', newline='\n')


write(pages / 'README.md', """# v37 pages

One page in this round.

- [Management Reports](administration-reports/README.md): Administration › Management Reports, today and three proposals.
""")

write(page / 'README.md', f"""# Management Reports page folder

The v37 round's page: Administration › Management Reports (`/Administration/Reports`).

- [how-it-works.md](how-it-works.md): today's page, read from the live source on 9 October 2026, with thirteen findings.
- [how-it-should-work.md](how-it-should-work.md): what the operator settled and the proposed rules, each tied to its lettered item.
- [states/](states/README.md), [panels/](panels/README.md) and [dialogs/](dialogs/README.md).

## Screenshots

From `v37-shots/` (9 October 2026, `check-management-reports-v37.py`). They are offline mockup captures, not application evidence. The running page, from the local visual host with no report data, is in [live-shots](../../current/live-shots/live-reports-1440.png).

| # | Design | State | Widths |
| --- | --- | --- | --- |
""" + '\n'.join(rows) + '\n')

write(page / 'states' / 'README.md', """# Management Reports: states

Each is `?state=` on every design file and on Today. Shot numbers are on the [page README](../README.md).

| State | What it shows | Today | Designs |
| --- | --- | --- | --- |
| `default` | The populated period, 08 Sep 2026 09:41 to 09 Oct 2026 09:41 (C: May to October 2026) | Four sections | Per design |
| `empty` | Nothing produced, sent or received in the period; held Cases still show, because held is now | Empty rows and zeros | The same rows |
| `person` | Person r.khan | Engineer activity alone narrows | The same, with Person on its head |
| `engineer-unavailable` | Engineer activity failed | Unavailable; every download refuses | Unavailable; only its CSV and the workbook refuse |
| `principal-unavailable` | Reports by Principal and Turnaround failed (one read) | Unavailable; every download refuses | Unavailable; only their CSVs and the workbook refuse |
| `monthly-unavailable` | By month failed | Unavailable; every download refuses | Unavailable; only its CSV and the workbook refuse |
| `invalid` | From after To | The error in Engineer activity, its false zero, Unavailable elsewhere | The error in the period bar, no report drawn, the Case list stays |
| `preset` | The Monthly invoicing preset chosen | Eight columns ticked, Save preset and Remove preset | The same |
| `refused` | The Case list with no column | "Choose at least one column." | The same |
| `busy`, `done` | Download workbook running, then finished | In Engineer activity's form | In the page head |
| `proposals` | Every proposal switch on (Queues, Cases by stage, Previous period, Period presets, Outcomes) | Not applicable | Per design |
""")

write(page / 'panels' / 'README.md', """# Management Reports: panels

| Panel | Today | A | B | C |
| --- | --- | --- | --- | --- |
| Period | Inside Engineer activity | Bar under the title | Bar under the title | Months, bar under the title |
| Engineer activity | First panel, with the filter | Panel, Person in its head | A report | Panel below the ledger |
| Reports by Principal | Panel with By month inside | Panel, Work choice | A report, Work choice | The ledger: Measure and Work choices, the chosen Principal under it |
| By month | Table inside Reports by Principal | Its own panel and CSV | A report | Folded into the ledger and the chosen Principal |
| Turnaround | Panel | Panel | A report | Panel |
| Case list | Panel | Panel | A report | Panel |
| Queues (item E) | No | Switch | Switch, a report | Switch |
| Cases by stage (item F) | No | Switch | Switch, a report | Switch |
| Outcomes (item O) | No | Switch | Switch, a report | Switch |
| Period tiles (item Q) | No | No | Five tiles that open their report | No |
""")

write(page / 'dialogs' / 'README.md', """# Management Reports: dialogs

The page has no dialog today and none is proposed. Downloads and Case list refusals answer with a toast; Case list preset changes redraw the page.
""")
print(f'{len(rows)} screenshot rows written.')
