# Character creation personality and RSI initialization

Normal Margin creation retains the chosen personality in separate nullable `characters.trait_id`, creates its own RSI row and initializes five matching attributes. `profession` and TitleAbility remain independent. NULL retains existing trait 299 behavior. Assigned invalid values are detected, not silently changed. This does not repair the legacy 90-spent-points versus 16-point metadata discrepancy.

## Primary contract

Pinned DLL SHA256: `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`. Exact slices and callers: `research/protocol/character-creation-personality-source.json`.

Creation Finish (62054cb7) maps UI indexes 0-9 through literal table 62870600 to **308,304,303,306,305,302,307,299,301,300**. Native constructor 6253c750 and sender 6253e820 produce a 77-byte fixed body: opcode 0c, sixteen appearance u32s, plain u32 TraitID at body offset 65, and four u16 string offsets. Sized strings include their terminating NUL. The server's two spacer bytes make TraitID padded offset 67. It is not profession, a UI index or an encoded GOID.

Native printer 62540820 proves **HeadID at body+9** (literal628ce9ac), **FacialHairID at+25** (628ce978), **FacialHairColorID at+29** (628ce964). The old parser confused those fields. The corrected parser preserves actual request appearance values, checks known body/head/hair/clothing descriptors, the shipped gender/slot creation-choice whitelist and existing RSI encoding widths, and validates every string boundary before mutation. Malformed/unsupported creation uses existing transport close, not an invented LTMS numeric status. The sourced optional female StartingTights field at body+57 is preserved as slot103 and the existing four-bit female leggins model; optional zero is accepted only for optional categories. Known non-creation clothing is refused rather than granted. Native initialization 620523c1 starts female HeadID100 and HairID0. Actual library.rsia allocation plus native pack/unpack closes Head100 to face0 and Head112 to face5 (source evidence JSON). One erroneous CSV row is corrected from112/internal0 to100/internal0, retaining112/internal5. Source-initial Hair0 encodes ordinal0, as native62686bd3 explicitly does. Nineteen other existing legacy head-index discrepancies are recorded and deferred; this is not an all-appearance correction. Any remaining ambiguous selected key refuses instead of guessing. The fourth string's semantics remain unclaimed; its framing is validated but contents unused.

New-character 02 starts with TraitID u32 (62542738). Returning 02 has the existing 32-byte header followed by TraitID (62541d4e). Both populate mediator+f88; InventoryMgr copies that selector to+160. Attribute response and point budget use it. TraitID is always present.

|Trait|Focus|Perception|Reason|Belief|Vitality|
|---|---:|---:|---:|---:|---:|
|299|8|11|8|5|8|
|300|8|5|8|11|8|
|301|5|8|11|8|8|
|302|11|8|5|8|8|
|303|8|8|8|5|11|
|304|8|8|8|11|5|
|305|8|8|5|11|8|
|306|8|8|11|5|8|
|307|11|5|8|8|8|
|308|5|11|8|8|8|

All sum 40. Native StartingValue 8 and signed class-5 contributions are sourced. Existing EXP-derived point budget and shown cap 30 remain unchanged. Allocation reads trait and EXP from the same locked character row. Normalized 80b6 remains 8 plus spent points: trait 308 Focus 5 to 6 and trait 304 Belief 11 to 12 both return 9. Assigned incomplete attribute rows refuse rather than invent values; NULL legacy fallback remains unchanged.

## Persistence and policy boundaries

The two-step protocol remains: 0a commits the owned name reservation plus an empty RSI shell; 0c validates fully and consumes that current authenticated Margin reservation once. Aborted/invalid creation can leave that reservation and shell. Its existing lifetime is unchanged; there is no new ghost cleanup/expiry policy.

Idempotent startup initialization adds nullable unsigned trait_id and converts **only rsivalues** to InnoDB. Baseline actually observed RSI MyISAM. Existing storage initialization already qualifies characters, archive and inventory. Every reservation/completion checks the actual four participant engines and trait column shape. Startup DDL is outside the creation transaction and failure stops startup. No main database migration was executed during development.

Completion owns its connection, locks the exact account-bound character and RSI shell, requires NULL trait/empty initial rows, then atomically writes names/background, trait, RSI, existing vitals, unchanged fourteen starting grants, actual selected clothing and all five bases. All four tables must be InnoDB. SQL failure rolls back; replay cannot rewrite/regrant. Packet publication follows transaction disposal. A network loss after commit does not undo the completed character; normal later authentication can load it. Clothing retains the existing count 0/purity 0 materialization policy; the XML choices are an admission source, not a repair/purity economy rewrite. This is explicit backend atomicity policy, not a claim about the retail backend or every legacy creation/name/account pipeline. The existing signed-smallint RSI character key is not widened; unsupported IDs fail rather than wrap.

## Baseline and validation

Preserved baseline `0743a8ee276c7959205047212f22e98ce9096364`, production exactly c31c9d8b: **FAIL 20.2 s, exit 1**. Account-only authentication seed; no character/RSI clone. Normal 0a reserved character 147 and normal 0c returned all blocks without creation exception, but RSI count stayed 0. Requested 308 produced fixed 299 attributes; profession stayed 2. Engines: characters/archive/inventory InnoDB, RSI MyISAM. The baseline appearance followed the old server's incorrect head offset and was NOT native-correct; the independently observed missing-RSI/trait defects remain valid.

Proof: `mxo-character-creation-baseline-mxotest_run_1_37e46a1a3a.json`, SHA256 `a858c519732c5e5b123c780d2ee2990de19669acabb37188b95c7fcb997d10be`. Actual Release hds SHA256 `0AED4FE86E44D7BD39374385A6FA613D0CC82C3162A9CD690CC622B4720445FE`. Original proof/stdout are unchanged.

Current final Release full suite: **4,822 passed**, including the source-default correction; prior final focus **83 passed**. Final fixture-only same-Margin/legacy-case rebuild and fresh validation are pending. Registered `character-creation-personality` covers normal 0a/0c for308/304, same-Margin first world entry, actual new/return02, male appearance and native-default female body/head/hair with selected allowed clothing/tights, actual attributes and ordinary 80b5/relog, malformed/replay refusal, final-write rollback, two actual completion connections and legacyNULL no-migration behavior. Controlled EXP/level 2 preparation AFTER normal creation enables one attribute point; it does not claim creation earns XP. No trait/RSI/attribute SQL seed bypasses normal creation.

## Preserved first implementation failure and ingress correction

First implementation run at bb1ae753: **FAIL 20.3 s, exit 1**, before0c, on approved Debug885076608AC7D95729FD620917DFCBE9C38181D7B4A027ADE4D0B4C495DAD137. Valid normal0a body20 had PKCS suffix02/02 after decryption; legacy decryptMargin intentionally returns the suffix, so the new strict body-length guard rejected it. Proof `mxo-character-personality-mxotest_run_1_ef58cd28ac.json`, SHA256 `776137d69924652f003e3d49e7085f150c736d4a878a5bd835f77b8ed0171baf`; owned PID4564 exited and slot was released. Four actual participant engines were InnoDB. Original evidence is unchanged.

Creation alone now opts into exact envelope decoding: declared u16 body size at decrypted+4, CRC32 over size/timestamp/body, block length and every PKCS byte must match before returning the body. Only0a/0c opt in. Existing decryptMargin and other consumers retain their previous bytes/behavior. The strict name/body/string checks are not weakened to accept arbitrary suffixes. Encrypted codec tests cover all16 padding sizes and corrupt size/CRC/padding, truncated ciphertext and unrelated opcodes. Retry validation is pending.

Second implementation retry at280cef9a: **FAIL24.5s, exit1**, actual approved Release2C57BF68CCCE423068FD6A4266CD3F8212AD9C529638EA2968C0F7183AB92711. Exact framing passed and reservation reached SQL, exposing omitted required `status` (no schema default). Proof `mxo-character-personality-mxotest_run_1_deef28a99a.json`, SHA256 `191d2cb5f9280c68e92207cbde1cce9eed107096febde00ebcde1a8b71b0e383`; PID49788 exited/disposed. The insert now supplies the old EF entity's status0 explicitly and retains existing defaults, including rotation139/profession2. Reservation exceptions use logged close instead of escaping the Margin thread. A schema regression enumerates all required non-default columns in one pass. Clothing raw count is corrected to existing AddItemToSlot/default0, with purity0; no stack policy change. These corrections are **parked pending fresh validation**. Both implementation failures and the original baseline remain unchanged. No successful implementation fresh run or native UI pass is claimed.

## Claude-only native one-pass

Discovered modes `character-personality-308` and `character-personality-304` use **CSR110 only**, seeded consumer profiles, not native character creation. Actual Level/EXP 50, coherent bases, 49 points; no dev/GM/RNG.

1. Mode 308: Character > Upgrade > Attributes must show **5/11/8/8/8**,49 points. Add one Focus, Apply All. Expect 6 and 48 points, unchanged Info. Relog and confirm all values/points.
2. Mode 304: confirm **8/8/8/11/5**,49 points. Add one Belief, Apply All. Expect 12 and 48 points, unchanged Info. Relog and confirm.

NativeQa uses an explicit legacy-column clone instead of SELECT *, preserving every existing legacy source value/profession while leaving optional trait NULL for ordinary profiles even if the main source schema later contains that column. Only these two profiles assign a trait and coherent bases in their disposable fixture. This is fixture policy, not a main-data migration.

Native UI is pending. No other native account/character, automatic legacy point migration or broader personality effects are claimed.
