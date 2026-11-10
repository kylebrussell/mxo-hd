"""Writes data/mission_contacts.csv from the client's own contact list.

Source: <client>/resource/missions/definitions/sponsors.xml, the contacts of retail's "Missile" mission system (a plain
file in the install). Optionally checks each name-id against a decoded stringtable_client.tsv (id, name, text).

    python -I research/missions/gen_mission_contacts.py <sponsors.xml> <data/mission_contacts.csv> [stringtable_client.tsv]
"""
import sys
import xml.etree.ElementTree as ET


def num(text):
    text = (text or "0").strip()
    return int(text, 16) if text.lower().startswith("0x") else int(text)


def main(src, dst, strings=None):
    texts = {}
    if strings:
        for line in open(strings, encoding="utf-8"):
            parts = line.rstrip("\n").split("\t")
            if len(parts) >= 3:
                try:
                    texts[int(parts[0], 16)] = parts[2]
                except ValueError:
                    pass

    rows = []
    for c in ET.parse(src).getroot().iter("contact"):
        name_id = num(c.get("name-id"))
        if texts and name_id in texts and texts[name_id] != c.get("name"):
            print("name differs from the string table: %s %s %r" % (c.get("id"), c.get("name"), texts[name_id]))
        rows.append([num(c.get("id")), num(c.get("org")), c.get("name"), "%08x" % name_id, c.get("code") or "",
                     "%08x" % num(c.get("face")), num(c.get("min-rep")), num(c.get("max-rep")),
                     "%08x" % num(c.get("intro-text-id"))])

    with open(dst, "w", newline="\n", encoding="utf-8") as out:
        out.write("id,org,name,name_id,code,face,min_rep,max_rep,intro_text_id\n")
        for r in rows:
            out.write(",".join(str(v) for v in r) + "\n")
    print("%d contacts" % len(rows))


if __name__ == "__main__":
    main(*sys.argv[1:])
