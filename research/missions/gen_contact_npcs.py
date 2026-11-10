"""Writes data/mission_contact_npcs.csv: where each neighbourhood contact stands, and how they look.

The client's world data has 51 "NPC_MissionContact" statics (goid 6520, type 78190000 in data/staticObjects_*.csv): 23 in
the Slums, 17 Downtown, 11 International. Retail spawned each contact bound to one of them: 08 78 19 <static> ... with
Position, ContactName, DescriptionID (the RSI), ContactID and Orientation. Which contact stands at which static comes from:

  capture       retail's own creation record in research/packet-dumps (contact id, name and RSI)
  contact_type  the sector's "Contact Type" property in the client's .metr world file (data/sector_properties.csv),
                which is the sponsors.xml contact id
  npc_list      data/NPC_COLLECTION.xml (an older emulator's list of 13 Slums contacts, by static id, with RSIs)
  guide         the contact's map position in the Prima guide (2005), which lands on the static to the metre

RSI: retail's where captured, else NPC_COLLECTION.xml's, else the client's model named after the contact
(resource/characters-NPC/Contacts/<name>.moa). Grisaille has no model of her own; Gray.moa is a guess.

    python -I research/missions/gen_contact_npcs.py <repo>
"""
import csv
import glob
import os
import sys

# static (mxoId as in data/staticObjects_*.csv) -> contact id, sources, rsi, rsi source
SPOTS = {
    # Slums
    "ca004009": (2, "capture contact_type npc_list", "340a0058", "capture"),
    "5901a009": (1, "contact_type", "330a0058", "model"),
    "3c1d4011": (9, "contact_type npc_list", "720a0058", "npc_list"),
    "2d000013": (16, "guide", "580a0058", "model"),
    "04000018": (18, "guide", "4a0a0058", "model"),
    "2d00501a": (24, "contact_type npc_list", "560a0058", "npc_list"),
    "1e002024": (12, "contact_type npc_list", "250d0098", "npc_list"),
    "6f03c026": (22, "guide", "510a0058", "model"),
    "820d302e": (3, "capture contact_type npc_list", "350a0058", "capture"),
    "c607f034": (17, "contact_type", "490a0058", "guess"),
    "6b029036": (6, "contact_type npc_list", "360a0058", "npc_list"),
    "0c002037": (11, "guide", "450a0058", "model"),
    "81055037": (21, "contact_type", "8d0a0058", "model"),
    "4900c03e": (14, "contact_type npc_list", "750a0058", "npc_list"),
    "0b009045": (20, "contact_type", "4e0a0058", "model"),
    "950a5046": (4, "capture contact_type npc_list", "7a0a0058", "capture"),
    "b1061047": (23, "contact_type", "780a0058", "model"),
    "5e00b047": (7, "contact_type npc_list", "7c0a0058", "npc_list"),
    "2f035048": (15, "contact_type npc_list", "4f0a0058", "npc_list"),
    "2400a048": (13, "contact_type npc_list", "520a0058", "npc_list"),
    "3504004d": (19, "contact_type", "310a0058", "model"),
    "7700204e": (10, "contact_type npc_list", "760a0058", "npc_list"),
    "1c003050": (8, "contact_type npc_list", "2d0d0098", "npc_list"),
    # Downtown
    "4f00f001": (42, "guide", "4d0a0058", "model"),
    "bf272003": (44, "capture", "730a0058", "capture"),
    "80204003": (51, "capture", "240d0098", "capture"),
    "0f23b004": (47, "capture", "7e0b0058", "capture"),
    "eb0c1005": (49, "guide", "400d0098", "model"),
    "31022012": (40, "contact_type", "7b0a0058", "model"),
    "712ed014": (48, "capture", "320a0058", "capture"),
    "02007016": (39, "guide", "7e0a0058", "model"),
    "00257017": (46, "contact_type", "7d0b0058", "model"),
    "7535101c": (45, "guide", "8b0a0058", "model"),
    "a536701c": (52, "guide", "550a0058", "model"),
    "3709101e": (41, "contact_type", "530a0058", "model"),
    "7100e027": (37, "contact_type", "4b0a0058", "model"),
    "bc07002a": (38, "guide", "8a0a0058", "model"),
    "9a14102a": (43, "contact_type", "890a0058", "model"),
    "673da02b": (36, "capture", "7d0a0058", "capture"),
    "e327502d": (50, "capture", "7a0b0058", "capture"),
    # International
    "86002001": (33, "capture", "7c0b0058", "capture"),
    "72002003": (32, "contact_type", "460a0058", "model"),
    "56003003": (25, "contact_type", "540a0058", "model"),
    "c50b8006": (30, "guide", "7b0b0058", "model"),
    "5e01d006": (27, "guide", "570a0058", "model"),
    "2d00f009": (26, "capture", "480a0058", "capture"),
    "ad05500c": (35, "capture", "8f0a0058", "capture"),
    "e521700d": (34, "capture", "790b0058", "capture"),
    "0d00300f": (28, "guide", "770a0058", "model"),
    "0e039010": (31, "contact_type", "4c0a0058", "model"),
    "5703f017": (29, "capture", "7f0b0058", "capture"),
}

DISTRICTS = {"1": "Slums", "2": "Downtown", "3": "International"}


def main(repo):
    names = {r["id"]: r["name"] for r in csv.DictReader(open(os.path.join(repo, "data", "mission_contacts.csv")))}
    hoods = {}
    props = os.path.join(repo, "data", "sector_properties.csv")
    if os.path.exists(props):
        for r in csv.DictReader(open(props)):
            hoods[(r["metr_id"], r["sector"])] = r["neighbourhood"]

    rows = []
    for path in sorted(glob.glob(os.path.join(repo, "data", "staticObjects_*.csv"))):
        for r in csv.DictReader(open(path)):
            if r["type"] != "78190000":
                continue
            contact, sources, rsi, rsi_source = SPOTS[r["mxoId"]]
            rows.append([contact, names[str(contact)], r["metr_id"], r["sector_id"], r["mxoId"], r["pos_x"], r["pos_y"],
                         r["pos_z"], r["quat"], rsi, hoods.get((r["metr_id"], r["sector_id"]), ""), sources, rsi_source])

    assert len(rows) == len(SPOTS), len(rows)
    rows.sort(key=lambda r: r[0])
    with open(os.path.join(repo, "data", "mission_contact_npcs.csv"), "w", newline="\n") as out:
        out.write("contact_id,name,metr_id,sector,static_id,x,y,z,quat,rsi,neighbourhood,placed_by,rsi_from\n")
        for r in rows:
            out.write(",".join(str(v) for v in r) + "\n")
    print(len(rows), "contacts placed")


if __name__ == "__main__":
    main(sys.argv[1])
