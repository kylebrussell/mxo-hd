#!/usr/bin/env python3
"""Write a copy of the MxO launcher that trusts this server's root key.

launcher.exe (MxOEmu full client 7.6005) embeds a 2048-bit root RSA modulus,
big-endian, twice: at file offsets 0x0B6080 and 0x0B6B38 (the second copy is
the one FUN_00447260 loads; the string "pubkey.dat" follows it directly). The
launcher uses that key to verify the server key in AS_GetPublicKeyReply. This
script copies the launcher and replaces both copies with the modulus of the
root key (privKeyXML2) in hds/utils/encryption/MxoRSA.cs. The original file is
never modified. See docs/CLIENT-SETUP-PLAN.md.

Usage: scripts/patch-client-launcher.py <launcher.exe> [--out <path>] [--force]
Then start the patched copy with: <patched>.exe -nopatch -clone
"""
import argparse
import base64
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
RSA_SOURCE = REPO / "hds" / "utils" / "encryption" / "MxoRSA.cs"
ROOT_KEY_NAME = "privKeyXML2"
SLOTS = (0x0B6080, 0x0B6B38)
SLOT_LEN = 256
MARKER = b"pubkey.dat"


def root_modulus():
    text = RSA_SOURCE.read_text()
    m = re.search(ROOT_KEY_NAME + r'\s*=\s*"(<RSAKeyValue>.*?</RSAKeyValue>)"', text)
    if not m:
        sys.exit(f"{ROOT_KEY_NAME} not found in {RSA_SOURCE}")
    xml = m.group(1)
    modulus = base64.b64decode(re.search(r"<Modulus>(.*?)</Modulus>", xml).group(1))
    exponent = int.from_bytes(base64.b64decode(re.search(r"<Exponent>(.*?)</Exponent>", xml).group(1)), "big")
    if len(modulus) != SLOT_LEN or exponent != 17:
        sys.exit(f"{ROOT_KEY_NAME} must be 2048-bit with e=17 (launcher hardcodes 0x11); got {len(modulus) * 8}-bit e={exponent}")
    return modulus


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("launcher", type=Path)
    ap.add_argument("--out", type=Path, help="patched copy (default: launcher_hd.exe next to the input)")
    ap.add_argument("--force", action="store_true", help="overwrite an existing output file")
    args = ap.parse_args()

    data = bytearray(args.launcher.read_bytes())
    out = args.out or args.launcher.with_name("launcher_hd.exe")
    key = root_modulus()

    # Layout checks: both slots in range and identical, marker right after the second slot.
    end = SLOTS[1] + SLOT_LEN
    if len(data) < end + len(MARKER) or data[end:end + len(MARKER)] != MARKER:
        sys.exit(f"{args.launcher}: unexpected layout (no '{MARKER.decode()}' after 0x{SLOTS[1]:X}); "
                 "this script targets the MxOEmu 7.6005 launcher.exe")
    current = [bytes(data[o:o + SLOT_LEN]) for o in SLOTS]
    if current[0] != current[1]:
        sys.exit(f"{args.launcher}: the two key slots differ; refusing to patch")
    if current[0] == key:
        print(f"{args.launcher} already carries this server's root key; nothing to do")
        return 0
    print(f"replacing root key {current[0][:6].hex()}…{current[0][-4:].hex()} with {key[:6].hex()}…{key[-4:].hex()}")

    if out.resolve() == args.launcher.resolve():
        sys.exit("refusing to overwrite the input launcher; pass a different --out")
    if out.exists() and not args.force:
        sys.exit(f"{out} exists; pass --force to overwrite")

    for offset in SLOTS:
        data[offset:offset + SLOT_LEN] = key
    out.write_bytes(bytes(data))

    written = out.read_bytes()
    assert all(written[o:o + SLOT_LEN] == key for o in SLOTS)
    print(f"wrote {out} ({len(written):,} bytes); start it with: {out.name} -nopatch -clone")
    return 0


if __name__ == "__main__":
    sys.exit(main())
