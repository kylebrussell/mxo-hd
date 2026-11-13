# Prison devices

Only two actual equipped tools are supported: Prison Mentality Device 42722 activates 949, and Prison Mastery Device 42723 activates 957. Acquisition is unimplemented. The QA fixture seeds these real items for an equipment test; it does not invent a vendor, quest, drop or learned ability. Neither ability becomes a free Actions entry or a new player-loadability audit candidate.

## Primary evidence

The pinned GOB SHA256 is `3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`. A scan of all 44,406 stored records found exactly these two non-ability references. Both are component-10 tools with explicit ActivatedAbility and ActivatedAbilityRequiresEquip=1. They require Awakened at level 30 or 45 and prohibit crafting, decompilation, trading and sale. Their equip modifier targets null attribute 710 and terminates through RemovePrisonMod, which deactivates BOTH 949 and 957.

The pinned client.dll SHA256 is `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`. Native equipment code compares their actual category `0x6c505449` at 621e90e0 and maps automatic destination 107 to slot 106 at 621e90e9. This is category-derived, not an inferred Tool/Class=4002 table. RequiresEquip getter 625da150 is consumed by validator 621d8870; 621d8929 rejects activation from a bag slot below 96. Successful device activation at 621ed3d7 calls 621ecf10 with the actual ability, flag 1 and actual slot. The normal 41-byte 80b9 body stores tool slot at offset 35 and flag at 36. The server requires exact ability 949/957, slot 106, flag 1 and matching owned equipment. Bag possession or an uploaded ability cannot substitute for the device.

Stored effects are a 1.5-second cast, 30-second buff, initial toughness/stat-144 value 1 or 2, and a 360-second recast shared through timer 958. Timer 958 is not treated as an unsupported buff-stacking family. Shipped tempmods.py lines 1969–1995 add 2 while the PREVIOUS value is below 25, or add 3 while below 26, on positive health damage. The script does not clamp the new value. Terminal RestoreDoubleIS restores `min(100, 2 * Value)`; lines 2960–2962 remove both parents when a device is unequipped. No IS cost or item consumption is invented.

The raw gate and disassembly are retained in Temp/mxo-prison-devices-source-gate.md and the associated mxo-prison-* evidence files. No retail activation capture or current acquisition producer was recovered. Exact script chat-string presentation and native tool UI remain unverified.

## Integration and policy limits

An isolated helper handles only these two pairs. Ordinary equip, unequip and move routes dispatch the exact devices before the book-only slot-106 handler. Books and compiler/writer sessions retain their handlers. An empty destination is required: this is a conservative server equipment policy. Ownership and prerequisites are checked before moving. Activation retains normal readiness, control, global cast, buffer and cooldown guards, then revalidates item, prerequisite, cast stamp, world and life at landing. There are no code-storage, memory, recipe or charge-codec changes.

Each cast owns its buff definition. The existing player health-change seam supplies incoming damage, including ordinary and DOT damage, without another combat processor or RNG. Successful removal first removes the active generation under the existing lock, so terminal refund runs once. Unequipping removes both parents and pending casts while leaving unrelated effects alone. Old expiry cannot refund a replacement generation. Existing death, world and logout lifetime behavior and IS maximum clamping remain ordinary server policies; no new retail persistence claim is made. Toughness uses current shared resistance consumers with their existing baseline limitations.

## Claude-only one-pass

Discovered mode and case: `prison-devices`. Native setup uses CSR 110 at Mara, actual level/EXP 50, loaded Awakened and both real devices in the bag. There are no 949/957 archive rows, GM flag or forced RNG. Dev health commands are preparation only.

1. Equip bag-1 Prison Mentality Device in the program-launcher slot 106 through ordinary UI. Activate through Actions > Tools or Activate Item. Capture the equipment move and actual 80b9 slot 106 / flag 1. Bag-only and free-ability requests must fail.
2. Use `?health 5000`, then `?health 4900` for one deterministic health-change witness. Expect toughness 1 to become 3. Healing must not increase it. Wait 30 seconds: the effect ends and nominal 6 IS is restored, subject to ordinary regeneration and maximum.
3. Unequip to an empty bag slot. Both Prison parents must be gone, while unrelated buffs and the device remain. Re-equipping does not clear the 360-second family timer.
4. For Mastery, use a fresh profile or wait the real family timer. Equip bag 2, activate normally, and expect 2 to become 5 after one health decrement, with nominal 10 IS on unequip. Moving the item during its 1.5-second cast must prevent the buff from landing without consuming anything.

Headless proof retains actual RPC/03, slots/count, cast cancellation, changing modifiers, expiry/unequip, parser results and actual DLL path/configuration/hash on failure as well as success. Native appearance, discovery and clickability remain pending Claude observation. No native action has been performed by Codex.

## Initial validation (superseded by identity hardening)

Full Release suite: **4,587 passed** in 80 seconds. Final focused device, crafting-session, archive and health-callback checks: **19 passed**, including the subsequently added fixture no-RNG/no-free-grant contract. Both actual NativeQa configurations built and emitted the discovered plan and read-only provenance; Python snapshot policy **9 passed**. No fresh scenario has run yet.

Actual Release hds SHA256: `35471afe3bda33d443a5c91f510a107f945549499d24b40e05d566cec86c5a24`; Debug: `390a1208b16722bb188c40c302b395706190b6462caa0aa5ca518194dc03f2da`. Logs and read-only plans are retained under `Temp/mxo-prison-*`. An initial C# language-version compile error and a test-only arithmetic expectation failure were corrected and their logs retained. No failed fresh proof is being treated as a pass.

## Row identity and reset safeguards

Equipment now uses a dedicated transaction with no schema changes. It requires the existing InnoDB character, inventory and ability tables; locks the character and source/destination inventory rows; checks the exact inventory primary key, GOID and raw count; verifies an empty destination and current SQL prerequisite; then updates that exact row and commits before buff, queue or transport-fence work. Competing moves cannot both adopt a stale slot snapshot. Devices use native bag slots 1 through 95 (`slot < 0x60`), with 106 as equipment. Other item conventions remain unchanged.

Pending casts retain the physical inventory row ID as well as slot, GOID and count, plus world, life, cast stamp and transport epoch. Landing rereads that same row before taking the existing transport fence around actual buff application. Removing and reinserting a different row with the same GOID cannot inherit the cast. Equipment uses ordinary world/life/shutdown guards, without borrowing the ability-only Powerless or Incapacitated restrictions; activation still requires normal ability controls. These are source-authority safeguards, not a generic equipment framework.

The disposable fixture adds two independent competing SQL moves, a deliberate precommit SQL failure with no move acknowledgement, and a same-GOID replacement during the cast. Unit tests exercise actual fenced application versus world/life/shutdown/reset-and-resume invalidation. These checks do not claim fresh success before they run. Actual master 05673205668c6a7c9f434ec67439e09b0d913866 was merged before these revisions.


## Current identity-hardening validation

After merging actual master 05673205 and adding the row/epoch safeguards, the final full Release suite passed **4,613 tests** in 68 seconds; the focused device/crafting/archive/health checks passed **25 tests**. Both actual NativeQa builds, discovered plans and read-only provenance passed, as did **9** Python snapshot-policy tests. The earlier new Powerless test initially omitted its state-proc discriminator; that fixture was corrected and the entire suite subsequently passed. No production workaround was made for the fixture failure.

Current Release hds SHA256: `5efa5a72ee508e370d0d0d501603923927524a4134279de83d8b786c26520ddc`; Debug: `759eceb044c3f6932ce6bf463b01ba769f19aedc9601a0a33adf727a0fa393fe`. Evidence files are `Temp/mxo-prison-identity-*`. The transactional races, deliberate SQL rollback and same-GOID replacement wire checks are prepared but **not yet run**; fresh remains subject to root review and grant. Native UI and acquisition limitations remain unchanged.

The cleanup hook covers the exact device equip/unmount/move routes and normal buff removal. This package does not retrofit every inventory mutation. Direct administrative or database deletion/replacement of an already-active item is outside that cleanup hook; pending casts still reject a replacement physical row. Other inventory paths retain their existing slot/ownership rules. No claim is made that every arbitrary deletion runs RemovePrisonMod.

The fresh fixture now records ordered absolute IS updates in each session and asserts an exact adjacent +6 at Mentality expiry and +10 at Mastery unequip, below the resource cap. Ordinary +8 regeneration must also remain independently visible; it is not disabled or folded into the refund assertion. Denial checks advance the receive cursor before each request and require a newly received denial count, so historical matching headers cannot satisfy them. These are fixture assertions only; fresh remains unrun.
