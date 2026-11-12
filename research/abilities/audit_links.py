"""Rebase the audit's docs-relative evidence links for a portable review bundle."""
import os
import re
from pathlib import Path
from urllib.parse import quote, urlsplit


def rebase_links(markdown, root, output_dir, source_base_url=None, bundled=True):
    root=Path(root).resolve()
    output_dir=Path(output_dir).resolve()
    if source_base_url and urlsplit(source_base_url).scheme != 'https':
        raise ValueError('The pinned repository source base must use HTTPS')
    companions={'docs/ABILITY-PARITY.csv':'ABILITY-PARITY.csv',
                'research/abilities/server_coverage.json':'server_coverage.json'}
    def replace(match):
        target=match.group(1)
        parsed=urlsplit(target)
        if parsed.scheme or parsed.netloc or target.startswith('#'):
            return match.group(0)
        source=(root / 'docs' / parsed.path).resolve()
        relative=source.relative_to(root).as_posix()
        suffix=('?' + parsed.query if parsed.query else '') + ('#' + parsed.fragment if parsed.fragment else '')
        if bundled and relative in companions:
            destination=companions[relative]
        elif source_base_url:
            destination=source_base_url.rstrip('/') + '/' + quote(relative,safe='/')
        else:
            destination=Path(os.path.relpath(source,output_dir)).as_posix()
        return '](' + destination + suffix + ')'
    return re.sub(r'\]\(([^)]+)\)',replace,markdown)
