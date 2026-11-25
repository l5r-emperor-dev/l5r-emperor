#!/usr/bin/env python3
"""Install the mandatory, version-pinned WebP pack before a development launch/export.

Packaged games include it and need neither GitHub credentials nor runtime downloads.
Only optimized artwork is downloaded. Installation is atomic and shared via .cache.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import urllib.error
import urllib.request

ROOT=Path(__file__).resolve().parents[1]


def sha256(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()


def verified(path, receipt):
    return path.is_file() and path.stat().st_size==receipt['bytes'] and sha256(path)==receipt['sha256']


def validate_coverage(root):
    content=root/'src/L5R.Client/Content'
    art=json.loads((content/'catalogue-art.json').read_text())
    printings=json.loads((content/'catalogue-printings.json').read_text())['cards']
    if art.get('schema_version')!=1 or art.get('quality')!=85:raise ValueError('Invalid WebP artwork manifest')
    missing={p['sha256'] for variants in printings.values() for p in variants if p['sha256'] not in art['scans']}
    for source in (content/'Published').glob('*.jpg'):
        key='res://Content/Published/'+source.name
        sha=sha256(source)
        if art['resources'].get(key)!=sha or sha not in art['scans']:missing.add(key)
    if missing:raise RuntimeError('Catalogue artwork pack needs a publisher update for '+str(len(missing))+' new or changed printings; rebuild with tools/content/build_catalogue_art.py before distributing this version.')


def download_public(receipt, path):
    """Without the GitHub CLI (#1353): the repository is public, so its release asset downloads over plain HTTPS."""
    url=f"https://github.com/{receipt['repository']}/releases/download/{receipt['tag']}/{receipt['asset']}"
    with urllib.request.urlopen(url,timeout=60) as response,path.open('wb') as f:shutil.copyfileobj(response,f,1<<20)


def install(root, archive=None, verify_only=False):
    receipt=json.loads((root/'content/catalogue-art-pack.json').read_text())
    if (receipt.get('schema_version')!=1 or receipt.get('quality')!=85
        or receipt.get('repository') not in ('l5r-emperor-dev/l5r-emperor','alfredoh7/l5r-emperor')
        or receipt.get('asset')!='L5R-Catalogue-WebP85.zip'
        or len(receipt.get('sha256',''))!=64
        or any(c not in '0123456789abcdef' for c in receipt['sha256'])):
        raise ValueError('Invalid catalogue artwork release receipt')
    validate_coverage(root)
    destination=root/'src/L5R.Client/Content/catalogue-art.zip'
    if verified(destination,receipt):return 'Catalogue WebP artwork is ready (offline).'
    if verify_only:raise RuntimeError('Required catalogue artwork pack missing or damaged. Run the game launcher to install it.')
    shared=root/'.cache/catalogue-art'/f"{receipt['sha256']}.zip";shared.parent.mkdir(parents=True,exist_ok=True)
    if archive is not None:
        if not verified(archive,receipt):raise ValueError('Artwork archive does not match the pinned release')
        source=archive
    elif verified(shared,receipt):source=shared
    else:
        executable=shutil.which('gh');env=os.environ.copy()
        if not executable:
            for name in ('gh','gh.exe'):
                bundled=root/'.tools/github-cli'/name
                if bundled.is_file():executable=str(bundled);break
            if (root/'.cache/github').is_dir():env.setdefault('GH_CONFIG_DIR',str(root/'.cache/github'))
        print(f"Installing catalogue artwork ({receipt['bytes']/1048576:.0f} MiB, first launch only)…",flush=True)
        with tempfile.TemporaryDirectory(prefix='download-',dir=shared.parent) as directory:
            download=Path(directory)/receipt['asset']
            if executable:subprocess.run([executable,'release','download',receipt['tag'],'--repo',receipt['repository'],'--pattern',receipt['asset'],'--dir',directory],check=True,env=env,timeout=1800)
            else:download_public(receipt,download)
            if not verified(download,receipt):raise ValueError('Downloaded artwork failed integrity verification; existing installation was preserved')
            os.replace(download,shared)
        source=shared
    destination.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.NamedTemporaryFile(prefix='catalogue-',suffix='.zip',dir=destination.parent,delete=False) as f:temporary=Path(f.name)
    try:
        shutil.copyfile(source,temporary)
        if not verified(temporary,receipt):raise ValueError('Artwork copy failed integrity verification')
        os.replace(temporary,destination)
    finally:temporary.unlink(missing_ok=True)
    return 'Complete WebP catalogue installed. Future launches work offline.'

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--archive',type=Path);p.add_argument('--verify-only',action='store_true');a=p.parse_args()
    try:print(install(ROOT,a.archive,a.verify_only))
    except (OSError,ValueError,RuntimeError,subprocess.SubprocessError,urllib.error.URLError) as e:raise SystemExit(f'Catalogue installation failed: {e}')
