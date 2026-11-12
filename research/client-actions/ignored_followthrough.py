"""Read-only C1 record decoder and reproducible native command evidence.

Decoder input is a standalone RPC, NOT an arbitrary packet substring. No server
handler imports this module. Run with client.dll and --client-tools pointing to
mxo-research/tools/client-data to recover the command strings from the archive.
"""
import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path

CLIENT_SHA256 = "cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a"
SLICES = {
    "targetdamage_command_branch": (0x62258267, 18),
    "targetdamage_sender": (0x62259838, 23),
    "targetdamage_constructor": (0x62133600, 49),
    "stuck_command_branch": (0x62258116, 10),
    "stuck_slash_call": (0x6225818e, 14),
    "stuck_local_gate_and_sender": (0x62246b40, 112),
    "stuck_constructor": (0x622420c0, 48),
    "interrupted_completion_sender": (0x620ae840, 126),
}

def decode_rpc(raw):
    """Decode proven request fields only; reject trailing/truncated records."""
    if not raw:
        raise ValueError("empty RPC")
    width = 2 if raw[0] in (0x80, 0x81) else 1
    if len(raw) < width:
        raise ValueError("truncated opcode")
    op = raw[:width].hex(" ")
    body = raw[width:]
    shapes = {"3b": 0, "81 55": 0, "81 56": 0, "81 37": 4,
              "81 46": 8, "81 aa": 5, "80 f2": 4, "80 f6": 8,
              "80 ef": 2, "80 89": 7}
    if op not in shapes:
        raise ValueError("unsupported request")
    if len(body) != shapes[op]:
        raise ValueError("wrong standalone RPC size")
    result = {"opcode": op}
    if op == "81 37":
        result["TimeRequested"] = struct.unpack("<I", body)[0]
    elif op == "81 46":
        result["Range"], result["Range2"] = struct.unpack("<II", body)
    elif op == "81 aa":
        result["UserData"] = struct.unpack("<I", body[:4])[0]
        result["Interrupted"] = body[4]
    elif op in ("80 f2", "80 f6"):
        result["FactionID"] = struct.unpack("<I", body[:4])[0]
        if op == "80 f6":
            result["ConstructID"] = struct.unpack("<I", body[4:])[0]
    elif op == "80 ef":
        result["Firewall"], result["Defence"] = body
    elif op == "80 89":
        result["Submessage"], result["TransactionCode"], result["HovercraftSlot"] = body[:3]
        result["Data"] = struct.unpack("<I", body[3:])[0]
    return result

def extract(client):
    raw = Path(client).read_bytes()
    sha = hashlib.sha256(raw).hexdigest()
    if sha != CLIENT_SHA256:
        raise ValueError("unreviewed client image")
    pe = struct.unpack_from("<I", raw, 0x3c)[0]
    if raw[pe:pe+4] != b"PE\0\0":
        raise ValueError("not PE")
    machine, count = struct.unpack_from("<HH", raw, pe+4)
    optional_size = struct.unpack_from("<H", raw, pe+20)[0]
    base = struct.unpack_from("<I", raw, pe+52)[0]
    if machine != 0x14c:
        raise ValueError("not reviewed x86 image")
    sections = [struct.unpack_from("<IIII", raw, pe+24+optional_size+40*i+8) for i in range(count)]
    def read(va, n):
        rva = va-base
        for length, start, size, offset in sections:
            if start <= rva and rva+n <= start+size:
                return raw[offset+rva-start:offset+rva-start+n]
        raise ValueError("unmapped evidence slice")
    slices = {name: {"va": hex(va), "bytes": read(va,n).hex()} for name,(va,n) in SLICES.items()}
    assert read(0x62258267,6) == bytes.fromhex("81fed3000060")
    assert read(0x62258116,6) == bytes.fromhex("81fe5200003a")
    assert read(0x622420e7,5) == bytes.fromhex("66c7008155")
    assert read(0x620ae86e,5) == bytes.fromhex("66c70081aa")
    return {"client_sha256": sha, "slices": slices}

def command_strings(tools):
    sys.path.insert(0, str(tools))
    import rezmap_ltb
    row = next(r for r in rezmap_ltb.index().values() if r["path"].lower().endswith("stringtable_client.sta"))
    raw = rezmap_ltb.read(row)
    strings = {}
    for ident in (0x600000d3, 0x3a000052, 0x3a000055):
        at = raw.find(struct.pack("<I", ident))
        if at < 0:
            raise ValueError("missing primary string")
        n = struct.unpack_from("<I", raw, at+4)[0]
        strings[hex(ident)] = raw[at+8:at+8+n].decode("utf-16le").rstrip("\0")
    assert strings["0x600000d3"] == "/targetdamage"
    assert strings["0x3a000052"] == "/stuck"
    return {"resource": row["path"], "sha256": hashlib.sha256(raw).hexdigest(), "strings": strings}

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("client")
    parser.add_argument("--client-tools", required=True)
    args = parser.parse_args()
    result = extract(args.client)
    result["stringtable"] = command_strings(args.client_tools)
    print(json.dumps(result, indent=2))
