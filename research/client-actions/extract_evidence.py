"""Read-only PE evidence slices. python extract_evidence.py client.dll output.json

Uses PE virtual addresses translated through section headers, never raw-file address guesses.
Disassemble the resulting bytes as x86 for the routines cited in docs/IGNORED-CLIENT-ACTIONS.md.
No client loading, injection, execution, network, database or native-QA operations.
"""
import hashlib
import json
import struct
import sys
from pathlib import Path

SLICES = {
    "afk_constructor": (0x62240b40, 36),
    "pill_constructor": (0x621d35c0, 40),
    "vendor_close_constructor": (0x6210a180, 48),
    "collector_close_constructor": (0x62071840, 48),
    "elevator_close_constructor": (0x620ae350, 40),
    "native_compile_button": (0x620836b0, 225),
    "petition_request_initializer": (0x621ad020, 144),
    "petition_ack_initializer": (0x62147e00, 130),
    "petition_header_initializer": (0x62147f40, 180),
    "petition_detail_initializer": (0x62148100, 150),
    "petition_category_initializer": (0x6212e4d0, 91),
    "ticket_status_switch": (0x6209699a, 60),
    "ticket_status_jump_table": (0x62096bf8, 24),
    "knowledge_search_initializer": (0x62148540, 160),
    "knowledge_article_initializer": (0x621486f0, 152),
    "knowledge_policy_initializer": (0x62148880, 138),
    "snoop_crew_constructor": (0x621a5122, 51),
    "snoop_faction_constructor": (0x621b7d02, 51),
    "hovercraft_constructor": (0x62038cd6, 77),
    "hovercraft_response_dispatch": (0x621c8e40, 137),
    "construct_array_element": (0x620b4940, 26),
    "hack_construct_constructor": (0x620b4a70, 52),
    "construct_settings_initializer": (0x62083b00, 23),
    "range_constructor": (0x62241a50, 52),
    "range_sender": (0x6225909a, 49),
    "time_sender": (0x62078cd0, 67),
    "time_common_ui_caller": (0x62079640, 96),
    "time_inbound_dispatch_entry": (0x6217e9c0 + (0x137 - 12) * 4, 4),
}


def extract(client):
    raw = Path(client).read_bytes()
    pe = struct.unpack_from("<I", raw, 0x3c)[0]
    assert raw[pe:pe + 4] == b"PE\0\0"
    machine, count = struct.unpack_from("<HH", raw, pe + 4)
    optional_size = struct.unpack_from("<H", raw, pe + 20)[0]
    assert machine == 0x14c and struct.unpack_from("<H", raw, pe + 24)[0] == 0x10b
    base = struct.unpack_from("<I", raw, pe + 52)[0]
    sections = []
    for index in range(count):
        at = pe + 24 + optional_size + 40 * index
        virtual_size, rva, raw_size, offset = struct.unpack_from("<IIII", raw, at + 8)
        sections.append((rva, virtual_size, offset, raw_size))
    result = {"client_sha256": hashlib.sha256(raw).hexdigest(), "image_base": hex(base), "slices": {}}
    for name, (address, size) in SLICES.items():
        rva = address - base
        section = next(s for s in sections if s[0] <= rva < s[0] + s[1])
        delta = rva - section[0]
        assert delta + size <= section[3], (name, "slice exceeds file-backed section")
        offset = section[2] + delta
        result["slices"][name] = {"virtual_address": hex(address), "file_offset": hex(offset),
                                  "bytes": raw[offset:offset + size].hex()}
    return result


if __name__ == "__main__":
    output = json.dumps(extract(sys.argv[1]), indent=2, sort_keys=True) + "\n"
    Path(sys.argv[2]).write_text(output, encoding="utf-8", newline="\n")
