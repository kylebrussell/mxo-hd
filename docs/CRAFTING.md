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
2. takes one bag slot (below 97) per ingredient, lowest slots first;
3. removes them with `5f <slot> 01`;
4. puts the result into the first free slot with `5e`.

Those are the same inventory messages retail used for sells and loot. If an ingredient is missing, the reply lists what is missing.
The dev chat command `?compile <goid>` runs it.

## Retail evidence

- No capture in `research/packet-dumps` shows a compile, a decompile, or the Code Writer window being used.
- **Write Code (970)** (`actions_combat_marketplace_abilitychangecoder`, line 65987): the client sends `80 b9 ca 03 0d 00 ee 00 ... 28 05 01 00 00 00 00`. The `05 01` in the tail is bag slot 5 with the item flag, which is presumably the Code Compiler Tool.
  - The server answers `80 bd 05 ca 03 ... 01` plus an `80 bc` record, then the same pair with the flags flipped about 0.3 s later.
  - Nothing else follows.
- **Upload at a hardline** (client `80 c7 <hardline> 04 00`): the server answers with:
  - a `2e` system message;
  - `5f <slot> 01` for each code bit or fragment;
  - one `69 00 <u16> <u32> 00 00` per stored kind (code storage);
  - `81 18 ...`.

  Not implemented.
- **Recycle (`5d`)** pays information only (`80 e7` / `80 e4` reason 0x0f, then `5f`), never code bits. The server already does this.
- **Client strings:** `viewcrafting.cpp`, `viewcompile.cpp`, `Crafting_RecipeDDList`, `CS_Button_Compile`, `CompileCost`, `RecipeListMD5`, `LTOBJSRV_ADDEDUNKNOWNRECIPE`, `LTPS_SPNORECIPEID`, `decompileItem`.

  These suggest:
  - the Code Writer lists the character's *known* recipes, sent by the server and checked with an MD5;
  - compiling costs information;
  - the compile request names a recipe id.

## Still missing

- The RPC the Code Writer's Compile button sends, and the server's reply. Capture it with the real client:
  1. open the Code Writer (Write Code);
  2. click Compile;
  3. read `UnknownRPC.txt`.
- How known recipes reach the client (`RecipeListMD5`), and how decompiling teaches them.
- The info cost of a compile (`CompileCost`, `CompileCompileCostMultiplier`).
- Code storage (uploading at hardlines, and taking ingredients from storage instead of the bag).
