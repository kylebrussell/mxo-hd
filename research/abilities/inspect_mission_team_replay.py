"""Read-only native Abort/team guards from the pinned 7.6005 client. Never loads the DLL."""
import argparse
import hashlib
import struct
from pathlib import Path

EXPECTED = "cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a"
PROBES = {
    "cached_team_leader_equals_own_character": (0x621bfc40, "8b15cce49d62568b712c33c03bd60f94c05ec3"),
    "abort_dialog_tests_leader_then_selects_leave_prompt": (0x620d67b9, "e882940e0084c05e743d"),
    "leave_confirmation_dispatches_to_team_manager": (0x620d6949, "b9a8c89e62e8ed9b0e00"),
    "leave_sends_nothing_unless_group_state_is_three": (0x621c0549, "837e3003756f"),
    "leave_request_header_inside_joined_state_branch": (0x621c0577, "c60071"),
    "full_team_consumer_passes_packet_leader_to_setter": (0x622145c4, "8b4e108b510c8b03528bcbff500c"),
}

def inspect(path):
    raw = Path(path).read_bytes()
    if hashlib.sha256(raw).hexdigest() != EXPECTED:
        raise ValueError("Unsupported client hash; do not apply these addresses to another build")
    pe = struct.unpack_from("<I", raw, 0x3c)[0]
    count = struct.unpack_from("<H", raw, pe + 6)[0]
    optional = struct.unpack_from("<H", raw, pe + 20)[0]
    base = struct.unpack_from("<I", raw, pe + 52)[0]
    sections = []
    for i in range(count):
        at = pe + 24 + optional + 40 * i
        _, rva, size, offset = struct.unpack_from("<IIII", raw, at + 8)
        sections.append((base + rva, size, offset))
    for name, (va, expected) in PROBES.items():
        address, size, offset = next(s for s in sections if s[0] <= va < s[0] + s[1])
        wanted = bytes.fromhex(expected)
        actual = raw[offset + va - address:offset + va - address + len(wanted)]
        if actual != wanted:
            raise ValueError(f"{name} differs at {va:08x}: {actual.hex()}")
        print(f"PASS {name} {va:08x}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("client", type=Path)
    inspect(parser.parse_args().client)
