from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path


PLUGIN_SKILLS = {
    "repoplugin-task-contracts": {
        "persist-repository-task-artifact",
        "resolve-repository-task",
        "validate-repository-task",
    },
    "repoplugin-planning": {
        "apply-collisionspike-domain",
        "draft-implementation-plan",
        "explore-solution-options",
        "generate-plan-pack",
        "plan-repository-change",
        "research-repository-change",
        "review-implementation-plan",
        "route-collisionspike-azure",
    },
    "repoplugin-implementation": {"implement-plan-pack"},
    "repoplugin-review": {"review-implementation", "triage-pr-feedback"},
    "repoplugin-validation": {"test-and-validate-repository-change"},
    "repoplugin-debugging": {"debug-repository-failure"},
    "repoplugin-documentation": {
        "audit-repository-documentation",
        "bootstrap-repository-documentation",
        "maintain-repository-documentation",
    },
    "repoplugin-ui-ux": {"apply-collision-engineers-ui-style", "plan-ui-ux-change"},
}
INTERNAL_PLUGIN_SKILLS = {
    "persist-repository-task-artifact",
    "resolve-repository-task",
    "validate-repository-task",
    "draft-implementation-plan",
    "explore-solution-options",
    "generate-plan-pack",
    "research-repository-change",
    "review-implementation-plan",
}
REQUIRED_PLUGIN_FILES = {
    "repoplugin-task-contracts": {
        "scripts/Invoke-RepopluginTaskOperation.ps1",
        "scripts/Repoplugin.Task.psm1",
        "scripts/Test-RepopluginTask.ps1",
    },
    "repoplugin-ui-ux": {
        "skills/apply-collision-engineers-ui-style/references/migration-manifest.md"
    },
}
UI_ASSET_HASHES = {
    "FuturaCyrillicBold.ttf": "469e412e1092bdd479567d24394204910055545fa58c83d73fc902b5e6ce66fb",
    "FuturaCyrillicBook.ttf": "d3683a0e512f269edbebda9e095db4de44e77d016579cab3902b6b5779a02447",
    "FuturaCyrillicDemi.ttf": "0b9247daae3773e74cf61bd5101aebf3b0587247c2784086dabc8a8f8704cf96",
    "FuturaCyrillicMedium.ttf": "0cf4e3f4f5bf2caa3f8fb0daec6a1cf69fe246ecab846f061ef5e75d740b70c3",
    "logo_no_margin.png": "e7247be45911c46905343473e4c57b9f6ed7a450563d19c508c2d9652c2c63e2",
    "web_logo_white.png": "c7331585e122138f50efdb5cfc3a90ef45a8342c0bb36660a7d78bd3c2e988d5",
}


def fail(message: str) -> None:
    raise ValueError(message)


def load_json(path: Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise ValueError(f"{path}: required file is missing") from exc
    except json.JSONDecodeError as exc:
        raise ValueError(f"{path}: invalid JSON: {exc}") from exc


def frontmatter(skill_file: Path) -> dict[str, str]:
    lines = skill_file.read_text(encoding="utf-8").splitlines()
    if not lines or lines[0] != "---":
        fail(f"{skill_file}: missing opening frontmatter delimiter")
    try:
        closing = lines.index("---", 1)
    except ValueError as exc:
        raise ValueError(f"{skill_file}: missing closing frontmatter delimiter") from exc

    metadata: dict[str, str] = {}
    for line in lines[1:closing]:
        if not line.strip():
            continue
        key, separator, value = line.partition(":")
        if not separator:
            fail(f"{skill_file}: unsupported frontmatter line {line!r}")
        metadata[key.strip()] = value.strip().strip('"')
    if set(metadata) != {"name", "description"}:
        fail(f"{skill_file}: frontmatter must contain only name and description")
    if not metadata["description"] or re.search(r"\[?TODO\]?", skill_file.read_text(encoding="utf-8")):
        fail(f"{skill_file}: description is missing or TODO remains")
    if re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", metadata["name"]) is None:
        fail(f"{skill_file}: skill name must be lower-case hyphen-case")
    return metadata


def validate_skill(skill_dir: Path) -> str:
    skill_file = skill_dir / "SKILL.md"
    interface_file = skill_dir / "agents" / "openai.yaml"
    if not skill_file.is_file() or not interface_file.is_file():
        fail(f"{skill_dir}: SKILL.md or agents/openai.yaml is missing")

    metadata = frontmatter(skill_file)
    name = metadata["name"]
    if name != skill_dir.name:
        fail(f"{skill_dir}: skill name does not match directory")

    interface = interface_file.read_text(encoding="utf-8")
    for field in ("display_name", "short_description", "default_prompt"):
        if not re.search(rf'^\s+{field}:\s+".+"\s*$', interface, re.MULTILINE):
            fail(f"{interface_file}: missing quoted {field}")
    if f"${name}" not in interface:
        fail(f"{interface_file}: default_prompt must mention ${name}")

    suite_skills = set().union(*PLUGIN_SKILLS.values())
    if name in suite_skills:
        policy = re.search(
            r"^\s+allow_implicit_invocation:\s+(true|false)\s*$", interface, re.MULTILINE
        )
        expected = "false" if name in INTERNAL_PLUGIN_SKILLS else "true"
        if policy is None or policy.group(1) != expected:
            fail(f"{interface_file}: allow_implicit_invocation must be {expected}")

    for target in re.findall(r"\]\(([^)]+)\)", skill_file.read_text(encoding="utf-8")):
        target = target.strip().strip("<>").split("#", 1)[0]
        if not target or re.match(r"^[a-z][a-z0-9+.-]*:", target, re.IGNORECASE):
            continue
        if not (skill_dir / target).resolve().is_file():
            fail(f"{skill_file}: missing linked resource {target}")
    return name


def validate_plugin(plugin_root: Path, marketplace: dict[str, object]) -> None:
    expected_skills = PLUGIN_SKILLS.get(plugin_root.name)
    if expected_skills is None:
        fail(f"{plugin_root}: unexpected suite plugin")

    manifest_path = plugin_root / ".codex-plugin" / "plugin.json"
    manifest = load_json(manifest_path)
    if not isinstance(manifest, dict) or manifest.get("name") != plugin_root.name:
        fail(f"{manifest_path}: manifest name must match the plugin directory")
    if re.fullmatch(r"\d+\.\d+\.\d+", str(manifest.get("version", ""))) is None:
        fail(f"{manifest_path}: version must be strict semver")
    if manifest.get("skills") != "./skills/":
        fail(f"{manifest_path}: skills must be ./skills/")
    for unsupported in ("apps", "mcpServers", "hooks"):
        if unsupported in manifest:
            fail(f"{manifest_path}: {unsupported} is not part of this skills-only plugin")

    actual_skills = {
        path.name
        for path in (plugin_root / "skills").iterdir()
        if path.is_dir() and (path / "SKILL.md").is_file()
    }
    if actual_skills != expected_skills:
        fail(
            f"{plugin_root}: skill set mismatch; "
            f"missing={sorted(expected_skills - actual_skills)}, "
            f"unexpected={sorted(actual_skills - expected_skills)}"
        )
    for relative in REQUIRED_PLUGIN_FILES.get(plugin_root.name, set()):
        if not (plugin_root / relative).is_file():
            fail(f"{plugin_root}: required file is missing: {relative}")
    if plugin_root.name == "repoplugin-ui-ux":
        asset_root = plugin_root / "skills" / "apply-collision-engineers-ui-style" / "assets"
        actual_assets = {path.name for path in asset_root.iterdir() if path.is_file()}
        if actual_assets != set(UI_ASSET_HASHES):
            fail(
                f"{asset_root}: approved asset set mismatch; "
                f"missing={sorted(set(UI_ASSET_HASHES) - actual_assets)}, "
                f"unexpected={sorted(actual_assets - set(UI_ASSET_HASHES))}"
            )
        for name, expected_hash in UI_ASSET_HASHES.items():
            actual_hash = hashlib.sha256((asset_root / name).read_bytes()).hexdigest()
            if actual_hash != expected_hash:
                fail(f"{asset_root / name}: approved asset hash mismatch")

    entries = marketplace.get("plugins")
    if not isinstance(entries, list):
        fail("marketplace plugins must be an array")
    matching = [entry for entry in entries if isinstance(entry, dict) and entry.get("name") == plugin_root.name]
    if len(matching) != 1:
        fail(f"marketplace must contain exactly one {plugin_root.name} entry")
    entry = matching[0]
    if entry.get("source") != {"source": "local", "path": f"./plugins/{plugin_root.name}"}:
        fail(f"marketplace source is incorrect for {plugin_root.name}")
    if entry.get("policy") != {"installation": "AVAILABLE", "authentication": "ON_INSTALL"}:
        fail(f"marketplace policy is incorrect for {plugin_root.name}")
    if entry.get("category") != "Productivity":
        fail(f"marketplace category is incorrect for {plugin_root.name}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate repository and plugin skills.")
    parser.add_argument("roots", nargs="*", type=Path, default=[Path(".codex/skills")])
    parser.add_argument("--plugin", action="append", type=Path, default=[])
    parser.add_argument("--marketplace", type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if bool(args.plugin) != (args.marketplace is not None):
        fail("--plugin and --marketplace must be supplied together")

    skills: list[Path] = []
    for root in args.roots:
        found = sorted(path.parent.parent for path in root.glob("*/agents/openai.yaml"))
        if not found:
            fail(f"{root}: no project skills found")
        skills.extend(found)

    names: set[str] = set()
    for skill in skills:
        name = validate_skill(skill)
        if name in names:
            fail(f"duplicate skill name across validated roots: {name}")
        names.add(name)

    if args.plugin:
        plugin_names = {path.name for path in args.plugin}
        if plugin_names != set(PLUGIN_SKILLS):
            fail(
                "suite plugin set mismatch; "
                f"missing={sorted(set(PLUGIN_SKILLS) - plugin_names)}, "
                f"unexpected={sorted(plugin_names - set(PLUGIN_SKILLS))}"
            )
        marketplace = load_json(args.marketplace)
        if not isinstance(marketplace, dict) or marketplace.get("name") != "personal":
            fail(f"{args.marketplace}: marketplace name must be personal")
        if any(
            isinstance(entry, dict) and entry.get("name") == "repoplugin"
            for entry in marketplace.get("plugins", [])
        ):
            fail(f"{args.marketplace}: superseded repoplugin entry remains")
        for plugin_root in args.plugin:
            validate_plugin(plugin_root, marketplace)

    print(f"Validated {len(skills)} project skills and {len(args.plugin)} suite plugins.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(error, file=sys.stderr)
        raise SystemExit(1) from error
