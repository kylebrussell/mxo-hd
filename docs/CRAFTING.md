# Code crafting

What is known about compiling items, where it comes from, and what is still missing.

## Recipes: the client's cookbook

The 7.6005 client ships every recipe in `resource/GameObjects/Cookbook.cbb` (2,053,348 bytes).
`scripts/export-client-cookbook.py` turns it into `data/recipes.csv`, which the server loads (`Crafting.Recipes`).

The file is little-endian and parses exactly to its end:

| Field | Size | Meaning |
|---|---|---|
| version | u32 | 1 |
| count | u32 | 38,663 recipes |
| per recipe: zero | u32 | always 0 |
| level | u32 | 1-50, rises with the item tier (read as the level) |
| type | u32 | four ASCII bytes: `ICP0` consumable pill, `IASh` shirt, `FCC0` / `FCF0` / `FCV0` class / function / variable fragment, ... |
| field A, field B | u32, u32 | ids with a type byte on top; meaning unknown (B may be the recipe's own id) |
| n | u32 | ingredient count |
| result | u32 | goid of what the recipe makes; ability codes are `0x80000000 | ability << 10` |
| ingredients | n x u32 | goids; a goid listed twice is needed twice |

The recipes form a chain:

- **Code Bits** (`Bit1`-`Bit8`, goids 7332-7339, dropped by NPCs) compile into **code fragments**:
  Class Routines (`ClassFrag_*`), Function Subroutines (`FuncFrag_*`) and Variable Fragments (`VariableFrag_C/U/R*`).
  47 recipes, levels 1-5. Example: Function Subroutine: Consumable (`FuncFrag_Pill_F02`, 7458) = Code Bit 5 + Code Bit 5.
- **Fragments** compile into everything else: 38,165 recipes start with Class Routine: Item, and 451 make ability codes.
  Example: Health Pill 1.0 (2399, level 1) = ClassFrag_Item_T01 + FuncFrag_Consumable_F02 + FuncFrag_Pill_F02
  + VariableFrag_C05 + VariableFrag_C15.

## What the server does now

`CraftingHandler.Compile(resultGoid)`:

1. looks up the recipe;
2. takes one item per ingredient from the bag (below slot 97), lowest slots first; a stack gives several;
3. takes each one out: a stack shrinks (`69 01`), the last item leaves its slot (`5f <slot> 01`);
4. adds the result: a fragment joins a stack of its kind, anything else takes the first free slot (`5e`).

If an ingredient is missing, the reply lists what is missing. The dev chat command `?compile <goid>` runs it.

Native compile requests now use structured `81 2e`; inventory-target Decompile276 uses `81 31`.
The old scan for a plausible recipe inside an `80 b9` ability request is no longer dispatched.
[Combat action evidence](CLIENT-COMBAT-ACTIONS.md#requests) documents layouts, source addresses, validation,
script contests, persistent learning and remaining Code Writer/fragment-recovery limits.

## Known recipes (margin opcode 0x0a)

The margin's character data block 0x0a is the known-recipe list: u32 `4`, then recipe result goids. All 425 goids
in the list the margin always sent are cookbook results (tools, weapons, clothing). The server now adds the 47
recipes made only of Code Bits (the fragments) and the five health pills (`Crafting.BuildKnownRecipesBlock`).
Per-character learned recipes are appended from `char_recipes` and survive a fresh margin login.

## Stacks

Retail keeps a stack's size as (count - 1) * 4 (`ItemStacks`):

- **Item add:** the third descriptor byte of `5e <slot u16> <goid u32> 00 00 <amount> <type> 01`, e.g. a marketplace stack of 20 Code Bit 8: `5e 44 00 ab 1c 00 00 00 00 4c 00 01`. Listing prices confirm it: 20 sold for 20,000, 9 (`20`) for 9,000, 4 (`0c`) for 4,000.
- **Stack update:** `69 01 <slot u32> <(count - 1) * 4 u32>`. Icy Snowballs bought one at a time sent `5e` with amount 00, then `69 01 36 00 00 00` with 04, 08, 0c, and so on. A used Oracle cookie sent `69 01` instead of `5f`.

The database's `inventory.count` holds the stack size (0, as in every older row, means one). The margin sends the
amount byte at login. Code Bits, code fragments and consumables stack, up to 64 (the largest the byte holds;
retail's real limit is unknown).

## Retail rules in the client's scripts

The client's `resource/Python/Monolith.ltp` is a zip archive with `LTPb` in place of `PK`. It holds the
Python 2.5 rule scripts, including `crafting_server.pyc`, `recipes.pyc` and `ability/hacker/crafting.pyc`.
`CompileTest` in `crafting_server.pyc`:

- **Info cost:** `VendorPrice` × `constants.Crafting.CompileCompileCostMultiplier`, with 1000 for items without a price. It is reduced by CraftingCostDiscountAbility and the signal boost.
- **Not enough info:** "not enough info to compile"; nothing is spent.
- **Success:** if 4 × (CompileSkillAbility level + 10% of the category crafting ability) + gauss(50, 0..100) beats 4 × the item's `Complexity` + 50.
- **Info is spent** on success and failure alike.

The multiplier and the per-item `Complexity` are not in the client files, so the server does not charge yet.

## Code storage (uploading at a hardline)

Retail kept code storage in the known-abilities list, margin character data opcode 0x07. Each entry is
`<slot u16> <value u32>`:

- an ability is `0x80000000 | abilityId << 7 | level` (this is what `char_abilities.ability_id + level` already gives);
- stored code is `0x84000000 | goid << 7 | (count - 1)`.

Trinity's last-day login (`crashover2k_trinitys_proxy_second_pc_011`) mixes both kinds in one list. For example,
`24 00 85 53 0e 84` is slot 0x24 holding six Code Bit 4 (7335), and `00 cd 04 84` is one ClassFrag 2458.

Uploading is hardline booth action 04: the client sends `80 c7 <view> <spawn> 04 00`, or `80 c8 <booth> 04 00` for a
static booth. When there was code in the bag, retail answered in this order:

1. `2e 07 ...`: a system message from the client's string table (`64 00 00 2e 00`), with the character's name as
   its argument. It was byte-identical in both captures.
2. `5f <bag slot> 01` for every bag slot that was uploaded.
3. `69 00 <slot u16> <value u32> 00 00` for every kind already in storage. In `abilitychangecoder`, Code Bit 4 in
   slot 0x1e4 went from `8e 53 0e 84` at line 61114 to `8f 53 0e 84` at line 62075, one higher after a single
   looted Code Bit 4 was uploaded.
4. `5e <slot u16> 00 00 00 00 <value u32> 00` for a new kind (`luizoe_recursion2` 1936). This is the bag-add frame
   with goid 0, the packed value in place of `00 00 <amount> <type>`, and 00 in place of 01.
5. `81 18 ...`.

An upload with nothing to store got only `81 18` (`81 18 00 00 0d 00 00 98`, the answer PR #29 sends).

Count bits hold count − 1, a reading the captures favour but don't prove. New kinds arrived with counts 0, 2 and
0x13, and never as an empty entry. Seven bits allow 128 per kind.

**On this server:**

- `CodeStorageHandler.UploadCode` moves every Code Bit and code fragment from the bag into storage, sends the
  records above, and stores them as `char_abilities` rows (`ability_id` = `CodeStorage.BaseValue(goid)`, `level` =
  count − 1, `is_loaded` = 0). The margin's known-abilities list therefore sends them at login as retail did.
- New kinds take the slot after the character's highest ability slot. When a kind is full, the rest stays in the bag
  (`69 01`).
- Compiling uses bag items first and storage entries second (`69 00` with the lower count). This is a guess: no
  capture shows where the Code Writer takes its ingredients from.
- Retail evidence against uploading fragments: in `abilitychangecoder` 62075, a ClassFrag 2436 looted just before
  the upload stayed in the bag, while `luizoe_recursion2` uploaded ClassFrags (2438, 2450). The server uploads
  fragments.

## Retail evidence

- No capture in `research/packet-dumps` shows a compile, a decompile, or the Code Writer window being used.
- **Write Code (970)** (`actions_combat_marketplace_abilitychangecoder`, line 65987): the client sends `80 b9 ca 03 0d 00 ee 00 ... 28 05 01 00 00 00 00`. The `05 01` in the tail is bag slot 5 with the item flag, which is presumably the Code Compiler Tool.
  - The server answers `80 bd 05 ca 03 ... 01` plus an `80 bc` record, then the same pair with the flags flipped about 0.3 s later.
  - Nothing else follows.
- **Upload at a hardline**: see "Code storage" above.
- **Recycle (`5d`)** pays information only (`80 e7` / `80 e4` reason 0x0f, then `5f`), never code bits. The server already does this.
- **Client strings:** `viewcrafting.cpp`, `viewcompile.cpp`, `Crafting_RecipeDDList`, `CS_Button_Compile`, `CompileCost`, `RecipeListMD5`, `LTOBJSRV_ADDEDUNKNOWNRECIPE`, `LTPS_SPNORECIPEID`, `decompileItem`.

  These suggest:
  - the Code Writer lists the character's *known* recipes, sent by the server and checked with an MD5;
  - compiling costs information;
  - the compile request names a recipe id.

## Remaining native validation

The compiler request/reply layouts, `.12` cost, skill contest and per-character recipe learning now have
binary/script evidence and headless coverage. See [the native checklist and precise remaining limits](CLIENT-COMBAT-ACTIONS.md#native-checklist-and-current-status).
The Code Writer/Analyzer UI path, fragment recovery, purity/stability, signal boosts, charges, XP and the other
inventory-target abilities still need implementation or verification. Existing storage and stack behavior is unchanged by this work.
