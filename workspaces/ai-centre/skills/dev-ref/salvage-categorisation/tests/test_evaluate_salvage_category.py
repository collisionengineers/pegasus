import json
import importlib.util
import subprocess
import sys
import unittest
from pathlib import Path


SKILL_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = SKILL_ROOT / "scripts" / "evaluate_salvage_category.py"
FIXTURES = Path(__file__).resolve().parent / "fixtures"


def load_evaluator():
    spec = importlib.util.spec_from_file_location("evaluate_salvage_category", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class SalvageDecisionTableTests(unittest.TestCase):
    def run_fixture(self, name: str):
        result = subprocess.run(
            [sys.executable, str(SCRIPT), str(FIXTURES / name)],
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
        return result, json.loads(result.stdout)

    def test_positive_cat_s_fixture(self):
        result, payload = self.run_fixture("cat_s_positive.json")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(payload["category"], "S")
        self.assertEqual(payload["confidence"], "final")
        self.assertEqual(payload["matched_rule"], "cat-s-structural-repairable")
        self.assertFalse(payload["review_required"])

    def test_negative_unresolved_fixture_requires_review(self):
        result, payload = self.run_fixture("unresolved_negative.json")

        self.assertEqual(result.returncode, 1)
        self.assertIsNone(payload["category"])
        self.assertEqual(payload["confidence"], "unresolved")
        self.assertTrue(payload["review_required"])
        self.assertIn("structural_damage_unknown", payload["review_triggers"])
        self.assertIn("hv_battery", payload["review_triggers"])

    def test_human_readable_special_factors_force_review(self):
        evaluator = load_evaluator()
        table = evaluator.load_json(SKILL_ROOT / "references" / "salvage-decision-table.v1.json")

        result = evaluator.evaluate({
            "repairable": True,
            "structural_damage": True,
            "special_factors": ["HV battery", "parts-reuse"],
            "evidence_quality": "strong",
        }, table)

        self.assertEqual(result["category"], "S")
        self.assertEqual(result["confidence"], "provisional")
        self.assertTrue(result["review_required"])
        self.assertEqual(result["normalised_inputs"]["special_factors"], ["hv_battery", "parts_reuse"])
        self.assertIn("hv_battery", result["review_triggers"])
        self.assertIn("parts_reuse", result["review_triggers"])



if __name__ == "__main__":
    unittest.main()
