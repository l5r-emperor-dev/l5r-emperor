#!/usr/bin/env python3
"""Wrap an exported build into one shareable ZIP for friends (Windows or macOS).

Run `bash tools/export.sh windows` (or `macos`, on a Mac) first; this only repackages its
already-validated output. Never fetches or switches branches, and never publishes anywhere.
The macOS package must be made on a Mac: it keeps the bundle's ad-hoc signature, permissions
and links with ditto, then re-verifies the signature and starts the packaged game (#1317)."""
from pathlib import Path
import argparse
import os
import plistlib
import shutil
import subprocess
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = ROOT / 'artifacts' / 'share'
FOLDER_NAME = 'L5R Emperor'
EXPORTS = {
    'windows': ROOT / 'artifacts' / 'windows' / 'L5R-Emperor-Windows.zip',
    'macos': ROOT / 'artifacts' / 'macos' / 'L5R-Emperor.zip',
}
OUTPUTS = {
    'windows': OUTPUT_DIR / 'L5R-Emperor-Play-Windows.zip',
    'macos': OUTPUT_DIR / 'L5R-Emperor-Play-macOS.zip',
}

# Never CALL user-derived paths and keep delayed expansion off, matching the other
# generated .cmd launchers in this repo (tools/previews/package.py), so a Windows
# username containing ! still works and extraction paths don't get double-expanded.
LAUNCHER = '''@echo off
setlocal DisableDelayedExpansion
set "L5R_ROOT=%~dp0"
if not exist "%L5R_ROOT%game\\L5R-Emperor.exe" goto missing
if not exist "%L5R_ROOT%game\\L5R-Emperor.pck" goto missing
start "L5R Emperor" /D "%L5R_ROOT%game" "%L5R_ROOT%game\\L5R-Emperor.exe" --log-file "%L5R_ROOT%game.log"
exit /b 0
:missing
echo Extract the entire ZIP first, then open "Play L5R Emperor.cmd" from the extracted folder.
echo Keep the game folder beside this launcher.
pause
exit /b 1
'''.replace('\n', '\r\n')

DECKS = '''Choose either Clan (Lion or Crane) from Practice to play solo against the
built-in opponent, or use Private Match to play online with a friend.

STARTER DECKS AND YOUR OWN DECKS
Ready-made decks (Crab Control, Crane Dominance, Dragon Destruction, Lion Legacy,
Mantis Manipulation, Phoenix Talons, Scorpion Lethargy, Scottish Lion Deck, Spider
Conquer, Unicorn Maidens and Allan Lion MCA) are added to your deck list the first time
you open a version that has them. Choose them in Practice, or open them in the Deck Builder.
Decks you build or edit are saved in your user profile, not in this folder:
  {decks}
Deleting this folder and extracting a newer version keeps all of your decks.
A starter deck you delete or change stays that way; it is never restored.
'''

READ_ME = {
    'windows': '''L5R EMPEROR (WINDOWS)

HOW TO PLAY
1. Right-click the downloaded ZIP and choose Extract All.
2. Open the extracted folder and double-click "Play L5R Emperor.cmd".
   Keep the game folder beside the launcher; don't move it out on its own.
3. If Windows shows an unsigned-app warning ("Windows protected your PC"),
   click "More info" then "Run anyway". This is a private development build,
   not a signed public release.

No installation, Godot editor or .NET SDK is required; everything needed to
run is already inside this ZIP.

''' + DECKS.format(decks='%APPDATA%\\Godot\\app_userdata\\L5R · Emperor\\decks') + '''
If something goes wrong, "game.log" appears next to this launcher after you
try to run it; include it if you report a problem.
''',
    'macos': '''L5R EMPEROR (MAC)

HOW TO PLAY
1. Double-click the downloaded ZIP to unzip it (Safari may already have done this).
   You can move the "L5R Emperor" folder anywhere, for example to Applications.
2. Open the folder and double-click the L5R Emperor app.
3. The first time, macOS says it can't check the app for malicious software,
   because this is a private development build that isn't signed by an Apple
   developer. Click Done (not Move to Trash), then open System Settings >
   Privacy & Security, scroll down to the message about the app and click
   "Open Anyway". Confirm with your password. After that it opens normally.
   If macOS instead says the app "is damaged", open Terminal, type
       xattr -dr com.apple.quarantine
   followed by a space, drag the app onto the Terminal window, press Return,
   and open the app again.

Runs natively on both Apple silicon and Intel Macs. No installation, Godot
editor or .NET SDK is required; everything needed to run is inside the app.

''' + DECKS.format(decks='~/Library/Application Support/Godot/app_userdata/L5R · Emperor/decks') + '''
If something goes wrong, include this log if you report a problem:
  ~/Library/Application Support/Godot/app_userdata/L5R · Emperor/logs/godot.log
(In Finder, choose Go > Go to Folder and paste the folder part of that path.)
''',
}


def package_windows(export_zip, output_zip):
    staging = OUTPUT_DIR / '_staging'
    if staging.exists():
        shutil.rmtree(staging)
    folder = staging / FOLDER_NAME
    game = folder / 'game'
    game.mkdir(parents=True)
    with zipfile.ZipFile(export_zip) as archive:
        archive.extractall(game)
    (folder / 'Play L5R Emperor.cmd').write_bytes(LAUNCHER.encode('utf-8'))
    (folder / 'READ ME.txt').write_text(READ_ME['windows'], encoding='utf-8')
    if output_zip.exists():
        output_zip.unlink()
    with zipfile.ZipFile(output_zip, 'w', zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(folder.rglob('*')):
            if path.is_file():
                archive.write(path, path.relative_to(staging).as_posix())
    shutil.rmtree(staging)


def mac_app(folder):
    apps = list(folder.glob('*.app'))
    if len(apps) != 1:
        sys.exit(f'Expected exactly one Mac app bundle in {folder}, found {len(apps)}.')
    return apps[0]


def package_macos(export_zip, output_zip):
    # Python's zipfile drops the bundle's executable bits and links, which breaks the
    # signature; ditto keeps them. System temporary storage avoids cloud-folder metadata.
    if sys.platform != 'darwin':
        sys.exit('Package the macOS friends pack on a Mac (it needs ditto and codesign).')
    with tempfile.TemporaryDirectory(prefix='l5r-share-') as directory:
        folder = Path(directory) / FOLDER_NAME
        folder.mkdir()
        subprocess.run(['/usr/bin/ditto', '-x', '-k', str(export_zip), str(folder)], check=True)
        mac_app(folder)
        (folder / 'READ ME.txt').write_text(READ_ME['macos'], encoding='utf-8')
        if output_zip.exists():
            output_zip.unlink()
        subprocess.run(['/usr/bin/ditto', '-c', '-k', '--norsrc', '--noextattr', '--keepParent',
            str(folder), str(output_zip)], check=True)
    # Check what a friend gets: unzip the finished package, verify the signature, start it.
    with tempfile.TemporaryDirectory(prefix='l5r-share-check-') as directory:
        subprocess.run(['/usr/bin/ditto', '-x', '-k', str(output_zip), directory], check=True)
        folder = Path(directory) / FOLDER_NAME
        if not (folder / 'READ ME.txt').is_file():
            sys.exit('Packaged Mac folder has no READ ME.')
        app = mac_app(folder)
        subprocess.run(['/usr/bin/codesign', '--verify', '--deep', '--strict', str(app)], check=True)
        info = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
        executable = app / 'Contents/MacOS' / info['CFBundleExecutable']
        env = {key: value for key, value in os.environ.items()
            if not key.startswith('DOTNET_ROOT') and key != 'DOTNET_HOST_PATH'}
        smoke = subprocess.run([str(executable), '--headless', '--quit-after', '3'],
            capture_output=True, text=True, timeout=60, env=env)
        output = smoke.stdout + smoke.stderr
        log = ROOT / 'artifacts' / 'checks' / 'macos-share-smoke.log'
        log.parent.mkdir(parents=True, exist_ok=True)
        log.write_text(output, encoding='utf-8')
        if smoke.returncode or 'ERROR:' in output:
            sys.exit(f'Packaged Mac game failed to start; see {log}.')


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--platform', choices=sorted(EXPORTS), default='windows')
    platform = parser.parse_args().platform
    export_zip, output_zip = EXPORTS[platform], OUTPUTS[platform]
    if not export_zip.is_file():
        sys.exit(f'Missing {export_zip}. Run: bash tools/export.sh {platform}')
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    (package_macos if platform == 'macos' else package_windows)(export_zip, output_zip)
    print(output_zip)


if __name__ == '__main__':
    main()
