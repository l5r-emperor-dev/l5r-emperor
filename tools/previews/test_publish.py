import unittest
import tempfile
from pathlib import Path
from unittest.mock import patch
from publish import deliver, verify_asset, stage
from package import REPO, digest


class PublishTests(unittest.TestCase):
    def test_missing_or_corrupt_companion_cannot_be_delivered(self):
        receipt = {"asset": "review.zip", "bytes": 12, "sha256": "a" * 64,
                   "companions": [{"asset": "review-Part2.zip", "bytes": 9, "sha256": "b" * 64}]}
        release = {"assets": [{"name": "review.zip", "size": 12, "digest": "sha256:" + "a" * 64}]}
        with self.assertRaises(ValueError):
            verify_asset(receipt, release)
        release["assets"].append({"name": "review-Part2.zip", "size": 9, "digest": "sha256:" + "c" * 64})
        with self.assertRaises(ValueError):
            verify_asset(receipt, release)

    def test_delayed_draft_listing_resumes_one_release_without_notification(self):
        with tempfile.TemporaryDirectory() as temp:
            archive = Path(temp) / "review.zip"
            archive.write_bytes(b"validated fixture package")
            spec = {"source_commit": "a" * 40, "source_pr": 410, "title": "Review"}
            receipt = {"repository": REPO, "source_commit": spec["source_commit"],
                       "asset": archive.name, "bytes": archive.stat().st_size,
                       "sha256": digest(archive), "tag": "preview-fixture"}
            draft = {"draft": True, "assets": []}
            uploaded = {"draft": True, "assets": [{"name": archive.name,
                "size": receipt["bytes"], "digest": "sha256:" + receipt["sha256"]}]}
            with patch("publish.api", return_value={"private": True}), patch("publish.gh") as gh, \
                 patch("publish.release_for", side_effect=[None, None, draft, uploaded]), patch("publish.time.sleep"):
                stage(spec, receipt, archive)
                self.assertEqual(sum(c.args[:2] == ("release", "create") for c in gh.call_args_list), 1)
                self.assertEqual(sum(c.args[:2] == ("release", "upload") for c in gh.call_args_list), 1)
                self.assertFalse(any("POST" in c.args for c in gh.call_args_list))

    def test_public_release_requires_explicit_owner_authorized_opt_in(self):
        with tempfile.TemporaryDirectory() as temp:
            archive = Path(temp) / "review.zip"
            archive.write_bytes(b"owner-authorized game preview")
            spec = {"source_commit": "a" * 40, "source_pr": 1200, "title": "Restrictions"}
            receipt = {"repository": REPO, "source_commit": spec["source_commit"],
                       "asset": archive.name, "bytes": archive.stat().st_size,
                       "sha256": digest(archive), "tag": "preview-fixture"}
            with patch("publish.api", return_value={"private": False}), patch("publish.gh") as gh:
                with self.assertRaisesRegex(ValueError, "world-readable"):
                    stage(spec, receipt, archive)
                gh.assert_not_called()
            draft = {"draft": True, "assets": []}
            uploaded = {"draft": True, "assets": [{"name": archive.name,
                "size": receipt["bytes"], "digest": "sha256:" + receipt["sha256"]}]}
            with patch("publish.api", return_value={"private": False}), patch("publish.gh") as gh, \
                 patch("publish.release_for", side_effect=[None, draft, uploaded]):
                stage(spec, receipt, archive, allow_public_repository=True)
                self.assertEqual(sum(c.args[:2] == ("release", "upload") for c in gh.call_args_list), 1)
                self.assertFalse(any("POST" in c.args for c in gh.call_args_list))

    def test_corrupt_or_incomplete_upload_is_rejected(self):
        receipt = {"asset": "review.zip", "bytes": 12, "sha256": "a" * 64}
        for assets in [[], [{"name": "review.zip", "size": 11, "digest": "sha256:" + "a" * 64}],
                       [{"name": "review.zip", "size": 12, "digest": "sha256:" + "b" * 64}]]:
            with self.subTest(assets=assets), self.assertRaises(ValueError):
                verify_asset(receipt, {"assets": assets})

    def test_no_notification_before_exact_windows_check_passes(self):
        for runs in [[], [{"name": "Windows review package", "id": 1, "conclusion": "failure"}],
                     [{"name": "Unrelated green test", "id": 1, "conclusion": "success"}],
                     [{"name": "Windows review package", "id": 1, "conclusion": "success"},
                      {"name": "Windows review package", "id": 2, "conclusion": None}]]:
            with self.subTest(runs=runs), patch("publish.api", return_value={"check_runs": runs}), patch("publish.gh") as gh:
                with self.assertRaises(ValueError):
                    deliver({}, {}, "a" * 40)
                gh.assert_not_called()

    def test_different_receipt_cannot_reuse_green_check(self):
        with patch("publish.api", return_value={"check_runs": [
            {"name": "Windows review package", "id": 1, "conclusion": "success"}
        ]}), patch("publish.gh", return_value='{"sha256":"different"}') as gh:
            with self.assertRaises(ValueError):
                deliver({"slug": "fixture"}, {"sha256": "expected"}, "a" * 40)
            self.assertEqual(gh.call_count, 1)
            self.assertNotIn("POST", gh.call_args.args)


if __name__ == "__main__":
    unittest.main()
