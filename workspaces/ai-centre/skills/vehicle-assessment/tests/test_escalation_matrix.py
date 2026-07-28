import importlib.util
import sys
from pathlib import Path
from unittest import TestCase


SKILL_ROOT = Path(__file__).resolve().parents[1]
SCRIPTS_DIR = SKILL_ROOT / "scripts"
VALIDATOR_PATH = SCRIPTS_DIR / "validate_escalation_matrix.py"


def load_validator():
    if str(SCRIPTS_DIR) not in sys.path:
        sys.path.insert(0, str(SCRIPTS_DIR))
    spec = importlib.util.spec_from_file_location("validate_escalation_matrix", VALIDATOR_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class EscalationMatrixTests(TestCase):
    def test_default_matrix_is_valid(self):
        validator = load_validator()

        errors = validator.validate(validator.load())

        self.assertEqual(errors, [])

    def test_default_matrix_covers_core_zones(self):
        validator = load_validator()
        data = validator.load()
        zone_ids = {zone["zone"] for zone in data["impact_zones"]}

        for expected in ["front_corner", "side_sill_b_pillar", "wheel_kerb", "underbody", "water_flood", "fire", "ev_battery_zone"]:
            self.assertIn(expected, zone_ids)

    def test_unknown_escalation_code_rejected(self):
        validator = load_validator()
        data = validator.load()
        data["impact_zones"][0]["escalations"].append("made_up_code")

        errors = validator.validate(data)

        self.assertTrue(any("unknown code 'made_up_code'" in error for error in errors))

    def test_duplicate_zone_and_empty_lists_rejected(self):
        validator = load_validator()
        data = validator.load()
        data["impact_zones"].append(dict(data["impact_zones"][0]))
        data["impact_zones"][1]["systems_at_risk"] = []

        errors = validator.validate(data)

        self.assertTrue(any("is duplicated" in error for error in errors))
        self.assertIn("impact_zones[1].systems_at_risk must be a non-empty list of strings", errors)
