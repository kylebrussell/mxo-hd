"""Read-only80a1 flag consumer proof; python inspect_mission_dialog_flags.py client.dll."""
from pathlib import Path
import hashlib,json,sys
b=Path(sys.argv[1]).read_bytes();sha=hashlib.sha256(b).hexdigest()
assert sha=="cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a"
def at(va,n):return b[va-0x62000000:va-0x62000000+n]
assert at(0x622112d4,8)==bytes.fromhex("8b56108a5a1284db")
assert at(0x622112f4,3)==bytes.fromhex("f6c301")
assert at(0x62211311,3)==bytes.fromhex("f6c302")
assert at(0x62211321,3)==bytes.fromhex("f6c304")
assert at(0x6220f23d,2)==bytes.fromhex("6a16")
assert at(0x6287922c,57).startswith(b"\\matrixstaging\\game\\src\\client\\ui\\viewmissionstatus.cpp")
print(json.dumps({"sha256":sha,"dispatcher":"62179756->622111f0","display_offset":18,"bit1":"portrait dialog event6220d370","bit2":"mission-status text list6220f220->620d5720, UI22","bit4":"suppresses mission-dialog history6220f830 (general chat unproven)","client_flags_offset":19,"hostility_gate":"none in the80a1 dispatcher"},indent=2))
