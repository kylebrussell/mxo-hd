# Free-cast viral contest — PARTIAL server policy

Normal positive-cast TF2 attacks previously went directly to damage and effects. They now test each existing legal single-target or target-centered-area recipient once, before damage rolls, resistance, tempmods and hostile procs. An explicit cast origin keeps already resolved queued exchanges, passive/child/aura/DOT callbacks and other test functions outside this contest. Selector4 and pet35 remain gated.

Primary script: `research/client-scripts/generic_free_attacks.py`. Its attack342, defense183, consistency637 and actual player tactic factors feed a strict attack>defense test. Block reduces attack input to0 but still rolls. NPCs lack a modeled current tactic; multiplier1 is an explicit server policy. Only current modeled buff/passive deltas are available. Native base/inherent totals and percentage-link coefficients remain unresolved; this implementation supplies none.

The chosen modern policy uses native integer mean/sigma construction, signed16 narrowing, range100/69 and the inverse table from client6258da00/6279f2f0. `research/protocol/freecast-viral-roll.json` records table bits, thresholds, SHA256 and independent literal/exhaustive oracle outputs. Tests cover all32768 rand15 values for seven input vectors. The unsigned left-tail clamp and32767 return-mean edge are preserved. Selecting Gaussian modern flags, max consistency100, independent server rand15 draws and nearest-even64-significand arithmetic is **OURS**. The live x87 control word, server settings and random sequence are unproven; this is no claim of exact retail probability or sequence.

Captured `81 67` hit/deflect events in `research/protocol/freecast-viral-events.json` establish stringIDs240000f1/f2, flag1, ability-ID type08 and four signed fields attack/0/defense/0. They are byte-tested against source capture records. Deflection uses sourced FX2800059c with unchanged health. Recipients are caster, victim and created-view observers in the same world; hidden/foreign-world observers receive nothing. Existing cost and cooldown policy is retained, including cost paid at cast start.

## Diagnostic profiles and one-pass native checklist

`freecast-viral-hit` and `freecast-viral-deflect` load2/17/1006/33/57/314/272 at the existing WinterFrost fixture. Neither grants GM status nor changes combat, damage, callback or health RNG. Only this new contest consumes `MXO_FREECAST_VIRAL_QA_ATTACK15`/`DEFENSE15`, gated by exact dev1, marker1, actual validated mxotest_ database, live authenticated non-LA actor, owned loaded ability and protected-character exclusions. Generic callback draw variables remain blank. Default profiles clear all new keys.

1. In the hit profile select WinterFrost and cast normal Static Blast33 outside interlock. Expect the viral hit log, normal animation and damage.
2. In the deflect profile repeat without activating314. Expect the deflect log/effect, unchanged target HP and paid IS/cooldown. This profile forces the contest only; ordinary damage rolls and combat remain normal.
3. With a legal enemy player, normal Logic Blast57 should follow the same hit/deflect profile; a nearby observer should see the result. Headless tests cover this route; real client appearance remains Claude-only and pending.
4. 314 and272 remain real existing buffs: activation changes modeled attack or defense inputs. They can change the diagnostic outcome, so the baseline checklist deliberately leaves them inactive.

No new generic buffs, viral immunity/resistance contest, pet authority or caster-centered area consumer is claimed. Headless framing and authoritative state are separate from native rendering acceptance.
