import hashlib
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from urllib.parse import unquote, urlsplit
from research.abilities.audit_links import rebase_links

ROOT=Path(__file__).resolve().parents[2]
PIN='0123456789abcdef0123456789abcdef01234567'
BASE='https://github.com/kylebrussell/mxo-hd/blob/' + PIN + '/'
LINKS=re.compile(r'\]\(([^)]+)\)')

class AuditLinkTests(unittest.TestCase):
    def generate(self,output,pinned=False):
        # Link tests deliberately use the historical inventory as a fixed input fixture.
        command=[sys.executable,str(ROOT/'research/abilities/build_audit.py'),'--output-dir',str(output),
                 '--coverage',str(ROOT/'research/abilities/server_coverage.json')]
        if pinned: command += ['--source-base-url',BASE]
        subprocess.run(command,cwd=ROOT,check=True,stdout=subprocess.PIPE,text=True)
        return (output/'ABILITY-PARITY.md').read_text(encoding='utf-8')

    def check_links(self,report,output,pinned):
        targets=LINKS.findall(report)
        self.assertGreater(len(targets),300)
        self.assertIn('ABILITY-PARITY.csv',targets)
        self.assertIn('server_coverage.json',targets)
        for target in targets:
            parsed=urlsplit(target)
            if parsed.scheme:
                if not target.startswith(BASE):
                    continue # Existing external links remain external in either output mode.
                self.assertTrue(pinned)
                source=ROOT/unquote(parsed.path.split('/blob/'+PIN+'/',1)[1])
            else:
                source=output/unquote(parsed.path)
            self.assertTrue(source.is_file(),target)
        self.assertTrue(any('tempmods.py#L' in target for target in targets))
        self.assertTrue(any(target.endswith('docs/ABILITY-AUDIT.md') for target in targets))

    def test_artifact_depth_and_nested_local_output_resolve_every_link(self):
        snapshot=(ROOT/'docs/ABILITY-PARITY.md').read_bytes()
        output=ROOT/'artifacts/ability-audit'/'link-fixture'
        self.check_links(self.generate(output),output,False)
        with tempfile.TemporaryDirectory(prefix='mxo-audit-links-') as temp:
            nested=Path(temp)/'review'/'nested'
            self.check_links(self.generate(nested),nested,False)
        self.assertEqual(snapshot,(ROOT/'docs/ABILITY-PARITY.md').read_bytes())

    def test_ci_bundle_uses_pinned_source_urls_local_companions_and_repeats_exactly(self):
        with tempfile.TemporaryDirectory(prefix='mxo-audit-ci-links-') as temp:
            output=Path(temp)
            self.check_links(self.generate(output,True),output,True)
            files=['ABILITY-PARITY.md','ABILITY-PARITY.csv','server_coverage.json']
            first={name:hashlib.sha256((output/name).read_bytes()).hexdigest() for name in files}
            self.generate(output,True)
            self.assertEqual(first,{name:hashlib.sha256((output/name).read_bytes()).hexdigest() for name in files})

    def test_external_urls_and_anchors_are_preserved(self):
        source='[web](https://example.com/path?q=1#part) [local](#heading) [script](../research/client-scripts/tempmods.py#L342)'
        result=rebase_links(source,ROOT,ROOT/'artifacts/ability-audit',BASE)
        self.assertIn('[web](https://example.com/path?q=1#part)',result)
        self.assertIn('[local](#heading)',result)
        self.assertIn('('+BASE+'research/client-scripts/tempmods.py#L342)',result)

if __name__=='__main__': unittest.main()
