# Desperation 1149: combat consumer investigation

Desperation's gameplay effect remains unresolved. The snapshot foundation in PR #130 is **COSMETIC**: a modifier record does not establish a combat benefit. This investigation changes no runtime behavior or audit status.

## Primary sources and actual combat contract

The shipped [DesperationFunction](../research/client-scripts/tempmods.py#L2402) samples health at application and writes Initiative 1044, influenced by Initiative Bonus 1046. Above half health it writes `10 * (1 + bonus / 100)`; at or below half health it writes `(1 - HP / MaxHP) * 100 * (1 + bonus / 100)`. This establishes the modifier's input, not its gameplay consumer.

The shipped [ResolveCombat](../research/client-scripts/combat.py#L121) delegates attacker selection, withdrawal, and standard resolution to `interlock.combat_resolution`:

- [determineAttacker](../research/client-scripts/interlock/combat_resolution.py#L128) selects by withdrawal, prone state, opportunity attack, preferred tactic, ganging/target slot, ability level, then a random tie. It does not read Initiative 1044 or `result.initiative`.
- [getCTandDTotals](../research/client-scripts/interlock/combat_resolution.py#L325) reads prepopulated `tacticalValue` and typed defense tactics. A side hits when its CT exceeds the opponent's DT. Special moves select the defense type through `abilitylib.getDefenseTacticBonusType`.
- [standard resolution](../research/client-scripts/interlock/combat_resolution.py#L355) compares CT totals when both sides hit with special moves; otherwise a hitting defender special can take priority.
- [withdrawal](../research/client-scripts/interlock/combat_resolution.py#L463) succeeds when the withdrawing defender's `tacticalValue` exceeds the attacker's.

A search of all recovered Python scripts finds reads of `tacticalValue`, but no assignment or native call that populates it. [SelectCombatAbilityLevel](../research/client-scripts/interlock/combat_abil_level.py#L75) sets initiative to level plus the tactic's 20/10/0 bonus. No recovered Python caller reads that result as Initiative 1044.

## Native evidence

Inspected `client.dll` SHA-256: `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`. Addresses below are image virtual addresses for this binary only.

| Address | Recovered contract |
|---|---|
| `625c5df0`, read at `625c5e50` | Tactic-total helper conditionally reads stat 1044 through `625c4e90`, also adding a typed tactic stat, Sneak Attack Combat Tactics 879 and Combat Tactics 163. |
| `625c1b00`, `62515b40` | Non-sentinel ability requires CastWhatHow 0 or 2; 2 additionally requires the supplied subject state flag. |
| `620f70de`, `620f7190`, `620f723f`, `620f72ee`, `620f7e16`, `620f7ecb`, `620f7f7a`, `620f8029` | All eight recovered direct callers pass ability sentinel `0x1000`, skipping the 1044 branch, and feed the tactic-roll wrapper before UI formatting. |
| `6258d770` | Python `GetCombatTacticRoll` wrapper; all eight recovered direct callers are those UI paths. |
| `628234aa` | Startup binds string `GetCombatTacticRoll` at `628d8608` into `62b0af48`. |
| `620b9bb0`, string reference `620b9bde` | Debug serializer formats unsigned 16-bit TacticalValue from existing packet payload `+1`; it does not generate the value. String `TacticalValue:` at `628765f8` has only this literal reference. |
| `621573a6`, `6215744d` | References to `ForcedTacticalValue:` at `62881924` format signed 16-bit packet payload `+9`, rather than generating a roll. |
| `629c8680` | Python method table binds `getTacticRoll` string `628d838c` to entry `6258dae0`. |
| `6258dae0`, call at `6258db2b` | Parses three integers and calls `6258da00`, then returns a signed 16-bit Python integer. This is the only recovered direct caller of `6258da00`. |
| `6258da00` | Roll generation from base, consistency and range, with native console controls and Gaussian helper `6279f470`; no ability-stat read. |

No literal pointer reference to `625c5df0` was found. These searches do not prove the absent retail server lacks other callers. In particular, the conditional client helper alone does not establish which server attacks pass a real ability, their state flags, or how the result becomes `tacticalValue`.

## Reproduction

The following Python snippet uses `pefile` and `capstone` to inspect a user-supplied copy of the pinned binary without executing it. Change only the path. It verifies the hash before decoding. Repeat `dis`, `refs`, and `calls` for the addresses in the table; the PE memory image keeps virtual-address offsets correct.

```python
import hashlib, re, struct
import pefile, capstone
path = r'C:\path\to\client.dll'
raw = open(path, 'rb').read()
assert hashlib.sha256(raw).hexdigest() == 'cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a'
pe = pefile.PE(data=raw)
base = pe.OPTIONAL_HEADER.ImageBase
memory = pe.get_memory_mapped_image()
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
def dis(address, size):
    for i in md.disasm(memory[address-base:address-base+size], address):
        print(hex(i.address), i.mnemonic, i.op_str)
def refs(address):
    print([hex(base+m.start()) for m in re.finditer(re.escape(struct.pack('<I', address)), memory)])
def calls(address):
    for s in pe.sections:
        if not s.Characteristics & 0x20000000:
            continue
        for p in range(s.VirtualAddress, s.VirtualAddress + s.Misc_VirtualSize - 5):
            if memory[p] in (0xe8, 0xe9) and base+p+5+struct.unpack_from('<i', memory, p+1)[0] == address:
                print(hex(base+p))
dis(0x625c5df0, 580)
calls(0x625c5df0)
refs(0x625c5df0)
dis(0x620b9bb0, 176)
refs(0x628765f8)
dis(0x6258dae0, 97)
calls(0x6258da00)
```

The raw relative-call search can produce data matches; disassemble each result and its enclosing function before treating it as a caller. Absence of literal references cannot exclude indirect calls or a separate server executable.

## Capture boundary and required evidence

The [existing capture analysis](../research/client-scripts/README.md#what-the-captures-add-to-the-scripts) identifies move value as the attack roll in 692/725 rounds, and the withdrawal margin in 46/46 withdrawal rounds. It establishes wire semantics, not Initiative's influence. No labeled controlled Desperation trace was recovered. Searching raw `7d 04` is insufficient: those bytes also occur in unrelated IDs and coordinates.

The current [interlock roll model](../hds/world/Structures/CombatInterlock.cs#L164) explicitly describes its level-based CT base as a guess. Adding 1044 there would introduce a server policy. Promoting Desperation to a real sourced effect requires the retail engine producer of `tacticalValue`, a trusted trace of its ability/state/stat inputs, or controlled retail before/during/after captures with HP, 1044/1046, all CT/DT inputs and attack IDs sufficient to distinguish distribution and qualifiers. Emulator native QA can validate the snapshot lifecycle; it cannot establish that missing retail server contract.
