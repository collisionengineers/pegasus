from __future__ import annotations

import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock
import urllib.error

import github_research as research


def repository(name: str, **overrides: object) -> dict[str, object]:
    value: dict[str, object] = {
        "full_name": name,
        "name": name.split("/", 1)[1],
        "owner": {"login": name.split("/", 1)[0]},
        "html_url": f"https://github.com/{name}",
        "description": f"Description for {name}",
        "language": "Python",
        "topics": ["research"],
        "license": {"spdx_id": "MIT", "name": "MIT License"},
        "stargazers_count": 10,
        "forks_count": 2,
        "watchers_count": 10,
        "open_issues_count": 1,
        "default_branch": "main",
        "fork": False,
        "archived": False,
        "disabled": False,
        "private": False,
        "visibility": "public",
        "size": 100,
        "created_at": "2025-01-01T00:00:00Z",
        "updated_at": "2026-01-01T00:00:00Z",
        "pushed_at": "2026-01-01T00:00:00Z",
    }
    value.update(overrides)
    return value


class FakeClient:
    name = "fake"

    def __init__(self, *, authenticated: bool = False) -> None:
        self.authenticated = authenticated
        self.rate_limit = {"remaining": 42}
        self.calls: list[tuple[str, dict[str, object], bool]] = []
        self.responses: dict[str, object] = {}
        self.errors: dict[str, Exception] = {}

    def get(self, path: str, params: dict[str, object] | None = None, *, allow_404: bool = False) -> object:
        self.calls.append((path, params or {}, allow_404))
        if path in self.errors:
            raise self.errors[path]
        if path in self.responses:
            return self.responses[path]
        if path.endswith("/readme") or path.endswith("/releases/latest") or path.endswith("/community/profile"):
            return None
        if path.endswith("/contents") or path.endswith("/commits") or path.endswith("/issues") or path.endswith("/pulls"):
            return []
        if path.endswith("/releases") or path.endswith("/contributors"):
            return []
        if path == "search/code":
            return {"items": []}
        raise AssertionError(f"Unexpected request: {path}")


class FakeResponse:
    def __init__(self, value: object, headers: dict[str, str] | None = None) -> None:
        self.body = json.dumps(value).encode()
        self.headers = headers or {}

    def read(self, *args: object, **kwargs: object) -> bytes:
        return self.body

    def __enter__(self) -> "FakeResponse":
        return self

    def __exit__(self, *args: object) -> None:
        return None


class QueryTests(unittest.TestCase):
    def test_query_enforces_public_defaults_and_filters(self) -> None:
        query = research.build_query(
            "embedded analytics",
            language="TypeScript",
            topics=["dashboard"],
            min_stars=25,
            updated_after="2025-01-01",
        )
        self.assertEqual(
            query,
            'embedded analytics is:public fork:false archived:false language:"TypeScript" topic:dashboard stars:>=25 pushed:>=2025-01-01',
        )

    def test_query_rejects_private_or_internal_scope(self) -> None:
        for query in ("widgets is:private", "widgets visibility:internal"):
            with self.subTest(query=query), self.assertRaises(ValueError):
                research.build_query(query)

    def test_public_check_rejects_private_records(self) -> None:
        self.assertTrue(research.public_repository(repository("one/public")))
        self.assertFalse(research.public_repository(repository("one/private", private=True, visibility="private")))


class SearchTests(unittest.TestCase):
    def test_search_paginates_deduplicates_and_skips_private(self) -> None:
        client = FakeClient()
        first_page = [repository(f"owner/repo-{index}") for index in range(100)]
        private = repository("owner/private", private=True, visibility="private")

        def get(path: str, params: dict[str, object] | None = None, *, allow_404: bool = False) -> object:
            self.assertEqual(path, "search/repositories")
            page = (params or {}).get("page")
            if page == 1:
                return {"total_count": 103, "incomplete_results": False, "items": first_page}
            return {"total_count": 103, "incomplete_results": False, "items": [first_page[0], private, repository("owner/final")]}

        client.get = get  # type: ignore[method-assign]
        result = research.search_repositories(client, "topic is:public", 101)
        self.assertEqual(result["returned"], 101)
        names = [item["full_name"] for item in result["repositories"]]
        self.assertEqual(names[-1], "owner/final")
        self.assertNotIn("owner/private", names)


class InspectionTests(unittest.TestCase):
    def configured_client(self, *, authenticated: bool = False) -> FakeClient:
        client = FakeClient(authenticated=authenticated)
        client.responses["repos/owner/project"] = repository("owner/project")
        client.responses["repos/owner/project/readme"] = {
            "name": "README.md",
            "path": "README.md",
            "sha": "abc",
            "html_url": "https://github.com/owner/project/blob/main/README.md",
            "encoding": "base64",
            "content": "SGVsbG8gcmVzZWFyY2g=",
        }
        client.responses["repos/owner/project/releases/latest"] = {
            "tag_name": "v1.0.0",
            "name": "One",
            "published_at": "2026-01-01T00:00:00Z",
            "draft": False,
            "prerelease": False,
            "html_url": "https://github.com/owner/project/releases/tag/v1.0.0",
        }
        return client

    def test_quick_inspection_decodes_readme_without_activity_calls(self) -> None:
        client = self.configured_client()
        result = research.inspect_repository(client, "owner/project", "quick")
        self.assertEqual(result["readme"]["text"], "Hello research")
        paths = [call[0] for call in client.calls]
        self.assertNotIn("repos/owner/project/commits", paths)
        self.assertEqual(result["latest_release"]["tag"], "v1.0.0")

    def test_standard_inspection_preserves_partial_errors(self) -> None:
        client = self.configured_client()
        client.errors["repos/owner/project/commits"] = research.APIError(503, "temporarily unavailable")
        result = research.inspect_repository(client, "owner/project", "standard")
        self.assertEqual(result["errors"][0]["source"], "commits")
        self.assertEqual(result["errors"][0]["status"], 503)
        self.assertEqual(result["repository"]["visibility"], "public")

    def test_deep_inspection_reports_skipped_unauthenticated_code_search(self) -> None:
        client = self.configured_client(authenticated=False)
        result = research.inspect_repository(client, "owner/project", "deep", ["PluginRegistry"])
        self.assertIn("Authenticated GitHub access was unavailable, so requested code search was skipped", result["gaps"])
        self.assertFalse(any(path == "search/code" for path, _, _ in client.calls))

    def test_non_public_detail_is_refused(self) -> None:
        client = FakeClient(authenticated=True)
        client.responses["repos/owner/private"] = repository("owner/private", private=True, visibility="private")
        with self.assertRaises(research.PublicOnlyError):
            research.inspect_repository(client, "owner/private", "quick")


class BackendTests(unittest.TestCase):
    @mock.patch.object(research, "gh_is_authenticated", return_value=False)
    @mock.patch.object(research.shutil, "which", return_value="/usr/bin/gh")
    def test_auto_falls_back_to_rest(self, which: mock.Mock, authenticated: mock.Mock) -> None:
        client = research.make_client("auto")
        self.assertIsInstance(client, research.RestClient)

    @mock.patch.object(research, "gh_is_authenticated", return_value=True)
    @mock.patch.object(research.shutil, "which", return_value="/usr/bin/gh")
    def test_auto_prefers_authenticated_gh(self, which: mock.Mock, authenticated: mock.Mock) -> None:
        client = research.make_client("auto")
        self.assertIsInstance(client, research.GhClient)

    def test_rest_uses_get_retries_and_records_rate_limit_without_token(self) -> None:
        error = urllib.error.HTTPError(
            "https://api.github.com/example",
            503,
            "Unavailable",
            {},
            io.BytesIO(b'{"message":"try again"}'),
        )
        response = FakeResponse({"ok": True}, {"X-RateLimit-Remaining": "59", "X-RateLimit-Limit": "60"})
        sleeper = mock.Mock()
        client = research.RestClient(token="", sleeper=sleeper)
        with mock.patch.object(research.urllib.request, "urlopen", side_effect=[error, response]) as urlopen:
            value = client.get("example")
        self.assertEqual(value, {"ok": True})
        self.assertEqual(urlopen.call_args.args[0].get_method(), "GET")
        self.assertNotIn("Authorization", urlopen.call_args.args[0].headers)
        sleeper.assert_called_once()
        self.assertEqual(client.rate_limit["remaining"], 59)

    @mock.patch.object(research.subprocess, "run")
    def test_gh_uses_get_and_parses_json(self, run: mock.Mock) -> None:
        run.return_value = subprocess.CompletedProcess([], 0, '{"ok": true}', "")
        client = research.GhClient("gh")
        self.assertEqual(client.get("rate_limit"), {"ok": True})
        command = run.call_args.args[0]
        self.assertEqual(command[command.index("--method") + 1], "GET")

    def test_rate_limit_snapshot_reads_gh_resource_shape(self) -> None:
        client = FakeClient(authenticated=True)
        client.rate_limit = {}
        client.responses["rate_limit"] = {
            "resources": {
                "core": {"limit": 5000, "remaining": 4999, "used": 1, "reset": 123},
                "search": {"limit": 30, "remaining": 29, "used": 1, "reset": 456},
            }
        }
        snapshot = research.rate_limit_snapshot(client)
        self.assertEqual(snapshot["search"]["remaining"], 29)
        self.assertEqual(snapshot["core"]["reset"], 123)


class OutputAndCliTests(unittest.TestCase):
    def test_collect_inspects_explicit_repository_without_search(self) -> None:
        client = FakeClient()
        client.responses["repos/owner/project"] = repository("owner/project")
        args = research.build_parser().parse_args(["collect", "--repo", "owner/project", "--depth", "quick"])
        result = research.run_collect(args, client)
        self.assertIsNone(result["search"])
        self.assertEqual(result["coverage"]["completed_inspections"], 1)
        self.assertEqual(result["inspected"][0]["repository"]["full_name"], "owner/project")

    def test_emit_writes_same_deterministic_json_to_stdout_and_file(self) -> None:
        value = {"z": 1, "a": {"b": 2}}
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "nested" / "evidence.json"
            stream = io.StringIO()
            with mock.patch.object(research.sys, "stdout", stream):
                research.emit(value, output)
            self.assertEqual(stream.getvalue(), output.read_text(encoding="utf-8"))
            self.assertLess(stream.getvalue().index('"a"'), stream.getvalue().index('"z"'))

    def test_parser_rejects_invalid_repository(self) -> None:
        parser = research.build_parser()
        with mock.patch.object(research.sys, "stderr", io.StringIO()), self.assertRaises(SystemExit):
            parser.parse_args(["inspect", "not-a-repository"])

    def test_collect_requires_query_or_repo(self) -> None:
        with mock.patch.object(research, "make_client") as make_client, mock.patch.object(
            research.sys, "stderr", io.StringIO()
        ), self.assertRaises(SystemExit):
            research.main(["collect"])
        make_client.assert_not_called()


if __name__ == "__main__":
    unittest.main()
