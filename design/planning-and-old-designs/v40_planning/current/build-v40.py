"""Build the v40 Contacts mockup: one offline file, pegasus_contacts_v40.html.

The shell (rail, utility bar, Administration nav, shared dialogs) and the
Principal's Report sending, Salvage matrix, Default inspection location and
Principal API panels are the server's own render, taken from the v39 capture
(`../../v39_planning/current/captured/contact-dfd-read.html`). The CSS, fonts,
scripts and the mark are inlined from src/Pegasus.Web/wwwroot on the branch.
The Contacts list, the Contact's own form, Report generation and the Add contact
dialogs are written here from the live Razor (Index.cshtml, Edit.cshtml) with
the round's proposal applied. Every state is a <template>; a runtime places the
chosen one before the page scripts run. "Mockup controls" is demo control,
not product UI.

Run from the repository root:
  python design/planning-and-old-designs/v40_planning/current/build-v40.py
"""
from __future__ import annotations

import base64
import html as H
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
CAPTURE = ROOT / "design/planning-and-old-designs/v39_planning/current/captured/contact-dfd-read.html"
WEB = ROOT / "src/Pegasus.Web/wwwroot"
VERSION = "v40"
OUT = HERE / "pegasus_contacts_v40.html"
FINGERPRINT = re.compile(r"\.[a-z0-9]{10}\.(css|js|png|woff2)")

TYPES = [  # (enum value, label as RoleLabel, group heading as FRD-17 names the directory)
    ("Principal", "Principal", "Principals"),
    ("ClaimSource", "Claim Source", "Claim Sources"),
    ("Repairer", "Repairer", "Repairers"),
    ("Storage", "Storage", "Storage"),
    ("ThirdPartyEngineer", "Third Party Engineer", "Third Party Engineers"),
]
LABEL = {t: l for t, l, _ in TYPES}

# Principals: the fifteen seeded codes and names (V1PlatformFoundation), plus one
# synthetic replaced code for the inactive state. Seeded contact fields are blank.
PRINCIPALS = [
    ("QDOS", "QDOS", "", "", "", "QDOS26214", "2026-10-12"),
    ("PCH", "Performance Car Hire / Parkhouse", "", "", "", "PCH26118", "2026-10-09"),
    ("AX", "AX", "", "", "", "AX26340", "2026-10-11"),
    ("FW", "Fairway Legal", "", "", "", "", ""),
    ("QCL", "QC Law", "", "", "", "QCL26022", "2026-09-30"),
    ("OAK", "Oakwood Solicitors", "", "", "", "", ""),
    ("SBL", "Smart Business Link", "", "", "", "", ""),
    ("BLACK", "Blackstone Legal", "", "", "", "", ""),
    ("RJS", "Robert James Solicitors", "", "", "", "RJS26057", "2026-10-07"),
    ("DFD", "Davison Flynn Duke Solicitors", "", "", "", "DFD26031", "2026-10-10"),
    ("KBS", "Knightsbridge Solicitors", "", "", "", "", ""),
    ("MP", "Montreal Prestige", "", "", "", "MP26090", "2026-10-02"),
    ("YML", "YM Law / Network HD UK", "", "", "", "", ""),
    ("ALS", "Auto Logistic Solutions Ltd", "", "", "", "", ""),
    ("BC", "Baker Coleman", "", "", "", "", ""),
]
INACTIVE_PRINCIPAL = ("EXC", "Example Claims Ltd")

# Every other contact is synthetic: (type, name, person, email, phone, postcode, linked codes, last case, date, active)
OTHERS = [
    ("ClaimSource", "Accident Specialists", "Dana Hughes", "claims@accident-specialists.example", "0161 496 0321", "M1 4BT", ["DFD", "RJS"], "DFD26031", "2026-10-10", True),
    ("ClaimSource", "Car 2 Go", "", "desk@car2go.example", "0113 496 0870", "LS1 5AB", ["DFD"], "DFD26012", "2026-09-22", True),
    ("ClaimSource", "CarClaims", "Priya Shah", "new@carclaims.example", "", "", ["DFD", "QDOS"], "QDOS26201", "2026-10-06", True),
    ("ClaimSource", "Claim Specialists", "", "", "", "", ["DFD"], "", "", True),
    ("ClaimSource", "Expert Claims", "Tom Reid", "tom.reid@expertclaims.example", "0151 496 0444", "L2 2DP", ["DFD", "AX"], "AX26332", "2026-10-08", True),
    ("ClaimSource", "Rapid Rental Solutions", "", "hire@rapidrental.example", "0121 496 0101", "B5 4QS", ["DFD", "PCH"], "PCH26118", "2026-10-09", True),
    ("ClaimSource", "SMC", "", "", "", "", ["DFD"], "", "", True),
    ("Repairer", "Riverside Coachworks", "Mark Ellis", "workshop@riverside-coachworks.example", "0114 496 0712", "S9 2RQ", ["AX", "QDOS"], "QDOS26214", "2026-10-12", True),
    ("Repairer", "Northside Accident Repair Centre", "Joanne Pike", "estimates@northside-arc.example", "0191 496 0230", "NE6 1TU", ["QDOS"], "QDOS26198", "2026-10-03", True),
    ("Repairer", "Moorgate Bodyshop", "", "office@moorgate-bodyshop.example", "01274 496 015", "BD3 9LP", ["MP", "PCH"], "MP26090", "2026-10-02", True),
    ("Repairer", "Hollow Lane Bodyshop", "", "", "01422 496 330", "HX1 2NE", [], "", "", False),
    ("Storage", "Eastfield Vehicle Storage", "Sam Okafor", "yard@eastfield-storage.example", "01302 496 400", "DN4 5HX", ["QDOS", "AX"], "AX26340", "2026-10-11", True),
    ("Storage", "Riverside Recovery & Storage", "", "recovery@riverside-rs.example", "0114 496 0999", "S9 3JD", ["QDOS"], "QDOS26187", "2026-09-28", True),
    ("ThirdPartyEngineer", "Pennine Engineering Assessors", "Ruth Calder", "reports@pennine-ea.example", "01484 496 212", "HD1 6AA", ["QDOS"], "QDOS26170", "2026-09-19", True),
    ("ThirdPartyEngineer", "Coastline Vehicle Assessors", "", "office@coastline-va.example", "", "", [], "", "", True),
]

# Synthetic, written to the length of the seeded SOP text (about 1,300 characters,
# paragraphs separated by line breaks). It is not any Principal's SOP.
NOTES_LONG = "\n".join([
    "Instructions arrive by e-mail from the claims team; the letter names the client, the vehicle and the hire company.",
    "Physical inspections only, unless the instruction says the vehicle cannot be seen; then image based assessment with the client's photographs.",
    "Total loss: report a total loss when repairs exceed 60% of the pre-accident value; between 55% and 60%, call the handler before the report goes.",
    "Hire: state the repair duration in working days and the date the vehicle became unroadworthy, if it did.",
    "Salvage: quote the category and the salvage figure from the matrix; never name a salvage buyer.",
    "Images: at least one of each damaged area and one of the odometer; none showing people or other vehicles' registrations.",
    "Estimate: attach the repairer's estimate when one was supplied; if figures were agreed, say who agreed them and when.",
    "Queries: answer within two working days and copy the handler named on the instruction.",
    "Fee note: a separate PDF with the Principal's reference in the file name.",
    "Supplementary reports keep the original reference and the date of first inspection.",
    "Mileage: record the odometer reading from the photograph; where none was taken, say so and use the instruction's figure.",
    "Do not discuss liability or fault with the client, the repairer or the hire company.",
])
NOTES_SHORT = "\n".join([
    "Hire vehicles from this source: state the hire start date when the instruction gives it.",
    "Send queries to the handler on the instruction, never to the general claims inbox.",
])

E = H.escape


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def asset_bytes(url: str) -> bytes:
    return (WEB / FINGERPRINT.sub(r".\1", url.lstrip("/"))).read_bytes()


def css_with_fonts(name: str) -> str:
    text = asset_bytes(f"/css/{name}").decode("utf-8")

    def font(match: re.Match[str]) -> str:
        data = (WEB / "css" / FINGERPRINT.sub(r".\1", match.group(1))).resolve().read_bytes()
        return f"url(data:font/woff2;base64,{base64.b64encode(data).decode('ascii')})"

    return re.sub(r"url\((\.\./fonts/[^)]+)\)", font, text)


def inline_images(markup: str) -> str:
    return re.sub(
        r'src="(/images/[^"]+\.png)"',
        lambda m: f'src="data:image/png;base64,{base64.b64encode(asset_bytes(m.group(1))).decode("ascii")}"',
        markup,
    )


def balanced(markup: str, start: int, tag: str) -> str:
    """The element opening at `start`, through its matching close tag."""
    depth, i = 0, start
    pattern = re.compile(rf"<(/?){tag}\b")
    for m in pattern.finditer(markup, start):
        depth += -1 if m.group(1) else 1
        if depth == 0:
            return markup[start: markup.index(">", m.end()) + 1]
        i = m.end()
    raise SystemExit(f"unbalanced <{tag}> at {start}")


def panel(markup: str, label: str) -> str:
    i = markup.index(f'<section class="panel" aria-labelledby="{label}"')
    return balanced(markup, i, "section")


# ---- the captured shell and Principal panels -------------------------------

capture = read(CAPTURE)
body = re.search(r"<body[^>]*>(.*)</body>", capture, re.S).group(1)
body = re.sub(r'<script src="/js/[^"]+"></script>\s*', "", body)
content_open = body.index('<div class="content">') + len('<div class="content">')
main_close = body.index("</main>")
content_close = body.rindex("</div>", content_open, main_close)
SHELL_BEFORE, SHELL_AFTER = body[:content_open], body[content_close:]
# The capture predates two Administration nav changes on dev: Reports is
# "Management Reports" and "Automation & AI" follows AI jobs where composed.
NAV = re.search(r'<nav class="admin-nav panel".*?</nav>', body[content_open:content_close], re.S).group(0)
NAV = NAV.replace("<span>Reports</span>", "<span>Management Reports</span>")
NAV = NAV.replace(
    "<span>AI jobs</span>\n        </a>",
    '<span>AI jobs</span>\n        </a>\n        <a href="/Administration/Automation">\n            <svg class="icon" aria-hidden="true"><use href="#icon-bot" /></svg>\n            <span>Automation &amp; AI</span>\n        </a>',
)
assert "Automation &amp; AI" in NAV and "Management Reports" in NAV, "admin nav patch did not apply"
CAPTURED_CONTENT = body[content_open:content_close]
SENDING = panel(capture, "principal-sending-title")
SALVAGE = panel(capture, "principal-salvage-title")
LOCATION = panel(capture, "principal-location-title")
API = panel(capture, "principal-api-title")
replace_at = capture.index('data-dialog-open="replace-principal"')
REPLACE = capture[capture.rindex('<div class="button-row">', 0, replace_at): capture.index("</dialog>", replace_at) + len("</dialog>")]

# ---- helpers ---------------------------------------------------------------


def page_header(title: str) -> str:
    return (
        '<header class="page-header">\n    <div class="page-title">\n            <p class="eyebrow">Administration</p>\n'
        f"        <h1>{E(title)}</h1>\n    </div>\n</header>\n"
    )


def chip(active: bool) -> str:
    return '<span class="status status--navy">Active</span>' if active else '<span class="status status--neutral">Disabled</span>'


def dash(value: str) -> str:
    return E(value) if value else "&#x2014;"


def contacts() -> list[dict]:
    rows = [
        dict(type="Principal", code=c, name=n, person=p, email=e, phone=t, postcode="", links=[], last=l, date=d, active=True)
        for c, n, p, e, t, l, d in PRINCIPALS
    ]
    rows.append(dict(type="Principal", code=INACTIVE_PRINCIPAL[0], name=INACTIVE_PRINCIPAL[1], person="", email="", phone="", postcode="", links=[], last="", date="", active=False))
    rows += [
        dict(type=t, code="", name=n, person=p, email=e, phone=ph, postcode=pc, links=lk, last=l, date=d, active=a)
        for t, n, p, e, ph, pc, lk, l, d, a in OTHERS
    ]
    return rows


# Columns per type: only fields a contact already has (ContactDirectoryRecord,
# the Principal's code, the contact's Principal links).
COLUMNS = {
    "Principal": ["code", "name", "person", "email", "phone", "last", "status"],
    "ClaimSource": ["name", "person", "email", "phone", "links", "last", "status"],
    "Repairer": ["name", "person", "email", "phone", "postcode", "links", "last", "status"],
    "Storage": ["name", "person", "email", "phone", "postcode", "links", "last", "status"],
    "ThirdPartyEngineer": ["name", "person", "email", "phone", "links", "last", "status"],
}
HEAD = {
    "code": "Principal code", "name": "Name", "person": "Contact person", "email": "Email", "phone": "Phone",
    "postcode": "Postcode", "links": "Linked principals", "last": "Last case", "status": "Status", "type": "Type",
}


def cell(row: dict, column: str) -> str:
    if column == "code":
        return f'<td class="mono">{E(row["code"])}</td>'
    if column == "name":
        return f'<td>{E(row["name"])}</td>'
    if column == "person":
        return f"<td>{dash(row['person'])}</td>"
    if column in ("email", "phone"):
        return f'<td class="mono">{dash(row[column])}</td>'
    if column == "postcode":
        return f"<td>{dash(row['postcode'])}</td>"
    if column == "links":
        return f'<td class="mono">{dash(", ".join(row["links"]))}</td>'
    if column == "last":
        return f"<td>{dash(row['last'])}</td>"
    if column == "type":
        return f"<td>{E(LABEL[row['type']])}</td>"
    return f"<td>{chip(row['active'])}</td>"


def row_attrs(row: dict) -> str:
    search = " ".join([row["name"], row["person"], row["email"], row["code"]]).lower()
    return f'data-v40-row data-name="{E(row["name"].lower())}" data-date="{row["date"]}" data-type="{row["type"]}" data-search="{E(search)}"'


def open_href(row: dict) -> str:
    key = {"Principal": "principal", "ClaimSource": "claim-source", "Repairer": "repairer", "Storage": "storage", "ThirdPartyEngineer": "tpe"}[row["type"]]
    if row["type"] == "Principal" and not row["active"]:
        key = "principal-inactive"
    return f"?state={key}"


def toolbar(sort_options: list[tuple[str, str]], type_all: bool = True) -> str:
    types = ('<option value="">All types</option>' if type_all else "") + "".join(f'<option value="{t}">{l}</option>' for t, l, _ in TYPES)
    sorts = "".join(f'<option value="{v}">{l}</option>' for v, l in sort_options)
    return (
        '<form method="get" class="contact-toolbar" data-auto-submit>'
        '<div class="field"><label for="Search">Name or contact</label><input type="search" autocomplete="off" id="Search" name="Search" value="" /></div>'
        f'<div class="field"><label for="Type">Type</label><select id="Type" name="Type">{types}</select></div>'
        f'<div class="field"><label for="Sort">Sort</label><select id="Sort" name="Sort">{sorts}</select></div>'
        '<button class="btn" type="submit">Apply</button></form>'
    )


def type_dialog() -> str:
    tiles = "".join(f'<a class="contact-kind" href="?state=list&amp;dialog=create&amp;create={t}"><strong>{l}</strong></a>' for t, l, _ in TYPES)
    return (
        '<div class="dialog-backdrop" data-dialog="contact-type-dialog" hidden>\n'
        '    <section class="dialog dialog--compact" role="dialog" aria-modal="true" aria-labelledby="contact-type-title">\n'
        '        <div class="dialog-head"><h2 id="contact-type-title" tabindex="-1" data-dialog-initial-focus>Add contact</h2><button class="dialog-close" type="button" data-dialog-close aria-label="Close"><svg class="icon" aria-hidden="true"><use href="#icon-x" /></svg></button></div>\n'
        f'        <div class="dialog-body"><div class="contact-kind-grid">{tiles}</div></div>\n'
        '        <div class="dialog-foot"><button class="btn" type="button" data-dialog-close>Cancel</button></div>\n'
        "    </section>\n</div>\n"
    )


def create_dialog(kind: str, name: str = "", matches: list[dict] | None = None) -> str:
    label = LABEL[kind]
    principal = ""
    if kind == "Principal":
        principal = (
            '<div class="field"><label class="req" for="PrincipalCode">Principal code</label><input autocomplete="off" id="PrincipalCode" name="PrincipalCode" value="" /></div>'
            '<div class="field"><label for="PrincipalInspectionMode">Default inspection</label><select id="PrincipalInspectionMode" name="PrincipalInspectionMode"><option value="PhysicalAddress">Physical address</option><option value="ImageBasedAssessment">Image based assessment</option></select></div>'
        )
    else:
        options = "".join(f'<option value="{c}">{E(n)} &#xB7; {c}</option>' for c, n, *_ in PRINCIPALS)
        principal = (
            f'<div class="field"><label for="AssociatedPrincipalIds">Linked principals</label><select id="AssociatedPrincipalIds" name="AssociatedPrincipalIds" multiple size="4">{options}</select>'
            f"<small>Optional. Select every Principal this {label.lower()} works with.</small></div>"
        )
    found = ""
    if matches:
        rows = "".join(
            f'<div class="button-row"><span>{E(m["name"])} &#xB7; {LABEL[m["type"]]}</span><a class="btn btn--small" href="{open_href(m)}">Open</a></div>'
            for m in matches
        )
        found = f'<section class="panel" aria-labelledby="possible-matches-title" data-v40-match><div class="panel-head"><h3 id="possible-matches-title">Possible existing contacts</h3></div><div class="panel-body stack">{rows}</div></section>'
    return (
        f'<div class="dialog-backdrop" data-dialog="contact-create-{kind}" hidden>\n'
        '<section class="dialog dialog--wide dialog--contact" role="dialog" aria-modal="true" aria-labelledby="contact-create-title">\n'
        f'<div class="dialog-head"><h2 id="contact-create-title" tabindex="-1" data-dialog-initial-focus>New {label}</h2><button class="dialog-close" type="button" data-dialog-close aria-label="Close"><svg class="icon" aria-hidden="true"><use href="#icon-x" /></svg></button></div>\n'
        '<form id="contact-create-form" method="post"><div class="dialog-body stack"><div class="form-grid form-grid--2">'
        f'<div class="field field-wide"><label class="req" for="Name">Organisation name</label><input autocomplete="organization" id="Name" name="Name" value="{E(name)}" /></div>'
        '<div class="field"><label for="ContactPerson">Contact person</label><input autocomplete="name" id="ContactPerson" name="ContactPerson" value="" /></div>'
        '<div class="field"><label for="Email">Email</label><input type="email" autocomplete="email" id="Email" name="Email" value="" /></div>'
        '<div class="field"><label for="Telephone">Telephone</label><input type="tel" inputmode="tel" pattern="^\\+?[0-9 ]+$" autocomplete="tel" id="Telephone" name="Telephone" value="" /></div>'
        '<div class="field field-tall"><label for="Address">Address</label><textarea rows="2" autocomplete="street-address" id="Address" name="Address"></textarea></div>'
        '<div class="field"><label for="Postcode">Postcode</label><input autocomplete="postal-code" id="Postcode" name="Postcode" value="" /></div>'
        f"{principal}</div>"
        '<div class="button-row" data-v40-match><button class="btn btn--small" type="submit" formnovalidate>Check existing contacts</button></div>'
        f"{found}</div>"
        f'<div class="dialog-foot"><button class="btn" type="button" data-dialog-close>Cancel</button><button class="btn btn--primary" type="submit">Save {label}</button></div>'
        "</form></section></div>\n"
    )


def layout(title: str, inner: str, after: str = "") -> str:
    return page_header(title) + f'<div class="admin-layout">{NAV}<div class="stack">{inner}</div></div>{after}'


# ---- Contacts list -----------------------------------------------------------

ALL = contacts()


def list_proposed() -> str:
    head = (
        '<section class="panel" aria-labelledby="contacts-title"><div class="panel-head"><div><h2 id="contacts-title">Contacts</h2></div>'
        '<button class="btn btn--primary" type="button" data-dialog-open="contact-type-dialog">Add contact</button></div>'
        + toolbar([("Name", "Name"), ("LastCase", "Last case")])
        + '<div class="empty" data-v40-empty hidden><p>No contacts found.</p></div></section>'
    )
    groups = []
    for t, _, heading in TYPES:
        cols = COLUMNS[t]
        ths = "".join(f"<th>{HEAD[c]}</th>" for c in cols) + '<th><span class="sr-only">Open</span></th>'
        rows = sorted((r for r in ALL if r["type"] == t), key=lambda r: r["name"].lower())
        trs = "".join(
            f"<tr {row_attrs(r)}>" + "".join(cell(r, c) for c in cols) + f'<td class="right"><a class="btn btn--small" href="{open_href(r)}">Open</a></td></tr>'
            for r in rows
        )
        gid = f"contacts-{t.lower()}-title"
        groups.append(
            f'<section class="panel" aria-labelledby="{gid}" data-v40-group="{t}"><div class="panel-head"><h2 id="{gid}">{heading}</h2></div>'
            f'<div class="table-wrap no-border"><table><caption class="sr-only">{heading}</caption><thead><tr>{ths}</tr></thead><tbody>{trs}</tbody></table></div></section>'
        )
    matches = [r for r in ALL if r["name"].startswith("Riverside")]
    dialogs = type_dialog() + "".join(create_dialog(t) for t, _, _ in TYPES if t != "Repairer") + create_dialog("Repairer", "Riverside", matches)
    return layout("Contacts", head + "".join(groups), dialogs)


def list_live() -> str:
    """Index.cshtml as dev renders it today, with the same fixtures."""
    head = (
        '<section class="panel" aria-labelledby="contacts-title"><div class="panel-head"><div><h2 id="contacts-title">Contacts</h2>'
        '<p class="panel-title-meta">Principals, claim sources, repairers, storage and third party engineers.</p></div>'
        '<button class="btn btn--primary" type="button" data-dialog-open="contact-type-dialog">Add contact</button></div>'
        + toolbar([("Name", "Name"), ("Type", "Type"), ("LastCase", "Last case")])
    )
    cols = ["name", "type", "person", "email", "phone", "last", "status"]
    ths = "".join(f"<th>{HEAD[c]}</th>" for c in cols) + '<th><span class="sr-only">Open</span></th>'
    trs = "".join(
        f"<tr {row_attrs(r)}>" + "".join(cell(r, c) for c in cols) + f'<td class="right"><a class="btn btn--small" href="{open_href(r)}">Open</a></td></tr>'
        for r in sorted(ALL, key=lambda r: r["name"].lower())
    )
    table = f'<div class="table-wrap no-border" data-v40-group="all"><table><caption class="sr-only">Contact directory</caption><thead><tr>{ths}</tr></thead><tbody>{trs}</tbody></table></div>'
    return layout("Contacts", head + table + '<div class="empty" data-v40-empty hidden><p>No contacts found.</p></div></section>', type_dialog())


# ---- Contact edit --------------------------------------------------------------


def field(label: str, control: str, wide: bool = False, req: bool = False, for_id: str = "") -> str:
    return f'<div class="field{" field-wide" if wide else ""}"><label{" class=\"req\"" if req else ""} for="{for_id}">{label}</label>{control}</div>'


def links_section(kind: str, linked: list[str]) -> str:
    boxes = "".join(
        f'<label class="choice"><input type="checkbox" name="PrincipalAssociationKeys" value="{kind}:{c}"{" checked" if c in linked else ""} /> <span>{E(n)} ({c})</span></label>'
        for c, n, *_ in PRINCIPALS
    )
    return (
        '<section class="contact-section" aria-labelledby="linked-principals-title" data-v40-links>'
        f'<h3 id="linked-principals-title">Linked principals</h3><div class="stack contact-links" data-v40-links-list>{boxes}</div></section>'
    )


def guidance_section(notes: str, guidance: str = "") -> str:
    return (
        '<section class="contact-section" aria-labelledby="case-guidance-title"><h3 id="case-guidance-title">Case guidance</h3>'
        f'<div class="field"><label for="GuidanceTemplate">Guidance for new Cases</label><textarea rows="4" maxlength="4000" id="GuidanceTemplate" name="GuidanceTemplate">{E(guidance)}</textarea></div>'
        f'<div class="field" data-notes-on-every-case><label for="NotesOnEveryCase">Notes on every Case</label><textarea rows="4" maxlength="2000" id="NotesOnEveryCase" name="NotesOnEveryCase" data-v40-notes>{E(notes)}</textarea></div>'
        "</section>"
    )


def contact_panel(c: dict) -> str:
    kind = c["type"]
    grid = [
        field("Organisation name", f'<input autocomplete="organization" id="Name" name="Name" value="{E(c["name"])}" />', wide=True, req=True, for_id="Name"),
        field("Type", f'<input id="ContactType" value="{LABEL[kind]}" readonly data-v40-type />', for_id="ContactType"),
    ]
    if kind == "Principal":
        grid.append(field("Principal code", f'<input id="PrincipalCode" value="{E(c["code"])}" readonly class="mono" />', for_id="PrincipalCode"))
    else:
        grid.append('<div class="field" aria-hidden="true"></div>')
    grid += [
        field("Contact person", f'<input autocomplete="name" id="ContactPerson" name="ContactPerson" value="{E(c["person"])}" />', for_id="ContactPerson"),
        field("Phone", f'<input type="tel" inputmode="tel" id="Telephone" name="Telephone" value="{E(c["phone"])}" />', for_id="Telephone"),
        field("Email", f'<input type="email" autocomplete="email" id="Email" name="Email" value="{E(c["email"])}" />', wide=True, for_id="Email"),
        field("Address", f'<textarea rows="3" autocomplete="street-address" id="Address" name="Address">{E(c.get("address", ""))}</textarea>', for_id="Address"),
        field("Postcode", f'<input autocomplete="postal-code" id="Postcode" name="Postcode" value="{E(c["postcode"])}" />', for_id="Postcode"),
    ]
    if kind == "Principal":
        grid += [
            field("Inspection mode", f'<input id="PrincipalInspectionMode" value="{c["mode"]}" readonly data-v40-inspection-mode />', for_id="PrincipalInspectionMode"),
            field("Accepted e-mail identities", f'<input id="AcceptedIdentities" value="{E(c["identities"])}" readonly />', for_id="AcceptedIdentities"),
        ]
    sections = ""
    if kind in ("Principal", "ClaimSource"):
        sections += guidance_section(c.get("notes", ""))
    if kind != "Principal":
        sections += links_section(kind, c["links"])
    return (
        f'<section class="panel" aria-labelledby="contact-title" data-v40-panel="Contact"><div class="panel-head"><div><h2 id="contact-title">{E(c["name"])}</h2></div>{chip(c["active"])}</div>'
        '<form method="post" class="panel-body stack contact-editor" data-contact-editor><fieldset class="stack">'
        f'<div class="form-grid form-grid--2">{"".join(grid)}</div>{sections}'
        f'<label class="choice"><input type="checkbox" id="Active" name="Active" value="true"{" checked" if c["active"] else ""} /> <span>Active</span></label>'
        '</fieldset><div class="button-row"><button type="submit" class="btn btn--primary">Save contact</button><a href="?state=list" class="btn">Cancel</a></div></form></section>'
    )


REPORT_GENERATION = (
    '<section class="panel" aria-labelledby="principal-report-title"><div class="panel-head"><h2 id="principal-report-title">Report generation</h2></div>'
    '<form method="post" class="panel-body stack"><fieldset class="stack"><div class="field"><label for="principal-default-fee">Default fee (&#xA3;)</label>'
    '<input id="principal-default-fee" name="DefaultFee" type="number" inputmode="decimal" min="0.01" step="0.01" required value="180.00" /></div>'
    '<div class="button-row"><button type="submit" class="btn btn--primary">Save report settings</button></div></fieldset></form></section>'
)

PRINCIPAL_FIXTURE = dict(
    type="Principal", code="DFD", name="Davison Flynn Duke Solicitors", person="", email="", phone="", postcode="",
    mode="Physical address", identities="dfd-solicitors.co.uk", notes=NOTES_LONG, active=True, links=[],
)


def edit_principal() -> str:
    c = PRINCIPAL_FIXTURE
    panels = contact_panel(c) + REPORT_GENERATION + SENDING + SALVAGE + LOCATION + API + REPLACE
    return layout(c["name"], panels)


def edit_principal_inactive() -> str:
    c = dict(PRINCIPAL_FIXTURE, code=INACTIVE_PRINCIPAL[0], name=INACTIVE_PRINCIPAL[1], identities="", notes="", active=False)
    empty = '<section class="panel"><div class="empty"><p>This principal is inactive. Change the settings on its successor.</p></div></section>'
    return layout(c["name"], contact_panel(c) + empty)


def edit_other(kind: str, name: str) -> str:
    row = next(r for r in ALL if r["name"] == name)
    c = dict(row, address={"Repairer": "Unit 4, Mill Road\nSheffield", "Storage": "Eastfield Yard, Carr Lane\nDoncaster"}.get(kind, ""), notes=NOTES_SHORT if kind == "ClaimSource" else "")
    return layout(c["name"], contact_panel(c))


def edit_principal_live() -> str:
    """Edit as the server rendered it on 6 October 2026 (v39 capture of PR 1015)."""
    return CAPTURED_CONTENT


STATES = [
    ("list", "Contacts: organised by type (proposed)", list_proposed()),
    ("list-live", "Contacts: as built today", list_live()),
    ("principal", "Principal (DFD): proposed", edit_principal()),
    ("principal-saved", "Principal: Save report sending lands on its panel", edit_principal()),
    ("principal-refused", "Principal: a refused Salvage matrix save", edit_principal()),
    ("principal-inactive", "Principal replaced: inactive", edit_principal_inactive()),
    ("principal-live", "Principal: as built (v39 capture, 6 Oct)", edit_principal_live()),
    ("claim-source", "Claim Source: proposed", edit_other("ClaimSource", "Accident Specialists")),
    ("repairer", "Repairer: proposed", edit_other("Repairer", "Riverside Coachworks")),
    ("storage", "Storage: proposed", edit_other("Storage", "Eastfield Vehicle Storage")),
    ("tpe", "Third Party Engineer: proposed", edit_other("ThirdPartyEngineer", "Pennine Engineering Assessors")),
]

VARIABLES = {
    "list": (["grouped", "single"], "List: one panel per type, or one type at a time"),
    "layout": (["stack", "tabs"], "Principal: panels stacked, or as tabs"),
    "notes": (["fit", "rows"], "Notes on every Case: grows to its text, or four rows (today)"),
    "links": (["grid", "list"], "Linked principals: three columns, or one (today)"),
    "match": (["open", "none"], "Add contact: Check existing contacts opens a match, or is removed"),
}

PROPOSAL_CSS = """
/* v40 proposal, scoped: the classes Stage 2 would add to site.css. */
.contact-links.contact-links--grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:6px 16px}
@media(max-width:760px){.contact-links.contact-links--grid{grid-template-columns:minmax(0,1fr)}}
textarea[data-notes-fit]{resize:vertical;overflow:hidden;line-height:1.5}
.contact-tabs{margin:0 0 -1px}
"""

STRIP_CSS = """
#mock-strip{position:fixed;left:12px;bottom:12px;z-index:9999;background:#1d2229;color:#e6e9ee;font:12.5px/1.4 system-ui,sans-serif;border-radius:8px;box-shadow:0 6px 24px rgba(0,0,0,.35);width:340px}
#mock-strip summary{cursor:pointer;padding:8px 12px;font-weight:650;letter-spacing:.02em}
#mock-strip .mock-body{padding:0 12px 12px;display:grid;gap:8px;max-height:70vh;overflow:auto}
#mock-strip label{display:grid;gap:2px}
#mock-strip select,#mock-strip button{font:inherit;padding:4px 6px;border-radius:4px;border:1px solid #3a424d;background:#2a313a;color:inherit}
#mock-strip small{color:#9aa4b2}
#mock-strip h4{margin:4px 0 0;font-size:11px;text-transform:uppercase;letter-spacing:.06em;color:#9aa4b2}
@media print{#mock-strip{display:none}}
"""

RUNTIME = r"""
(function () {
  var STATES = __STATES__, VARS = __VARS__, DEFAULTS = {};
  Object.keys(VARS).forEach(function (k) { DEFAULTS[k] = VARS[k][0][0]; });
  var params = new URLSearchParams(location.search);
  var key = params.get('state');
  if (!STATES.some(function (s) { return s.key === key; })) { key = STATES[0].key; }
  var stored = {}; try { stored = JSON.parse(localStorage.getItem('v40.opts') || '{}'); } catch (e) { stored = {}; }
  var opts = Object.assign({}, DEFAULTS, stored);
  (params.get('opt') || '').split(',').forEach(function (pair) {
    var kv = pair.split(':'); if (kv.length === 2 && VARS[kv[0]] && VARS[kv[0]][0].indexOf(kv[1]) >= 0) { opts[kv[0]] = kv[1]; }
  });
  var template = document.getElementById('mock-state-' + key);
  document.getElementById('mock-content').innerHTML = template.innerHTML;
  document.title = template.getAttribute('data-title') + ' (v40 mockup)';
  document.documentElement.setAttribute('data-mock-state', key);
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };

  // ---- list: the server's filter and sort, done in place --------------------
  function filterList() {
    var search = ($('#Search') || {}).value || '', type = ($('#Type') || {}).value || '', sort = ($('#Sort') || {}).value || 'Name';
    search = search.trim().toLowerCase();
    var shown = 0;
    $$('[data-v40-group]').forEach(function (group) {
      var t = group.getAttribute('data-v40-group');
      var body = $('tbody', group), rows = $$('tr[data-v40-row]', group);
      rows.sort(function (a, b) {
        if (sort === 'LastCase') { var da = a.dataset.date || '', db = b.dataset.date || ''; if (da !== db) { return da < db ? 1 : -1; } }
        if (sort === 'Type' && a.dataset.type !== b.dataset.type) { return a.dataset.type < b.dataset.type ? -1 : 1; }
        return a.dataset.name < b.dataset.name ? -1 : 1;
      }).forEach(function (r) { body.appendChild(r); });
      var visible = 0;
      rows.forEach(function (r) {
        var ok = (!search || r.dataset.search.indexOf(search) >= 0) && (t !== 'all' || !type || r.dataset.type === type);
        r.hidden = !ok; if (ok) { visible++; }
      });
      var groupOk = t === 'all' ? visible > 0 : (!type || type === t) && visible > 0;
      group.hidden = !groupOk; shown += groupOk ? visible : 0;
    });
    var empty = $('[data-v40-empty]'); if (empty) { empty.hidden = shown > 0; }
    window.__mockShown = shown;
  }
  HTMLFormElement.prototype.submit = function () { if (this.matches('[data-auto-submit]')) { filterList(); } };
  document.addEventListener('submit', function (event) {
    event.preventDefault();
    if (event.target.matches && event.target.matches('[data-auto-submit]')) { filterList(); }
  }, true);

  if ($('#Type')) {
    if (params.get('type')) { $('#Type').value = params.get('type'); }
    if (params.get('q')) { $('#Search').value = params.get('q'); }
    if (params.get('sort')) { $('#Sort').value = params.get('sort'); }
    if (key === 'list' && opts.list === 'single') {
      var all = $('#Type option[value=""]'); if (all) { all.remove(); }
      if (!$('#Type').value) { $('#Type').value = 'Principal'; }
    }
    filterList();
  }

  // ---- edit: variables ------------------------------------------------------
  $$('[data-v40-notes]').forEach(function (area) {
    if (opts.notes !== 'fit') { return; }
    area.setAttribute('data-notes-fit', '');
    var fit = function () { area.style.height = 'auto'; area.style.height = (area.scrollHeight + 2) + 'px'; };
    area.addEventListener('input', fit); window.addEventListener('resize', fit); requestAnimationFrame(fit); document.fonts.ready.then(fit);
  });
  $$('[data-v40-links-list]').forEach(function (list) { list.classList.toggle('contact-links--grid', opts.links === 'grid'); });
  $$('[data-v40-match]').forEach(function (el) { if (opts.match === 'none') { el.remove(); } });

  var stack = $('.admin-layout > .stack');
  if (stack && /^principal(-saved|-refused)?$/.test(key) && opts.layout === 'tabs') {
    var panels = $$(':scope > section.panel', stack);
    var tabs = document.createElement('div');
    tabs.className = 'tabs contact-tabs'; tabs.setAttribute('role', 'tablist'); tabs.setAttribute('aria-label', 'Contact');
    var want = params.get('tab');
    panels.forEach(function (p, i) {
      var h = $('h2', p); var name = p.getAttribute('data-v40-panel') || h.textContent.trim();
      var b = document.createElement('button'); b.type = 'button'; b.className = 'tab'; b.setAttribute('role', 'tab'); b.textContent = name;
      b.id = 'v40-tab-' + i; p.id = p.id || 'v40-tabpanel-' + i; b.setAttribute('aria-controls', p.id); p.setAttribute('role', 'tabpanel');
      b.addEventListener('click', function () { select(i); });
      tabs.appendChild(b);
    });
    stack.insertBefore(tabs, panels[0]);
    var select = function (n) {
      panels.forEach(function (p, i) { p.hidden = i !== n; tabs.children[i].setAttribute('aria-selected', String(i === n)); });
    };
    var first = 0;
    panels.forEach(function (p, i) { if (want && $('h2', p).textContent.trim() === want) { first = i; } });
    if (key === 'principal-saved') { first = panels.indexOf($('[aria-labelledby=principal-sending-title]', stack)); }
    if (key === 'principal-refused') { first = panels.indexOf($('[aria-labelledby=principal-salvage-title]', stack)); }
    select(first);
    window.__mockTabs = tabs;
  }

  // ---- states that need a nudge -------------------------------------------
  var focusSel = null;
  if (key === 'principal-saved') {
    var form = $('[data-report-sending-editor]');
    var n = document.createElement('div'); n.className = 'notice notice--success'; n.setAttribute('role', 'status'); n.setAttribute('data-v40-saved', '');
    n.textContent = "The principal's report sending rules were updated.";
    form.insertBefore(n, form.firstChild); focusSel = '[aria-labelledby=principal-sending-title]';
  }
  if (key === 'principal-refused') {
    var salvage = $('[data-salvage-matrix-editor]');
    var v = document.createElement('div'); v.className = 'validation-summary'; v.setAttribute('role', 'alert'); v.setAttribute('data-v40-refused', '');
    v.innerHTML = '<ul><li>Cat S: From must not be more than To.</li></ul>';
    salvage.insertBefore(v, salvage.firstChild); focusSel = '[aria-labelledby=principal-salvage-title]';
    var s = $('[data-salvage-bands=S] tr'); if (s) { var ins = $$('input[type=number]', s); if (ins.length > 2) { ins[0].value = '5000.00'; ins[1].value = '2500.00'; ins[2].value = '20'; } }
  }

  // ---- the strip ------------------------------------------------------------
  var strip = $('#mock-strip');
  var go = function (next) { location.search = next.toString(); };
  var stateSelect = $('#mock-state', strip);
  STATES.forEach(function (s) { var o = document.createElement('option'); o.value = s.key; o.textContent = s.label; o.selected = s.key === key; stateSelect.appendChild(o); });
  stateSelect.addEventListener('change', function () { var p = new URLSearchParams(); p.set('state', stateSelect.value); go(p); });
  var dialogSelect = $('#mock-dialog', strip);
  dialogSelect.value = params.get('dialog') ? (params.get('dialog') + (params.get('create') ? ':' + params.get('create') : '')) : '';
  dialogSelect.addEventListener('change', function () {
    var p = new URLSearchParams(); p.set('state', 'list');
    if (dialogSelect.value) { var parts = dialogSelect.value.split(':'); p.set('dialog', parts[0]); if (parts[1]) { p.set('create', parts[1]); } }
    go(p);
  });
  var vars = $('#mock-vars', strip);
  Object.keys(VARS).forEach(function (k) {
    var l = document.createElement('label'); l.innerHTML = '<small></small>';
    l.firstChild.textContent = VARS[k][1];
    var s = document.createElement('select'); s.setAttribute('data-var', k);
    VARS[k][0].forEach(function (v) { var o = document.createElement('option'); o.value = v; o.textContent = k + ': ' + v; o.selected = opts[k] === v; s.appendChild(o); });
    s.addEventListener('change', function () { opts[k] = s.value; localStorage.setItem('v40.opts', JSON.stringify(opts)); params.delete('opt'); go(params); });
    l.appendChild(s); vars.appendChild(l);
  });
  $('#mock-reset', strip).addEventListener('click', function () { localStorage.removeItem('v40.opts'); params.delete('opt'); go(params); });
  if (params.get('strip') === '0') { strip.removeAttribute('open'); }

  // No server: application links and posts stay on the page.
  document.addEventListener('click', function (event) {
    var a = event.target.closest && event.target.closest('a[href]');
    if (!a || a.closest('#mock-strip')) { return; }
    var href = a.getAttribute('href') || '';
    if (href.charAt(0) === '/' || /^https?:/.test(href)) { event.preventDefault(); }
  }, true);
  window.fetch = function () { return Promise.resolve(new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } })); };

  window.addEventListener('load', function () {
    var d = params.get('dialog');
    var target = d === 'type' ? '[data-dialog="contact-type-dialog"]' : (d === 'create' || d === 'matches') ? '[data-dialog="contact-create-' + (params.get('create') || 'Repairer') + '"]' : null;
    if (target && $(target) && $(target).pegasusOpen) { $(target).pegasusOpen(); }
    // After the page scripts have placed focus, land on the panel a state is about.
    // ponytail: the page settles late inside an iframe (fonts, fitted notes), so land twice.
    var land = function () { if (focusSel && $(focusSel)) { var el = $(focusSel); window.scrollTo(0, el.getBoundingClientRect().top + window.scrollY - 64); } };
    document.fonts.ready.then(function () {
      setTimeout(land, 200);
      setTimeout(function () { land(); requestAnimationFrame(function () { window.__mockReady = true; }); }, 700);
    });
  });
  window.__mock = { key: key, opts: opts, filter: filterList };
})();
"""


def build() -> None:
    styles = re.findall(r'<link rel="stylesheet" href="/css/([^"]+)"', capture)
    scripts = [s for s in re.findall(r'<script src="/js/([^"]+)"></script>', capture)]
    css = "\n".join(css_with_fonts(s) for s in styles)
    inline_scripts = "\n".join(
        "<script>" + asset_bytes(f"/js/{s}").decode("utf-8").replace("</script", "<\\/script") + "</script>" for s in scripts
    )
    templates = "\n".join(
        f'<template id="mock-state-{k}" data-title="{E(label)}">{inline_images(markup)}</template>' for k, label, markup in STATES
    )
    runtime = RUNTIME.replace("__STATES__", json.dumps([{"key": k, "label": l} for k, l, _ in STATES])).replace(
        "__VARS__", json.dumps({k: [v[0], v[1]] for k, v in VARIABLES.items()})
    )
    dialogs = (
        '<option value="">No dialog</option><option value="type">Add contact: choose the type</option>'
        + "".join(f'<option value="create:{t}">New {l}</option>' for t, l, _ in TYPES)
        + '<option value="matches:Repairer">New Repairer: Check existing contacts found two</option>'
    )
    strip = (
        '<details id="mock-strip" open><summary>Mockup controls</summary><div class="mock-body">'
        "<small>v40 &#xB7; Contacts. Demo control, not product UI.</small>"
        '<h4>Page and state</h4><label><select id="mock-state" aria-label="Mockup state"></select></label>'
        f'<h4>Dialogs</h4><label><select id="mock-dialog" aria-label="Mockup dialog">{dialogs}</select></label>'
        '<h4>Variables</h4><div id="mock-vars" style="display:grid;gap:6px"></div><button type="button" id="mock-reset">Reset variables</button>'
        "<small>Query string: <code>state</code>, <code>dialog</code>, <code>create</code>, <code>type</code>, <code>q</code>, <code>sort</code>, <code>tab</code>, <code>opt=key:value,&#x2026;</code>, <code>strip=0</code>.</small>"
        "</div></details>"
    )
    shell = inline_images(SHELL_BEFORE) + '<div id="mock-content"></div>' + inline_images(SHELL_AFTER)
    document = (
        '<!DOCTYPE html>\n<html lang="en" data-busy-label="Working&#x2026;" data-busy-still="Still">\n<head>\n<meta charset="utf-8" />\n'
        '<meta name="viewport" content="width=device-width, initial-scale=1.0" />\n<title>Contacts (v40 mockup)</title>\n'
        f"<style>{css}</style>\n<style>{PROPOSAL_CSS}</style>\n<style>{STRIP_CSS}</style>\n</head>\n<body>\n{shell}\n{templates}\n{strip}\n"
        f"<script>{runtime}</script>\n{inline_scripts}\n</body>\n</html>\n"
    )
    OUT.write_text(document, encoding="utf-8")
    print(f"{OUT.name}: {len(STATES)} states, {len(document) // 1024} KB")


if __name__ == "__main__":
    build()
