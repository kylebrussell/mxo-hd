# Mission research

What was found on retail missions, and from where. `docs/MISSIONS.md` covers what's built from it.

| File | What it is | How far to trust it |
|---|---|---|
| `capture-missions.json` | Every mission in the retail packet logs in `research/packet-dumps`: 17 titles (4 complete, 6 partial, 7 title only), with lines, goals, NPCs, positions, rewards and decoder notes | Decoded directly. Fields marked as attributions, such as what completed a goal, are inferred |
| `tutorial-wire-spec.md`, `tutorial-goals.json` | The tutorial, goal by goal, from 10 captured runs and the client's script | Each claim is marked observed, script or inferred |
| `Mission986-decompiled.py` | The tutorial's logic, decompiled from the client's Monolith pack | Retail's own script, but it isn't complete: some goals are completed from server data it doesn't contain |
| `mission986_goals.tsv`, `mission986_dialog.tsv` | The tutorial's goals and voiced lines, joined to the client's string table and audio | Client data |
| `screenshots-index.tsv`, `screenshots-pilot-*.md` | Every mission folder in the screenshot logs, and two missions transcribed as a trial | Dialogue reads clearly, with speakers in order. Lines can be lost at scroll boundaries; uncertain words are marked `[?]` |
| `web-missions.md`, `web-notes.md` | 436 mission names from the web, 56 with real objectives, with sources ranked | Fan sources. Not checked against captures except where noted. The Wayback Machine couldn't be read with the tools used, and the Fandom wiki was only seen through search excerpts |

## Not in the repo

- **Screenshot logs:** [hdneo/mxo-missions](https://github.com/hdneo/mxo-missions), Vesuveus's "Filing Cabinet": 9,098 images of 431 story missions (90 Zion, 177 Machine, 164 Merovingian), 3.9 GB. Clone it next to the repo if you need it. These are Archive replays of the critical missions, and they give title, contact, dialogue, item text and sometimes rewards. They don't give objectives, triggers, NPC positions, RSI, levels or doors, so a mission built from them needs those placed by hand. Zion chapters 5 to 11 are nearly all absent from this copy. The [Google Drive original](https://drive.google.com/drive/folders/1NUDd-jZ3mzUsvCNAkAbbQ2JnCJOgXbLI) may have the Zion missions the GitHub copy lacks; it hasn't been checked.
- **The full Warboy archive** (365 MB): only a six-file derivative is here, and no local copy of the rest was found.
- **Client audio and strings:** they ship with the game. `dialog.pkb` and `dialog_1a.pkb` hold 1,136 voice clips with no transcripts, and `zmisc_1.pkb` holds 31,142 strings.
