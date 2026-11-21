#!/usr/bin/env python3
"""Build a versioned, self-contained Windows review from an exact source commit."""
import argparse
import hashlib
import json
import re
import shutil
import subprocess
import tempfile
import zipfile
from pathlib import Path

REPO = "l5r-emperor-dev/l5r-emperor"
ROOT = Path(__file__).resolve().parents[2]
RELEASE_LIMIT = 2 * 1024**3 - 1


def split_delivery(archive_path, receipt, spec, limit=RELEASE_LIMIT, chunk_size=1400 * 1024**2):
    """Keep the exact full ZIP; distribute oversized builds as extract-together ZIPs."""
    if archive_path.stat().st_size <= limit:
        return receipt
    if not 0 < chunk_size < limit:
        raise ValueError("Chunk size must be smaller than the release limit")
    complete = archive_path.with_name(archive_path.stem + "-Complete.zip")
    if complete.exists():
        raise ValueError("Complete archive already exists; use a new output/version")
    archive_path.rename(complete)
    bundle = {"schema_version": 1, "folder": receipt["folder"], "source_commit": receipt["source_commit"],
              "bytes": complete.stat().st_size, "sha256": digest(complete), "parts": []}
    assets = []
    with tempfile.TemporaryDirectory(prefix="parts-", dir=archive_path.parent) as temporary:
        with complete.open("rb") as source:
            index = 0
            while source.tell() < complete.stat().st_size:
                index += 1
                part = Path(temporary) / f"game.zip.{index:03}"
                remaining = chunk_size
                with part.open("wb") as output:
                    while remaining and (block := source.read(min(remaining, 8 * 1024**2))):
                        output.write(block)
                        remaining -= len(block)
                bundle["parts"].append({"file": part.name, "bytes": part.stat().st_size, "sha256": digest(part)})
        for index, part in enumerate(bundle["parts"], 1):
            asset = archive_path if index == 1 else archive_path.with_name(archive_path.stem + f"-Part{index}.zip")
            with zipfile.ZipFile(asset, "w", zipfile.ZIP_STORED) as output:
                prefix = receipt["folder"] + "/"
                output.write(Path(temporary) / part["file"], prefix + part["file"])
                if index == 1:
                    output.writestr(prefix + "review-bundle.json", json.dumps(bundle, indent=2) + "\n")
                    output.write(ROOT / "tools/previews/prepare_review.ps1", prefix + "Prepare Review.ps1")
                    output.writestr(prefix + "Start Review.cmd",
                        '@echo off\r\nsetlocal DisableDelayedExpansion\r\n'
                        'set "L5R_REVIEW_ROOT=%~dp0"\r\n'
                        'powershell.exe -NoProfile -Command "try { & ([scriptblock]::Create([IO.File]::ReadAllText($env:L5R_REVIEW_ROOT + \'Prepare Review.ps1\'))) -BundleRoot $env:L5R_REVIEW_ROOT; exit 0 } catch { Write-Host $_; exit 1 }"\r\n'
                        'if errorlevel 1 pause\r\n')
                    output.writestr(prefix + "READ ME FIRST.txt",
                        "Download ALL ZIP parts from this version. Extract every ZIP into the SAME destination folder.\n"
                        "Open Start Review.cmd. It verifies and prepares the complete game on first launch, then opens it.\n"
                        "No build tools, runtime downloads, administrator access or security-setting changes are needed.\n\n"
                        + readme(spec))
                    output.writestr(prefix + "Send Feedback.url",
                        f"[InternetShortcut]\r\nURL=https://github.com/{REPO}/pull/{spec['source_pr']}\r\n")
            if asset.stat().st_size > limit:
                raise ValueError("A delivery ZIP still exceeds the release limit")
            assets.append({"asset": asset.name, "bytes": asset.stat().st_size, "sha256": digest(asset)})
    return {**receipt, **assets[0], "companions": assets[1:],
            "complete_archive": {"bytes": bundle["bytes"], "sha256": bundle["sha256"]}}


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read_spec(path):
    spec = json.loads(path.read_text(encoding="utf-8"))
    if spec.get("schema_version") != 1:
        raise ValueError("Unsupported preview specification")
    if not re.fullmatch(r"[a-z][a-z0-9-]{1,48}", spec["slug"]):
        raise ValueError("Invalid preview slug")
    if not re.fullmatch(r"[a-f0-9]{40}", spec["source_commit"]):
        raise ValueError("Use an exact 40-character source commit")
    if type(spec["source_pr"]) is not int or spec["source_pr"] < 1:
        raise ValueError("Invalid source PR")
    if type(spec["revision"]) is not int or spec["revision"] < 1:
        raise ValueError("Invalid package revision")
    if not re.fullmatch(r"[a-zA-Z0-9-]+", spec["reviewer"]):
        raise ValueError("Invalid reviewer")
    names = []
    for scenario in spec["scenarios"]:
        name, mode = scenario["name"], scenario["mode"]
        if not re.fullmatch(r"(?:Other previews/)?[A-Za-z][A-Za-z0-9 -]{1,55}", name):
            raise ValueError("Unsafe scenario filename")
        if mode is not None and not re.fullmatch(r"[a-z][a-z0-9-]{1,100}", mode):
            raise ValueError("Unsafe capture mode")
        names.append(name.lower())
    if len(set(names)) != len(names) or "start review" not in names:
        raise ValueError("Preview needs unique launchers and Start Review")
    return spec


def identity(spec):
    return f"pr{spec['source_pr']}-{spec['source_commit'][:10]}-r{spec['revision']}"


def launcher(spec, scenario):
    # Never CALL user-derived paths: a second cmd expansion corrupts percent signs.
    # Delayed expansion stays off so Windows usernames containing ! work too.
    parent = "..\\" if "/" in scenario["name"] else ""
    ident = identity(spec)
    mode = scenario["mode"]
    args = f"--profile=review-{spec['source_commit'][:10]}"
    if mode:
        args += f' --capture="%L5R_REVIEW_DATA%\\{mode}.png" --capture-mode={mode}'
    text = f'''@echo off
setlocal DisableDelayedExpansion
set "L5R_REVIEW_ROOT=%~dp0{parent}"
set "L5R_REVIEW_DATA=%LOCALAPPDATA%\\L5R\\Previews\\{ident}"
if not exist "%L5R_REVIEW_ROOT%game\\L5R-Emperor.exe" goto missing
if not exist "%L5R_REVIEW_ROOT%game\\L5R-Emperor.pck" goto missing
if not exist "%L5R_REVIEW_DATA%" mkdir "%L5R_REVIEW_DATA%"
if not exist "%L5R_REVIEW_DATA%" goto storage
start "L5R Preview" /D "%L5R_REVIEW_ROOT%game" "%L5R_REVIEW_ROOT%game\\L5R-Emperor.exe" --resolution 1440x900 --log-file "%L5R_REVIEW_DATA%\\game.log" -- {args}
exit /b 0
:missing
echo Extract the entire ZIP first, then open Start Review.cmd from the extracted folder.
echo Keep the game folder beside this launcher.
pause
exit /b 1
:storage
echo Could not create the preview's local data folder. Please send this message with your feedback.
pause
exit /b 1
'''
    return text.replace("\n", "\r\n")


def readme(spec):
    steps = "\n".join(f"{i}. {step}" for i, step in enumerate(spec["review_steps"], 1))
    limits = "\n".join(f"- {item}" for item in spec["known_limitations"])
    return f'''L5R REVIEW — {spec['title']}

Alfredo would like Mendon to review this preview.

START HERE
Right-click the downloaded ZIP > Extract All. Open the extracted folder and
double-click Start Review.cmd. Keep the game folder and all its files together.
No Godot, .NET SDK, Git or source build is required. Once downloaded, it runs offline.
If Windows shows an unsigned-app warning, check that this is the package you
downloaded from the linked GitHub release; ask Claude for help if unsure.

WHAT TO TRY
{steps}

FEEDBACK
Open Send Feedback.url and leave a comment describing what happened, what you
expected and which preview you opened. Screenshots are welcome. You can also
reply to the GitHub notification email. A comment records playtest feedback;
formal PR approval is a separate GitHub review action.

Your feedback page: https://github.com/{REPO}/pull/{spec['source_pr']}
Package: {identity(spec)}
Source commit: {spec['source_commit']}

You can close and reopen a launcher to reset its prepared scenario. The full-game
launcher opens this same preview build's menus. Settings/decks are isolated under
%APPDATA%\\L5R\\Previews\\{identity(spec)}. Logs and prepared-table captures are in
%LOCALAPPDATA%\\L5R\\Previews\\{identity(spec)}.

KNOWN LIMITATIONS
{limits}
'''


def prepare_folder(spec, export_zip, folder):
    folder.mkdir(parents=True)
    game = folder / "game"
    game.mkdir()
    with zipfile.ZipFile(export_zip) as archive:
        for member in archive.infolist():
            name = member.filename
            path = Path(name)
            if (path.is_absolute() or ".." in path.parts or "\\" in name or ":" in name
                    or ((member.external_attr >> 16) & 0o170000) == 0o120000):
                raise ValueError("Unsafe path in exported ZIP")
            if path.suffix.lower() in {".cs", ".pdb", ".csproj", ".sln", ".slnx"}:
                raise ValueError("Developer files must not ship in a preview")
        archive.extractall(game)
    for filename in ("L5R-Emperor.exe", "L5R-Emperor.pck",
                     "data_L5R.Client_windows_x86_64/L5R.Client.dll",
                     "data_L5R.Client_windows_x86_64/L5R.Core.dll",
                     "data_L5R.Client_windows_x86_64/hostfxr.dll"):
        if not (game / filename).is_file() or (game / filename).stat().st_size == 0:
            raise ValueError(f"Export is missing {filename}")
    # Godot reads this beside the exported binary before resolving user://.
    (game / "override.cfg").write_text(
        '[application]\nconfig/use_custom_user_dir=true\n'
        f'config/custom_user_dir_name="L5R/Previews/{identity(spec)}"\n', encoding="utf-8")
    for scenario in spec["scenarios"]:
        path = folder / (scenario["name"] + ".cmd")
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(launcher(spec, scenario).encode("utf-8"))
    (folder / "READ ME FIRST.txt").write_text(readme(spec), encoding="utf-8")
    (folder / "Send Feedback.url").write_bytes(
        f"[InternetShortcut]\r\nURL=https://github.com/{REPO}/pull/{spec['source_pr']}\r\n".encode())
    (folder / "preview.json").write_text(json.dumps(spec, indent=2) + "\n", encoding="utf-8")


def verify_source(source, spec):
    head = subprocess.check_output(["git", "-C", str(source), "rev-parse", "HEAD"], text=True).strip()
    if head != spec["source_commit"]:
        raise ValueError("Source checkout does not match the pinned preview commit")
    if subprocess.check_output(["git", "-C", str(source), "status", "--porcelain", "--untracked-files=no"], text=True).strip():
        raise ValueError("Source has tracked changes; commit them and update the specification first")
    untracked = subprocess.check_output(
        ["git", "-C", str(source), "ls-files", "--others", "--exclude-standard", "-z"], text=True).split("\0")
    # Godot generates these adjacent metadata files during import. Everything
    # else must be committed: in particular C# globbing includes untracked .cs.
    if any(path and not path.endswith((".import", ".uid")) for path in untracked):
        raise ValueError("Source contains untracked files other than Godot import metadata")
    return head


def package(spec_path, source, output):
    spec = read_spec(spec_path)
    head = verify_source(source, spec)
    subprocess.run([shutil.which("bash") or "bash", "tools/export.sh", "windows"], cwd=source, check=True)
    export_zip = source / "artifacts/windows/L5R-Emperor-Windows.zip"
    export_receipt = json.loads((source / "artifacts/checks/package-manifest.json").read_text())
    match = next(item for item in export_receipt if item["path"] == "artifacts/windows/L5R-Emperor-Windows.zip")
    if match["sha256"] != digest(export_zip):
        raise ValueError("Windows export checksum did not match its validation receipt")
    output.mkdir(parents=True, exist_ok=True)
    name = "L5R-Review-" + identity(spec)
    archive_path = output / (name + "-Windows.zip")
    if archive_path.exists():
        raise ValueError("Package already exists; use a new output directory or increment revision")
    with tempfile.TemporaryDirectory(prefix="review-", dir=output) as temporary:
        folder = Path(temporary) / name
        prepare_folder(spec, export_zip, folder)
        with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
            for path in sorted(folder.rglob("*")):
                if path.is_file():
                    archive.write(path, path.relative_to(Path(temporary)))
    receipt = {"schema_version": 1, "repository": REPO, "source_pr": spec["source_pr"],
               "source_commit": head, "title": spec["title"], "slug": spec["slug"],
               "tag": "preview-" + identity(spec) + "-windows", "asset": archive_path.name,
               "bytes": archive_path.stat().st_size, "sha256": digest(archive_path),
               "folder": name, "launcher": "Start Review.cmd", "reviewer": spec["reviewer"]}
    receipt = split_delivery(archive_path, receipt, spec)
    receipt_path = output / (spec["slug"] + ".receipt.json")
    receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(receipt_path)
    return receipt_path


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--spec", required=True, type=Path)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--out", type=Path, default=ROOT / "artifacts/previews")
    args = parser.parse_args()
    package(args.spec.resolve(), args.source.resolve(), args.out.resolve())
