"""Coverage audit: every label constant and literal the live Case record
renders, checked against the built mockup. Run from the repository root:

    python design/planning-and-old-designs/v27_planning/current/v27-build/audit.py
"""
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[5]
WEB = ROOT / "src" / "Pegasus.Web"
SOURCES = [WEB / "Pages/Cases/Details.cshtml", *sorted((WEB / "Pages/Cases/Shared").glob("_Case*.cshtml")),
           WEB / "Pages/Cases/Shared/_EvaHandoff.cshtml", WEB / "Pages/Shared/_Layout.cshtml",
           WEB / "Pages/Shared/_ShellDialogs.cshtml", WEB / "Pages/Shared/_ReasonDialog.cshtml",
           WEB / "Pages/Shared/_EditFinishConfirm.cshtml"]
HTML = (pathlib.Path(__file__).resolve().parent.parent / "pegasus_case_record_v27.html").read_text(encoding="utf-8")

CONST = re.compile(r'public const string (\w+)\s*=\s*"((?:[^"\\]|\\.)*)"')
labels: dict[str, set[str]] = {}
for f in [WEB / "Presentation/CaseWorkspaceLabels.cs", WEB / "Presentation/OperatorLabels.cs"]:
    for m in CONST.finditer(f.read_text(encoding="utf-8")):
        labels.setdefault(m.group(1), set()).add(m.group(2).replace("\\u00a3", "£"))

REF = re.compile(r"@(?:[A-Za-z]+\.)+([A-Z]\w+)(?![\w(])")
LIT = re.compile(r">([A-Z][^<>@{}]{2,60})<")
refs: set = set()
for f in SOURCES:
    text = f.read_text(encoding="utf-8")
    refs.update(REF.findall(text))
    refs.update(("lit", s.strip()) for s in LIT.findall(text))


def present(value: str) -> bool:
    return value in HTML or value.replace("&", "&amp;") in HTML or value.replace("'", "&#39;") in HTML


missing = []
for ref in sorted(refs, key=str):
    if isinstance(ref, tuple):
        if ref[1] and not present(ref[1]):
            missing.append("literal: " + ref[1])
    else:
        for value in labels.get(ref, ()):
            if value and not present(value):
                missing.append(f'{ref} = "{value}"')
print(f"{len(refs)} references; {len(missing)} missing")
print("\n".join(missing))
