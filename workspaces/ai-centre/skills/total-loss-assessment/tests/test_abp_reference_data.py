import importlib.util
import sys
from pathlib import Path
from unittest import TestCase


SKILL_ROOT = Path(__file__).resolve().parents[1]
SCRIPTS_DIR = SKILL_ROOT / "scripts"
VALIDATOR_PATH = SCRIPTS_DIR / "validate_abp_reference_data.py"


def load_validator():
    if str(SCRIPTS_DIR) not in sys.path:
        sys.path.insert(0, str(SCRIPTS_DIR))
    spec = importlib.util.spec_from_file_location("validate_abp_reference_data", VALIDATOR_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class AbpReferenceDataTests(TestCase):
    def test_default_reference_data_is_valid(self):
        validator = load_validator()

        errors = validator.validate(validator.load())

        self.assertEqual(errors, [])

    def test_rejects_invalid_labour_rate_and_extra_type(self):
        validator = load_validator()
        data = validator.load()
        data["labour_rates"]["standard_per_hour"] = 0
        data["always_include_extras"][0]["type"] = "rnr"

        errors = validator.validate(data)

        self.assertIn("labour_rates.standard_per_hour must be a positive number", errors)
        self.assertIn("always_include_extras[0].type must be one of ['specialist_fixed', 'specialist_wu']", errors)

    def test_rejects_wrong_sundry_parts_unit(self):
        validator = load_validator()
        data = validator.load()
        data["materials"]["sundry_parts_pct"] = 0.035

        errors = validator.validate(data)

        self.assertIn("materials.sundry_parts_pct must be 3.5 percentage points", errors)

    def test_malformed_nested_data_returns_errors(self):
        validator = load_validator()
        data = validator.load()
        data["labour_rates"] = []
        data["materials"] = []
        data["always_include_extras"] = ["not an object"]

        errors = validator.validate(data)

        self.assertIn("labour_rates must be an object", errors)
        self.assertIn("materials must be an object", errors)
        self.assertIn("always_include_extras[0] must be an object", errors)
