#!/usr/bin/env python3
"""Report whether MxO client executables embed the auth server's RSA public key.

The client only accepts an auth server whose signing key matches one compiled
into the executable. This script pulls the RSA moduli out of
hds/utils/encryption/MxoRSA.cs and searches each given file for them in both
big- and little-endian byte order. It also reports partial (32-byte prefix)
hits so a near-match or a different key layout is visible.

Usage: scripts/client-key-check.py <matrix.exe> [<launcher.exe> ...]
"""
import base64
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
RSA_SOURCE = REPO / "hds" / "utils" / "encryption" / "MxoRSA.cs"


def load_keys():
    text = RSA_SOURCE.read_text()
    keys = {}
    for name, xml in re.findall(r'const string (\w+)\s*=\s*"(<RSAKeyValue>.*?</RSAKeyValue>)"', text):
        modulus = base64.b64decode(re.search(r"<Modulus>(.*?)</Modulus>", xml).group(1))
        exponent = int.from_bytes(base64.b64decode(re.search(r"<Exponent>(.*?)</Exponent>", xml).group(1)), "big")
        keys[name] = (modulus, exponent)
    if not keys:
        sys.exit(f"no RSA keys found in {RSA_SOURCE}")
    return keys


def find_all(data, needle):
    hits, start = [], 0
    while (idx := data.find(needle, start)) != -1:
        hits.append(idx)
        start = idx + 1
    return hits


def main(paths):
    keys = load_keys()
    for name, (modulus, exponent) in keys.items():
        print(f"{name}: {len(modulus) * 8}-bit, e={exponent}, modulus starts {modulus[:8].hex()}")
    found_any = False
    for path in map(Path, paths):
        data = path.read_bytes()
        print(f"\n{path} ({len(data):,} bytes)")
        for name, (modulus, _) in keys.items():
            for order, blob in (("big-endian", modulus), ("little-endian", modulus[::-1])):
                full = find_all(data, blob)
                prefix = find_all(data, blob[:32])
                if full:
                    found_any = True
                    print(f"  FULL MATCH {name} {order} at " + ", ".join(hex(o) for o in full))
                elif prefix:
                    print(f"  prefix-only {name} {order} at " + ", ".join(hex(o) for o in prefix))
        for label, offset in (("wiki offset", 0x0B6080), ("wiki offset", 0x0B6B38)):
            if offset + 32 <= len(data):
                print(f"  {label} {hex(offset)}: {data[offset:offset + 32].hex()}")
    print("\nResult:", "server key IS embedded" if found_any else "server key NOT found in any file")
    return 0 if found_any else 1


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    sys.exit(main(sys.argv[1:]))
