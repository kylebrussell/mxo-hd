using System;
using System.Collections.Generic;

namespace hds
{
    public enum MissionGoalType
    {
        Talk,    // 80 c7 ... 09 on the NPC
        Defeat,  // the NPC dies
        Loot,    // the NPC's loot window holds the item
        Get,     // 80 c7 ... 0d on the NPC: it hands over the item
        Give,    // 80 c7 ... 0e on the NPC: the player hands over the item
        DefeatAll, // every hostile NPC of the phase dies
        Escort,  // the NPC follows the player; done when they bring it where "to" says
        Protect, // fails the mission if the NPC dies; done with the phase's other goals
        Search,  // using the object opens it, holding the item
        Put,     // using the object while carrying the item leaves it there
        Use,     // using the object
        Upload,  // uploading at a hardline while carrying the item
        Survive  // holding out: done "seconds" after it opens (from reaching the area, in a generated phase)
    }

    public class MissionObjective
    {
        public int goal; // goal id as retail numbered it (80 a0); not always the order they open in
        public string description;
        public MissionGoalType type;
        public ushort npcId;       // MissionNpc.id this objective is about (the first, when it names several)
        public List<ushort> npcIds = new List<ushort>(); // every NPC it names: npc="2,3" for "Lead both hostages out"

        /// <summary>The NPCs this objective is about.</summary>
        public List<ushort> Npcs => npcIds.Count > 0 ? npcIds : new List<ushort> { npcId };

        public bool Has(ushort id) => id != 0 && (npcId == id || npcIds.Contains(id));
        public UInt32 item;        // gameobject id for Get, Give and Loot
        public int afterGoal = -1; // opens when this goal is done; otherwise it's open from the start of the phase
        public ushort objectId;    // MissionObjectDef.id for Search, Put and Use
        public string escortTo = "exit"; // Escort: "exit", "npc:<id>", "object:<id>" or "hardline"
        public bool optional;      // the phase can end without it
        public List<int> waves = new List<int>(); // DefeatAll: only these waves (0: the phase's own hostiles); empty: all of them
        public double seconds;     // Survive: how long to hold out
        public double limit;       // the mission fails if this isn't done this many seconds after it opens; 0: no limit
        public List<MissionLine> talkLines = new List<MissionLine>(); // talking to the NPC while this is open
        public List<MissionLine> doneLines = new List<MissionLine>(); // when it completes
    }
}
