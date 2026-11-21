#!/usr/bin/env python3
"""Reproducible metadata-only printing index. Never copies bulk scans or grants support."""
import hashlib
import json
from pathlib import Path


def package(root):
    cards = {c['id']: c for c in json.loads((root/'content/oracle/emperor-catalogue.json').read_text())['cards']}
    supplemental = root/'content/oracle/emperor-supplemental-catalogue.json'  # issue #812
    if supplemental.is_file():
        cards.update({c['id']: c for c in json.loads(supplemental.read_text())['cards'] if c.get('emperor_edition_numbers')})  # a card from another set (Quelsa) keeps its pinned game face; its scan would need an art-pack release
    index = {}
    for manifest in sorted((root/'content/oracle').glob('*/artwork-manifest.json')):
        data = json.loads(manifest.read_text())
        for art in data.get('images', data.get('downloaded', [])):
            if art['card_id'] not in cards:
                continue
            url = art.get('source_url', '')
            if not url.startswith('https://images.oracleofthevoid.com/l5r/'):
                raise ValueError('Unexpected scan host')
            entry = dict(id=art['id'], set=art['printing_set'], printing_id=str(art['printing_id']),
                         source_url=url, sha256=art['sha256'], numbers=art.get('numbers', []))
            index.setdefault(art['card_id'], {})[art['id']] = entry
    output = {'schema_version': 1, 'cards': {id: sorted(arts.values(), key=lambda a: (a['set'] != 'Emperor Edition', a['set'], a['id'])) for id, arts in sorted(index.items())}}
    path = root/'src/L5R.Client/Content/catalogue-printings.json'
    path.write_text(json.dumps(output, ensure_ascii=False, separators=(',', ':'), sort_keys=True)+'\n')
    return {'catalogue_cards':len(cards),'cards_with_scan_metadata':len(index),'scan_variants':sum(map(len,index.values()))}

if __name__ == '__main__':
    print(json.dumps(package(Path(__file__).resolve().parents[2]), indent=2))
