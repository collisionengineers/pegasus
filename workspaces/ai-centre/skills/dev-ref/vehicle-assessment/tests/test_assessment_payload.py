import importlib.util
import json
import sys
from pathlib import Path
from unittest import TestCase


SKILL_ROOT = Path(__file__).resolve().parents[2]
SCRIPTS_DIR = SKILL_ROOT / "scripts"
FIXTURES_DIR = Path(__file__).resolve().parent / "fixtures"
VALIDATOR_PATH = SCRIPTS_DIR / "validate_assessment_payload.py"


def load_validator():
    if str(SCRIPTS_DIR) not in sys.path:
        sys.path.insert(0, str(SCRIPTS_DIR))
    spec = importlib.util.spec_from_file_location("validate_assessment_payload", VALIDATOR_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


def load_fixture(name: str) -> dict:
    with (FIXTURES_DIR / name).open(encoding="utf-8") as handle:
        return json.load(handle)


class AssessmentPayloadTests(TestCase):
    def test_valid_fixture_passes(self):
        validator = load_validator()

        errors, warnings = validator.validate_payload(load_fixture("assessment_payload_valid.json"))

        self.assertEqual(errors, [])
        self.assertEqual(warnings, [])

    def test_invalid_fixture_reports_expected_errors(self):
        validator = load_validator()

        errors, _ = validator.validate_payload(load_fixture("assessment_payload_invalid.json"))

        self.assertIn("operations[0].wu must be a positive number for repair", errors)
        self.assertTrue(any(error.startswith("operations[1].type must be one of") for error in errors))

    def test_operation_justification_fields_accepted(self):
        validator = load_validator()
        payload = load_fixture("assessment_payload_valid.json")
        payload["operations"].append(
            {
                "type": "repair",
                "guide": "1481",
                "wu": 12.0,
                "desc": "LEFT FRONT DOOR",
                "justification": "Crease through lower body line - photo 4",
                "evidence_label": "case evidence",
                "status": "estimated",
            }
        )

        errors, warnings = validator.validate_payload(payload)

        self.assertEqual(errors, [])
        self.assertEqual(warnings, [])

    def test_specialist_wu_produces_routing_warning(self):
        validator = load_validator()
        payload = load_fixture("assessment_payload_valid.json")
        payload["operations"].append({"type": "specialist_wu", "desc": "QC AND ROAD TEST", "wu": 10})

        errors, warnings = validator.validate_payload(payload)

        self.assertEqual(errors, [])
        self.assertTrue(any("specialist_wu" in warning for warning in warnings))

    def test_missing_rates_and_vehicle_fields_rejected(self):
        validator = load_validator()
        payload = load_fixture("assessment_payload_valid.json")
        del payload["rates"]["labour_rate"]
        del payload["vehicle"]["vin"]

        errors, _ = validator.validate_payload(payload)

        self.assertIn("rates.labour_rate is required", errors)
        self.assertIn("vehicle.vin is required", errors)
