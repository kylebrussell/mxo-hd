"""Read-only Welcome payout/level evidence; does not infer retail scaling from correlation.

Run from any directory with Python3. Uses the checked-in decoded mission index to
locate four exact completion packets, then verifies raw80a2/80e6/80e5 values.
"""
import argparse
import json
import re
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HEADER = re.compile(r"[A-Za-z]+->[A-Za-z]+ \[")
HEX = re.compile(r"(?:[0-9a-fA-F]{2} ?)+")


def packets(path, last_line):
    current = None
    for number, line in enumerate(path.read_text(errors="replace").splitlines(), 1):
        if HEADER.match(line):
            if current is not None:
                yield current
            if number > last_line:
                return
            current = [number, line, bytearray()]
        elif current is not None and HEX.fullmatch(line.strip()):
            current[2].extend(bytes.fromhex(line))
    if current is not None:
        yield current


def level_for_experience(experience):
    # Native625d8590/625d8610, documented/tested in PlayerProgression.cs.
    result = 1
    for level in range(2, 51):
        base = sum(100*k*k if k < 10 else 1000*k for k in range(1, level))
        if experience >= base * 5 * (level-1):
            result = level
    return result


def inspect():
    catalog = json.loads((ROOT/"research/missions/capture-missions.json").read_text())
    mission = next(row for row in catalog["missions"] if row["title"] == "Welcome to the Matrix")
    evidence = []
    for instance in mission["instances"]:
        source, result = instance["source"], instance["result"]
        file = ROOT/"research/packet-dumps"/source["file"]
        all_packets = list(packets(file, result["line"]))
        packet = next(data for line, _, data in all_packets if line == result["line"])
        complete = b"\x80\xa2\x00" + struct.pack("<HHIII", result["enemy_kills"], result["team_killed"],
            result["seconds"], result["info"], result["xp"])
        assert packet.count(complete) == 1, "Exact catalogued completion must match raw bytes"
        gain = b"\x0a\x80\xe6" + struct.pack("<II", result["xp"], 2)
        assert packet.count(gain) == 1, "Framed mission gain must match completion reward"
        offset = packet.index(gain) + len(gain)
        assert packet[offset:offset+3] == b"\x0a\x80\xe5"
        total, gain_type = struct.unpack_from("<II", packet, offset+3)
        assert gain_type == 2 and total >= result["xp"]
        before = total-result["xp"]
        evidence.append(dict(file=source["file"], accept_line=source["line"], completion_line=result["line"],
            difficulty=instance["info_8099"]["difficulty"], xp=result["xp"], info=result["info"],
            pre_reward_experience=before, pre_reward_level=level_for_experience(before),
            post_reward_experience=total, post_reward_level=level_for_experience(total),
            limitation="Level derived from exact reward total; team/difficulty/accept-level inputs are not fully known."))
    return evidence


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    text = json.dumps(inspect(), indent=2) + "\n"
    if args.output:
        args.output.write_text(text, encoding="utf-8")
    else:
        print(text, end="")
