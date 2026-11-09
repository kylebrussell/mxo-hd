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
        Give     // 80 c7 ... 0e on the NPC: the player hands over the item
    }

    public class MissionObjective
    {
        public int goal; // goal id as retail numbered it (80 a0); not always the order they open in
        public string description;
        public MissionGoalType type;
        public ushort npcId;       // MissionNpc.id this objective is about
        public UInt32 item;        // gameobject id for Get, Give and Loot
        public int afterGoal = -1; // opens when this goal is done; otherwise it's open from the start of the phase
        public List<MissionLine> talkLines = new List<MissionLine>(); // talking to the NPC while this is open
        public List<MissionLine> doneLines = new List<MissionLine>(); // when it completes
    }
}
