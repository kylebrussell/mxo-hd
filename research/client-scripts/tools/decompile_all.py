"""Decompile every .pyc under SRC into OUT (same layout). uncompyle6 first, per module with a timeout;
on total failure, fall back to xdis disassembly. Usage: decompile_all.py SRC OUT LOG"""
import os, sys, subprocess
src, out, log = sys.argv[1:4]
py = sys.executable
rows = []
for root, dirs, files in os.walk(src):
    for f in sorted(files):
        if not f.endswith('.pyc'):
            continue
        p = os.path.join(root, f)
        rel = os.path.relpath(p, src)
        dst = os.path.join(out, rel[:-4] + '.py')
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        status = 'ok'
        try:
            r = subprocess.run([os.path.join(os.path.dirname(py), 'uncompyle6.exe'), p], capture_output=True, text=True, timeout=300, encoding='utf-8', errors='replace')
            text = r.stdout
            if r.returncode != 0 or not text.strip():
                status = 'uncompyle6 failed rc=%d' % r.returncode
        except subprocess.TimeoutExpired:
            text = ''; status = 'timeout'
        nfail = text.count('This code section failed')
        if status != 'ok' or 'Parse error' in text and nfail == 0 and len(text) < 500:
            r2 = subprocess.run([os.path.join(os.path.dirname(py), 'pydisasm.exe'), p], capture_output=True, text=True, timeout=300, encoding='utf-8', errors='replace')
            text = ('# uncompyle6 could not decompile this module (%s); xdis disassembly follows.\n' % status) + \
                   '\n'.join('# ' + l for l in (r.stderr or '').splitlines()[-20:]) + '\n"""\n' + r2.stdout.replace('"""', "'''") + '\n"""\n'
            status = 'xdis'
        elif nfail:
            status = 'partial (%d functions as disassembly)' % nfail
        with open(dst, 'w', encoding='utf-8', newline='\n') as fh:
            fh.write(text)
        rows.append((rel.replace(os.sep, '/'), status, len(text.splitlines())))
        print(rows[-1], flush=True)
with open(log, 'w') as fh:
    for r in rows:
        fh.write('%s\t%s\t%d\n' % r)
